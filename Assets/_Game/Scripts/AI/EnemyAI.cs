using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Искусственный интеллект врага. Раз в thinkInterval секунд "думает":
///  1) собирает команды из свободных злодеев;
///  2) отправляет свободные здоровые команды к самой выгодной цели. Важность целей:
///     портал и объект события → общая миссия → миссии злодеев → нападение на базу.
///     Объект события тем ценнее, чем ближе конец события (артефакт получит последний владелец).
///     На портал ходит и тогда, когда закрыть его сразу не получится — набирать прогресс по территориям.
///     Базу противника атакует, если она пустая, если у неё мало HP или если наша команда
///     точно сильнее гарнизона и дома остаётся ещё команда. Во время события и первые 2 минуты игры
///     (мирное время) на базу не нападает;
///  3) за полминуты до события не отправляет команды на долгие дела — готовится к событию;
///  4) отступает, если охрана цели оказалась сильнее (на верное поражение не идёт);
///  5) тратит золото на одно действие: найм, постройку по плану, улучшение построек или базы.
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
    [Tooltip("Во сколько раз объекты, портал и общая миссия ценнее остальных целей")]
    [SerializeField] private float objectPriority = 3f;
    [Tooltip("Если у базы противника HP не больше этого — ИИ старается её добить (даже при равных силах)")]
    [SerializeField] private int finishBaseHp = 400;
    [Tooltip("Мирное время: первые столько секунд игры ИИ не нападает на базу противника (даже пустую)")]
    [SerializeField] private float baseAttackGrace = 120f;

    [Header("События")]
    [Tooltip("За сколько секунд до события ИИ перестаёт отправлять команды на долгие дела")]
    [SerializeField] private float eventPrepTime = 35f;
    [Tooltip("Ценность объекта события (растёт к концу события: последний владелец получит артефакт)")]
    [SerializeField] private float eventObjectValue = 900f;

    private float timer;                                                        // До следующего решения
    private float playTime;                                                     // Сколько секунд идёт игра (время карты)
    private readonly Dictionary<Squad, ISquadTarget> orders = new Dictionary<Squad, ISquadTarget>(); // Куда уже отправлены команды

    private ResourceManager RM => ResourceManager.Instance;
    private HeroManager HM => HeroManager.Instance;
    private SquadManager SM => SquadManager.Instance;
    private MainBase Base => MainBase.Get(team);
    private EventManager EM => EventManager.Instance;

    /// <summary>Скоро начнётся событие (затишье почти кончилось) — готовимся.</summary>
    private bool EventSoon => EM != null && !EM.IsRunning && EM.TimeLeft < eventPrepTime;

    /// <summary>Идёт событие.</summary>
    private bool EventRunning => EM != null && EM.IsRunning;

    private void Start() => timer = startDelay;

    /// <summary>Отсчитываем время до следующего решения.</summary>
    private void Update()
    {
        if (!aiEnabled || Base == null) return;
        playTime += WorldTime.DeltaTime;
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
        SendDefenders();
        DispatchSquads();

        // Плутоний тратится отдельно: прокачка в Лаборатории, потом усиление (если есть Институт)
        if (!TryLevelUpHero()) TryBoostHero();

        // Артефакты из инвентаря: активируемые — сразу использовать, вещи — надеть на сильных героев
        UseArtifacts();

        // Одно "денежное" действие за раз — по приоритету
        if (HM.GetHeroes(team).Count < 2 && TryHire()) return;
        if (TryBuild()) return;
        if (TryHire()) return;
        if (TryUpgradeBuildings()) return;
        TryUpgradeBase();
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
            if (site.TryGetDefendingTeam(out Team owner) && owner == team) continue; // это защита своего объекта
            if (!SM.CanRetreat(squad, out _)) continue; // бой уже начался — поздно

            int power = BattleCalculator.SquadPower(squad);
            bool lose = BattleCalculator.Forecast(power, site.DefenderPower) == BattleForecast.Lose;
            // Портал: смотрим на НАШИ непройденные территории; если следующую потянем — идём набирать прогресс
            // Если едем перебивать противника (битва за флаг) — охрану не считаем, важна только его команда
            if (site is Portal pt)
                lose = !(pt.IsContested || (pt.IsUnderAttack && pt.AttackingTeam != team))
                       && BattleCalculator.Forecast(power, pt.RemainingPower(team)) == BattleForecast.Lose && !CanFarmPortal(pt, power);
            // Битва за флаг: команда противника намного сильнее — тоже уходим
            SquadUnit foe = site.IsContested ? site.PlayerContestant : null;
            if (foe != null && foe.Squad.Owner != team &&
                BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.SquadPower(foe.Squad)) == BattleForecast.Lose)
                lose = true;
            bool badBaseAttack = site is MainBase mb && !MayAttackBase(mb);
            if (!lose && !badBaseAttack) continue;
            if (SM.TryRetreat(squad, out _))
                Log(badBaseAttack ? $"команда {squad.Number} отменяет нападение на «{site.SiteName}»"
                                  : $"команда {squad.Number} отступает от «{site.SiteName}»: охрана слишком сильная");
        }
    }

    /// <summary>
    /// Защита своих объектов: если противник готовится захватить наш объект,
    /// а охрана одна не справится — отправляем команду, которая успеет доехать
    /// до конца подготовки и вместе с охраной не даст противнику уверенно победить.
    /// Из подходящих команд выбираем самую слабую (сильные нужнее для захватов).
    /// </summary>
    private void SendDefenders()
    {
        foreach (MapObject o in MapObject.All)
        {
            if (!o.CanReinforce(team) || IsOrdered(o)) continue;
            int attackPower = BattleCalculator.SquadPower(o.Attacker.Squad);
            int guards = BattleCalculator.GarrisonPower(o.Data);
            if (BattleCalculator.Forecast(attackPower, guards) == BattleForecast.Lose) continue; // охрана справится сама
            // Объект события — главный: шлём самую сильную подходящую команду (и раненых чуть охотнее)
            bool isEvent = EM != null && o == EM.EventObject;

            Squad best = null;
            int bestPower = isEvent ? int.MinValue : int.MaxValue;
            foreach (Squad s in SM.GetSquads(team))
            {
                if (s.Status != SquadStatus.AtBase || orders.ContainsKey(s) || s.HpFraction < (isEvent ? 0.3f : 0.4f)) continue; // сильно раненых не шлём
                int p = BattleCalculator.SquadPower(s);
                if (BattleCalculator.Forecast(attackPower, guards + p) == BattleForecast.Win) continue; // не удержим
                if (SM.EstimateTravelTime(s, o) > o.PrepLeft - 1f) continue;                        // не успеем
                if (isEvent ? p > bestPower : p < bestPower) { bestPower = p; best = s; }
            }
            if (best != null && SM.SendSquad(best, o, out _))
            {
                orders[best] = o;
                Log($"отправил команду {best.Number} на защиту «{o.Data.displayName}»");
            }
        }
    }

    /// <summary>
    /// Можно ли вообще думать о нападении на базу противника:
    /// не во время события и не перед ним (есть цели важнее), и
    /// база пустая, или у неё мало HP, или у нас есть вторая команда (одна остаётся дома).
    /// Хватит ли сил конкретной команде — решает BaseAttackForecastOk.
    /// </summary>
    private bool MayAttackBase(MainBase enemyBase)
    {
        if (playTime < baseAttackGrace) return false; // мирное время в начале игры
        if (EventRunning || EventSoon) return false;
        if (enemyBase.DefenderPower <= 0) return true;
        if (enemyBase.Hp <= finishBaseHp) return true;
        return SM.GetSquads(team).Count >= 2;
    }

    /// <summary>
    /// Хватит ли команде сил на нападение: на пустую базу — всегда; добить базу с малым HP —
    /// при равных силах; иначе — только если команда точно сильнее гарнизона.
    /// </summary>
    private bool BaseAttackForecastOk(MainBase enemyBase, int squadPower, out BattleForecast f)
    {
        f = BattleCalculator.Forecast(squadPower, enemyBase.DefenderPower);
        if (enemyBase.DefenderPower <= 0) return true;
        if (enemyBase.Hp <= finishBaseHp) return f != BattleForecast.Lose;
        return f == BattleForecast.Win;
    }

    /// <summary>Хватит ли команде сил пройти хотя бы следующую территорию портала (прогресс сохранится).</summary>
    private bool CanFarmPortal(Portal p, int squadPower)
    {
        int next = p.NextWavePower(team);
        return next > 0 && p.GetProgress(team) + 1 < p.TerritoryCount && squadPower * 0.7f >= next;
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
        // Во время события медлить нельзя — отправляем и немного раненые команды
        float minHp = EventRunning ? Mathf.Min(minHpToSend, 0.5f) : minHpToSend;
        foreach (Squad s in SM.GetSquads(team))
        {
            if (s.Status != SquadStatus.AtBase) continue;
            atBase++;
            if (s.HpFraction >= minHp && !orders.ContainsKey(s)) ready.Add(s);
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
                if (m == null || m.Side != team || m.AssignedSquad != null || IsOrdered(m)) continue;
                if (m.Data.GetSuccessChance(squad.Power) < minMissionChance) continue;
                if (!m.CanAccept(squad, out _)) continue;
                // Перед событием — только миссии, после которых команда успеет вернуться
                if (EventSoon && SM.EstimateTravelTime(squad, m) * 2f + m.Data.duration > EM.TimeLeft + 15f) continue;
                float value = m.Data.rewardGold + m.Data.rewardPlutonium * 15f
                              + (m.Data.levelUpTeam ? 300f : 0f) + (m.Data.unlockBuilding != null ? 500f : 0f);
                float score = value / (Vector3.Distance(home, m.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = m; }
            }

        // Общая миссия: награда как у объекта (золото, плутоний, артефакты), охрана по территориям.
        // Если её уже выполняет противник — вступаем в битву за флаг, только если успеем и не слабее его
        GlobalMission gm = GlobalMissionManager.Instance != null ? GlobalMissionManager.Instance.Current : null;
        if (gm != null && !gm.IsCompleted && !IsOrdered(gm) && !EventSoon && gm.CanAccept(squad, out _))
        {
            bool join = gm.IsUnderAttack;
            bool ok = !join || (gm.CanJoin(team) && SM.EstimateTravelTime(squad, gm) <= gm.PrepLeft - 1f
                      && BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.SquadPower(gm.Attacker.Squad)) != BattleForecast.Lose);
            var forecast = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), gm.DefenderPower);
            if (ok && forecast != BattleForecast.Lose)
            {
                GlobalMissionData d = gm.Data;
                float value = d.rewardGold + d.rewardPlutonium * 15f + d.rewardArtifacts * 400f;
                if (forecast == BattleForecast.Equal) value *= 0.7f;
                value *= objectPriority; // общая миссия ценна, как объект
                if (join) value *= 1.6f; // перебить противника — заодно не дать ему награду
                float score = value / (Vector3.Distance(home, gm.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = gm; }
            }
        }

        // Портал (событие): закрыть его — артефакт и урон базе противника, не закрыть — урон нам.
        // Сила охраны — только ещё не пройденные нами территории
        Portal portal = Portal.Instance;
        if (portal != null && portal.IsOpen && !portal.IsClosed && !IsOrdered(portal) && portal.CanAccept(squad, out _))
        {
            bool join = portal.IsUnderAttack;
            bool ok = !join || (portal.CanJoin(team) && SM.EstimateTravelTime(squad, portal) <= portal.PrepLeft - 1f
                      && BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.SquadPower(portal.Attacker.Squad)) != BattleForecast.Lose);
            ok &= SM.EstimateTravelTime(squad, portal) + portal.PrepTime < portal.TimeLeft; // успеть до конца события
            int power = BattleCalculator.SquadPower(squad);
            var forecast = BattleCalculator.Forecast(power, portal.RemainingPower(team));
            // Закрыть сразу не выйдет — но следующую территорию потянем: идём набирать прогресс
            bool farm = forecast == BattleForecast.Lose && !join && CanFarmPortal(portal, power);
            // Противник уже готовится закрыть портал — вступаем в битву за флаг, даже если охрана нам
            // не по силам: главное, чтобы наша команда была не слабее его (это проверено выше в ok)
            if (ok && (join || forecast != BattleForecast.Lose || farm))
            {
                float value = 1200f + portal.Data.rewardArtifacts * 400f;
                if (forecast == BattleForecast.Equal) value *= 0.8f;
                if (farm) value *= 0.5f;
                if (join) value *= 1.5f; // не дать противнику закрыть портал без боя
                value *= objectPriority;
                float score = value / (Vector3.Distance(home, portal.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = portal; }
            }
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

            // Объект события: успеть до конца; чем ближе конец, тем ценнее (артефакт — последнему владельцу)
            if (EM != null && o == EM.EventObject)
            {
                if (SM.EstimateTravelTime(squad, o) + o.PrepTime + 3f > o.EventTimeLeft) continue;
                value += eventObjectValue * (1f + 1.5f * EM.Progress);
            }

            // Охрана объекта: на верное поражение не идём, на равный бой — неохотно
            var forecast = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), BattleCalculator.GarrisonPower(d));
            if (forecast == BattleForecast.Lose) continue;
            if (forecast == BattleForecast.Equal) value *= 0.7f;
            value *= objectPriority; // объекты — главный приоритет
            if (join) value *= 1.3f; // заодно не дать объект противнику
            float score = value / (Vector3.Distance(home, o.ApproachPoint) + 5f);
            if (score > bestScore) { bestScore = score; best = o; }
        }

        // База противника: пустая, с малым HP или наша команда точно сильнее гарнизона
        // (и дома остаётся ещё команда). Чем меньше у неё HP, тем заманчивее.
        MainBase enemyBase = MainBase.Get(team == Team.Player ? Team.Enemy : Team.Player);
        if (enemyBase != null && !IsOrdered(enemyBase) && MayAttackBase(enemyBase) && enemyBase.CanAccept(squad, out _))
        {
            if (BaseAttackForecastOk(enemyBase, BattleCalculator.SquadPower(squad), out BattleForecast f))
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

    /// <summary>Прокачать в Лаборатории (за плутоний) самого сильного героя, которого ещё можно прокачать.</summary>
    private bool TryLevelUpHero()
    {
        HeroInstance best = null;
        foreach (HeroInstance h in HM.GetHeroes(team))
        {
            if (!HM.CanLevelUp(h, out _) || !RM.CanAfford(team, 0, HM.GetLevelUpCost(h))) continue;
            if (best == null || HeroPower(h) > HeroPower(best)) best = h;
        }
        if (best == null || !HM.TryLevelUp(best, out _)) return false;
        Log($"прокачал {best.Data.displayName} до ур. {best.Level}");
        return true;
    }

    /// <summary>
    /// Артефакты: активируемые используются сразу ("+1 уровень" — самому сильному герою, который ещё растёт),
    /// вещи надеваются на самого сильного героя со свободной ячейкой (кроме тех, кто на задании).
    /// </summary>
    private void UseArtifacts()
    {
        ArtifactManager am = ArtifactManager.Instance;
        if (am == null) return;
        foreach (ArtifactData a in new List<ArtifactData>(am.GetStash(team)))
        {
            if (a.kind == ArtifactKind.Activatable)
            {
                HeroInstance target = null;
                if (a.effect == ArtifactEffect.FreeHeroLevel)
                {
                    foreach (HeroInstance h in HM.GetHeroes(team))
                        if (h.Level < HM.MaxHeroLevel && (target == null || HeroPower(h) > HeroPower(target))) target = h;
                    if (target == null) continue; // пока некого прокачать — артефакт ждёт
                }
                if (am.TryActivate(team, a, target, out _)) Log($"использовал артефакт «{a.displayName}»");
                continue;
            }

            // Кому надеть: сильному герою, которому вещь подходит (перчатка — тем, у кого скилы от Атаки,
            // пояс — от Спец. атаки); неподходящему — только если больше некому
            HeroInstance best = null;
            float bestScore = 0f;
            foreach (HeroInstance h in HM.GetHeroes(team))
            {
                if (h.Status == HeroStatus.OnMission) continue;
                bool hasFree = false;
                foreach (ArtifactData s in h.Slots) if (s == null) hasFree = true;
                if (!hasFree) continue;
                bool useless = (a.bonus.attack > 0 && h.Data.skillDamage != DamageType.Attack)
                            || (a.bonus.specialAttack > 0 && h.Data.skillDamage != DamageType.Special);
                float score = HeroPower(h) * (useless ? 0.3f : 1f);
                if (score > bestScore) { bestScore = score; best = h; }
            }
            if (best != null && am.TryEquipFree(best, a, out _)) Log($"надел «{a.displayName}» на {best.Data.displayName}");
        }
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
