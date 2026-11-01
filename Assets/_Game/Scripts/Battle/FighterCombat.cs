using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Атаки бойца: авто атака, энергия и выстрелы личных скилов.
///
/// АВТО АТАКА (урон — от "Авто атаки", ослабляется Защитой цели, разбивает коробки):
///  - все, кроме Стрелка, — удар вплотную;
///  - Стрелок — пуля издалека, а если враг подошёл вплотную — тоже бьёт рукой (TryAuto(true)).
/// Зачарованный (Шарм Суккуба) бьёт своих союзников.
/// Скилы есть только у героев со своим набором (HeroKit): они тратят энергию через TrySpendEnergy
/// и стреляют через FireShot. Энергия восстанавливается в energyRegenRate раз медленнее параметра "Реген энергии".
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
    [Tooltip("Какая доля параметра 'Реген энергии' восстанавливается в секунду (0.5 = половина). Меньше — реже скилы")]
    [SerializeField] private float energyRegenRate = 0.5f;

    [Header("Авто атака на расстоянии")]
    [Tooltip("Скорость пуль авто атаки")]
    [SerializeField] private float autoShotSpeed = 11f;
    [Tooltip("Размер пуль авто атаки (1 — как снаряд скила)")]
    [SerializeField] private float autoShotSize = 0.5f;

    private static int nextAttackId; // Счётчик номеров атак (общий для всех)

    private Fighter fighter;
    private float cooldownTimer;      // До следующей атаки
    private bool attackActive;        // Идёт фаза атаки
    private bool hitDone;             // Удар в этой атаке уже нанесён
    private bool autoMelee;           // Текущая авто атака — вплотную (рукой), а не выстрелом
    private int currentAttackId;      // Номер текущей атаки
    private readonly List<Fighter> hitThisAttack = new List<Fighter>(); // Кого уже ударили этой атакой

    /// <summary>Текущая энергия.</summary>
    public float Energy { get; private set; }

    /// <summary>Максимум энергии.</summary>
    public float MaxEnergy { get; private set; }

    private float energyRegen; // Энергии в секунду

    /// <summary>Началась авто атака. Для внешнего вида.</summary>
    public event Action AttackStarted;

    /// <summary>Авто атака вплотную (все, кроме Стрелка).</summary>
    public bool IsMeleeAuto => fighter.Profile.autoRange <= 0f;

    /// <summary>Дальность авто атаки.</summary>
    public float AutoRange => IsMeleeAuto ? fighter.meleeRange : fighter.Profile.autoRange;

    /// <summary>Можно ли сейчас сделать авто атаку.</summary>
    public bool CanAuto => fighter.CanAct && cooldownTimer <= 0f;

    private void Awake() => fighter = GetComponent<Fighter>();

    /// <summary>Задать энергию (вызывает Fighter.Init).</summary>
    public void Setup(int maxEnergy, int regen)
    {
        MaxEnergy = maxEnergy;
        Energy = maxEnergy * 0.5f; // в бой выходят с половиной энергии
        energyRegen = regen;
    }

    /// <summary>Назначить префаб снаряда.</summary>
    public void SetProjectilePrefab(Projectile prefab) => projectilePrefab = prefab;

    /// <summary>Хватает ли энергии на amount.</summary>
    public bool HasEnergy(float amount) => Energy >= amount;

    /// <summary>Потратить энергию на скил. Вернёт false, если энергии не хватает (тогда ничего не тратится).</summary>
    public bool TrySpendEnergy(float amount)
    {
        if (Energy < amount) return false;
        Energy -= amount;
        return true;
    }

    /// <summary>
    /// Попробовать сделать авто атаку (если можно).
    /// closeRange — враг вплотную: Стрелок бьёт рукой, а не стреляет.
    /// </summary>
    public bool TryAuto(bool closeRange = false)
    {
        if (!CanAuto) return false;
        autoMelee = closeRange || IsMeleeAuto;
        if (!fighter.SetState(FighterState.Attacking)) return false;
        attackActive = true;
        hitDone = false;
        currentAttackId = ++nextAttackId;
        hitThisAttack.Clear();
        cooldownTimer = fighter.attackCooldown;
        AttackStarted?.Invoke();
        return true;
    }

    /// <summary>Таймеры, энергия и ход фазы атаки.</summary>
    private void Update()
    {
        float dt = Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        if (fighter.IsAlive) Energy = Mathf.Min(MaxEnergy, Energy + energyRegen * energyRegenRate * dt);

        if (!attackActive) return;
        if (fighter.State != FighterState.Attacking) { attackActive = false; return; } // атаку прервали ударом

        float duration = fighter.attackDuration;
        if (!hitDone && fighter.StateTime >= duration * hitMoment)
        {
            hitDone = true;
            DoAuto();
        }
        if (fighter.StateTime >= duration)
        {
            attackActive = false;
            fighter.SetState(FighterState.Idle);
        }
    }

    // ---------- Авто атака ----------

    /// <summary>Авто атака: удар вплотную или пуля.</summary>
    private void DoAuto()
    {
        float power = fighter.Stats.autoAttack;
        if (autoMelee)
        {
            MeleeHit(power, fighter.knockbackForce, fighter.meleeRange);
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
            hitsBoxes = true,
            auto = true
        });
    }

    /// <summary>Удар вплотную по всем противникам перед бойцом (один раз за атаку), разбивает коробки.</summary>
    private void MeleeHit(float power, float knock, float range)
    {
        Vector2 me = fighter.Position;
        int facing = fighter.Movement.Facing;
        foreach (Fighter enemy in BattleManager.Instance.FoesOf(fighter))
        {
            if (!enemy.IsAlive || hitThisAttack.Contains(enemy)) continue;
            Vector2 d = enemy.Position - me;
            if (d.x * facing < -0.2f) continue;                        // цель за спиной
            if (Mathf.Abs(d.x) > range) continue;                      // слишком далеко
            if (Mathf.Abs(d.y) > fighter.depthTolerance) continue;    // другая глубина
            hitThisAttack.Add(enemy);

            bool landed = enemy.Health.TakeHit(new HitInfo
            {
                attacker = fighter,
                attackId = currentAttackId,
                damage = CalcDamage(power, enemy.Stats.defense),
                knockback = new Vector2(facing * knock, UnityEngine.Random.Range(-0.4f, 0.4f)),
                stagger = 1,
                special = false,
                auto = true
            });
            if (landed && fighter.Kit != null) fighter.Kit.OnAutoHit(enemy); // например, кровотечение Сойки
        }

        foreach (BattleBox box in BattleManager.Instance.Boxes)
        {
            if (box == null || !box.CanBeHit) continue;
            Vector2 d = (Vector2)box.transform.position - me;
            if (d.x * facing < -0.2f || Mathf.Abs(d.x) > range + 0.3f || Mathf.Abs(d.y) > fighter.depthTolerance) continue;
            box.Hit();
        }
    }

    // ---------- Выстрелы ----------

    /// <summary>Новый номер атаки (для ударов личных скилов героя).</summary>
    public static int NewAttackId() => ++nextAttackId;

    /// <summary>Прервать текущую авто атаку (удар не будет нанесён).</summary>
    public void CancelAttack() => attackActive = false;

    /// <summary>Выстрел для личных скилов героя (каждый снаряд — отдельная атака). Вернёт снаряд (null — нет префаба).</summary>
    public Projectile FireShot(ShotInfo info)
    {
        currentAttackId = ++nextAttackId;
        return Shoot(info);
    }

    /// <summary>Выпустить снаряд в сторону взгляда.</summary>
    private Projectile Shoot(ShotInfo info)
    {
        if (projectilePrefab == null) return null;
        int facing = fighter.Movement.Facing;
        Vector3 pos = transform.position + new Vector3(facing * 0.6f, 0f, 0f);
        Projectile p = Instantiate(projectilePrefab, pos, Quaternion.identity, BattleManager.Instance.RuntimeRoot);
        p.Launch(fighter, facing, info, currentAttackId, projectileHeight);
        return p;
    }

    /// <summary>Формула урона: сила атаки, ослабленная защитой цели, ± 10% случайности.</summary>
    public static float CalcDamage(float power, int defense) =>
        power * 100f / (100f + Mathf.Max(0, defense)) * BattleManager.DamageMultiplier * UnityEngine.Random.Range(0.9f, 1.1f);
}
