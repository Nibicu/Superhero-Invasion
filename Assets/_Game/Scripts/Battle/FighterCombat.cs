using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Атаки бойца: авто атака, энергия и выстрелы личных скилов.
///
/// АВТО АТАКА (урон — от "Авто атаки", ослабляется Защитой цели, разбивает коробки):
///  - все, кроме Стрелка, — удар вплотную;
///  - Стрелок — пуля издалека, а если враг подошёл вплотную — тоже бьёт рукой (TryAuto(true)).
/// СЕРИЯ УДАРОВ (вплотную): удары идут сериями — у Танка 2 удара, у остальных 3.
///  Лёгкие удары быстрые (chainHitCooldown), последний — добивающий: урон ×1.5, отбрасывает и сбивает с ног,
///  после него передышка (finisherRecovery). Промах, прерванный удар или пауза дольше comboResetTime — серия сначала.
/// ЗАХВАТ: шатающегося или оглушённого врага вплотную можно схватить (TryGrab) — 3 удара коленом,
///  третий — бросок: отбрасывает и сбивает с ног. Пока держит, другие бить схваченного не могут.
///  Держащего сбили с ног (или он зашатался) — захват срывается.
/// УДАР С РАЗБЕГА: бегущий боец рядом с врагом бьёт в прыжке (TryDashAttack) — урон ×1.5,
///  отбрасывает и сбивает с ног. Не чаще раза в dashCooldown секунд.
/// ПРЫЖОК (TryJump): дуга вверх и к цели; в прыжке, как только враг рядом, — удар в прыжке:
///  урон ×1.3, отбрасывает и сбивает с ног (подброшенного врага — отбрасывает в сторону). Не чаще раза в jumpCooldown секунд.
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

    [Header("Серия ударов (вплотную)")]
    [Tooltip("Через сколько секунд от начала удара можно бить следующим ударом серии")]
    [SerializeField] private float chainHitCooldown = 0.45f;
    [Tooltip("Передышка после добивающего удара (от его начала)")]
    [SerializeField] private float finisherRecovery = 1.1f;
    [Tooltip("Если следующий удар позже этого (сек после прошлого) — серия начинается сначала")]
    [SerializeField] private float comboResetTime = 0.7f;
    [Tooltip("Урон добивающего удара (× Авто атака)")]
    [SerializeField] private float finisherDamage = 1.5f;
    [Tooltip("Сила отбрасывания добивающего удара (× сила удара бойца)")]
    [SerializeField] private float finisherKnockback = 2.2f;
    [Tooltip("Лёгкие удары серии лишь подталкивают (× сила удара бойца) — враг остаётся рядом для следующего удара")]
    [SerializeField] private float lightKnockback = 0.15f;
    [Tooltip("Танк бьёт медленнее: во столько раз дольше паузы серии")]
    [SerializeField] private float tankSlowdown = 1.3f;

    [Header("Захват")]
    [Tooltip("Урон удара коленом (× Авто атака)")]
    [SerializeField] private float kneeDamage = 0.8f;
    [Tooltip("Когда первый удар коленом (сек после захвата)")]
    [SerializeField] private float kneeFirst = 0.3f;
    [Tooltip("Интервал между ударами коленом")]
    [SerializeField] private float kneeInterval = 0.35f;
    [Tooltip("Сколько ударов коленом (последний — бросок)")]
    [SerializeField] private int kneeCount = 3;
    [Tooltip("Сила броска (× сила удара бойца)")]
    [SerializeField] private float throwKnockback = 2.5f;
    [Tooltip("Дольше этого захват не держится (сек)")]
    [SerializeField] private float grabMaxTime = 1.6f;

    [Header("Удар с разбега")]
    [Tooltip("Урон удара с разбега (× Авто атака)")]
    [SerializeField] private float dashDamage = 1.5f;
    [Tooltip("Сила отбрасывания (× сила удара бойца)")]
    [SerializeField] private float dashKnockback = 2.4f;
    [Tooltip("С какого расстояния (по горизонтали) бегущий боец бьёт с разбега")]
    [SerializeField] private float dashRange = 1.8f;
    [Tooltip("Скорость прыжка вперёд")]
    [SerializeField] private float dashLunge = 7f;
    [Tooltip("Сколько длится удар с разбега (сек)")]
    [SerializeField] private float dashDuration = 0.4f;
    [Tooltip("Не чаще раза в столько секунд")]
    [SerializeField] private float dashCooldown = 4f;

    [Header("Прыжок")]
    [Tooltip("Скорость прыжка вверх (выше — дольше в воздухе)")]
    [SerializeField] private float jumpSpeed = 8f;
    [Tooltip("Самая большая скорость прыжка вперёд")]
    [SerializeField] private float jumpMaxForward = 6f;
    [Tooltip("Урон удара в прыжке (× Авто атака)")]
    [SerializeField] private float jumpAttackDamage = 1.3f;
    [Tooltip("Сила отбрасывания удара в прыжке (× сила удара бойца)")]
    [SerializeField] private float jumpKnockback = 2.2f;
    [Tooltip("Дальность удара в прыжке (× дальность ближнего боя)")]
    [SerializeField] private float jumpReach = 1.3f;
    [Tooltip("Не чаще раза в столько секунд")]
    [SerializeField] private float jumpCooldown = 3f;

    [Header("Авто атака на расстоянии")]
    [Tooltip("Скорость пуль авто атаки")]
    [SerializeField] private float autoShotSpeed = 11f;
    [Tooltip("Размер пуль авто атаки (1 — как снаряд скила)")]
    [SerializeField] private float autoShotSize = 0.5f;

    private static int nextAttackId; // Счётчик номеров атак (общий для всех)
    private const int Knockdown = 99; // "Очки сбивания" добивающего удара — сразу с ног

    private Fighter fighter;
    private float cooldownTimer;      // До следующей атаки
    private bool attackActive;        // Идёт фаза атаки
    private bool hitDone;             // Удар в этой атаке уже нанесён
    private bool autoMelee;           // Текущая авто атака — вплотную (рукой), а не выстрелом
    private int currentAttackId;      // Номер текущей атаки
    private float lastMeleeTime = -10f; // Когда был последний удар вплотную (для серии)
    private bool chainBroken = true;  // Серия прервалась (промах или удар прервали) — следующий удар будет первым
    private bool jumpAttackDone;      // В этом прыжке уже ударил
    private float jumpAttackAt = -10f; // Когда ударил в прыжке (StateTime) — для позы
    private float jumpTimer;          // До следующего прыжка
    private bool dashing;             // Идёт удар с разбега
    private float dashTimer;          // До следующего удара с разбега
    private Fighter held;             // Кого держит в захвате (null — никого)
    private float grabClock;          // Сколько уже держит
    private int kneesDone;            // Сколько ударов коленом нанесено
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

    /// <summary>Сколько ударов в серии: у Танка 2, у остальных 3.</summary>
    public int ChainLength => fighter.Class == UnitClass.Tank ? 2 : 3;

    /// <summary>Какой по счёту удар серии сейчас (1..ChainLength; 0 — выстрел). Для внешнего вида.</summary>
    public int ComboStep { get; private set; }

    /// <summary>Текущий удар — добивающий (последний в серии).</summary>
    public bool IsFinisher => ComboStep > 0 && ComboStep >= ChainLength;

    /// <summary>Сейчас идёт удар с разбега (для внешнего вида).</summary>
    public bool IsDashAttack => dashing && attackActive;

    /// <summary>Можно ли прыгнуть (боец свободен и прыжок перезарядился).</summary>
    public bool CanJump => fighter.CanAct && jumpTimer <= 0f;

    /// <summary>Сейчас бьёт в прыжке (первые 0.3 с после удара) — для внешнего вида.</summary>
    public bool IsJumpAttacking => fighter.State == FighterState.Jumping && fighter.StateTime - jumpAttackAt < 0.3f;

    /// <summary>Прыгнуть к target: дугой так, чтобы приземлиться у цели (вперёд не быстрее jumpMaxForward).</summary>
    public bool TryJump(Fighter target)
    {
        if (!CanJump || target == null) return false;
        float airTime = 2f * jumpSpeed / Mathf.Max(1f, fighter.Movement.Gravity);
        Vector2 d = target.Position - fighter.Position;
        float stopShort = Mathf.Sign(d.x) * fighter.meleeRange * 0.6f; // приземлиться перед целью, а не на ней
        Vector2 ground = new Vector2(Mathf.Clamp((d.x - stopShort) / airTime, -jumpMaxForward, jumpMaxForward), d.y / airTime);
        fighter.Movement.FaceTowards(target.Position.x);
        if (!fighter.StartJump(jumpSpeed, ground)) return false;
        jumpTimer = jumpCooldown;
        jumpAttackDone = false;
        jumpAttackAt = -10f;
        attackActive = false;
        ComboStep = 0;
        chainBroken = true;
        return true;
    }

    /// <summary>Этот прыжок — без удара в прыжке (полёт или прыжок скила: урон наносит сам скил).</summary>
    public void SkipJumpAttack() => jumpAttackDone = true;

    /// <summary>В прыжке: как только враг впереди рядом — удар в прыжке (один раз за прыжок).</summary>
    private void UpdateJump()
    {
        if (jumpAttackDone) return;
        int facing = fighter.Movement.Facing;
        float reach = fighter.meleeRange * jumpReach;
        bool any = false;
        foreach (Fighter enemy in BattleManager.Instance.FoesOf(fighter))
        {
            if (!enemy.IsAlive || enemy.IsInvulnerable) continue;
            Vector2 d = enemy.Position - fighter.Position;
            if (d.x * facing < -0.2f || Mathf.Abs(d.x) > reach || Mathf.Abs(d.y) > fighter.depthTolerance) continue;
            if (!any) { any = true; currentAttackId = ++nextAttackId; }
            enemy.Health.TakeHit(new HitInfo
            {
                attacker = fighter,
                attackId = currentAttackId,
                damage = CalcDamage(fighter.Stats.autoAttack * jumpAttackDamage, enemy.Stats.defense),
                knockback = new Vector2(facing * fighter.knockbackForce * jumpKnockback, UnityEngine.Random.Range(-0.3f, 0.3f)),
                stagger = Knockdown,
                special = false,
                auto = true
            });
        }
        if (!any) return;
        jumpAttackDone = true;
        jumpAttackAt = fighter.StateTime;
        AttackStarted?.Invoke();
    }

    /// <summary>Удар с разбега перезарядился (ИИ подбегает бегом, чтобы ударить).</summary>
    public bool DashReady => dashTimer <= 0f;

    /// <summary>Можно ли ударить target с разбега: боец бежит, удар готов, враг впереди рядом на той же глубине.</summary>
    public bool CanDashAttack(Fighter target)
    {
        if (!CanAuto || !DashReady || !fighter.Movement.IsRunning || target == null || !target.IsTargetable) return false;
        Vector2 d = target.Position - fighter.Position;
        return d.x * fighter.Movement.Facing > 0f && Mathf.Abs(d.x) <= dashRange
               && Mathf.Abs(d.x) >= fighter.meleeRange * 0.9f && Mathf.Abs(d.y) <= fighter.depthTolerance; // вплотную — не с разбега
    }

    /// <summary>Удар с разбега: прыжок вперёд, кого задел — урон, отлетает и падает.</summary>
    public bool TryDashAttack(Fighter target)
    {
        if (!CanDashAttack(target) || !fighter.SetState(FighterState.Attacking)) return false;
        attackActive = true;
        dashing = true;
        hitDone = false;
        autoMelee = true;
        ComboStep = 0;
        chainBroken = true;
        currentAttackId = ++nextAttackId;
        hitThisAttack.Clear();
        dashTimer = dashCooldown;
        cooldownTimer = finisherRecovery;
        int facing = fighter.Movement.Facing;
        fighter.Movement.ApplyKnockback(new Vector2(facing * dashLunge, 0f)); // прыжок вперёд
        AttackStarted?.Invoke();
        return true;
    }

    /// <summary>Удар с разбега идёт: пока летим — бьём всех, кого задели (один раз каждого).</summary>
    private void UpdateDash()
    {
        if (fighter.State != FighterState.Attacking) { attackActive = false; dashing = false; return; } // сбили в прыжке
        if (fighter.StateTime >= 0.08f)
        {
            float power = fighter.Stats.autoAttack * dashDamage;
            int facing = fighter.Movement.Facing;
            foreach (Fighter enemy in BattleManager.Instance.FoesOf(fighter))
            {
                if (!enemy.IsAlive || hitThisAttack.Contains(enemy)) continue;
                Vector2 d = enemy.Position - fighter.Position;
                if (d.x * facing < -0.2f || Mathf.Abs(d.x) > fighter.meleeRange || Mathf.Abs(d.y) > fighter.depthTolerance) continue;
                hitThisAttack.Add(enemy);
                enemy.Health.TakeHit(new HitInfo
                {
                    attacker = fighter,
                    attackId = currentAttackId,
                    damage = CalcDamage(power, enemy.Stats.defense),
                    knockback = new Vector2(facing * fighter.knockbackForce * dashKnockback, UnityEngine.Random.Range(-0.3f, 0.3f)),
                    stagger = Knockdown,
                    special = false,
                    auto = true
                });
            }
        }
        if (fighter.StateTime >= dashDuration)
        {
            attackActive = false;
            dashing = false;
            fighter.SetState(FighterState.Idle);
        }
    }

    /// <summary>Счётчик ударов коленом (изменился — кадры удара играют сначала). Для внешнего вида.</summary>
    public int KneeCycle { get; private set; }

    /// <summary>Сколько секунд прошло с последнего удара коленом (для позы).</summary>
    public float SinceKnee => grabClock - (kneeFirst + (kneesDone - 1) * kneeInterval);

    /// <summary>Можно ли схватить target: свободен, враг беззащитен (шатается или оглушён) и стоит вплотную.</summary>
    public bool CanGrab(Fighter target)
    {
        if (!CanAuto || target == null || !target.IsDefenseless || target.HeldBy != null || target.IsInvulnerable) return false;
        Vector2 d = target.Position - fighter.Position;
        return Mathf.Abs(d.x) <= fighter.meleeRange * 1.15f && Mathf.Abs(d.y) <= fighter.depthTolerance;
    }

    /// <summary>Схватить target (если можно). Дальше всё идёт само: 3 удара коленом и бросок.</summary>
    public bool TryGrab(Fighter target)
    {
        if (!CanGrab(target) || !fighter.SetState(FighterState.Holding)) return false;
        if (!target.BeginHeld(fighter)) { fighter.SetState(FighterState.Idle); return false; }
        held = target;
        grabClock = 0f;
        kneesDone = 0;
        attackActive = false;
        ComboStep = 0;
        fighter.Movement.FaceTowards(target.Position.x);
        target.Movement.FaceTowards(fighter.Position.x);
        if (fighter.View != null) fighter.View.Pop("ЗАХВАТ!", new Color(1f, 0.75f, 0.3f));
        return true;
    }

    /// <summary>Держим: схваченный стоит перед нами; удары коленом по расписанию; последний — бросок.</summary>
    private void UpdateGrab(float dt)
    {
        if (fighter.State != FighterState.Holding || !held.IsAlive || held.HeldBy != fighter)
        {
            ReleaseGrab(); // нас сбили, враг выбыл или вырвался
            return;
        }
        grabClock += dt;
        int facing = fighter.Movement.Facing;
        Vector3 spot = transform.position + new Vector3(facing * fighter.meleeRange * 0.75f, 0f, 0f);
        held.transform.position = BattleManager.Instance.ClampToArena(held, spot);

        if (kneesDone < kneeCount && grabClock >= kneeFirst + kneesDone * kneeInterval)
        {
            kneesDone++;
            Knee(kneesDone == kneeCount);
            return;
        }
        if (grabClock >= grabMaxTime) ReleaseGrab();
    }

    /// <summary>Удар коленом; последний — бросок (сначала отпускаем, чтобы удар сбил с ног и отбросил).</summary>
    private void Knee(bool last)
    {
        Fighter target = held;
        int facing = fighter.Movement.Facing;
        KneeCycle++;
        if (last) ReleaseGrab();
        target.Health.TakeHit(new HitInfo
        {
            attacker = fighter,
            attackId = ++nextAttackId,
            damage = CalcDamage(fighter.Stats.autoAttack * kneeDamage, target.Stats.defense),
            knockback = last ? new Vector2(facing * fighter.knockbackForce * throwKnockback, 0f) : Vector2.zero,
            stagger = last ? Knockdown : 0,
            special = false,
            auto = true
        });
        if (!last) return;
        cooldownTimer = finisherRecovery;
        if (fighter.View != null) fighter.View.Pop("БРОСОК", new Color(1f, 0.75f, 0.3f));
    }

    /// <summary>Отпустить схваченного (бросок, нас сбили, время вышло).</summary>
    private void ReleaseGrab()
    {
        Fighter target = held;
        held = null;
        if (target != null) target.EndHeld();
        if (fighter.State == FighterState.Holding) fighter.SetState(FighterState.Idle);
    }

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
        if (autoMelee)
        {
            // Серия: следующий удар или сначала (промах, прервали, долгая пауза, серия кончилась)
            bool fresh = chainBroken || Time.time - lastMeleeTime > comboResetTime || ComboStep >= ChainLength;
            ComboStep = fresh ? 1 : ComboStep + 1;
            chainBroken = false;
            float slow = fighter.Class == UnitClass.Tank ? tankSlowdown : 1f;
            cooldownTimer = (IsFinisher ? finisherRecovery : chainHitCooldown) * slow;
        }
        else
        {
            ComboStep = 0;
            cooldownTimer = fighter.attackCooldown;
        }
        AttackStarted?.Invoke();
        return true;
    }

    /// <summary>Таймеры, энергия и ход фазы атаки.</summary>
    private void Update()
    {
        float dt = Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        if (dashTimer > 0f) dashTimer -= dt;
        if (jumpTimer > 0f) jumpTimer -= dt;
        if (fighter.IsAlive) Energy = Mathf.Min(MaxEnergy, Energy + energyRegen * energyRegenRate * dt);
        if (held != null) { UpdateGrab(dt); return; }
        if (dashing) { UpdateDash(); return; }
        if (fighter.State == FighterState.Jumping) { UpdateJump(); return; }

        if (!attackActive) return;
        if (fighter.State != FighterState.Attacking) { attackActive = false; chainBroken = true; return; } // атаку прервали ударом — серия сначала

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
        if (fighter.Kit != null) fighter.Kit.OnAutoAttack(); // например, каждая 5-я авто атака Рейнджера — доп. стрела
        if (autoMelee)
        {
            bool finisher = IsFinisher;
            MeleeHit(finisher ? power * finisherDamage : power,
                     fighter.knockbackForce * (finisher ? finisherKnockback : lightKnockback),
                     fighter.meleeRange, finisher);
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

    /// <summary>
    /// Удар вплотную по всем противникам перед бойцом (один раз за атаку), разбивает коробки.
    /// finisher — добивающий удар серии: сбивает с ног. Никого не задел — серия прерывается.
    /// </summary>
    private void MeleeHit(float power, float knock, float range, bool finisher)
    {
        Vector2 me = fighter.Position;
        int facing = fighter.Movement.Facing;
        lastMeleeTime = Time.time;
        bool any = false;
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
                stagger = finisher ? Knockdown : 1,
                special = false,
                auto = true
            });
            any |= landed;
            if (landed && fighter.Kit != null) fighter.Kit.OnAutoHit(enemy); // например, кровотечение Сойки
        }
        if (!any) chainBroken = true; // промах — серия сначала

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
