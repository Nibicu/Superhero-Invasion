using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Искусственный интеллект врага. Раз в thinkInterval секунд "думает":
///  1) собирает команды из свободных злодеев;
///  2) отправляет свободные здоровые команды к самой выгодной цели.
///     Объекты — главный приоритет (они дают большие бонусы). Миссии — для прокачки.
///     Базу противника атакует, только если захватил больше половины объектов
///     или база противника пустая (без гарнизона);
///  3) отступает, если охрана цели оказалась сильнее (на верное поражение не идёт);
///  4) тратит золото на одно действие: найм, постройку по плану, улучшение построек или базы.
/// ИИ пользуется теми же правилами, что и игрок (ResourceManager, MainBase,
/// HeroManager, SquadManager) — никаких читов.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Общее")]
    [Tooltip("Включить ИИ врага")]
    [SerializeField] private bool aiEnabled = true;
    [Tooltip("За какую сторону играет ИИ")]
    [SerializeField] private Team team = Team.Enemy;
    [Tooltip("Через сколько секунд после старта ИИ начинает действовать")]
    [SerializeField] private float startDelay = 5f;
    [Tooltip("Как часто ИИ принимает решения (сек). Меньше — враг активнее")]
    [SerializeField] private float thinkInterval = 3f;
    [Tooltip("Писать действия ИИ в консоль (для отладки)")]
    [SerializeField] private bool logActions = true;

    [Header("Стройка")]
    [Tooltip("План стройки по порядку. Одна постройка может встречаться несколько раз (если её можно строить много)")]
    [SerializeField] private BuildingData[] buildOrder;

    [Header("Команды")]
    [Tooltip("Новую команду ИИ создаёт, только когда все команды на базе набрали столько героев (вместе с капитаном). Чем больше — тем сильнее команды")]
    [SerializeField] private int fullSquadSize = 4;
    [Tooltip("Команда с здоровьем ниже этой доли сначала лечится на базе")]
    [SerializeField, Range(0f, 1f)] private float minHpToSend = 0.7f;
    [Tooltip("ИИ берёт миссию, только если шанс успеха не ниже этого")]
    [SerializeField, Range(0f, 1f)] private float minMissionChance = 0.6f;
    [Tooltip("Оставлять одну команду дома гарнизоном (если команд 2 и больше). Сильные команды уходят захватывать объекты")]
    [SerializeField] private bool keepGarrison = true;
    [Tooltip("Насколько ИИ хочет атаковать базу противника (ценность атаки)")]
    [SerializeField] private float baseAttackValue = 600f;
    [Tooltip("Во сколько раз объекты ценнее остальных целей (объекты — главный приоритет)")]
    [SerializeField] private float objectPriority = 3f;
    [Tooltip("Какую долю объектов карты нужно захватить, чтобы атаковать базу с гарнизоном (0.5 = больше половины)")]
    [SerializeField, Range(0f, 1f)] private float objectsShareForBaseAttack = 0.5f;

    [Header("Прокачка героев")]
    [Tooltip("Прокачивать героев за золото, только если после покупки останется не меньше этой суммы (запас на базу и найм)")]
    [SerializeField] private int levelUpGoldReserve = 1500;

    private float timer;                                                        // До следующего решения
    private readonly Dictionary<Squad, ISquadTarget> orders = new Dictionary<Squad, ISquadTarget>(); // Куда уже отправлены команды

    private ResourceManager RM => ResourceManager.Instance;
    private HeroManager HM => HeroManager.Instance;
    private SquadManager SM => SquadManager.Instance;
    private MainBase Base => MainBase.Get(team);

    private void Start() => timer = startDelay;

    /// <summary>Отсчитываем время до следующего решения.</summary>
    private void Update()
    {
        if (!aiEnabled || Base == null) return;
        timer -= WorldTime.DeltaTime;
        if (timer > 0f) return;
        timer = thinkInterval;
        Think();
    }

    /// <summary>Один "ход мысли" ИИ.</summary>
    private void Think()
    {
        CleanupOrders();
        RetreatFromLostFights();
        FormSquads();
        DispatchSquads();

        // Плутоний тратится отдельно — на усиление героев (если есть Институт)
        TryBoostHero();

        // Одно "денежное" действие за раз — по приоритету
        if (HM.GetHeroes(team).Count < 2 && TryHire()) return;
        if (TryBuild()) return;
        if (TryHire()) return;
        if (TryUpgradeBuildings()) return;
        if (TryUpgradeBase()) return;
        TryLevelUpHero();
    }

    // ---------- Команды ----------

    /// <summary>Убрать приказы команд, которые уже вернулись или распущены.</summary>
    private void CleanupOrders()
    {
        var done = new List<Squad>();
        IReadOnlyList<Squad> squads = SM.GetSquads(team);
        foreach (var pair in orders)
            if (pair.Key.Status == SquadStatus.AtBase || !Contains(squads, pair.Key)) done.Add(pair.Key);
        foreach (Squad s in done) orders.Remove(s);
    }

    /// <summary>
    /// Распределить свободных героев: сначала дополняем команды на базе до fullSquadSize
    /// (сильные команды могут захватывать объекты), новую команду создаём,
    /// только когда все команды на базе полные. Если команд максимум — дополняем до предела.
    /// </summary>
    private void FormSquads()
    {
        List<HeroInstance> free = SM.GetFreeHeroes(team);
        if (free.Count == 0) return;
        free.Sort((a, b) => HeroPower(b).CompareTo(HeroPower(a))); // сильные первыми

        int limit = SM.CanCreateSquad(team) ? fullSquadSize : SM.MaxMembers + 1;
        foreach (Squad s in SM.GetSquads(team))
        {
            if (s.Status != SquadStatus.AtBase || s.Size >= limit || free.Count == 0) continue;
            var members = new List<HeroInstance>(s.Members);
            while (members.Count + 1 < limit && free.Count > 0)
            {
                members.Add(free[0]);
                free.RemoveAt(0);
            }
            if (SM.TryEditSquad(s, s.Captain, members, out _))
                Log($"усилил команду {s.Number} (героев: {s.Size})");
        }

        if (free.Count == 0 || !SM.CanCreateSquad(team)) return;
        HeroInstance captain = free[0];
        var newMembers = new List<HeroInstance>();
        for (int i = 1; i < free.Count && newMembers.Count < fullSquadSize - 1; i++) newMembers.Add(free[i]);
        if (SM.TryCreateSquad(team, captain, newMembers, out Squad squad, out _))
            Log($"создал команду {squad.Number}, капитан {captain.Data.displayName}");
    }

    /// <summary>
    /// Отступление: если команда едет к цели или готовится к бою, а охрана цели
    /// оказалась сильнее ("вы точно проиграете") — отзываем команду домой.
    /// К базе с гарнизоном без большинства объектов тоже не идём.
    /// </summary>
    private void RetreatFromLostFights()
    {
        foreach (var pair in new List<KeyValuePair<Squad, ISquadTarget>>(orders))
        {
            Squad squad = pair.Key;
            if (!(pair.Value is AttackableSite site)) continue;
            if (!SM.CanRetreat(squad, out _)) continue; // бой уже начался — поздно

            bool lose = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), site.DefenderPower) == BattleForecast.Lose;
            // Битва за флаг: команда противника намного сильнее — тоже уходим
            SquadUnit foe = site.IsContested ? site.PlayerContestant : null;
            if (foe != null && foe.Squad.Owner != team &&
                BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.SquadPower(foe.Squad)) == BattleForecast.Lose)
                lose = true;
            bool badBaseAttack = site is MainBase mb && !MayAttackBase(mb);
            if (!lose && !badBaseAttack) continue;
            if (SM.TryRetreat(squad, out _))
                Log($"команда {squad.Number} отступает от «{site.SiteName}»: охрана слишком сильная");
        }
    }

    /// <summary>
    /// Можно ли атаковать базу противника: у нас больше половины объектов карты
    /// или база противника пустая (гарнизона нет).
    /// </summary>
    private bool MayAttackBase(MainBase enemyBase)
    {
        int total = MapObject.All.Count;
        bool majority = total > 0 && MapObject.CountOwnedBy(team) > total * objectsShareForBaseAttack;
        return majority || enemyBase.DefenderPower <= 0;
    }

    /// <summary>
    /// Отправить свободные здоровые команды к лучшим целям — сильные первыми
    /// (им по силам охрана объектов). Если команд хотя бы две — последняя
    /// команда на базе остаётся дома гарнизоном.
    /// </summary>
    private void DispatchSquads()
    {
        var ready = new List<Squad>();
        int atBase = 0;
        foreach (Squad s in SM.GetSquads(team))
        {
            if (s.Status != SquadStatus.AtBase) continue;
            atBase++;
            if (s.HpFraction >= minHpToSend && !orders.ContainsKey(s)) ready.Add(s);
        }
        ready.Sort((a, b) => BattleCalculator.SquadPower(b).CompareTo(BattleCalculator.SquadPower(a)));
        bool needGarrison = keepGarrison && SM.GetSquads(team).Count >= 2;

        foreach (Squad s in ready)
        {
            if (needGarrison && atBase <= 1) return; // последняя команда — гарнизон
            ISquadTarget target = ChooseTarget(s);
            if (target == null) continue; // этой команде целей не нашлось — может, найдётся другой
            if (SM.SendSquad(s, target, out _))
            {
                orders[s] = target;
                atBase--;
                Log($"отправил команду {s.Number} → {target.TargetName}");
            }
        }
    }

    /// <summary>Выбрать самую выгодную цель для команды (ценность / расстояние).</summary>
    private ISquadTarget ChooseTarget(Squad squad)
    {
        Vector3 home = Base.transform.position;
        ISquadTarget best = null;
        float bestScore = 0f;

        // Миссии: только свободные и с хорошим шансом
        if (MissionManager.Instance != null)
            foreach (MissionMarker m in MissionManager.Instance.Active)
            {
                if (m == null || m.AssignedSquad != null || IsOrdered(m)) continue;
                if (m.Data.GetSuccessChance(squad.Power) < minMissionChance) continue;
                if (!m.CanAccept(squad, out _)) continue;
                float value = m.Data.rewardGold + m.Data.rewardPlutonium * 15f
                              + (m.Data.levelUpTeam ? 300f : 0f) + (m.Data.unlockBuilding != null ? 500f : 0f);
                float score = value / (Vector3.Distance(home, m.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = m; }
            }

        // Объекты: не наши, никто из наших туда не едет
        foreach (MapObject o in MapObject.All)
        {
            if (o.IsOwnedBy(team) || IsOrdered(o) || !o.CanAccept(squad, out _)) continue;

            // Объект уже захватывает противник: вступаем в битву за флаг, только если успеем
            // до конца подготовки и наша команда не слабее его команды
            bool join = o.IsUnderAttack;
            if (join)
            {
                if (!o.CanJoin(team) || SM.EstimateTravelTime(squad, o) > o.PrepLeft - 1f) continue;
                Squad rival = o.Attacker.Squad;
                if (BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.SquadPower(rival)) == BattleForecast.Lose) continue;
            }
            MapObjectData d = o.Data;
            float value = d.goldIncome * 3f + d.plutoniumIncome * 40f + d.heroLimitBonus * 150f
                          + (d.unlocksFactoryUpgrades ? 400f : 0f) + (d.allowsHeroBoost ? 300f : 0f);
            if (o.HasOwner) value += 200f; // отобрать у противника — вдвойне полезно

            // Охрана объекта: на верное поражение не идём, на равный бой — неохотно
            var forecast = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.GarrisonPower(d));
            if (forecast == BattleForecast.Lose) continue;
            if (forecast == BattleForecast.Equal) value *= 0.7f;
            value *= objectPriority; // объекты — главный приоритет
            if (join) value *= 1.3f; // заодно не дать объект противнику
            float score = value / (Vector3.Distance(home, o.ApproachPoint) + 5f);
            if (score > bestScore) { bestScore = score; best = o; }
        }

        // База противника: только при большинстве объектов или если она пустая.
        // Чем меньше у неё HP, тем заманчивее. На верное поражение не идём.
        MainBase enemyBase = MainBase.Get(team == Team.Player ? Team.Enemy : Team.Player);
        if (enemyBase != null && !IsOrdered(enemyBase) && MayAttackBase(enemyBase) && enemyBase.CanAccept(squad, out _))
        {
            var f = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), enemyBase.DefenderPower);
            if (f != BattleForecast.Lose)
            {
                float value = baseAttackValue * (2f - (float)enemyBase.Hp / enemyBase.MaxHp);
                if (f == BattleForecast.Equal) value *= 0.5f;
                float score = value / (Vector3.Distance(home, enemyBase.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = enemyBase; }
            }
        }
        return best;
    }

    /// <summary>Едет ли уже к этой цели одна из наших команд.</summary>
    private bool IsOrdered(ISquadTarget target)
    {
        foreach (ISquadTarget t in orders.Values)
            if (ReferenceEquals(t, target)) return true;
        return false;
    }

    // ---------- Деньги ----------

    /// <summary>Нанять самого сильного доступного героя, на которого хватает денег.</summary>
    private bool TryHire()
    {
        HeroData best = null;
        foreach (HeroData h in HM.GetRoster(team))
        {
            if (!HM.CanHire(team, h, out _) || !RM.CanAfford(team, h.hireCost)) continue;
            if (best == null || h.stars > best.stars || (h.stars == best.stars && h.hireCost > best.hireCost)) best = h;
        }
        if (best == null || !HM.TryHire(team, best, out _)) return false;
        Log($"нанял {best.displayName} ({best.stars}★)");
        return true;
    }

    /// <summary>Построить следующее здание из плана (если есть свободная ячейка и деньги).</summary>
    private bool TryBuild()
    {
        if (buildOrder == null) return false;
        int slot = FreeSlot();
        if (slot < 0) return false;

        var planned = new Dictionary<BuildingData, int>(); // сколько штук каждой постройки план требует "к этому моменту"
        foreach (BuildingData b in buildOrder)
        {
            if (b == null) continue;
            planned.TryGetValue(b, out int need);
            planned[b] = ++need;
            if (CountBuilt(b) >= need) continue;      // этот пункт плана уже выполнен
            if (!Base.CanBuildType(b)) continue;      // пока нельзя (не открыта / уже есть)
            BuildingLevel l = b.GetLevel(1);
            if (!RM.CanAfford(team, l.goldCost, l.plutoniumCost)) return false; // копим на этот пункт
            if (Base.TryBuild(slot, b, out _)) { Log($"построил {b.displayName}"); return true; }
            return false;
        }

        // План выполнен — строим открытые постройки, которых ещё нет (например, Лабораторию)
        foreach (BuildingData b in Base.GetBuildableList())
        {
            if (b.allowMultiple && CountBuilt(b) > 0) continue;
            BuildingLevel l = b.GetLevel(1);
            if (RM.CanAfford(team, l.goldCost, l.plutoniumCost) && Base.TryBuild(slot, b, out _))
            {
                Log($"построил {b.displayName}");
                return true;
            }
        }
        return false;
    }

    /// <summary>Улучшить одну постройку (Бараки — в первую очередь).</summary>
    private bool TryUpgradeBuildings()
    {
        int bestSlot = -1;
        for (int i = 0; i < MainBase.MaxSlots; i++)
        {
            BuildingInstance b = Base.GetBuilding(i);
            if (b == null || !Base.CanUpgradeBuilding(i, out _)) continue;
            BuildingLevel next = b.Data.GetLevel(b.Level + 1);
            if (!RM.CanAfford(team, next.goldCost, next.plutoniumCost)) continue;
            bestSlot = i;
            if (b.Data.type == BuildingType.Barracks) break;
        }
        if (bestSlot < 0 || !Base.TryUpgradeBuilding(bestSlot, out _)) return false;
        Log($"улучшил {Base.GetBuilding(bestSlot).Data.displayName} до ур. {Base.GetBuilding(bestSlot).Level}");
        return true;
    }

    /// <summary>Улучшить базу, когда все открытые ячейки заняты.</summary>
    private bool TryUpgradeBase()
    {
        if (Base.IsMaxLevel || FreeSlot() >= 0 || !RM.CanAfford(team, Base.UpgradeCost)) return false;
        if (!Base.TryUpgradeBase(out _)) return false;
        Log($"улучшил базу до ур. {Base.Level}");
        return true;
    }

    /// <summary>Усилить самого сильного героя за плутоний (нужен захваченный Институт).</summary>
    private bool TryBoostHero()
    {
        HeroInstance best = null;
        foreach (HeroInstance h in HM.GetHeroes(team))
        {
            if (!HM.CanBoost(h, out _) || !RM.CanAfford(team, 0, HM.GetBoostCost(h))) continue;
            if (best == null || HeroPower(h) > HeroPower(best)) best = h;
        }
        if (best == null || !HM.TryBoost(best, out _)) return false;
        Log($"усилил {best.Data.displayName} (усилений: {best.Boosts})");
        return true;
    }

    /// <summary>Прокачать самого дешёвого для прокачки героя, если золота с запасом.</summary>
    private bool TryLevelUpHero()
    {
        HeroInstance best = null;
        int bestCost = int.MaxValue;
        foreach (HeroInstance h in HM.GetHeroes(team))
        {
            int cost = HM.GetLevelUpCost(h);
            if (cost <= 0 || cost >= bestCost) continue;
            best = h;
            bestCost = cost;
        }
        // Если ячейки заняты, а базу можно улучшить — сначала копим на базу
        int reserve = levelUpGoldReserve;
        if (FreeSlot() < 0 && !Base.IsMaxLevel) reserve = Mathf.Max(reserve, Base.UpgradeCost);
        if (best == null || RM.GetGold(team) - bestCost < reserve) return false;
        if (!HM.TryLevelUp(best, out _)) return false;
        Log($"прокачал {best.Data.displayName} до ур. {best.Level}");
        return true;
    }

    // ---------- Помощники ----------

    /// <summary>Первая свободная открытая ячейка базы (или -1).</summary>
    private int FreeSlot()
    {
        for (int i = 0; i < Base.OpenSlots; i++)
            if (Base.GetBuilding(i) == null) return i;
        return -1;
    }

    /// <summary>Сколько штук такой постройки уже стоит на базе.</summary>
    private int CountBuilt(BuildingData data)
    {
        int n = 0;
        for (int i = 0; i < MainBase.MaxSlots; i++)
        {
            BuildingInstance b = Base.GetBuilding(i);
            if (b != null && b.Data == data) n++;
        }
        return n;
    }

    /// <summary>Сила одного героя (для выбора капитана).</summary>
    private static int HeroPower(HeroInstance h)
    {
        return BattleCalculator.HeroPower(h);
    }

    private static bool Contains(IReadOnlyList<Squad> list, Squad s)
    {
        foreach (Squad x in list) if (x == s) return true;
        return false;
    }

    /// <summary>Сообщение в консоль (если включено).</summary>
    private void Log(string text)
    {
        if (logActions) Debug.Log($"<color=#FF7A7A>[ИИ врага]</color> {text}");
    }
}
