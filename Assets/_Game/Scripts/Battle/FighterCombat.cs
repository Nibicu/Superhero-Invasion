using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Атаки бойца: авто атака и скил класса.
///
/// АВТО АТАКА (урон — от "Авто атаки", ослабляется Защитой цели, разбивает коробки):
///  - Танк и Боец — удар вплотную;
///  - Маг и Поддержка — энергетический сгусток на среднюю дистанцию;
///  - Стрелок — пуля издалека;
///  - если враг подошёл вплотную, Маг, Поддержка и Стрелок тоже бьют рукой (TryAuto(true)).
/// Энергия восстанавливается в energyRegenRate раз медленнее параметра "Реген энергии".
///
/// СКИЛ (тратит энергию, есть перезарядка; урон — от Атаки или Спец. атаки, смотря какой тип урона у бойца):
///  - Танк "Удар по земле" — урон всем врагам вокруг, отбрасывает и сбивает с ног;
///  - Боец "Мощный удар" — удар двойной силы, сбивает с ног;
///  - Маг "Энергетический снаряд" — большой сильный снаряд;
///  - Стрелок "Прицельный выстрел" — быстрая сильная пуля, пробивает насквозь;
///  - Поддержка "Лечение" — лечит союзника с самым низким здоровьем (или себя).
///
/// Удар/выстрел происходит в момент hitMoment фазы атаки — так видно замах.
/// Урон = сила × 100 / (100 + защита цели).
/// </summary>
[RequireComponent(typeof(Fighter))]
public class FighterCombat : MonoBehaviour
{
    [Tooltip("В какой момент фазы атаки наносится удар (доля от длительности)")]
    [SerializeField, Range(0f, 1f)] private float hitMoment = 0.45f;
    [Tooltip("Префаб снаряда (назначает BattleManager)")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("На какой высоте над землёй появляется снаряд")]
    [SerializeField] private float projectileHeight = 1.0f;

    [Header("Энергия")]
    [Tooltip("Какая доля параметра 'Реген энергии' восстанавливается в секунду (0.5 = половина). Меньше — реже скилы и лечение")]
    [SerializeField] private float energyRegenRate = 0.5f;

    [Header("Авто атака на расстоянии")]
    [Tooltip("Скорость пуль и сгустков авто атаки")]
    [SerializeField] private float autoShotSpeed = 11f;
    [Tooltip("Размер пуль и сгустков авто атаки (1 — как снаряд скила)")]
    [SerializeField] private float autoShotSize = 0.5f;

    [Header("Скилы")]
    [Tooltip("Танк: радиус удара по земле (по горизонтали)")]
    [SerializeField] private float slamRadius = 2.4f;
    [Tooltip("Танк: радиус удара по земле (по глубине)")]
    [SerializeField] private float slamDepth = 0.9f;
    [Tooltip("Танк: множитель урона удара по земле")]
    [SerializeField] private float slamDamage = 0.9f;
    [Tooltip("Боец: множитель урона мощного удара")]
    [SerializeField] private float strikeDamage = 2f;
    [Tooltip("Маг: множитель урона снаряда")]
    [SerializeField] private float mageShotDamage = 1.3f;
    [Tooltip("Стрелок: множитель урона прицельного выстрела")]
    [SerializeField] private float sniperDamage = 1.8f;
    [Tooltip("Поддержка: лечение = сила скила × это число + доля от макс. HP цели (ниже)")]
    [SerializeField] private float healPower = 1.5f;
    [Tooltip("Поддержка: дополнительное лечение — доля от макс. HP цели")]
    [SerializeField] private float healMaxHpPart = 0.06f;
    [Tooltip("Поддержка: на каком расстоянии можно лечить союзника")]
    [SerializeField] private float healRange = 6f;

    private static int nextAttackId; // Счётчик номеров атак (общий для всех)

    private Fighter fighter;
    private float cooldownTimer;      // До следующей атаки
    private float skillTimer;         // До следующего скила
    private bool attackActive;        // Идёт фаза атаки
    private bool hitDone;             // Удар в этой атаке уже нанесён
    private bool isSkill;             // Текущая атака — скил
    private Fighter healTarget;       // Кого лечит Поддержка
    private bool autoMelee;           // Текущая авто атака — вплотную (рукой), а не выстрелом
    private int currentAttackId;      // Номер текущей атаки
    private readonly List<Fighter> hitThisAttack = new List<Fighter>(); // Кого уже ударили этой атакой

    /// <summary>Текущая энергия.</summary>
    public float Energy { get; private set; }

    /// <summary>Максимум энергии.</summary>
    public float MaxEnergy { get; private set; }

    private float energyRegen; // Энергии в секунду

    /// <summary>Началась атака (true — скил). Для внешнего вида.</summary>
    public event Action<bool> AttackStarted;

    /// <summary>Авто атака вплотную (Танк, Боец) или на расстоянии (остальные).</summary>
    public bool IsMeleeAuto => fighter.Profile.autoRange <= 0f;

    /// <summary>Дальность авто атаки.</summary>
    public float AutoRange => IsMeleeAuto ? fighter.meleeRange : fighter.Profile.autoRange;

    /// <summary>Дальность скила (для ИИ).</summary>
    public float SkillRange
    {
        get
        {
            switch (fighter.Class)
            {
                case UnitClass.Tank: return slamRadius * 0.8f;
                case UnitClass.Fighter: return fighter.meleeRange * 1.2f;
                case UnitClass.Mage: return 8f;
                case UnitClass.Shooter: return 9.5f;
                default: return healRange;
            }
        }
    }

    /// <summary>Танк: попадает ли точка в зону удара по земле.</summary>
    public bool InSlamZone(Vector2 p) =>
        Mathf.Abs(p.x - fighter.Position.x) <= slamRadius && Mathf.Abs(p.y - fighter.Position.y) <= slamDepth;

    /// <summary>Можно ли сейчас сделать авто атаку.</summary>
    public bool CanAuto => fighter.CanAct && cooldownTimer <= 0f;

    /// <summary>Готов ли скил (перезарядка прошла, хватает энергии).</summary>
    public bool CanSkill => fighter.CanAct && skillTimer <= 0f && cooldownTimer <= 0f
                            && Energy >= fighter.Profile.skillCost
                            && (projectilePrefab != null || (fighter.Class != UnitClass.Mage && fighter.Class != UnitClass.Shooter));

    /// <summary>Идёт ли сейчас скил (для позы).</summary>
    public bool IsSpecialAttack => attackActive && isSkill;

    private void Awake() => fighter = GetComponent<Fighter>();

    /// <summary>Задать энергию (вызывает Fighter.Init).</summary>
    public void Setup(int maxEnergy, int regen)
    {
        MaxEnergy = maxEnergy;
        Energy = maxEnergy * 0.5f; // в бой выходят с половиной энергии
        energyRegen = regen;
        skillTimer = UnityEngine.Random.Range(0.5f, 2.5f); // чтобы не применяли скилы все разом
    }

    /// <summary>Назначить префаб снаряда.</summary>
    public void SetProjectilePrefab(Projectile prefab) => projectilePrefab = prefab;

    /// <summary>
    /// Попробовать сделать авто атаку (если можно).
    /// closeRange — враг вплотную: Маг, Поддержка и Стрелок бьют рукой, а не стреляют.
    /// </summary>
    public bool TryAuto(bool closeRange = false)
    {
        if (!CanAuto) return false;
        autoMelee = closeRange || IsMeleeAuto;
        StartAttack(false);
        return true;
    }

    /// <summary>Попробовать применить скил. ally — кого лечить (только для Поддержки).</summary>
    public bool TrySkill(Fighter ally = null)
    {
        if (!CanSkill) return false;
        Energy -= fighter.Profile.skillCost;
        skillTimer = fighter.Profile.skillCooldown;
        healTarget = ally;
        StartAttack(true);
        return true;
    }

    /// <summary>Начать фазу атаки.</summary>
    private void StartAttack(bool skill)
    {
        if (!fighter.SetState(FighterState.Attacking)) return;
        attackActive = true;
        hitDone = false;
        isSkill = skill;
        currentAttackId = ++nextAttackId;
        hitThisAttack.Clear();
        cooldownTimer = fighter.attackCooldown;
        AttackStarted?.Invoke(skill);
    }

    /// <summary>Таймеры, энергия и ход фазы атаки.</summary>
    private void Update()
    {
        float dt = Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        if (skillTimer > 0f) skillTimer -= dt;
        if (fighter.IsAlive) Energy = Mathf.Min(MaxEnergy, Energy + energyRegen * energyRegenRate * dt);

        if (!attackActive) return;
        if (fighter.State != FighterState.Attacking) { attackActive = false; return; } // атаку прервали ударом

        float duration = fighter.attackDuration;
        if (!hitDone && fighter.StateTime >= duration * hitMoment)
        {
            hitDone = true;
            if (isSkill) DoSkill();
            else DoAuto();
        }
        if (fighter.StateTime >= duration)
        {
            attackActive = false;
            fighter.SetState(FighterState.Idle);
        }
    }

    // ---------- Авто атака ----------

    /// <summary>Авто атака: удар вплотную или выстрел (пуля / сгусток).</summary>
    private void DoAuto()
    {
        int power = fighter.Stats.autoAttack;
        if (autoMelee)
        {
            MeleeHit(power, false, 1, fighter.knockbackForce, fighter.meleeRange, true);
            return;
        }
        Shoot(new ShotInfo
        {
            power = power,
            vsSpecial = false,
            speed = autoShotSpeed,
            range = AutoRange + 0.7f,
            size = autoShotSize,
            stagger = 1,
            knockback = 2f,
            hitsBoxes = true
        });
    }

    // ---------- Скилы ----------

    /// <summary>Скил класса.</summary>
    private void DoSkill()
    {
        float power = fighter.SkillPower;
        bool vs = fighter.SkillVsSpecial;
        switch (fighter.Class)
        {
            case UnitClass.Tank:
                GroundSlam(power * slamDamage, vs);
                break;
            case UnitClass.Fighter:
                MeleeHit(power * strikeDamage, vs, 3, fighter.knockbackForce * 1.5f, fighter.meleeRange * 1.2f, false);
                break;
            case UnitClass.Mage:
                Shoot(new ShotInfo { power = power * mageShotDamage, vsSpecial = vs, speed = fighter.projectileSpeed, range = 9f, size = 1.4f, stagger = 2, knockback = 5f });
                break;
            case UnitClass.Shooter:
                Shoot(new ShotInfo { power = power * sniperDamage, vsSpecial = vs, speed = fighter.projectileSpeed * 1.8f, range = 10f, size = 0.8f, stagger = 2, knockback = 4f, pierce = true });
                break;
            default:
                Heal(power);
                break;
        }
    }

    /// <summary>Танк: удар по земле — все противники вокруг получают урон и отлетают от танка.</summary>
    private void GroundSlam(float power, bool vsSpecial)
    {
        if (fighter.View != null) fighter.View.PlaySlam(slamRadius, slamDepth);
        foreach (Fighter enemy in BattleManager.Instance.GetOpponents(fighter.Faction))
        {
            if (!enemy.IsAlive || !InSlamZone(enemy.Position)) continue;
            float dir = Mathf.Sign(enemy.Position.x - fighter.Position.x + 0.001f);
            enemy.Health.TakeHit(new HitInfo
            {
                attacker = fighter,
                attackId = currentAttackId,
                damage = CalcDamage(power, vsSpecial ? enemy.Stats.specialDefense : enemy.Stats.defense),
                knockback = new Vector2(dir * fighter.knockbackForce * 1.6f, 0f),
                stagger = 3
            });
        }
    }

    /// <summary>Поддержка: вылечить союзника (если он погиб или его нет — себя).</summary>
    private void Heal(float power)
    {
        Fighter target = healTarget != null && healTarget.IsAlive ? healTarget : fighter;
        target.Health.Heal(power * healPower + target.Health.Max * healMaxHpPart);
        if (target.View != null) target.View.FlashHeal();
        healTarget = null;
    }

    /// <summary>
    /// Удар вплотную по всем противникам перед бойцом (один раз за атаку).
    /// boxes — разбивает ли коробки (только авто атака).
    /// </summary>
    private void MeleeHit(float power, bool vsSpecial, int stagger, float knock, float range, bool boxes)
    {
        Vector2 me = fighter.Position;
        int facing = fighter.Movement.Facing;
        foreach (Fighter enemy in BattleManager.Instance.GetOpponents(fighter.Faction))
        {
            if (!enemy.IsAlive || hitThisAttack.Contains(enemy)) continue;
            Vector2 d = enemy.Position - me;
            if (d.x * facing < -0.2f) continue;                        // цель за спиной
            if (Mathf.Abs(d.x) > range) continue;                      // слишком далеко
            if (Mathf.Abs(d.y) > fighter.depthTolerance) continue;    // другая глубина
            hitThisAttack.Add(enemy);

            enemy.Health.TakeHit(new HitInfo
            {
                attacker = fighter,
                attackId = currentAttackId,
                damage = CalcDamage(power, vsSpecial ? enemy.Stats.specialDefense : enemy.Stats.defense),
                knockback = new Vector2(facing * knock, UnityEngine.Random.Range(-0.4f, 0.4f)),
                stagger = stagger
            });
        }

        if (!boxes) return;
        foreach (BattleBox box in BattleManager.Instance.Boxes)
        {
            if (box == null || !box.CanBeHit) continue;
            Vector2 d = (Vector2)box.transform.position - me;
            if (d.x * facing < -0.2f || Mathf.Abs(d.x) > range + 0.3f || Mathf.Abs(d.y) > fighter.depthTolerance) continue;
            box.Hit();
        }
    }

    /// <summary>Выпустить снаряд в сторону взгляда.</summary>
    private void Shoot(ShotInfo info)
    {
        if (projectilePrefab == null) return;
        int facing = fighter.Movement.Facing;
        Vector3 pos = transform.position + new Vector3(facing * 0.6f, 0f, 0f);
        Projectile p = Instantiate(projectilePrefab, pos, Quaternion.identity, BattleManager.Instance.RuntimeRoot);
        p.Launch(fighter, facing, info, currentAttackId, projectileHeight);
    }

    /// <summary>Формула урона: сила атаки, ослабленная защитой цели, ± 10% случайности.</summary>
    public static float CalcDamage(float power, int defense) =>
        power * 100f / (100f + Mathf.Max(0, defense)) * BattleManager.DamageMultiplier * UnityEngine.Random.Range(0.9f, 1.1f);
}
