using System;
using UnityEngine;

/// <summary>
/// Боец на арене — "сердце" персонажа: команда, характеристики, настройки боя и состояние.
/// Сам ничего не делает, а связывает части:
///   FighterMovement — ходьба и отбрасывание,
///   FighterHealth   — здоровье, получение урона, смерть,
///   FighterCombat   — удары и снаряды,
///   FighterAI       — решает, куда идти и когда бить,
///   FighterView     — внешний вид (цвета, позы, позже — настоящие анимации).
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
    [Tooltip("Время между атаками (сек)")]
    public float attackCooldown = 0.8f;
    [Tooltip("Длительность фазы атаки (сек) — в это время боец стоит")]
    public float attackDuration = 0.35f;
    [Tooltip("Сила отбрасывания при ударе")]
    public float knockbackForce = 4f;

    [Header("Реакции")]
    [Tooltip("Сколько длится реакция на удар (сек)")]
    public float hurtDuration = 0.3f;
    [Tooltip("Сколько боец лежит, если его сбили с ног (сек)")]
    public float knockdownDuration = 1.1f;
    [Tooltip("Неуязвимость после подъёма (сек)")]
    public float getUpInvulnerability = 0.5f;
    [Tooltip("Сколько 'очков сбивания' подряд валит с ног (обычный удар = 1, снаряд = 2)")]
    public int staggerToKnockdown = 3;
    [Tooltip("Через сколько секунд без ударов очки сбивания обнуляются")]
    public float staggerResetTime = 1.5f;

    [Header("ИИ")]
    [Tooltip("На каком расстоянии боец замечает противника")]
    public float detectionRadius = 9f;

    [Header("Снаряды скилов")]
    [Tooltip("Скорость снаряда скила (перезарядка и цена скилов — в UnitClasses, по классу)")]
    public float projectileSpeed = 9f;

    private float invulnerableTimer; // Неуязвимость после подъёма

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

    /// <summary>Характеристики с учётом уровня, усилений и временного баффа из коробки.</summary>
    public HeroStats Stats => buffTimer > 0f ? baseStats.WithBoost(buffStat, buffMultiplier) : baseStats;

    /// <summary>Класс бойца (Танк, Боец, Маг, Стрелок, Поддержка).</summary>
    public UnitClass Class => Data.unitClass;

    /// <summary>Настройки класса: поведение, дальности, скил.</summary>
    public ClassProfile Profile => Data.Profile;

    /// <summary>Сила скилов: Атака или Спец. атака (зависит от типа урона бойца).</summary>
    public int SkillPower => Data.skillDamage == DamageType.Special ? Stats.specialAttack : Stats.attack;

    /// <summary>Ослабляется ли урон скилов Спец. защитой цели (а не Защитой).</summary>
    public bool SkillVsSpecial => Data.skillDamage == DamageType.Special;

    /// <summary>Где боец появился (X в мире) — охрана отступает к этой точке.</summary>
    public float HomeX { get; private set; }

    /// <summary>Территория арены, которую стережёт охранник (-1 — не охрана). Охрана не уходит со своей территории.</summary>
    public int Territory { get; set; } = -1;

    /// <summary>Боец из команды, пришедшей на защиту объекта.</summary>
    public bool IsReinforcement { get; set; }

    /// <summary>Охранник объекта (нейтральный юнит) — не отступает при низком HP.</summary>
    public bool IsGuard { get; set; }

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
    public bool CanAct => State == FighterState.Idle || State == FighterState.Moving;

    /// <summary>Жив ли боец.</summary>
    public bool IsAlive => State != FighterState.Dead;

    /// <summary>Неуязвим ли сейчас (лежит или только что поднялся).</summary>
    public bool IsInvulnerable => State == FighterState.KnockedDown || invulnerableTimer > 0f;

    /// <summary>Можно ли выбрать бойца целью (жив, не лежит, не неуязвим).</summary>
    public bool IsTargetable => IsAlive && !IsInvulnerable;

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
    public void Init(HeroData data, HeroStats stats, BattleFaction faction, float hpFraction, bool neutralLook = false)
    {
        Data = data;
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

        if (State == FighterState.Hurt && StateTime >= hurtDuration)
            SetState(FighterState.Idle);
        else if (State == FighterState.KnockedDown && StateTime >= knockdownDuration)
        {
            SetState(FighterState.Idle);
            invulnerableTimer = getUpInvulnerability;
        }
    }

    /// <summary>
    /// Сменить состояние. Вернёт false, если такой переход запрещён:
    /// из Dead — никуда; лежачий может только встать (Idle) или умереть;
    /// начать атаку можно только стоя или на ходу.
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
                return to == FighterState.Idle || to == FighterState.Hurt || to == FighterState.KnockedDown;
            case FighterState.Attacking:
                return to != FighterState.Moving && to != FighterState.Attacking;
            default: // Idle, Moving
                return true;
        }
    }
}
