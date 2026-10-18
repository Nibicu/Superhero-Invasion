using UnityEngine;

/// <summary>
/// Солдат (класс Боец, ближний бой). Навыки:
///  ПАССИВКА "Меткий стрелок" — 20% шанс, что выстрел или авто атака нанесёт в 1.5 раза больше урона.
///  1) "Залп" (ур. 1) — достаёт винтовку и стреляет 3 раза с интервалом 0.5 с (каждая пуля — 70% Атаки,
///     отбрасывает). Анимация выстрела играет на каждую пулю. Не применяет, пока враг вплотную или близко.
///  2) "Отступление" (ур. 2) — кувырок. Пока Залп перезаряжается и враг вплотную — кувырок от врага.
///     Исключение: у врага меньше здоровья, чем у Солдата, — кувырок к врагу (добить).
///  3) "Барьер" (ур. 4) — щит прочностью 30% HP на 6 с (впитывает любой урон), вокруг Солдата — тёмно-зелёный шар.
///  4) "Сверх человек" (ур. 5, пассивный) — пока HP меньше 40%, Реген HP +15.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Soldier). Кадры — в Data/Characters/Sprites_Soldier.
/// </summary>
public class SoldierKit : HeroKit
{
    private const int Volley = 0, Roll = 1, Barrier = 2, Superhuman = 3; // номера скилов

    [Header("Меткий стрелок (пассивка)")]
    [SerializeField] private float critChance = 0.2f;     // Шанс усиленного попадания
    [SerializeField] private float critMultiplier = 1.5f; // Во сколько раз больше урон

    [Header("Залп")]
    [SerializeField] private int volleyShots = 3;           // Сколько выстрелов
    [SerializeField] private float volleyInterval = 0.5f;   // Интервал между выстрелами
    [SerializeField] private float volleyDamage = 0.7f;     // Урон одной пули (доля Атаки)
    [SerializeField] private float volleyKnockback = 4f;    // Сила отбрасывания пули
    [SerializeField] private float volleyMinRange = 2.4f;   // Ближе — "враг близко", залп нельзя
    [SerializeField] private float volleyMaxRange = 9f;     // Дальность пуль
    [SerializeField] private float volleyKeepRange = 5.5f;  // На какой дистанции Солдат держится, пока залп готов
    [SerializeField] private float shotDelay = 0.125f;      // Через сколько после начала анимации вылетает пуля (кадр выстрела)

    [Header("Отступление")]
    [SerializeField] private float rollDistance = 3.2f;     // Длина кувырка от врага
    [SerializeField] private float rollTime = 0.45f;        // Сколько длится кувырок (неуязвим)
    [SerializeField] private float rollChaseMax = 6f;       // На каком расстоянии от слабого врага кувыркается к нему

    [Header("Барьер")]
    [SerializeField] private float barrierHpPart = 0.3f;    // Прочность щита — доля макс. HP
    [SerializeField] private float barrierTime = 6f;        // Сколько держится щит
    [SerializeField] private float barrierRange = 4f;       // Ставит щит, когда враг ближе этого

    [Header("Сверх человек (пассивка)")]
    [SerializeField] private float superhumanHp = 0.4f;     // Работает, пока HP меньше этой доли
    [SerializeField] private int superhumanRegen = 15;      // Сколько Регена HP добавляет

    private const float KnockDrag = 10f; // Как быстро гаснет движение кувырка (как отбрасывание в FighterMovement)
    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для выстрела

    private Fighter volleyTarget; // По кому идёт залп
    private int shotsFired;       // Сколько пуль уже выпущено
    private int cyclesStarted;    // Сколько раз уже начата анимация выстрела
    private bool castingBarrier;  // Текущее действие — Барьер
    private float actionLength;   // Сколько длится текущее действие

    // ---------- Внешний вид ----------

    public override int AnimSkill => !ActionAlive ? -1 : Pose == KitPose.Aim ? Volley : castingBarrier ? Barrier : -1;

    // ---------- Пассивки ----------

    /// <summary>Меткий стрелок: 20% шанс урона ×1.5 (для авто атак и пуль залпа).</summary>
    private float Marksman(float power)
    {
        if (Random.value >= critChance) return power;
        if (self.View != null) self.View.Pop("МЕТКО!", new Color(1f, 0.85f, 0.3f));
        return power * critMultiplier;
    }

    public override float ModifyAutoDamage(float power) => Marksman(power);

    /// <summary>Сверх человек: пока HP меньше 40%, Реген HP +15.</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (IsUnlocked(Superhuman) && self.Health != null && self.Health.Fraction < superhumanHp) s.hpRegen += superhumanRegen;
        return s;
    }

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

        // 3) Барьер — враг рядом, а Солдата уже бьют (или здоровья меньше 70%)
        if (IsReady(Barrier) && self.Shield <= 0f && NearestEnemyDistance() <= barrierRange
            && (self.Health.TimeSinceHit < 1.5f || self.Health.Fraction < 0.7f))
            return StartBarrier();

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

    /// <summary>Залп: 3 пули с интервалом, на каждую — своя анимация выстрела.</summary>
    private bool StartVolley(Fighter target)
    {
        if (!BeginAction(KitPose.Aim)) return false;
        StartCooldown(Volley);
        castingBarrier = false;
        volleyTarget = target;
        shotsFired = 0;
        cyclesStarted = 0;
        actionLength = 0.075f + shotDelay + volleyInterval * (volleyShots - 1) + 0.25f;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Кувырок от врага (или к слабому врагу).</summary>
    private bool StartRoll(Fighter target, bool towards)
    {
        if (!BeginAction(KitPose.Roll)) return false;
        StartCooldown(Roll);
        castingBarrier = false;
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

    /// <summary>Барьер: щит от любого урона.</summary>
    private bool StartBarrier()
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Barrier);
        castingBarrier = true;
        actionLength = 0.35f;
        self.AddShield(self.Health.Max * barrierHpPart, barrierTime);
        if (self.View != null) self.View.Pop("БАРЬЕР", new Color(0.4f, 0.9f, 0.5f));
        return true;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; castingBarrier = false; return; } // по Солдату попали — действие прервано
        ActionTime += Time.deltaTime;

        if (Pose == KitPose.Aim)
        {
            // Анимация выстрела: через 0.075 с и дальше с интервалом; пуля — на кадре выстрела
            if (cyclesStarted < volleyShots && ActionTime >= 0.075f + cyclesStarted * volleyInterval)
            {
                cyclesStarted++;
                AnimCycle++;
            }
            float nextShot = 0.075f + shotDelay + shotsFired * volleyInterval;
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
                    knockback = volleyKnockback,
                    color = new Color(1f, 0.9f, 0.45f)
                });
            }
        }

        if (ActionTime >= actionLength)
        {
            volleyTarget = null;
            castingBarrier = false;
            EndAction();
        }
    }
}
