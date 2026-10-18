using UnityEngine;

/// <summary>
/// Стажёр (класс Боец, ближний бой). Навыки:
///  ПАССИВКА "Храбрость" — пока живых врагов больше, чем союзников (вместе с ним), Защита +20.
///  1) "Волна" (ур. 1) — волна энергии летит вперёд как снаряд: урон от Атаки, сбивает с ног.
///  2) "Блок" (ур. 2) — срабатывает сам, когда по Стажёру попадает вражеский СКИЛ:
///     удар не проходит (ни урона, ни отбрасывания, ни стана), играет анимация блока.
///  3) "Силовые клинки" (ур. 4) — мощный удар перед собой: урон от Атаки всем в зоне, сбивает их с ног.
///  4) "Парирование" (ур. 5, пассивный) — 20% шанс, что вражеская авто атака по Стажёру не сработает.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Stajor). Кадры — в Data/Characters/Sprites_Stajor.
/// </summary>
public class StajorKit : HeroKit
{
    private const int Wave = 0, Block = 1, Blades = 2, Parry = 3; // номера скилов

    [Header("Храбрость (пассивка)")]
    [SerializeField] private int braveDefense = 20;          // Сколько Защиты добавляет

    [Header("Волна")]
    [SerializeField] private float waveDamage = 1.2f;        // Урон (доля Атаки)
    [SerializeField] private float waveRange = 8f;           // Дальность полёта
    [SerializeField] private float waveMinRange = 1.2f;      // Ближе — бьёт клинками/рукой, а не волной
    [SerializeField] private float waveSpeed = 9f;           // Скорость волны
    [SerializeField] private float waveCastTime = 0.25f;     // Через сколько секунд после начала вылетает волна
    [SerializeField] private float waveActionTime = 0.45f;   // Сколько длится применение

    [Header("Блок")]
    [SerializeField] private float blockTime = 0.5f;         // Сколько секунд держится блок (все удары в это время не проходят)

    [Header("Силовые клинки")]
    [SerializeField] private float bladesDamage = 1.8f;      // Урон (доля Атаки)
    [SerializeField] private float bladesRangeMult = 1.6f;   // Дальность удара (× дальность ближнего боя)
    [SerializeField] private float bladesDepthMult = 1.6f;   // Ширина зоны по глубине (× обычная)
    [SerializeField] private float bladesHitTime = 0.15f;    // Когда наносится удар
    [SerializeField] private float bladesActionTime = 0.45f; // Сколько длится удар
    [SerializeField] private float bladesKnockback = 7f;     // Сила отбрасывания

    [Header("Парирование (пассивка)")]
    [SerializeField] private float parryChance = 0.2f;       // Шанс, что авто атака врага не сработает

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для волны
    private const int Knockdown = 99;         // "Очки сбивания" — сразу с ног

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private bool struck;           // Волна вылетела / клинки ударили (в текущем действии)
    private float actionLength;    // Сколько длится текущее действие
    private Fighter waveTarget;    // В кого летит волна
    private float blockTimer;      // Сколько ещё держится блок
    private bool brave;            // Храбрость сейчас действует
    private float braveCheck;      // Таймер проверки Храбрости

    // ---------- Внешний вид ----------

    public override int AnimSkill => blockTimer > 0f ? Block : (ActionAlive ? current : -1);

    public override bool IsBlocking => blockTimer > 0f;

    /// <summary>Силовые клинки обычный удар не прерывает (мощный удар доводится до конца).</summary>
    protected override bool ActionUnstoppable => base.ActionUnstoppable || current == Blades;

    // ---------- Пассивки и блок ----------

    /// <summary>Храбрость: Защита +20, пока врагов больше, чем союзников.</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (brave) s.defense += braveDefense;
        return s;
    }

    /// <summary>Блок (от скилов) и Парирование (от авто атак).</summary>
    public override bool TryBlock(HitInfo hit)
    {
        if (blockTimer > 0f) return true; // блок уже стоит — всё отражается

        if (hit.auto)
        {
            // Парирование: 20% — авто атака врага не сработала
            if (!IsUnlocked(Parry) || Random.value >= parryChance) return false;
            if (self.View != null) self.View.Pop("ПАРИРОВАНИЕ", new Color(0.8f, 0.9f, 1f));
            return true;
        }

        // Блок: удар скилом — если блок открыт, перезарядился и Стажёр не оглушён
        if (!IsUnlocked(Block) || IsOnCooldown(Block) || self.IsStunned || !self.IsAlive) return false;
        StartCooldown(Block);
        blockTimer = blockTime;
        if (self.View != null) self.View.Pop("БЛОК", new Color(0.55f, 0.85f, 1f));
        if (hit.attacker != null) self.Movement.FaceTowards(hit.attacker.Position.x);

        // Своё действие прерывается — Стажёр встаёт в блок
        current = -1;
        Pose = KitPose.None;
        self.Combat.CancelAttack();
        if (self.State == FighterState.Attacking) self.SetState(FighterState.Idle);
        if (BeginAction(KitPose.Block)) actionLength = blockTime;
        return true;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 3) Силовые клинки — кто-то из врагов в зоне удара
        if (IsReady(Blades) && !retreating)
        {
            int facing = d.x >= 0f ? 1 : -1;
            if (CountInBladesZone(facing) > 0) return StartBlades(facing);
        }

        // 1) Волна — цель на одной линии, не вплотную и не слишком далеко
        if (IsReady(Wave) && target.IsTargetable && absY <= AimTolerance && absX >= waveMinRange && absX <= waveRange)
            return StartWave(target);

        return false;
    }

    /// <summary>Сколько врагов попадёт под Силовые клинки, если бить в сторону facing.</summary>
    private int CountInBladesZone(int facing)
    {
        int n = 0;
        foreach (Fighter e in BattleManager.Instance.GetOpponents(self.Faction))
            if (e.IsTargetable && InBladesZone(e, facing)) n++;
        return n;
    }

    /// <summary>Попадает ли враг в зону Силовых клинков (перед Стажёром).</summary>
    private bool InBladesZone(Fighter e, int facing)
    {
        Vector2 d = e.Position - self.Position;
        return d.x * facing >= -0.2f
               && Mathf.Abs(d.x) <= self.meleeRange * bladesRangeMult
               && Mathf.Abs(d.y) <= self.depthTolerance * bladesDepthMult;
    }

    // ---------- Скилы ----------

    /// <summary>Волна: замах, потом волна летит вперёд.</summary>
    private bool StartWave(Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Wave);
        current = Wave;
        struck = false;
        actionLength = waveActionTime;
        waveTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Силовые клинки: мощный удар перед собой.</summary>
    private bool StartBlades(int facing)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Blades);
        current = Blades;
        struck = false;
        actionLength = bladesActionTime;
        self.Movement.FaceTowards(self.Position.x + facing);
        return true;
    }

    /// <summary>Выпустить волну.</summary>
    private void FireWave()
    {
        if (waveTarget != null && waveTarget.IsAlive) self.Movement.FaceTowards(waveTarget.Position.x);
        SkillSprites look = self.View != null && self.View.Sheet != null ? self.View.Sheet.GetSkill(Wave) : null;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.attack * waveDamage,
            vsSpecial = false,
            speed = waveSpeed,
            range = waveRange,
            size = look != null && look.HasEffect ? 1.7f : 1.2f, // кадры волны — в том же масштабе, что персонаж
            stagger = Knockdown,
            knockback = 5f,
            color = new Color(0.55f, 0.85f, 1f),
            frames = look != null && look.HasEffect ? look.effect : null
        });
    }

    /// <summary>Ударить Силовыми клинками всех в зоне (урон от Атаки, сбивает с ног).</summary>
    private void StrikeBlades()
    {
        int facing = self.Movement.Facing;
        int id = FighterCombat.NewAttackId();
        foreach (Fighter e in BattleManager.Instance.GetOpponents(self.Faction))
        {
            if (!e.IsAlive || !InBladesZone(e, facing)) continue;
            e.Health.TakeHit(new HitInfo
            {
                attacker = self,
                attackId = id,
                damage = FighterCombat.CalcDamage(self.Stats.attack * bladesDamage, e.Stats.defense),
                knockback = new Vector2(facing * bladesKnockback, Random.Range(-0.4f, 0.4f)),
                stagger = Knockdown,
                special = false
            });
        }
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (blockTimer > 0f) blockTimer -= dt;
        UpdateBravery(dt);

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Стажёру попали — действие прервано
        ActionTime += dt;

        if (!struck && current == Wave && ActionTime >= waveCastTime) { struck = true; FireWave(); }
        if (!struck && current == Blades && ActionTime >= bladesHitTime) { struck = true; StrikeBlades(); }

        if (ActionTime >= actionLength)
        {
            current = -1;
            waveTarget = null;
            EndAction();
        }
    }

    /// <summary>Храбрость: раз в 0.25 с пересчитать, врагов ли больше, чем союзников.</summary>
    private void UpdateBravery(float dt)
    {
        braveCheck -= dt;
        if (braveCheck > 0f || BattleManager.Instance == null) return;
        braveCheck = 0.25f;
        int foes = 0, friends = 0;
        foreach (Fighter f in BattleManager.Instance.GetOpponents(self.Faction)) if (f.IsAlive) foes++;
        foreach (Fighter f in BattleManager.Instance.GetAllies(self.Faction)) if (f.IsAlive) friends++;
        bool now = self.IsAlive && foes > friends;
        if (now && !brave && self.View != null) self.View.Pop("ХРАБРОСТЬ", new Color(1f, 0.75f, 0.3f));
        brave = now;
    }
}
