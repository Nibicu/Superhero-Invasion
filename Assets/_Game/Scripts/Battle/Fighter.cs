using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Боец на арене — "сердце" персонажа: команда, характеристики, настройки боя и состояние.
/// Сам ничего не делает, а связывает части:
///   FighterMovement — ходьба и отбрасывание,
///   FighterHealth   — здоровье, получение урона, смерть,
///   FighterCombat   — удары и снаряды,
///   FighterAI       — решает, куда идти и когда бить,
///   FighterView     — внешний вид (цвета, позы, позже — настоящие анимации),
///   HeroKit         — личные навыки героя (3 скила), если они есть.
/// Состояния-эффекты: оглушение (стан — не ходит и не бьёт), немота (не применяет скилы),
/// щит (впитывает любой урон), шарм (бьёт своих), кровотечение (теряет HP со временем),
/// метка проклятья (Суккуб телепортируется к бойцу, когда у него мало HP).
/// Состояния меняются только через SetState — он не даёт делать несовместимое
/// (например, мёртвый не ходит и не бьёт).
/// </summary>
[RequireComponent(typeof(FighterMovement), typeof(FighterHealth), typeof(FighterCombat))]
public class Fighter : MonoBehaviour
{
    [Header("Ближний бой")]
    [Tooltip("Дальность удара (по горизонтали)")]
    public float meleeRange = 1.15f;
    [Tooltip("Насколько цель может отличаться по глубине (Y), чтобы удар попал")]
    public float depthTolerance = 0.45f;
    [Tooltip("Время между выстрелами Стрелка (сек). Удары вплотную идут сериями — их паузы в FighterCombat")]
    public float attackCooldown = 0.8f;
    [Tooltip("Длительность фазы атаки (сек) — в это время боец стоит")]
    public float attackDuration = 0.3f;
    [Tooltip("Сила отбрасывания при ударе")]
    public float knockbackForce = 4f;

    [Header("Реакции")]
    [Tooltip("Сколько длится реакция на удар (сек)")]
    public float hurtDuration = 0.3f;
    [Tooltip("Сколько боец лежит, если его сбили с ног (сек)")]
    public float knockdownDuration = 1.1f;
    [Tooltip("Неуязвимость после подъёма (сек)")]
    public float getUpInvulnerability = 0.5f;
    [Tooltip("Сколько 'очков сбивания' подряд — и боец шатается (лёгкий удар = 1). С ног валят только добивающие удары, броски и скилы")]
    [FormerlySerializedAs("staggerToKnockdown")] public int staggerToDizzy = 3;
    [Tooltip("Сколько секунд боец шатается (в это время его можно схватить)")]
    public float dizzyDuration = 1f;
    [Tooltip("После того как перестал шататься, столько секунд снова зашататься нельзя")]
    public float dizzyImmunity = 1.5f;
    [Tooltip("Через сколько секунд без ударов очки сбивания обнуляются")]
    public float staggerResetTime = 1.5f;

    [Header("ИИ")]
    [Tooltip("На каком расстоянии боец замечает противника")]
    public float detectionRadius = 9f;

    [Header("Снаряды скилов")]
    [Tooltip("Скорость снаряда скила (перезарядка и цена скилов — в UnitClasses, по классу)")]
    public float projectileSpeed = 9f;

    private float invulnerableTimer; // Неуязвимость после подъёма
    private float dizzyImmuneTimer;  // Сколько ещё нельзя снова зашататься
    private float groundClock;       // Сколько секунд сбитый с ног уже лежит на земле (после полёта)

    /// <summary>"Очки сбивания" удара, который сразу валит с ног (добивающий удар, бросок, сильный скил).</summary>
    public const int KnockdownStagger = 99;

    // ---------- Данные бойца ----------

    /// <summary>Чей боец (для цвета): наши герои — Player, все остальные — Enemy.</summary>
    public Team Team { get; private set; }

    /// <summary>Сторона в бою: наши герои, их охрана, вражеская команда или её охрана.</summary>
    public BattleFaction Faction { get; private set; }

    /// <summary>Охрана в битве за флаг — красится в нейтральный серый цвет.</summary>
    public bool NeutralLook { get; private set; }

    /// <summary>Своё место у флага (небольшой сдвиг от центра, чтобы герои не стояли в одной точке).</summary>
    public Vector2 RallyOffset { get; private set; }

    /// <summary>Описание героя/злодея (имя, цвет, портрет).</summary>
    public HeroData Data { get; private set; }

    /// <summary>
    /// Скорость авто атак: из файла героя (1 — обычная, 1.2 — на 20% быстрее) × навык (бубны Тени) × темп (Энерго темп Зед Рока).
    /// </summary>
    public float AttackSpeed => (Data != null ? Data.AutoAttackSpeedMultiplier : 1f)
                                * (Kit != null ? Kit.AttackSpeedMultiplier : 1f) * TempoMultiplier;

    private float tempoMultiplier = 1f; // Ускорение от чужого навыка (Энерго темп)
    private float tempoTimer;           // Сколько ещё секунд длится ускорение

    /// <summary>Темп: множитель скорости бега и авто атак от навыка союзника (1 — нет).</summary>
    public float TempoMultiplier => tempoTimer > 0f ? tempoMultiplier : 1f;

    /// <summary>Ускорить бойца (бег и авто атаки) в mult раз на seconds секунд (Энерго темп Зед Рока).</summary>
    public void ApplyTempo(float mult, float seconds)
    {
        tempoMultiplier = Mathf.Max(mult, 1f);
        tempoTimer = Mathf.Max(tempoTimer, seconds);
        StatusChanged?.Invoke(this);
    }

    /// <summary>Сколько длится авто атака с учётом скорости авто атак.</summary>
    public float AttackDuration => attackDuration / AttackSpeed;

    /// <summary>Характеристики с учётом уровня, усилений и временного баффа из коробки.</summary>
    public HeroStats Stats
    {
        get
        {
            HeroStats s = buffTimer > 0f ? baseStats.WithBoost(buffStat, buffMultiplier) : baseStats;
            return Kit != null ? Kit.ModifyStats(s) : s; // пассивки героя (например, Храбрость Стажёра)
        }
    }

    /// <summary>Класс бойца (Танк, Боец, Маг, Стрелок, Поддержка).</summary>
    public UnitClass Class => Data.unitClass;

    /// <summary>Настройки класса: поведение, дальности, скил.</summary>
    public ClassProfile Profile => Data.Profile;

    /// <summary>Где боец появился (X в мире) — охрана отступает к этой точке.</summary>
    public float HomeX { get; private set; }

    /// <summary>Территория арены, которую стережёт охранник (-1 — не охрана). Охрана не уходит со своей территории.</summary>
    public int Territory { get; set; } = -1;

    /// <summary>Боец из команды, пришедшей на защиту объекта.</summary>
    public bool IsReinforcement { get; set; }

    /// <summary>Охранник объекта (нейтральный юнит) — не отступает при низком HP.</summary>
    public bool IsGuard { get; set; }

    /// <summary>Уровень героя (от него зависит, какие скилы открыты).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Личные навыки героя (null — своих навыков пока нет, работает скил класса).</summary>
    public HeroKit Kit { get; private set; }

    /// <summary>Оглушён: не ходит, не бьёт, не применяет скилы.</summary>
    public bool IsStunned => stunTimer > 0f;

    /// <summary>Немота: не может применять скилы (авто атаки можно).</summary>
    public bool IsSilenced => silenceTimer > 0f;

    /// <summary>Шарм (Суккуб): бьёт авто атаками своих союзников, скилы не применяет.</summary>
    public bool IsCharmed => charmTimer > 0f;

    /// <summary>Кровотечение (Сойка): теряет здоровье со временем.</summary>
    public bool IsBleeding => bleedTimer > 0f;

    /// <summary>Метка проклятья (Суккуб): до конца боя.</summary>
    public bool IsCursed { get; private set; }

    /// <summary>Кто держит этого бойца в захвате (null — свободен).</summary>
    public Fighter HeldBy { get; private set; }

    /// <summary>Можно ли сейчас зашататься (не сразу после прошлого раза).</summary>
    public bool CanBecomeDizzy => dizzyImmuneTimer <= 0f;

    /// <summary>Беззащитен: шатается или оглушён — такого можно схватить.</summary>
    public bool IsDefenseless => IsAlive && (State == FighterState.Dizzy || IsStunned) && State != FighterState.KnockedDown;

    /// <summary>Клон (Фантом): без скилов, его HP не считается в итоге боя, исчезает вместе с хозяином.</summary>
    public bool IsClone { get; set; }

    /// <summary>Зомби (Паразит Мозга): воскрешённый враг на стороне Мозга (тоже считается клоном — не идёт в итог боя).</summary>
    public bool IsZombie { get; set; }

    /// <summary>Кто вызвал этого бойца (клоны Фантома): урон клона засчитывается ему. null — никто.</summary>
    public Fighter Summoner { get; set; }

    // ---------- Статистика боя (для полигона) ----------

    /// <summary>Сколько урона нанёс (вместе с уроном своих клонов).</summary>
    public float DamageDealt { get; set; }

    /// <summary>Сколько урона получил.</summary>
    public float DamageTaken { get; set; }

    /// <summary>Сколько здоровья вылечил союзникам и себе скилами.</summary>
    public float HealingDone { get; set; }

    /// <summary>Скольких противников добил.</summary>
    public int Kills { get; set; }

    /// <summary>Через сколько секунд от начала боя выбыл (−1 — дожил до конца).</summary>
    public float DeathTime { get; set; } = -1f;

    /// <summary>Кому засчитывать урон и добивания этого бойца (клону — его хозяину).</summary>
    public Fighter StatsOwner => Summoner != null ? Summoner : this;

    /// <summary>Сколько урона ещё впитает щит (0 — щита нет).</summary>
    public float Shield => shieldTimer > 0f ? shieldAmount : 0f;

    /// <summary>Эффект (стан, немота, щит) начался или закончился — для подписи над головой.</summary>
    public event Action<Fighter> StatusChanged;

    private float stunTimer;     // Сколько ещё оглушён
    private float silenceTimer;  // Сколько ещё немота
    private float charmTimer;    // Сколько ещё зачарован
    private float bleedTimer;    // Сколько ещё кровотечение
    private float bleedPerSecond; // Сколько HP в секунду отнимает кровотечение
    private Fighter bleedSource; // Кто наложил кровотечение (ему засчитывается урон)
    private float shieldTimer;   // Сколько ещё действует щит
    private float shieldAmount;  // Сколько урона ещё впитает щит

    /// <summary>Действует ли временный бафф.</summary>
    public bool HasBuff => buffTimer > 0f;

    /// <summary>Какой параметр усилен баффом.</summary>
    public BoostStat BuffStat => buffStat;

    /// <summary>Бафф начался или закончился (для внешнего вида).</summary>
    public event Action<Fighter> BuffChanged;

    private HeroStats baseStats;   // Характеристики без баффа
    private BoostStat buffStat;    // Какой параметр усилен
    private float buffMultiplier;  // Во сколько раз
    private float buffTimer;       // Сколько секунд баффу осталось

    /// <summary>Текущее состояние.</summary>
    public FighterState State { get; private set; } = FighterState.Idle;

    /// <summary>Сколько секунд боец в текущем состоянии.</summary>
    public float StateTime { get; private set; }

    /// <summary>Сменилось состояние (для внешнего вида и анимаций).</summary>
    public event Action<Fighter, FighterState> StateChanged;

    // ---------- Части бойца ----------

    public FighterMovement Movement { get; private set; }
    public FighterHealth Health { get; private set; }
    public FighterCombat Combat { get; private set; }
    public FighterView View { get; private set; }

    // ---------- Удобные проверки ----------

    /// <summary>Может ли боец сам что-то делать (ходить, начинать атаку).</summary>
    public bool CanAct => (State == FighterState.Idle || State == FighterState.Moving) && !IsStunned;

    /// <summary>Жив ли боец.</summary>
    public bool IsAlive => State != FighterState.Dead;

    /// <summary>Неуязвим ли сейчас (лежит или только что поднялся).</summary>
    public bool IsInvulnerable => State == FighterState.KnockedDown || invulnerableTimer > 0f;

    /// <summary>Можно ли выбрать бойца целью (жив, не лежит, не неуязвим, его никто не держит в захвате).</summary>
    public bool IsTargetable => IsAlive && !IsInvulnerable && HeldBy == null;

    /// <summary>Положение на арене (X — по горизонтали, Y — глубина).</summary>
    public Vector2 Position => transform.position;

    private void Awake()
    {
        Movement = GetComponent<FighterMovement>();
        Health = GetComponent<FighterHealth>();
        Combat = GetComponent<FighterCombat>();
        View = GetComponent<FighterView>();
    }

    /// <summary>
    /// Подготовить бойца к бою (вызывает BattleManager сразу после создания).
    /// faction — сторона в бою, hpFraction — с какой долей здоровья боец выходит (раненая команда),
    /// neutralLook — покрасить как нейтральную охрану.
    /// </summary>
    public void Init(HeroData data, HeroStats stats, BattleFaction faction, float hpFraction, bool neutralLook = false, int level = 1)
    {
        Data = data;
        Level = Mathf.Max(1, level);
        baseStats = stats;
        HomeX = transform.position.x;
        Faction = faction;
        Team = faction == BattleFaction.Heroes ? Team.Player : Team.Enemy;
        NeutralLook = neutralLook;
        RallyOffset = new Vector2(UnityEngine.Random.Range(-1.2f, 1.2f), UnityEngine.Random.Range(-0.6f, 0.6f));
        name = $"{faction}_{data.displayName}";
        Health.Setup(stats.hp, hpFraction);
        Movement.Setup(stats.speed);
        Combat.Setup(stats.energy, stats.energyRegen);
        if (View != null) View.Setup(this);
        SetState(FighterState.Idle);
    }

    /// <summary>Подключить личные навыки героя (вызывает BattleManager после Init).</summary>
    public void SetKit(HeroKit kit)
    {
        Kit = kit;
        if (kit != null) kit.Setup(this);
    }

    // ---------- Эффекты ----------

    /// <summary>Оглушить на seconds секунд (не ходит, не бьёт).</summary>
    public void ApplyStun(float seconds)
    {
        if (seconds <= 0f || !IsAlive || (Kit != null && Kit.IsBlocking)) return; // блок Стажёра
        stunTimer = Mathf.Max(stunTimer, seconds);
        StatusChanged?.Invoke(this);
    }

    /// <summary>Немота на seconds секунд (не применяет скилы).</summary>
    public void ApplySilence(float seconds)
    {
        if (seconds <= 0f || !IsAlive || (Kit != null && Kit.IsBlocking)) return; // блок Стажёра
        silenceTimer = Mathf.Max(silenceTimer, seconds);
        StatusChanged?.Invoke(this);
    }

    /// <summary>Шарм на seconds секунд: бьёт своих союзников (блок Стажёра защищает).</summary>
    public void ApplyCharm(float seconds)
    {
        if (seconds <= 0f || !IsAlive || (Kit != null && Kit.IsBlocking)) return;
        charmTimer = Mathf.Max(charmTimer, seconds);
        StatusChanged?.Invoke(this);
    }

    /// <summary>
    /// Кровотечение: за seconds секунд отнимает total здоровья. Не складывается и не продлевается:
    /// если боец уже кровоточит — ничего не происходит. Вернёт true, если кровотечение началось.
    /// </summary>
    public bool ApplyBleed(float total, float seconds, Fighter source)
    {
        if (IsBleeding || !IsAlive || seconds <= 0f || total <= 0f) return false;
        bleedTimer = seconds;
        bleedPerSecond = total / seconds;
        bleedSource = source;
        StatusChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Подбросить: боец взлетает со скоростью upSpeed и падает обратно; пока в воздухе — по нему проходит любой удар.
    /// Приземлился — лежит (сбит с ног). knock — заодно сдвинуть в сторону.
    /// </summary>
    public void Launch(float upSpeed, Vector2 knock)
    {
        if (!SetState(FighterState.Airborne)) return;
        Movement.StartAir(upSpeed, Vector2.zero);
        Movement.ApplyKnockback(knock);
        StatusChanged?.Invoke(this);
    }

    /// <summary>Прыгнуть: вверх со скоростью up и вперёд со скоростью ground. Вернёт false, если сейчас нельзя.</summary>
    public bool StartJump(float up, Vector2 ground)
    {
        if (!SetState(FighterState.Jumping)) return false;
        Movement.StartAir(up, ground);
        return true;
    }

    /// <summary>Приземлился (вызывает FighterMovement): подброшенный падает (лежит), прыгнувший встаёт.</summary>
    public void OnLanded()
    {
        if (State == FighterState.Airborne) SetState(FighterState.KnockedDown);
        else if (State == FighterState.Jumping) SetState(FighterState.Idle);
    }

    /// <summary>Зашататься: стоит, не бьёт, его можно схватить (на dizzyDuration секунд).</summary>
    public void BecomeDizzy()
    {
        if (SetState(FighterState.Dizzy)) StatusChanged?.Invoke(this);
    }

    /// <summary>Попасть в захват бойца by. Вернёт false, если сейчас нельзя (лежит, выбыл, уже схвачен).</summary>
    public bool BeginHeld(Fighter by)
    {
        if (HeldBy != null || !SetState(FighterState.Held)) return false;
        HeldBy = by;
        StatusChanged?.Invoke(this);
        return true;
    }

    /// <summary>Освободиться из захвата (бросок, держащего сбили, время вышло).</summary>
    public void EndHeld()
    {
        if (HeldBy == null) return;
        HeldBy = null;
        if (State == FighterState.Held) SetState(FighterState.Idle);
        StatusChanged?.Invoke(this);
    }

    /// <summary>Поставить метку проклятья (до конца боя).</summary>
    public void MarkCursed()
    {
        if (IsCursed || !IsAlive) return;
        IsCursed = true;
        StatusChanged?.Invoke(this);
    }

    /// <summary>Щит на seconds секунд, впитывает amount любого урона.</summary>
    public void AddShield(float amount, float seconds)
    {
        shieldAmount = amount;
        shieldTimer = seconds;
        StatusChanged?.Invoke(this);
    }

    /// <summary>Щит впитывает урон. Вернёт, сколько урона прошло сквозь щит.</summary>
    public float AbsorbShield(float damage)
    {
        if (shieldTimer <= 0f || shieldAmount <= 0f) return damage;
        float absorbed = Mathf.Min(shieldAmount, damage);
        shieldAmount -= absorbed;
        if (shieldAmount <= 0.01f) { shieldTimer = 0f; shieldAmount = 0f; } // щит пробит
        StatusChanged?.Invoke(this);
        return damage - absorbed;
    }

    /// <summary>Исчезнуть из боя (клон, когда хозяин выбыл).</summary>
    public void Vanish() => SetState(FighterState.Dead);

    /// <summary>Неуязвимость на seconds секунд (перекат).</summary>
    public void SetInvulnerable(float seconds) => invulnerableTimer = Mathf.Max(invulnerableTimer, seconds);

    /// <summary>Временно усилить параметр (бафф из коробки). Новый бафф заменяет старый.</summary>
    public void ApplyBuff(BoostStat stat, float multiplier, float duration)
    {
        buffStat = stat;
        buffMultiplier = multiplier;
        buffTimer = duration;
        BuffChanged?.Invoke(this);
    }

    /// <summary>Таймеры состояний: конец реакции на удар, подъём после падения, бафф.</summary>
    private void Update()
    {
        StateTime += Time.deltaTime;
        if (buffTimer > 0f)
        {
            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0f) BuffChanged?.Invoke(this);
        }
        if (invulnerableTimer > 0f) invulnerableTimer -= Time.deltaTime;
        if (dizzyImmuneTimer > 0f) dizzyImmuneTimer -= Time.deltaTime;
        if (Movement.InAir) groundClock = 0f; else groundClock += Time.deltaTime; // сколько уже на земле
        TickStatus(ref stunTimer);
        TickStatus(ref silenceTimer);
        TickStatus(ref charmTimer);
        TickStatus(ref shieldTimer);
        TickStatus(ref tempoTimer);
        if (bleedTimer > 0f && IsAlive)
        {
            float dt = Mathf.Min(Time.deltaTime, bleedTimer);
            Health.TakeDamageOverTime(bleedPerSecond * dt, bleedSource);
        }
        TickStatus(ref bleedTimer);

        if (State == FighterState.Hurt && StateTime >= hurtDuration)
            SetState(FighterState.Idle);
        else if (State == FighterState.Dizzy && StateTime >= dizzyDuration)
        {
            SetState(FighterState.Idle); // пришёл в себя — какое-то время снова не зашатается
            dizzyImmuneTimer = dizzyImmunity;
            StatusChanged?.Invoke(this);
        }
        else if (HeldBy != null && (!HeldBy.IsAlive || HeldBy.State != FighterState.Holding || State != FighterState.Held))
            EndHeld(); // держащий выбыл или его сбили — свободен
        else if (State == FighterState.KnockedDown && StateTime >= knockdownDuration && groundClock >= 0.4f) // сбитый в воздухе сначала падает, потом немного лежит
        {
            SetState(FighterState.Idle);
            invulnerableTimer = getUpInvulnerability;
        }
    }

    /// <summary>Уменьшить таймер эффекта; закончился — сообщить внешнему виду.</summary>
    private void TickStatus(ref float timer)
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        if (timer <= 0f) { timer = 0f; StatusChanged?.Invoke(this); }
    }

    /// <summary>
    /// Сменить состояние. Вернёт false, если такой переход запрещён:
    /// из Dead — никуда; лежачий может только встать (Idle) или умереть;
    /// начать атаку можно только стоя или на ходу; шатающегося можно схватить или сбить;
    /// схваченный только освобождается (Idle) или падает; держащего можно сбить или раскачать (Dizzy).
    /// </summary>
    public bool SetState(FighterState next)
    {
        if (!CanTransition(State, next)) return false;
        State = next;
        StateTime = 0f;
        StateChanged?.Invoke(this, next);
        return true;
    }

    /// <summary>Правила переходов между состояниями.</summary>
    private static bool CanTransition(FighterState from, FighterState to)
    {
        if (from == FighterState.Dead) return false;
        if (to == FighterState.Dead) return true;
        switch (from)
        {
            case FighterState.KnockedDown:
                return to == FighterState.Idle;
            case FighterState.Hurt:
                return to == FighterState.Idle || to == FighterState.Hurt || to == FighterState.KnockedDown
                       || to == FighterState.Dizzy || to == FighterState.Held || to == FighterState.Airborne;
            case FighterState.Attacking:
                return to != FighterState.Moving && to != FighterState.Attacking && to != FighterState.Holding && to != FighterState.Jumping;
            case FighterState.Dizzy:
                return to == FighterState.Idle || to == FighterState.KnockedDown || to == FighterState.Held || to == FighterState.Airborne;
            case FighterState.Held:
                return to == FighterState.Idle || to == FighterState.KnockedDown;
            case FighterState.Holding:
                return to == FighterState.Idle || to == FighterState.KnockedDown || to == FighterState.Dizzy || to == FighterState.Airborne;
            case FighterState.Jumping:
                return to == FighterState.Idle || to == FighterState.KnockedDown || to == FighterState.Airborne;
            case FighterState.Airborne:
                return to == FighterState.KnockedDown || to == FighterState.Airborne;
            default: // Idle, Moving
                return true;
        }
    }
}
