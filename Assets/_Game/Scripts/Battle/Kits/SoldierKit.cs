using UnityEngine;

/// <summary>
/// Солдат (класс Боец, ближний бой). Навыки:
///  ПАССИВКА "Меткий стрелок" — 20% шанс, что выстрел или авто атака нанесёт в 1.5 раза больше урона.
///  1) "Залп" (ур. 1) — достаёт винтовку и стреляет 3 раза с интервалом 0.5 с (урон от Атаки).
///     Средняя и дальняя дистанция: не применяет, пока враг вплотную или близко.
///  2) "Отступление" (ур. 2) — кувырок. Пока Залп перезаряжается и враг вплотную — кувырок от врага.
///     Исключение: у врага меньше здоровья, чем у Солдата, — кувырок к врагу (добить).
///  3) "Барьер" (ур. 4) — щит на 5 с, впитывает урон от Спец. атаки (только против Мага или Поддержки).
///  4) "Энерго оковы" (ур. 5) — кольцо из перчаток: попало — враг оглушён и под немотой 2 с (без урона).
/// Перезарядки — в файле героя (Data/Heroes/Hero_Soldier).
/// </summary>
public class SoldierKit : HeroKit
{
    private const int Volley = 0, Roll = 1, Barrier = 2, Shackles = 3; // номера скилов

    [Header("Меткий стрелок (пассивка)")]
    [SerializeField] private float critChance = 0.2f;     // Шанс усиленного попадания
    [SerializeField] private float critMultiplier = 1.5f; // Во сколько раз больше урон

    [Header("Залп")]
    [SerializeField] private int volleyShots = 3;           // Сколько выстрелов
    [SerializeField] private float volleyInterval = 0.5f;   // Интервал между выстрелами
    [SerializeField] private float volleyDamage = 0.8f;     // Урон одной пули (доля Атаки)
    [SerializeField] private float volleyMinRange = 2.4f;   // Ближе — "враг близко", залп нельзя
    [SerializeField] private float volleyMaxRange = 9f;     // Дальность пуль
    [SerializeField] private float volleyKeepRange = 5.5f;  // На какой дистанции Солдат держится, пока залп готов

    [Header("Отступление")]
    [SerializeField] private float rollDistance = 3.2f;     // Длина кувырка от врага
    [SerializeField] private float rollTime = 0.45f;        // Сколько длится кувырок (неуязвим)
    [SerializeField] private float rollChaseMax = 6f;       // На каком расстоянии от слабого врага кувыркается к нему

    [Header("Барьер")]
    [SerializeField] private float barrierHpPart = 0.25f;   // Плотность щита — доля макс. HP
    [SerializeField] private float barrierTime = 5f;        // Сколько держится щит

    [Header("Энерго оковы")]
    [SerializeField] private float shacklesRange = 7f;      // Дальность кольца
    [SerializeField] private float shacklesTime = 2f;       // Оглушение и немота (сек)

    private const float KnockDrag = 10f; // Как быстро гаснет движение кувырка (как отбрасывание в FighterMovement)
    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для выстрела

    private Fighter volleyTarget; // По кому идёт залп
    private int shotsFired;       // Сколько пуль уже выпущено
    private bool castingShackles; // Текущее действие — Энерго оковы (а не Барьер)
    private bool shacklesFired;   // Кольцо уже выпущено (в текущем действии)
    private float actionLength;   // Сколько длится текущее действие

    // ---------- Пассивка ----------

    /// <summary>Меткий стрелок: 20% шанс урона ×1.5 (для авто атак и пуль залпа).</summary>
    private float Marksman(float power)
    {
        if (Random.value >= critChance) return power;
        if (self.View != null) self.View.Pop("МЕТКО!", new Color(1f, 0.85f, 0.3f));
        return power * critMultiplier;
    }

    public override float ModifyAutoDamage(float power) => Marksman(power);

    // ---------- Решение ----------

    /// <summary>Пока Залп готов — держится на средней дистанции (стрелять), иначе дерётся вплотную.</summary>
    public override float PreferredRange(Fighter target) =>
        IsUnlocked(Volley) && !IsOnCooldown(Volley) && !self.IsSilenced ? volleyKeepRange : 0f;

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);
        bool aligned = absY <= AimTolerance;
        float close = self.meleeRange * 1.3f;
        bool enemyClose = NearestEnemyDistance() < volleyMinRange;

        // 4) Энерго оковы — сковать цель на линии выстрела
        if (IsReady(Shackles) && aligned && absX <= shacklesRange && !target.IsStunned && target.IsTargetable)
            return StartShackles(target);

        // 3) Барьер — только против Мага или Поддержки
        if (IsReady(Barrier) && self.Shield <= 0f && absX <= self.detectionRadius
            && (target.Class == UnitClass.Mage || target.Class == UnitClass.Support))
            return StartBarrier(target);

        // 1) Залп — на средней/дальней дистанции, когда враг не близко
        if (IsReady(Volley) && aligned && !enemyClose && absX >= volleyMinRange && absX <= volleyMaxRange)
            return StartVolley(target);

        // 2) Отступление — только пока Залп перезаряжается
        if (IsReady(Roll) && IsOnCooldown(Volley))
        {
            bool weaker = target.Health.Current < self.Health.Current;
            if (weaker && !retreating && absX > close && absX <= rollChaseMax && absY <= 1f)
                return StartRoll(target, true);                // слабого врага — догнать кувырком
            if (!weaker && absX <= close && absY <= self.depthTolerance * 1.5f)
                return StartRoll(target, false);               // враг вплотную — кувырок от него
        }
        return false;
    }

    /// <summary>Расстояние до ближайшего живого противника.</summary>
    private float NearestEnemyDistance()
    {
        float best = float.MaxValue;
        foreach (Fighter f in BattleManager.Instance.GetOpponents(self.Faction))
            if (f.IsAlive) best = Mathf.Min(best, Vector2.Distance(f.Position, self.Position));
        return best;
    }

    // ---------- Скилы ----------

    /// <summary>Залп: 3 пули с интервалом.</summary>
    private bool StartVolley(Fighter target)
    {
        if (!BeginAction(KitPose.Aim)) return false;
        StartCooldown(Volley);
        volleyTarget = target;
        shotsFired = 0;
        actionLength = 0.2f + volleyInterval * (volleyShots - 1) + 0.25f;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Кувырок от врага (или к слабому врагу).</summary>
    private bool StartRoll(Fighter target, bool towards)
    {
        if (!BeginAction(KitPose.Roll)) return false;
        StartCooldown(Roll);
        actionLength = rollTime;
        self.SetInvulnerable(rollTime);
        float side = Mathf.Sign(target.Position.x - self.Position.x);
        if (side == 0f) side = 1f;
        float dist = towards ? Mathf.Max(0.5f, Mathf.Abs(target.Position.x - self.Position.x) - self.meleeRange * 0.7f) : rollDistance;
        float dir = towards ? side : -side;
        float speed = Mathf.Min(10f, Mathf.Sqrt(2f * KnockDrag * dist)); // проедет ровно dist, пока движение гаснет
        self.Movement.FaceTowards(target.Position.x);
        self.Movement.ApplyKnockback(new Vector2(dir * speed, (target.Position.y - self.Position.y) * (towards ? 1.5f : 0f)));
        return true;
    }

    /// <summary>Барьер: щит от Спец. атаки.</summary>
    private bool StartBarrier(Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Barrier);
        actionLength = 0.3f;
        castingShackles = false;
        self.AddShield(self.Health.Max * barrierHpPart, barrierTime);
        if (self.View != null) self.View.Pop("БАРЬЕР", new Color(0.55f, 0.85f, 1f));
        return true;
    }

    /// <summary>Энерго оковы: кольцо летит в цель.</summary>
    private bool StartShackles(Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Shackles);
        actionLength = 0.4f;
        castingShackles = true;
        shacklesFired = false;
        volleyTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; return; } // по Солдату попали — действие прервано
        ActionTime += Time.deltaTime;

        if (Pose == KitPose.Aim)
        {
            // Пули: первая через 0.2 с, дальше — с интервалом
            float nextShot = 0.2f + shotsFired * volleyInterval;
            if (shotsFired < volleyShots && ActionTime >= nextShot)
            {
                if (volleyTarget != null && volleyTarget.IsAlive) self.Movement.FaceTowards(volleyTarget.Position.x);
                shotsFired++;
                self.Combat.FireShot(new ShotInfo
                {
                    power = Marksman(self.Stats.attack * volleyDamage),
                    vsSpecial = false,
                    speed = 15f,
                    range = volleyMaxRange,
                    size = 0.4f,
                    stagger = 1,
                    knockback = 1.5f,
                    color = new Color(1f, 0.9f, 0.45f)
                });
            }
        }
        else if (Pose == KitPose.Cast && castingShackles && !shacklesFired && ActionTime >= 0.15f) // кольцо оков
        {
            shacklesFired = true;
            if (volleyTarget != null && volleyTarget.IsAlive) self.Movement.FaceTowards(volleyTarget.Position.x);
            self.Combat.FireShot(new ShotInfo
            {
                power = 0f,
                vsSpecial = false,
                speed = 10f,
                range = shacklesRange + 0.5f,
                size = 1.1f,
                stagger = 0,
                knockback = 0f,
                stun = shacklesTime,
                silence = shacklesTime,
                color = new Color(0.4f, 0.95f, 1f)
            });
        }

        if (ActionTime >= actionLength)
        {
            volleyTarget = null;
            EndAction();
        }
    }
}
