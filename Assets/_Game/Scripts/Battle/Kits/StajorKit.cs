using UnityEngine;

/// <summary>
/// Стажёр (класс Боец, ближний бой). Навыки:
///  1) "Волна" (ур. 1) — волна энергии летит вперёд как снаряд: урон от Силы атаки, сбивает с ног.
///  2) "Блок" (ур. 3) — срабатывает сам, когда по Стажёру попадает вражеский СКИЛ:
///     удар не проходит (ни урона, ни отбрасывания, ни стана), играет анимация блока.
///  3) "Комбо" (ур. 5) — 3 сильных удара перед собой, каждый — 40% Силы атаки и стан 0.5 с;
///     последний удар отбрасывает и сбивает с ног.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Stajor). Кадры — в Data/Characters/Sprites_Stajor.
/// </summary>
public class StajorKit : HeroKit
{
    private const int Wave = 0, Block = 1, Combo = 2; // номера скилов

    [Header("Волна")]
    [SerializeField] private float waveDamage = 1.2f;        // Урон (доля Силы атаки)
    [SerializeField] private float waveRange = 8f;           // Дальность полёта
    [SerializeField] private float waveMinRange = 1.2f;      // Ближе — бьёт рукой/комбо, а не волной
    [SerializeField] private float waveSpeed = 9f;           // Скорость волны
    [SerializeField] private float waveCastTime = 0.25f;     // Через сколько секунд после начала вылетает волна
    [SerializeField] private float waveActionTime = 0.45f;   // Сколько длится применение

    [Header("Блок")]
    [SerializeField] private float blockTime = 0.5f;         // Сколько секунд держится блок (все удары в это время не проходят)

    [Header("Комбо")]
    [SerializeField] private int comboHits = 3;              // Сколько ударов
    [SerializeField] private float comboDamage = 0.4f;       // Урон одного удара (доля Силы атаки)
    [SerializeField] private float comboStun = 0.5f;         // Стан после каждого удара (сек)
    [SerializeField] private float comboFirstHit = 0.12f;    // Когда первый удар
    [SerializeField] private float comboInterval = 0.3f;     // Интервал между ударами
    [SerializeField] private float comboRangeMult = 1.3f;    // Дальность ударов (× дальность ближнего боя)
    [SerializeField] private float comboKnockback = 7f;      // Сила отбрасывания последнего удара

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для волны
    private const int Knockdown = 99;         // "Очки сбивания" — сразу с ног

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private bool struck;           // Волна вылетела (в текущем действии)
    private int comboDone;         // Сколько ударов комбо уже нанесено
    private float actionLength;    // Сколько длится текущее действие
    private Fighter waveTarget;    // В кого летит волна
    private float blockTimer;      // Сколько ещё держится блок

    // ---------- Внешний вид ----------

    public override int AnimSkill => blockTimer > 0f ? Block : (ActionAlive ? current : -1);

    public override bool IsBlocking => blockTimer > 0f;

    /// <summary>Комбо обычный удар не прерывает (серия доводится до конца).</summary>
    protected override bool ActionUnstoppable => base.ActionUnstoppable || current == Combo;

    // ---------- Блок ----------

    /// <summary>Блок: срабатывает на вражеский скил (авто атаки не ловит).</summary>
    public override bool TryBlock(HitInfo hit)
    {
        if (blockTimer > 0f) return true; // блок уже стоит — всё отражается
        if (hit.auto) return false;

        // Удар скилом — если блок открыт, перезарядился и Стажёр не оглушён
        if (!IsUnlocked(Block) || IsOnCooldown(Block) || self.IsStunned || !self.IsAlive) return false;
        StartCooldown(Block);
        blockTimer = blockTime;
        Pop("БЛОК", new Color(0.55f, 0.85f, 1f));
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

        // 3) Комбо — кто-то из врагов в зоне удара
        if (IsReady(Combo) && !retreating)
        {
            int facing = d.x >= 0f ? 1 : -1;
            foreach (Fighter e in Enemies)
                if (e.IsTargetable && InComboZone(e, facing)) return StartCombo(facing);
        }

        // 1) Волна — цель на одной линии, не вплотную и не слишком далеко
        if (IsReady(Wave) && target.IsTargetable && absY <= AimTolerance && absX >= waveMinRange && absX <= waveRange)
            return StartWave(target);

        return false;
    }

    /// <summary>Попадает ли враг под удар Комбо, если бить в сторону facing.</summary>
    private bool InComboZone(Fighter e, int facing)
    {
        Vector2 d = e.Position - self.Position;
        return d.x * facing >= -0.2f
               && Mathf.Abs(d.x) <= self.meleeRange * comboRangeMult
               && Mathf.Abs(d.y) <= self.depthTolerance * 1.2f;
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

    /// <summary>Комбо: серия ударов перед собой.</summary>
    private bool StartCombo(int facing)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Combo);
        current = Combo;
        comboDone = 0;
        actionLength = comboFirstHit + comboInterval * (comboHits - 1) + 0.25f;
        self.Movement.FaceTowards(self.Position.x + facing);
        Pop("КОМБО", new Color(1f, 0.8f, 0.35f));
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

    /// <summary>Один удар Комбо по всем в зоне: стан; последний — отбрасывает и сбивает с ног.</summary>
    private void StrikeCombo(bool last)
    {
        int facing = self.Movement.Facing;
        AnimCycle++; // кадры удара — заново на каждый удар
        foreach (Fighter e in Enemies)
        {
            if (!e.IsAlive || !InComboZone(e, facing)) continue;
            bool landed = HitEnemy(e, self.Stats.attack * comboDamage, false,
                new Vector2(facing * (last ? comboKnockback : 0.6f), Random.Range(-0.3f, 0.3f)), last ? Knockdown : 0);
            if (landed && !last) e.ApplyStun(comboStun);
        }
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (blockTimer > 0f) blockTimer -= dt;

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Стажёру попали — действие прервано
        ActionTime += dt;

        if (!struck && current == Wave && ActionTime >= waveCastTime) { struck = true; FireWave(); }
        if (current == Combo && comboDone < comboHits && ActionTime >= comboFirstHit + comboDone * comboInterval)
        {
            comboDone++;
            StrikeCombo(comboDone == comboHits);
        }

        if (ActionTime >= actionLength)
        {
            current = -1;
            waveTarget = null;
            EndAction();
        }
    }
}
