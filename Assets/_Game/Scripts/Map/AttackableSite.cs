using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Этап нападения на объект или базу.</summary>
public enum AttackPhase
{
    None,      // Никто не нападает
    Preparing, // Команда приехала, идёт подготовка к бою (карта не стоит, можно отступить)
    Deciding,  // Игрок выбирает в окне перед боем: автобой или ручной бой
    Fighting   // Идёт ручной бой на арене
}

/// <summary>
/// "Место нападения" — общая основа для объекта карты (MapObject) и главной базы (MainBase).
/// Порядок нападения одинаковый для всех:
///  1) команда приезжает → идёт подготовка к бою (prepTime, 20 с). Можно отступить;
///     если за это время приезжает команда ДРУГОЙ стороны — она тоже вступает (будет битва за флаг);
///     если приезжает команда ВЛАДЕЛЬЦА объекта — она становится подкреплением (защищает объект);
///  2) подготовка закончилась:
///     - две команды → окно перед боем только с "НАЧАТЬ БОЙ" (автобоя нет) → битва за флаг;
///     - защитников нет → нападающие сразу побеждают без боя;
///     - в бою участвует игрок (нападает или защищается) → окно перед боем (10 с на выбор);
///     - бьются враг и нейтральная охрана → автобой сразу;
///  3) автобой даёт итог мгновенно, ручной бой — после арены;
///  4) итог: победа (захват объекта / урон базе) или поражение, команды едут домой.
/// Пока идёт нападение, окна этого места закрыты и покупки в нём недоступны.
/// </summary>
public abstract class AttackableSite : MonoBehaviour, ISquadTarget
{
    [Header("Нападение")]
    [Tooltip("Сколько секунд идёт подготовка к бою после прибытия команды")]
    [SerializeField] private float prepTime = 20f;

    /// <summary>Нападение на какое-то место началось, изменилось или закончилось (окна закрываются/обновляются).</summary>
    public static event Action<AttackableSite> AttackChanged;

    private SquadUnit attacker;   // Фишка нападающей команды (null — никто не нападает)
    private SquadUnit challenger; // Вторая команда другой стороны, успевшая к подготовке (битва за флаг)
    private SquadUnit reinforcement; // Команда владельца, пришедшая на защиту (подкрепление)
    private AttackPhase phase;    // Этап нападения
    private float prepLeft;       // Сколько секунд подготовки осталось

    // ---------- Свойства ----------

    /// <summary>Нападающая команда на месте (null — нападения нет).</summary>
    public SquadUnit Attacker => attacker;

    /// <summary>Вторая нападающая команда (другой стороны) или null.</summary>
    public SquadUnit Challenger => challenger;

    /// <summary>Команда владельца, пришедшая на защиту объекта (или null).</summary>
    public SquadUnit Reinforcement => reinforcement;

    /// <summary>Есть ли подкрепление.</summary>
    public bool HasReinforcement => reinforcement != null;

    /// <summary>Можно ли сюда присылать команды на защиту (объекты — да, база — нет: её защищает гарнизон).</summary>
    public virtual bool AllowsReinforcement => false;

    /// <summary>
    /// Может ли сторона team прислать команду на защиту: на её объект напали,
    /// идёт подготовка к бою и подкрепления ещё нет.
    /// </summary>
    public bool CanReinforce(Team team) =>
        AllowsReinforcement && attacker != null && challenger == null && reinforcement == null
        && phase == AttackPhase.Preparing && attacker.Squad.Owner != team
        && TryGetDefendingTeam(out Team def) && def == team;

    /// <summary>Будет битва за флаг: на месте команды обеих сторон.</summary>
    public bool IsContested => challenger != null;

    /// <summary>Идёт ли нападение (от прибытия команды до итога).</summary>
    public bool IsUnderAttack => attacker != null;

    /// <summary>Этап нападения.</summary>
    public AttackPhase Phase => phase;

    /// <summary>Чья команда напала первой (имеет смысл, только если IsUnderAttack).</summary>
    public Team AttackingTeam => attacker != null ? attacker.Squad.Owner : Team.Player;

    /// <summary>Длительность подготовки к бою.</summary>
    public float PrepTime => prepTime;

    /// <summary>Сколько секунд подготовки осталось.</summary>
    public float PrepLeft => prepLeft;

    /// <summary>Прогресс подготовки 0..1 (для полоски на карте).</summary>
    public float PrepProgress => prepTime > 0f ? 1f - Mathf.Clamp01(prepLeft / prepTime) : 1f;

    /// <summary>Игрок защищается: враг напал на место, которым владеет игрок.</summary>
    public bool PlayerDefends =>
        attacker != null && challenger == null && attacker.Squad.Owner != Team.Player
        && TryGetDefendingTeam(out Team def) && def == Team.Player;

    /// <summary>
    /// Может ли команда стороны team присоединиться к нападению (битва за флаг):
    /// идёт подготовка, напала другая сторона, второй команды ещё нет, и этой стороне можно сюда нападать.
    /// </summary>
    public bool CanJoin(Team team) =>
        attacker != null && challenger == null && phase == AttackPhase.Preparing
        && attacker.Squad.Owner != team && CanBeAttackedBy(team, out _);

    /// <summary>Команда игрока в битве за флаг (или null).</summary>
    public SquadUnit PlayerContestant => !IsContested ? null : attacker.Squad.Owner == Team.Player ? attacker : challenger;

    /// <summary>Команда врага в битве за флаг (или null).</summary>
    public SquadUnit EnemyContestant => !IsContested ? null : attacker.Squad.Owner == Team.Player ? challenger : attacker;

    // ---------- Что каждое место определяет само ----------

    /// <summary>Название для сообщений и списка команд.</summary>
    public abstract string TargetName { get; }

    /// <summary>Куда подъезжает команда.</summary>
    public abstract Vector3 ApproachPoint { get; }

    /// <summary>Название для окна боя и арены.</summary>
    public abstract string SiteName { get; }

    /// <summary>Цвет арены.</summary>
    public abstract Color SiteColor { get; }

    /// <summary>Общая сила защитников (0 — защищать некому).</summary>
    public abstract int DefenderPower { get; }

    /// <summary>Защитники по волнам (одна волна = одна территория арены).</summary>
    public abstract List<List<BattleUnit>> GetDefenderWaves();

    /// <summary>Кто владеет местом и защищает его. false — место ничьё (нейтральное).</summary>
    public abstract bool TryGetDefendingTeam(out Team team);

    /// <summary>Можно ли этой стороне нападать сюда (своё нельзя и т.п.).</summary>
    protected abstract bool CanBeAttackedBy(Team team, out string reason);

    /// <summary>Нападающие победили: захват объекта или урон базе.</summary>
    protected abstract void OnAttackerWon(Squad squad);

    /// <summary>Перерисовать полоску/подпись нападения на карте.</summary>
    protected abstract void UpdateAttackVisuals();

    /// <summary>Подготовка закончилась, сейчас начнётся бой (база выбирает гарнизон).</summary>
    protected virtual void OnBattlePhaseStarting() { }

    /// <summary>Защитники потеряли долю здоровья в автобое (гарнизон базы).</summary>
    protected virtual void ApplyDefenderAutoLoss(float loss) { }

    /// <summary>
    /// После ручного боя: защитникам осталось defendersHp здоровья (гарнизон базы).
    /// result — весь итог боя (объект берёт из него HP команды подкрепления).
    /// </summary>
    protected virtual void SetDefenderHp(float defendersHp, BattleResult result) { }

    /// <summary>Нападение закончилось (база забывает гарнизон).</summary>
    protected virtual void OnAttackEnded() { }

    // ---------- Подготовка к бою ----------

    /// <summary>Тикает таймер подготовки (по времени карты — во время боя на арене стоит).</summary>
    protected virtual void Update()
    {
        if (phase != AttackPhase.Preparing) return;
        prepLeft -= WorldTime.DeltaTime;
        UpdateAttackVisuals();
        if (prepLeft <= 0f) StartBattlePhase();
    }

    // ---------- ISquadTarget ----------

    /// <summary>Можно ли отправить сюда команду.</summary>
    public bool CanAccept(Squad squad, out string reason)
    {
        if (CanReinforce(squad.Owner)) { reason = null; return true; } // на защиту своего объекта
        if (!CanBeAttackedBy(squad.Owner, out reason)) return false;
        if (attacker == null) return true;
        if (attacker.Squad.Owner == squad.Owner || (challenger != null && challenger.Squad.Owner == squad.Owner))
        {
            reason = "Здесь уже ваша команда";
            return false;
        }
        if (!CanJoin(squad.Owner))
        {
            reason = "Здесь уже идёт бой";
            return false;
        }
        return true;
    }

    /// <summary>Команду только что отправили (в пути) — месту ничего делать не нужно.</summary>
    public virtual void OnSquadDispatched(Squad squad) { }

    /// <summary>
    /// Команда приехала. Никого нет — начинается подготовка к бою.
    /// Идёт подготовка у команды другой стороны — вступаем вторыми (битва за флаг).
    /// Иначе — команда возвращается.
    /// </summary>
    public void OnSquadArrived(SquadUnit unit)
    {
        Team team = unit.Squad.Owner;

        // Команда владельца успела на защиту
        if (CanReinforce(team))
        {
            reinforcement = unit;
            unit.transform.position += new Vector3(-0.7f, 0.35f, 0f); // не стоять на фишке нападающих
            SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.Defending);
            ToastUI.Show(team == Team.Player
                ? $"Команда {unit.Squad.Number} успела на защиту «{SiteName}»! Бой через {Mathf.CeilToInt(prepLeft)} с"
                : $"Враг прислал подкрепление на защиту «{SiteName}»!");
            UpdateAttackVisuals();
            AttackChanged?.Invoke(this);
            return;
        }

        if (attacker != null && CanJoin(team))
        {
            challenger = unit;
            unit.transform.position += new Vector3(0.7f, 0.35f, 0f); // не стоять на фишке первой команды
            SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.Capturing);
            ToastUI.Show(team == Team.Player
                ? $"Команда {unit.Squad.Number} успела к «{SiteName}»! Будет битва за флаг через {Mathf.CeilToInt(prepLeft)} с"
                : $"Враг тоже напал на «{SiteName}»! Будет битва за флаг через {Mathf.CeilToInt(prepLeft)} с");
            UpdateAttackVisuals();
            AttackChanged?.Invoke(this);
            return;
        }

        if (attacker != null || !CanBeAttackedBy(team, out _))
        {
            // Пока ехали — место уже наше, здесь уже идёт бой или ждут две команды
            if (team == Team.Player)
                ToastUI.Show(TryGetDefendingTeam(out Team d) && d == team
                    ? $"{SiteName}: защищать уже не нужно (или поздно), команда возвращается"
                    : $"{SiteName}: нападение невозможно, команда возвращается");
            unit.ReturnHome();
            return;
        }

        attacker = unit;
        phase = AttackPhase.Preparing;
        prepLeft = prepTime;
        SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.Capturing);

        if (team == Team.Player)
            ToastUI.Show($"Команда {unit.Squad.Number} у цели «{SiteName}». Бой через {prepTime:0} с (можно отступить)");
        else if (PlayerDefends)
            ToastUI.Show($"Враг напал на «{SiteName}»! Бой через {prepTime:0} с");

        UpdateAttackVisuals();
        AttackChanged?.Invoke(this);
    }

    /// <summary>Можно ли команде отступить отсюда (только пока идёт подготовка).</summary>
    public bool CanRetreat(SquadUnit unit) =>
        (unit != attacker && unit != challenger && unit != reinforcement) || phase == AttackPhase.Preparing;

    /// <summary>
    /// Команда отступила (в пути или во время подготовки).
    /// Если отступила одна из двух команд — подготовка продолжается для оставшейся.
    /// </summary>
    public virtual void OnSquadRecalled(SquadUnit unit)
    {
        if (unit == reinforcement)
        {
            reinforcement = null;
            if (unit.Squad.Owner != Team.Player) ToastUI.Show($"Подкрепление врага ушло от «{SiteName}»");
            UpdateAttackVisuals();
            AttackChanged?.Invoke(this);
            return;
        }
        if (unit == challenger)
        {
            challenger = null;
            if (unit.Squad.Owner != Team.Player) ToastUI.Show($"Враг отступил от «{SiteName}»");
            UpdateAttackVisuals();
            AttackChanged?.Invoke(this);
            return;
        }
        if (unit != attacker) return;

        if (challenger != null)
        {
            // Первая команда ушла — вторая остаётся единственной, подготовка идёт дальше
            attacker = challenger;
            challenger = null;
            if (unit.Squad.Owner != Team.Player) ToastUI.Show($"Враг отступил от «{SiteName}»");
            UpdateAttackVisuals();
            AttackChanged?.Invoke(this);
            return;
        }

        bool enemyRetreated = unit.Squad.Owner != Team.Player;
        bool wasDefending = PlayerDefends;
        EndAttack();
        if (enemyRetreated && wasDefending) ToastUI.Show($"Враг отступил от «{SiteName}»");
    }

    // ---------- Бой ----------

    /// <summary>Подготовка закончилась — решаем, как пройдёт бой.</summary>
    private void StartBattlePhase()
    {
        prepLeft = 0f;
        OnBattlePhaseStarting();

        // Две команды — битва за флаг (только вручную)
        if (challenger != null)
        {
            phase = AttackPhase.Deciding;
            UpdateAttackVisuals();
            if (BattlePrepWindowUI.Instance != null) BattlePrepWindowUI.Instance.RequestBattle(this);
            else FinishContested(UnityEngine.Random.value < 0.5f, attacker.Squad.HpFraction, challenger.Squad.HpFraction);
            return;
        }

        // Защищать некому — победа без боя и без потерь
        if (DefenderPower <= 0)
        {
            Finish(true, attacker.Squad.HpFraction);
            return;
        }

        bool playerAttacks = attacker.Squad.Owner == Team.Player;
        if ((playerAttacks || PlayerDefends) && BattlePrepWindowUI.Instance != null)
        {
            // Игрок выбирает: автобой или ручной бой
            phase = AttackPhase.Deciding;
            UpdateAttackVisuals();
            BattlePrepWindowUI.Instance.RequestBattle(this);
            return;
        }
        ResolveAuto(); // игрок не участвует — сразу автобой
    }

    /// <summary>Отряд команды — для окна перед боем и арены.</summary>
    public static List<BattleUnit> GetSquadUnits(SquadUnit unit)
    {
        var list = new List<BattleUnit>();
        if (unit == null) return list;
        Squad s = unit.Squad;
        foreach (HeroInstance h in s.AllHeroes)
            list.Add(new BattleUnit
            {
                data = h.Data,
                stats = h.Stats,
                hpFraction = s.HpFraction,
                info = $"{UnitClasses.Name(h.Data.unitClass)}  •  Команда {s.Number}  •  Ур. {h.Level}  •  сила {BattleCalculator.HeroPower(h)}"
            });
        return list;
    }

    /// <summary>Отряд нападающих — для окна перед боем и арены.</summary>
    public List<BattleUnit> GetAttackerUnits() => GetSquadUnits(attacker);

    /// <summary>Автобой: итог сразу (уверенная победа / 50 на 50 / поражение) и потери HP.</summary>
    public void ResolveAuto()
    {
        if (attacker == null || IsContested) return; // в битве за флаг автобоя нет
        Squad squad = attacker.Squad;
        BattleForecast f = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), DefenderPower);
        bool won = BattleCalculator.RollAutoBattle(f);
        ApplyDefenderAutoLoss(won ? 0.5f : 0.2f);
        float hp = Mathf.Max(BattleCalculator.MinHpAfterBattle, squad.HpFraction - BattleCalculator.AutoBattleHpLoss(f, won));
        Finish(won, hp);
    }

    /// <summary>Игрок выбрал ручной бой — ждём итога арены.</summary>
    public void OnManualBattleStarted()
    {
        phase = AttackPhase.Fighting;
        UpdateAttackVisuals();
    }

    /// <summary>
    /// Ручной бой закончился. result — с точки зрения игрока (win = победили наши).
    /// Если игрок защищался, "наши" — это защитники.
    /// </summary>
    public void OnManualBattleFinished(BattleResult result)
    {
        if (attacker == null) return;
        bool defended = PlayerDefends;
        bool attackerWon = defended ? !result.win : result.win;
        SetDefenderHp(defended ? result.ourHp : result.theirHp, result);
        Finish(attackerWon, defended ? result.theirHp : result.ourHp);
    }

    /// <summary>Битва за флаг закончилась (result — с точки зрения игрока).</summary>
    public void OnContestedBattleFinished(BattleResult result)
    {
        if (!IsContested) return;
        bool playerFirst = attacker.Squad.Owner == Team.Player;
        bool attackerWon = playerFirst ? result.win : !result.win;
        FinishContested(attackerWon,
            playerFirst ? result.ourHp : result.rivalHp,
            playerFirst ? result.rivalHp : result.ourHp);
    }

    /// <summary>Итог битвы за флаг: победитель забирает место, обе команды едут домой.</summary>
    private void FinishContested(bool attackerWon, float attackerHp, float challengerHp)
    {
        SquadUnit a = attacker, c = challenger;
        a.Squad.HpFraction = attackerHp;
        c.Squad.HpFraction = challengerHp;
        Squad winner = attackerWon ? a.Squad : c.Squad;

        EndAttack();

        OnAttackerWon(winner);
        if (winner.Owner != Team.Player) ToastUI.Show($"Битва за «{SiteName}» проиграна — объект достался врагу");
        a.ReturnHome();
        c.ReturnHome();
    }

    /// <summary>Применить итог: здоровье команды, победа/поражение, команда едет домой.</summary>
    private void Finish(bool attackerWon, float attackerHp)
    {
        SquadUnit unit = attacker;
        Squad squad = unit.Squad;
        bool defended = PlayerDefends;
        int lost = Mathf.Max(0, Mathf.RoundToInt((squad.HpFraction - attackerHp) * 100f));
        squad.HpFraction = attackerHp;

        EndAttack();

        if (attackerWon)
        {
            OnAttackerWon(squad);
        }
        else if (defended)
        {
            ToastUI.Show($"Нападение на «{SiteName}» отбито!");
        }
        else if (squad.Owner == Team.Player)
        {
            ToastUI.Show($"Бой за «{SiteName}» проигран. Команда {squad.Number} потеряла {lost}% HP");
        }
        unit.ReturnHome();
    }

    /// <summary>Сбросить нападение и сообщить окнам.</summary>
    private void EndAttack()
    {
        // Подкрепление больше не нужно — едет домой
        if (reinforcement != null)
        {
            SquadUnit r = reinforcement;
            reinforcement = null;
            r.ReturnHome();
        }
        attacker = null;
        challenger = null;
        phase = AttackPhase.None;
        prepLeft = 0f;
        OnAttackEnded();
        UpdateAttackVisuals();
        AttackChanged?.Invoke(this);
    }

    /// <summary>Текст для сообщения "сюда нельзя, идёт нападение".</summary>
    public string AttackStatusText()
    {
        switch (phase)
        {
            case AttackPhase.Preparing:
                return IsContested
                    ? $"{SiteName}: две команды готовятся к битве за флаг, бой через {Mathf.CeilToInt(prepLeft)} с"
                    : CanReinforce(Team.Player)
                        ? $"{SiteName}: враг нападает! Отправьте команду на защиту — бой через {Mathf.CeilToInt(prepLeft)} с"
                        : $"{SiteName}: идёт нападение, бой через {Mathf.CeilToInt(prepLeft)} с";
            case AttackPhase.Deciding: return $"{SiteName}: начинается бой";
            case AttackPhase.Fighting: return $"{SiteName}: идёт бой";
            default: return SiteName;
        }
    }
}
