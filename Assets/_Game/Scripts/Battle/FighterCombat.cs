using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Атаки бойца.
/// Ближний удар: короткая фаза Attacking; в момент удара (hitMoment) один раз проверяются
/// противники перед бойцом в пределах дальности и глубины. Каждая цель получает
/// урон не больше одного раза за удар, но можно попасть по нескольким целям.
/// Урон = Атака × 100 / (100 + Защита цели).
/// Спец. атака: энергетический снаряд (тратит энергию, есть перезарядка).
/// Урон снаряда = Спец. атака × 100 / (100 + Спец. защита цели).
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

    private static int nextAttackId; // Счётчик номеров атак (общий для всех)

    private Fighter fighter;
    private float cooldownTimer;      // До следующей атаки
    private float specialTimer;       // До следующей спец. атаки
    private bool attackActive;        // Идёт фаза атаки
    private bool hitDone;             // Удар в этой атаке уже нанесён
    private bool isSpecial;           // Текущая атака — спец. атака
    private int currentAttackId;      // Номер текущей атаки
    private readonly List<Fighter> hitThisAttack = new List<Fighter>(); // Кого уже ударили этой атакой

    /// <summary>Текущая энергия.</summary>
    public float Energy { get; private set; }

    /// <summary>Максимум энергии.</summary>
    public float MaxEnergy { get; private set; }

    private float energyRegen; // Энергии в секунду

    /// <summary>Началась атака (true — спец. атака). Для внешнего вида.</summary>
    public event Action<bool> AttackStarted;

    /// <summary>Можно ли сейчас ударить.</summary>
    public bool CanMelee => fighter.CanAct && cooldownTimer <= 0f;

    /// <summary>Можно ли сейчас выстрелить.</summary>
    public bool CanSpecial => fighter.CanAct && specialTimer <= 0f && cooldownTimer <= 0f
                              && Energy >= fighter.specialEnergyCost && projectilePrefab != null;

    /// <summary>Идёт ли сейчас спец. атака.</summary>
    public bool IsSpecialAttack => attackActive && isSpecial;

    private void Awake() => fighter = GetComponent<Fighter>();

    /// <summary>Задать энергию (вызывает Fighter.Init).</summary>
    public void Setup(int maxEnergy, int regen)
    {
        MaxEnergy = maxEnergy;
        Energy = maxEnergy * 0.5f; // в бой выходят с половиной энергии
        energyRegen = regen;
        specialTimer = UnityEngine.Random.Range(0.5f, fighter.specialCooldown); // чтобы не стреляли все разом
    }

    /// <summary>Назначить префаб снаряда.</summary>
    public void SetProjectilePrefab(Projectile prefab) => projectilePrefab = prefab;

    /// <summary>Попробовать ударить (если можно).</summary>
    public bool TryMelee()
    {
        if (!CanMelee) return false;
        StartAttack(false);
        return true;
    }

    /// <summary>Попробовать выстрелить снарядом (если можно).</summary>
    public bool TrySpecial()
    {
        if (!CanSpecial) return false;
        Energy -= fighter.specialEnergyCost;
        specialTimer = fighter.specialCooldown;
        StartAttack(true);
        return true;
    }

    /// <summary>Начать фазу атаки.</summary>
    private void StartAttack(bool special)
    {
        if (!fighter.SetState(FighterState.Attacking)) return;
        attackActive = true;
        hitDone = false;
        isSpecial = special;
        currentAttackId = ++nextAttackId;
        hitThisAttack.Clear();
        cooldownTimer = fighter.attackCooldown;
        AttackStarted?.Invoke(special);
    }

    /// <summary>Таймеры, энергия и ход фазы атаки.</summary>
    private void Update()
    {
        float dt = Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        if (specialTimer > 0f) specialTimer -= dt;
        if (fighter.IsAlive) Energy = Mathf.Min(MaxEnergy, Energy + energyRegen * dt);

        if (!attackActive) return;
        if (fighter.State != FighterState.Attacking) { attackActive = false; return; } // атаку прервали ударом

        float duration = fighter.attackDuration;
        if (!hitDone && fighter.StateTime >= duration * hitMoment)
        {
            hitDone = true;
            if (isSpecial) SpawnProjectile();
            else DoMeleeHit();
        }
        if (fighter.StateTime >= duration)
        {
            attackActive = false;
            fighter.SetState(FighterState.Idle);
        }
    }

    /// <summary>Проверка попадания ближнего удара — один раз за атаку.</summary>
    private void DoMeleeHit()
    {
        Vector2 me = fighter.Position;
        int facing = fighter.Movement.Facing;
        foreach (Fighter enemy in BattleManager.Instance.GetOpponents(fighter.Faction))
        {
            if (!enemy.IsAlive || hitThisAttack.Contains(enemy)) continue;
            Vector2 d = enemy.Position - me;
            if (d.x * facing < -0.2f) continue;                        // цель за спиной
            if (Mathf.Abs(d.x) > fighter.meleeRange) continue;        // слишком далеко
            if (Mathf.Abs(d.y) > fighter.depthTolerance) continue;    // другая глубина
            hitThisAttack.Add(enemy);

            var hit = new HitInfo
            {
                attacker = fighter,
                attackId = currentAttackId,
                damage = CalcDamage(fighter.Stats.attack, enemy.Stats.defense),
                knockback = new Vector2(facing * fighter.knockbackForce, UnityEngine.Random.Range(-0.4f, 0.4f)),
                stagger = 1
            };
            enemy.Health.TakeHit(hit);
        }
    }

    /// <summary>Выпустить снаряд в сторону взгляда.</summary>
    private void SpawnProjectile()
    {
        if (projectilePrefab == null) return;
        int facing = fighter.Movement.Facing;
        Vector3 pos = transform.position + new Vector3(facing * 0.6f, 0f, 0f);
        Projectile p = Instantiate(projectilePrefab, pos, Quaternion.identity, BattleManager.Instance.RuntimeRoot);
        p.Launch(fighter, facing, fighter.projectileSpeed, currentAttackId, projectileHeight);
    }

    /// <summary>Формула урона: сила атаки, ослабленная защитой цели, ± 10% случайности.</summary>
    public static float CalcDamage(int power, int defense) =>
        power * 100f / (100f + Mathf.Max(0, defense)) * BattleManager.DamageMultiplier * UnityEngine.Random.Range(0.9f, 1.1f);
}
