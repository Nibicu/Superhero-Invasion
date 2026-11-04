using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Искусственный интеллект врага. Раз в thinkInterval секунд "думает".
/// Показатель силы ИИ больше не сравнивает (сила и реальный исход боя — разные вещи) — смотрит на ЧИСЛО героев:
///  1) собирает команды из свободных злодеев (по 4);
///  2) ПОРЯДОК ЦЕЛЕЙ: сначала ВАЖНЫЕ — объект события, общие миссии, портал (большой приоритет, только ПОЛНАЯ
///     команда — все 4 слота). Если полной команды дома нет — собирает "ударную" из сильнейших (прокачанных)
///     героев других команд на базе; пока важная цель ждёт, дома героев на обычные миссии не отпускает;
///  3) потом обычные миссии — команда не меньше рекомендованного числа героев (или с хорошим шансом);
///  4) нападение на базу — в последнюю очередь: только если этой команде больше некуда идти,
///     команда полная (4) и все её герои не ниже 2 уровня;
///  5) отступает, если в битве за флаг у противника героев больше (3 против 4) — потом придёт полной командой;
///  6) гарнизон дома не держит. Если на базу напали — отзывает команды с ОБЫЧНЫХ миссий (чтобы дома было 3–4 героя),
///     команды с важных целей не трогает. Только если у базы осталось HP на одну атаку — отзывает и самую сильную;
///  7) тратит золото на одно действие: найм, постройку по плану, улучшение построек или базы.
/// Объект события тем ценнее, чем ближе конец события. Во время события и первые 2 минуты игры
/// (мирное время) на базу не нападает; за полминуты до события не уходит на долгие дела.
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
    [Tooltip("ИИ берёт миссию, только если шанс успеха (по числу героев) не ниже этого")]
    [SerializeField, Range(0f, 1f)] private float minMissionChance = 0.6f;
    [Tooltip("На базу противника ИИ нападает, только если все герои команды не ниже этого уровня (и больше идти некуда)")]
    [SerializeField] private int minBaseAttackLevel = 2;
    [Tooltip("Во сколько раз объекты, портал и общая миссия ценнее остальных целей")]
    [SerializeField] private float objectPriority = 3f;
    [Tooltip("Если у базы противника HP не больше этого — ИИ старается её добить (даже при равных силах)")]
    [SerializeField] private int finishBaseHp = 400;
    [Tooltip("Мирное время: первые столько секунд игры ИИ не нападает на базу противника (даже пустую)")]
    [SerializeField] private float baseAttackGrace = 120f;

    [Header("Для тестов")]
    [Tooltip("Этого героя ИИ нанимает первым, как только хватит денег (пусто — как обычно, самого сильного)")]
    [SerializeField] private HeroData hireFirst;

    [Header("Перестройка команд")]
    [Tooltip("Новую команду ИИ создаёт, только если для неё есть хотя бы столько свободных героев")]
    [SerializeField] private int minSquadSize = 2;
    [Tooltip("Собирать полную ударную команду (4 героя) из других команд для события, портала или общей миссии")]
    [SerializeField] private bool allowRegroup = true;

    [Header("События")]
    [Tooltip("За сколько секунд до события ИИ перестаёт отправлять команды на долгие дела")]
    [SerializeField] private float eventPrepTime = 35f;
    [Tooltip("Ценность объекта события (растёт к концу события: последний владелец получит артефакт)")]
    [SerializeField] private float eventObjectValue = 900f;

    private float timer;                                                        // До следующего решения
    private float playTime;                                                     // Сколько секунд идёт игра (время карты)
    private readonly Dictionary<Squad, ISquadTarget> orders = new Dictionary<Squad, ISquadTarget>(); // Куда уже отправлены команды
    private Squad strikeSquad;          // Ударная команда, собранная для важной цели (или null)
    private AttackableSite strikeTarget; // Для какой цели её собрали

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
        DefendHome();       // на базу напали — отозвать команды с обычных миссий
        RetreatFromLostFights();
        Regroup();          // для важной цели нет полной команды — собрать ударную
        SplitBigSquads();   // важных целей нет — снова команды по 2–4 героя
        FormSquads();
        SendDefenders();
        DispatchSquads();

        // Плутоний тратится отдельно: прокачка в Лаборатории, потом усиление (если есть Институт)
        // Прокачка уровня за плутоний и усиление убраны — герои растут за счёт опыта

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
    /// Распределить свободных героев. Свободных хватает на новую команду (minSquadSize, обычно 2) —
    /// создаём её (до fullSquadSize героев): больше команд — больше миссий и денег.
    /// Остальных добавляем в команды на базе до fullSquadSize; если команд максимум — до предела (5).
    /// Сильную команду для важной цели собирает Regroup.
    /// </summary>
    private void FormSquads()
    {
        List<HeroInstance> free = SM.GetFreeHeroes(team);
        if (free.Count == 0) return;
        free.Sort((a, b) => HeroPower(b).CompareTo(HeroPower(a))); // сильные первыми

        if (SM.CanCreateSquad(team) && (free.Count >= minSquadSize || SM.GetSquads(team).Count == 0))
        {
            HeroInstance cap = free[0];
            var mem = new List<HeroInstance>();
            for (int i = 1; i < free.Count && mem.Count < fullSquadSize - 1; i++) mem.Add(free[i]);
            if (SM.TryCreateSquad(team, cap, mem, out Squad created, out _))
            {
                Log($"создал команду {created.Number}, капитан {cap.Data.displayName} (героев: {created.Size})");
                free.Remove(cap);
                foreach (HeroInstance h in mem) free.Remove(h);
            }
        }

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

    }

    /// <summary>
    /// Отступление (по числу героев, не по силе): в битве за флаг у противника героев больше, чем у нас
    /// (например, 3 против 4), — отзываем команду домой; Regroup потом соберёт полную команду из 4.
    /// Нападение на базу в неподходящий момент (событие, мирное время) тоже отменяем.
    /// </summary>
    private void RetreatFromLostFights()
    {
        foreach (var pair in new List<KeyValuePair<Squad, ISquadTarget>>(orders))
        {
            Squad squad = pair.Key;
            if (!(pair.Value is AttackableSite site)) continue;
            if (site.TryGetDefendingTeam(out Team owner) && owner == team) continue; // это защита своего объекта
            if (!SM.CanRetreat(squad, out _)) continue; // бой уже начался — поздно

            // Команда противника у той же цели: она в битве за флаг или напала первой (мы едем перебивать)
            Squad rival = null;
            if (site.IsContested)
            {
                SquadUnit foe = site.PlayerContestant != null && site.PlayerContestant.Squad.Owner != team ? site.PlayerContestant : site.EnemyContestant;
                if (foe != null && foe.Squad.Owner != team) rival = foe.Squad;
            }
            else if (site.IsUnderAttack && site.AttackingTeam != team) rival = site.Attacker.Squad;

            bool outnumbered = rival != null && rival.Size > squad.Size;
            bool badBaseAttack = site is MainBase mb && !MayAttackBase(mb);
            if (!outnumbered && !badBaseAttack) continue;
            if (SM.TryRetreat(squad, out _))
            {
                orders.Remove(squad);
                Log(badBaseAttack ? $"команда {squad.Number} отменяет нападение на «{site.SiteName}»"
                                  : $"команда {squad.Number} ({squad.Size} гер.) отступает от «{site.SiteName}»: у противника {rival.Size} — придём полной командой");
            }
        }
    }

    /// <summary>
    /// Защита своей базы без гарнизона: на базу напали (идёт подготовка) — отзываем команды с ОБЫЧНЫХ миссий,
    /// пока дома (и на пути домой) не наберётся 4 героя. Команды с важных целей (портал, объект, общая миссия)
    /// не трогаем. Исключение: у базы HP только на одну атаку — домой отзывается и самая сильная команда.
    /// </summary>
    private void DefendHome()
    {
        MainBase home = Base;
        if (home == null || !home.IsUnderAttack || home.Phase != AttackPhase.Preparing || home.AttackingTeam == team) return;

        // Сколько героев будет дома: уже дома + команды, которые возвращаются
        int coming = SM.GetHeroesAtHome(team).Count;
        foreach (Squad s in SM.GetSquads(team))
            if (s.Status == SquadStatus.Returning) coming += s.Size;
        bool lastStand = home.Hp <= home.DamagePerAttack; // ещё одна атака — и поражение

        // Последний рубеж: самая сильная команда (откуда угодно) — домой
        if (lastStand)
        {
            Squad strongest = null;
            foreach (Squad s in SM.GetSquads(team))
            {
                if (s.Status == SquadStatus.AtBase || s.Status == SquadStatus.Returning || !SM.CanRetreat(s, out _)) continue;
                if (strongest == null || TeamPower(s) > TeamPower(strongest)) strongest = s;
            }
            if (strongest != null && SM.TryRetreat(strongest, out _))
            {
                orders.Remove(strongest);
                coming += strongest.Size;
                Log($"база почти разрушена — самая сильная команда {strongest.Number} возвращается защищать");
            }
        }
        if (coming >= FullSize) return;

        // Команды с обычных миссий — ближние первыми (успеют вернуться)
        var candidates = new List<Squad>();
        foreach (var pair in orders)
            if (pair.Value is MissionMarker && pair.Key.Status != SquadStatus.Returning && SM.CanRetreat(pair.Key, out _))
                candidates.Add(pair.Key);
        candidates.Sort((a, b) => SM.EstimateTravelTime(a, orders[a]).CompareTo(SM.EstimateTravelTime(b, orders[b]))); // путь до миссии ≈ путь обратно
        foreach (Squad s in candidates)
        {
            if (coming >= FullSize) break;
            if (!SM.TryRetreat(s, out _)) continue;
            orders.Remove(s);
            coming += s.Size;
            Log($"на базу напали — команда {s.Number} бросает миссию и едет защищать базу (дома будет героев: {coming})");
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
            int guards = o.GuardPower; // у захваченного объекта охраны нет
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
    /// Можно ли вообще думать о нападении на базу противника: не в мирное время в начале игры,
    /// не во время события и не перед ним (там цели важнее). Гарнизон дома ИИ больше не держит.
    /// </summary>
    private bool MayAttackBase(MainBase enemyBase)
    {
        if (playTime < baseAttackGrace) return false; // мирное время в начале игры
        if (EventRunning || EventSoon) return false;
        return enemyBase.Hp > 0;
    }

    /// <summary>Сколько героев защищает базу противника прямо сейчас (дома, до 4).</summary>
    private static int DefendersCount(MainBase b)
    {
        Squad d = b.FindDefender();
        return d != null ? d.Size : 0;
    }

    // ---------- Число героев ----------

    /// <summary>Полная команда: капитан + все слоты (4 героя).</summary>
    private int FullSize => SM.MaxMembers + 1;

    /// <summary>Сколько всего героев у ИИ может ходить на задания (без запасных).</summary>
    private int TotalHeroes
    {
        get
        {
            int n = 0;
            foreach (HeroInstance h in HM.GetHeroes(team)) if (!HM.IsReserve(h)) n++;
            return n;
        }
    }

    /// <summary>
    /// Сколько героев нужно для важной цели: полная команда (4). Если у ИИ всего меньше 4 героев —
    /// все, что есть, но не меньше рекомендации цели.
    /// </summary>
    private int NeedSize(AttackableSite site) => Mathf.Min(FullSize, Mathf.Max(site.RecommendedHeroes, TotalHeroes));

    /// <summary>Сила команды — только чтобы выбрать, какие герои "прокачаннее" (решения о бое по ней не принимаются).</summary>
    private static int TeamPower(Squad s)
    {
        int p = 0;
        foreach (HeroInstance h in s.AllHeroes) p += HeroPower(h);
        return p;
    }

    // ---------- Перестройка команд ----------

    /// <summary>
    /// Важные цели сейчас: портал, объект события (не наш), общая миссия.
    /// На каждую идёт только полная команда (NeedSize).
    /// </summary>
    private List<AttackableSite> ImportantTargets()
    {
        var list = new List<AttackableSite>();
        Portal p = Portal.Instance;
        if (p != null && p.IsOpen && !p.IsClosed && p.TimeLeft > 40f) list.Add(p);
        MapObject o = EM != null ? EM.EventObject : null;
        if (o != null && o.IsActive && !o.IsOwnedBy(team) && o.EventTimeLeft > 40f) list.Add(o);
        if (GlobalMissionManager.Instance != null)
            foreach (GlobalMission g in GlobalMissionManager.Instance.Active)
                if (!g.IsCompleted) list.Add(g);
        return list;
    }

    /// <summary>Цель ещё актуальна (событие идёт, портал не закрыт, миссия не выполнена, объект не наш).</summary>
    private bool IsStillImportant(AttackableSite site)
    {
        if (site == null) return false;
        if (site is Portal p) return p.IsOpen && !p.IsClosed;
        if (site is MapObject o) return EM != null && o == EM.EventObject && o.IsActive && !o.IsOwnedBy(team);
        if (site is GlobalMission g) return !g.IsCompleted;
        return false;
    }

    /// <summary>
    /// Ударная команда: для важной цели (портал, объект события, общая миссия) нужна ПОЛНАЯ команда (4 героя).
    /// Если на базе такой нет — берём самую сильную команду на базе (или первого свободного героя) и добавляем
    /// к ней сильнейших (прокачанных) героев из других команд на базе и свободных — до 4.
    /// Не хватает героев дома — ждём (DispatchSquads пока не отпускает их на обычные миссии).
    /// </summary>
    private void Regroup()
    {
        // Ударная команда уже собрана и ещё дома, а её цель актуальна — ничего не трогаем
        if (strikeSquad != null && (!Contains(SM.GetSquads(team), strikeSquad) || strikeSquad.Status != SquadStatus.AtBase || !IsStillImportant(strikeTarget)))
        {
            strikeSquad = null;
            strikeTarget = null;
        }
        if (!allowRegroup || strikeSquad != null) return;
        if (Base.IsUnderAttack) return; // пока базу атакуют, менять команды нельзя

        float minHp = EventRunning ? Mathf.Min(minHpToSend, 0.5f) : minHpToSend;
        var home = new List<Squad>();
        foreach (Squad s in SM.GetSquads(team))
            if (s.Status == SquadStatus.AtBase && !orders.ContainsKey(s)) home.Add(s);

        foreach (AttackableSite target in ImportantTargets())
        {
            if (IsOrdered(target)) continue;
            int need = NeedSize(target);

            // Полная здоровая команда уже есть — перестраивать не нужно
            bool ready = false;
            foreach (Squad s in home)
                if (s.Size >= need && s.HpFraction >= minHp) ready = true;
            if (ready) continue;

            // Все, кого можно взять: свободные герои и герои команд на базе — сильные (прокачанные) первыми
            var pool = new List<HeroInstance>(SM.GetFreeHeroes(team));
            foreach (Squad s in home) pool.AddRange(s.AllHeroes);
            pool.Sort((a, b) => HeroPower(b).CompareTo(HeroPower(a)));
            if (pool.Count < need) continue; // героев дома не хватает — ждём, пока вернутся

            // Ядро — самая сильная команда на базе (её капитан останется капитаном); нет команд — новая из свободных
            Squad core = null;
            foreach (Squad s in home)
                if (core == null || TeamPower(s) > TeamPower(core)) core = s;

            var picked = new List<HeroInstance>(); // кого добавить к ядру
            int slots = need - (core != null ? core.Size : 0);
            foreach (HeroInstance h in pool)
            {
                if (slots <= 0) break;
                if (core != null && ContainsHero(core, h)) continue;
                picked.Add(h);
                slots--;
            }

            // Забираем героев из их команд: капитан ушёл — команду распускаем (остальные станут свободными)
            foreach (Squad s in home)
            {
                if (s == core) continue;
                bool takesCaptain = picked.Contains(s.Captain);
                var rest = new List<HeroInstance>();
                foreach (HeroInstance m in s.Members) if (!picked.Contains(m)) rest.Add(m);
                if (takesCaptain) SM.TryDisband(s, out _);
                else if (rest.Count != s.Members.Count) SM.TryEditSquad(s, s.Captain, rest, out _);
            }

            string err;
            if (core != null)
            {
                var members = new List<HeroInstance>(core.Members);
                members.AddRange(picked);
                if (!SM.TryEditSquad(core, core.Captain, members, out err)) { Log($"не смог собрать ударную команду: {err}"); return; }
            }
            else
            {
                if (picked.Count == 0 || !SM.TryCreateSquad(team, picked[0], picked.GetRange(1, picked.Count - 1), out core, out err))
                    return;
            }
            strikeSquad = core;
            strikeTarget = target;
            Log($"собрал полную ударную команду {core.Number} (героев: {core.Size}) для «{target.SiteName}»");
            return;
        }
    }

    /// <summary>Есть ли герой в команде (капитан или участник).</summary>
    private static bool ContainsHero(Squad s, HeroInstance h)
    {
        foreach (HeroInstance x in s.AllHeroes) if (x == h) return true;
        return false;
    }

    /// <summary>
    /// Есть ли важная цель, которую никто не взял, а полной команды для неё дома нет —
    /// тогда героев на базе на обычные миссии не отпускаем (копим полную команду).
    /// </summary>
    private bool WaitingForStrike()
    {
        if (strikeSquad != null) return false;
        foreach (AttackableSite t in ImportantTargets())
            if (!IsOrdered(t) && TotalHeroes >= NeedSize(t)) return true; // героев в принципе хватает — копим
        return false;
    }

    /// <summary>
    /// Важных целей нет (затишье без общей миссии) — большие команды (больше fullSquadSize героев)
    /// снова делим: самого слабого героя отпускаем, из свободных героев соберутся новые команды по 2–4.
    /// </summary>
    private void SplitBigSquads()
    {
        if (strikeSquad != null || EventRunning || EventSoon || Base.IsUnderAttack || ImportantTargets().Count > 0) return;
        if (!SM.CanCreateSquad(team)) return; // новую команду всё равно не создать — пусть остаются большими
        foreach (Squad s in SM.GetSquads(team))
        {
            if (s.Status != SquadStatus.AtBase || orders.ContainsKey(s) || s.Size <= fullSquadSize) continue;
            var members = new List<HeroInstance>(s.Members);
            int extra = s.Size - fullSquadSize + 1; // отпускаем столько, чтобы отпущенных хватило на новую команду
            members.Sort((a, b) => HeroPower(a).CompareTo(HeroPower(b))); // слабые первыми
            int release = Mathf.Min(Mathf.Max(extra, minSquadSize - SM.GetFreeHeroes(team).Count), members.Count - 1);
            if (release <= 0) continue;
            members.RemoveRange(0, release);
            if (SM.TryEditSquad(s, s.Captain, members, out _))
                Log($"разделил большую команду {s.Number}: отпустил героев — {release}, в команде {s.Size}");
        }
    }

    /// <summary>
    /// Отправить свободные здоровые команды к лучшим целям — большие (и прокачанные) первыми.
    /// Гарнизон дома не оставляем. Ударная команда сразу едет к своей важной цели.
    /// Если важная цель ждёт полную команду, а собрать её пока не из кого — остальных героев
    /// на обычные миссии не отпускаем (копим), на важные цели — отпускаем.
    /// </summary>
    private void DispatchSquads()
    {
        var ready = new List<Squad>();
        // Во время события медлить нельзя — отправляем и немного раненые команды
        float minHp = EventRunning ? Mathf.Min(minHpToSend, 0.5f) : minHpToSend;
        foreach (Squad s in SM.GetSquads(team))
            if (s.Status == SquadStatus.AtBase && s.HpFraction >= minHp && !orders.ContainsKey(s)) ready.Add(s);
        // Полные команды первыми, среди равных — прокачанные
        ready.Sort((a, b) => a.Size != b.Size ? b.Size.CompareTo(a.Size) : TeamPower(b).CompareTo(TeamPower(a)));

        // Ударная команда — сразу к своей цели
        if (strikeSquad != null && ready.Contains(strikeSquad) && strikeTarget.CanAccept(strikeSquad, out _)
            && SM.SendSquad(strikeSquad, strikeTarget, out _))
        {
            orders[strikeSquad] = strikeTarget;
            ready.Remove(strikeSquad);
            Log($"отправил ударную команду {strikeSquad.Number} (героев: {strikeSquad.Size}) → {strikeTarget.SiteName}");
            strikeSquad = null; // следующую важную цель можно готовить сразу
            strikeTarget = null;
        }

        bool hold = WaitingForStrike(); // копим полную команду для важной цели
        foreach (Squad s in ready)
        {
            if (s == strikeSquad) continue; // ждёт, пока её цель освободится
            ISquadTarget target = ChooseTarget(s, hold);
            if (target == null) continue; // этой команде целей не нашлось — может, найдётся другой
            if (SM.SendSquad(s, target, out _))
            {
                orders[s] = target;
                Log($"отправил команду {s.Number} ({s.Size} гер.) → {target.TargetName}");
            }
        }
    }

    /// <summary>
    /// Выбрать самую выгодную цель для команды (ценность / расстояние). Решения — по числу героев, не по силе:
    /// важные цели (общая миссия, портал, объект события) — только полной командой (NeedSize)
    /// и с большим приоритетом; обычные миссии — если героев хватает по рекомендации (или шанс хороший);
    /// база противника — только если больше идти некуда (см. CanAttackBaseWith).
    /// onlyImportant — обычные миссии и базу не брать (копим полную команду для важной цели).
    /// </summary>
    private ISquadTarget ChooseTarget(Squad squad, bool onlyImportant)
    {
        Vector3 home = Base.transform.position;
        ISquadTarget best = null;
        float bestScore = 0f;

        // Миссии: только свободные и с хорошим шансом (шанс считается по числу героев)
        if (MissionManager.Instance != null && !onlyImportant)
            foreach (MissionMarker m in MissionManager.Instance.Active)
            {
                if (m == null || m.Side != team || m.AssignedSquad != null || IsOrdered(m)) continue;
                if (m.Data.GetSuccessChance(squad.Size) < minMissionChance) continue;
                if (!m.CanAccept(squad, out _)) continue;
                // Перед событием — только миссии, после которых команда успеет вернуться
                if (EventSoon && SM.EstimateTravelTime(squad, m) * 2f + m.Data.duration > EM.TimeLeft + 15f) continue;
                float value = m.Data.rewardGold + m.Data.rewardPlutonium * 15f
                              + (m.Data.levelUpTeam ? 300f : 0f) + (m.Data.unlockBuilding != null ? 500f : 0f);
                // Большую команду на лёгкую миссию — неохотно (она нужнее на важных целях)
                if (squad.Size > m.Data.recommendedHeroes + 1) value *= 0.6f;
                float score = value / (Vector3.Distance(home, m.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = m; }
            }

        // Общая миссия: только полной командой. Если её уже выполняет противник — вступаем в битву за флаг,
        // только если успеем и у нас героев не меньше, чем у него
        if (GlobalMissionManager.Instance != null)
            foreach (GlobalMission gm in GlobalMissionManager.Instance.Active)
            {
                if (gm.IsCompleted || IsOrdered(gm) || squad.Size < NeedSize(gm) || !gm.CanAccept(squad, out _)) continue;
                bool join = gm.IsUnderAttack;
                if (join && !CanContest(squad, gm)) continue;
                GlobalMissionData d = gm.Data;
                float value = d.rewardGold + d.rewardPlutonium * 15f + d.rewardArtifacts * 400f + d.rewardFactionPoints;
                value *= objectPriority; // общая миссия — большой приоритет
                if (join) value *= 1.6f; // перебить противника — заодно не дать ему награду
                if (d.rewardHero) value += 900f; // герой в награду — очень ценно
                float score = value / (Vector3.Distance(home, gm.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = gm; }
            }

        // Портал (событие): закрыть его — артефакт и урон базе противника, не закрыть — урон нам. Только полной командой
        Portal portal = Portal.Instance;
        if (portal != null && portal.IsOpen && !portal.IsClosed && !IsOrdered(portal)
            && squad.Size >= NeedSize(portal) && portal.CanAccept(squad, out _))
        {
            bool join = portal.IsUnderAttack;
            bool ok = (!join || CanContest(squad, portal))
                      && SM.EstimateTravelTime(squad, portal) + portal.PrepTime < portal.TimeLeft; // успеть до конца события
            if (ok)
            {
                float value = 1200f + portal.Data.rewardArtifacts * 400f + portal.Data.rewardFactionPoints;
                if (join) value *= 1.5f; // не дать противнику закрыть портал без боя
                value *= objectPriority;
                float score = value / (Vector3.Distance(home, portal.ApproachPoint) + 5f);
                if (score > bestScore) { bestScore = score; best = portal; }
            }
        }

        // Объекты (событие «Объект»): только полной командой, никто из наших туда не едет
        foreach (MapObject o in MapObject.All)
        {
            if (o.IsOwnedBy(team) || IsOrdered(o) || squad.Size < NeedSize(o) || !o.CanAccept(squad, out _)) continue;
            bool join = o.IsUnderAttack;
            if (join && !CanContest(squad, o)) continue;
            MapObjectData d = o.Data;
            float value = d.eventGold + d.eventPlutonium * 15f + d.eventFactionPoints + 400f; // + артефакт

            // Объект события: успеть до конца события
            if (EM != null && o == EM.EventObject)
            {
                if (SM.EstimateTravelTime(squad, o) + o.PrepTime + 3f > o.EventTimeLeft) continue;
                value += eventObjectValue;
            }
            value *= objectPriority; // объекты — большой приоритет
            if (join) value *= 1.3f; // заодно не дать объект противнику
            float score = value / (Vector3.Distance(home, o.ApproachPoint) + 5f);
            if (score > bestScore) { bestScore = score; best = o; }
        }

        // База противника — в последнюю очередь: только если ни важной цели, ни обычной миссии для команды нет
        if (best != null || onlyImportant) return best;
        MainBase enemyBase = MainBase.Get(team == Team.Player ? Team.Enemy : Team.Player);
        if (enemyBase != null && !IsOrdered(enemyBase) && CanAttackBaseWith(squad, enemyBase)
            && MayAttackBase(enemyBase) && enemyBase.CanAccept(squad, out _))
        {
            int defenders = DefendersCount(enemyBase);
            if (defenders <= squad.Size || enemyBase.Hp <= finishBaseHp) return enemyBase;
        }
        return null;
    }

    /// <summary>
    /// Готова ли команда напасть на базу: полная (4 героя) и все её герои не ниже minBaseAttackLevel (2) уровня.
    /// </summary>
    private bool CanAttackBaseWith(Squad squad, MainBase enemyBase)
    {
        if (squad.Size < NeedSize(enemyBase)) return false;
        foreach (HeroInstance h in squad.AllHeroes)
            if (h.Level < minBaseAttackLevel) return false;
        return true;
    }

    /// <summary>
    /// Можно ли вступить в битву за флаг: противник ещё готовится, мы успеем доехать до конца подготовки
    /// и у нас героев не меньше, чем у него.
    /// </summary>
    private bool CanContest(Squad squad, AttackableSite site) =>
        site.CanJoin(team) && SM.EstimateTravelTime(squad, site) <= site.PrepLeft - 1f
        && squad.Size >= site.Attacker.Squad.Size;

    /// <summary>Едет ли уже к этой цели одна из наших команд.</summary>
    private bool IsOrdered(ISquadTarget target)
    {
        foreach (ISquadTarget t in orders.Values)
            if (ReferenceEquals(t, target)) return true;
        return false;
    }

    // ---------- Деньги ----------

    /// <summary>
    /// Тактическая пауза (вызывает PhaseManager): ИИ нанимает героев, прокачивает и усиливает их,
    /// надевает вещи, строит.
    /// </summary>
    public void OnTacticalPause()
    {
        // Открыть иконки фракции, на которые хватает очков (после всех 5 можно нанять своего 3★)
        if (FactionManager.Instance != null)
            for (int i = 0; i < 10 && FactionManager.Instance.TryUnlockNext(team); i++)
                Log($"открыл иконку фракции {FactionManager.Instance.GetUnlocked(team)}/{FactionManager.Instance.IconCount}");
        for (int i = 0; i < 10 && TryHire(); i++) { }
        for (int i = 0; i < 10 && (TryBuild() || TryUpgradeBuildings() || TryUpgradeBase()); i++) { } // строить можно только в паузе
        // Повысить звезду героям 5 уровня (сильнейшим первыми), пока хватает плутония
        var heroes = new List<HeroInstance>(HM.GetHeroes(team));
        heroes.Sort((a, b) => BattleCalculator.HeroPower(b).CompareTo(BattleCalculator.HeroPower(a)));
        foreach (HeroInstance h in heroes)
            if (HM.CanStarUp(h, out _) && RM.CanAfford(team, 0, HM.StarUpCost)) HM.TryStarUp(h, out _);
        UseArtifacts();
    }

    /// <summary>Драфт перед игрой: нанять одного героя (карта стоит, поэтому вызывает DraftUI). Вернёт false, если больше некого.</summary>
    public bool DraftHire() => TryHire();

    /// <summary>Нанять самого сильного доступного героя, на которого хватает денег.</summary>
    private bool TryHire()
    {
        HeroData best = null;
        // Для тестов: нужный герой — первым
        if (hireFirst != null && HM.CanHire(team, hireFirst, out _) && RM.CanAfford(team, hireFirst.hireCost))
            best = hireFirst;
        else foreach (HeroData h in HM.GetRoster(team))
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
