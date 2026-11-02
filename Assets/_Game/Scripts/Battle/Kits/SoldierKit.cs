using UnityEngine;

/// <summary>
/// Солдат (класс Боец, ближний бой). Навыки:
///  1) "Залп" (ур. 1) — достаёт винтовку и стреляет 3 раза с интервалом 0.5 с (каждая пуля — 70% Силы атаки,
///     отбрасывает). Анимация выстрела играет на каждую пулю. Не применяет, пока враг вплотную или близко.
///  2) "Скачок" (ур. 3) — кувырок от врага на среднюю дистанцию (неуязвим). Срабатывает, когда Залп готов,
///     а враг слишком близко, чтобы стрелять: Солдат отпрыгивает и стреляет. Только от врага; перезарядка — как у Залпа.
///  3) "Барьер" (ур. 5) — щит прочностью 30% HP на 6 с (впитывает любой урон), вокруг Солдата — тёмно-зелёный шар.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Soldier). Кадры — в Data/Characters/Sprites_Soldier.
/// </summary>
public class SoldierKit : HeroKit
{
    private const int Volley = 0, Leap = 1, Barrier = 2; // номера скилов

    [Header("Залп")]
    [SerializeField] private int volleyShots = 3;           // Сколько выстрелов
    [SerializeField] private float volleyInterval = 0.5f;   // Интервал между выстрелами
    [SerializeField] private float volleyDamage = 0.7f;     // Урон одной пули (доля Силы атаки)
    [SerializeField] private float volleyKnockback = 4f;    // Сила отбрасывания пули
    [SerializeField] private float volleyMinRange = 2.4f;   // Ближе — "враг близко", залп нельзя
    [SerializeField] private float volleyMaxRange = 9f;     // Дальность пуль
    [SerializeField] private float volleyKeepRange = 5.5f;  // На какой дистанции Солдат держится, пока залп готов
    [SerializeField] private float shotDelay = 0.125f;      // Через сколько после начала анимации вылетает пуля (кадр выстрела)

    [Header("Скачок")]
    [SerializeField] private float leapDistance = 3.5f;     // Длина кувырка от врага
    [SerializeField] private float leapTime = 0.45f;        // Сколько длится кувырок (неуязвим)

    [Header("Барьер")]
    [SerializeField] private float barrierHpPart = 0.3f;    // Прочность щита — доля макс. HP
    [SerializeField] private float barrierTime = 6f;        // Сколько держится щит
    [SerializeField] private float barrierRange = 4f;       // Ставит щит, когда враг ближе этого

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для выстрела
    private const float KnockDrag = 10f;      // Как быстро гаснет движение кувырка (как отбрасывание в FighterMovement)

    private Fighter volleyTarget; // По кому идёт залп
    private int shotsFired;       // Сколько пуль уже выпущено
    private int cyclesStarted;    // Сколько раз уже начата анимация выстрела
    private bool castingBarrier;  // Текущее действие — Барьер
    private float actionLength;   // Сколько длится текущее действие

    // ---------- Внешний вид ----------

    public override int AnimSkill => !ActionAlive ? -1 : Pose == KitPose.Aim ? Volley : castingBarrier ? Barrier : -1;

    // ---------- Решение ----------

    /// <summary>Пока Залп готов — держится на средней дистанции (стрелять), иначе дерётся вплотную.</summary>
    public override float PreferredRange(Fighter target) =>
        IsUnlocked(Volley) && !IsOnCooldown(Volley) && !self.IsSilenced ? volleyKeepRange : 0f;

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);
        float nearest = NearestEnemyDistance(self.Position);

        // 3) Барьер — враг рядом, а Солдата уже бьют (или здоровья меньше 70%)
        if (IsReady(Barrier) && self.Shield <= 0f && nearest <= barrierRange
            && (self.Health.TimeSinceHit < 1.5f || self.Health.Fraction < 0.7f))
            return StartBarrier();

        // 1) Залп — на средней/дальней дистанции, когда враг не близко
        if (IsReady(Volley) && absY <= AimTolerance && nearest >= volleyMinRange && absX >= volleyMinRange && absX <= volleyMaxRange)
            return StartVolley(target);

        // 2) Скачок — Залп готов, но враг слишком близко, чтобы стрелять: отпрыгнуть от него на среднюю дистанцию
        if (IsReady(Leap) && IsReady(Volley) && nearest < volleyMinRange)
        {
            Fighter from = NearestEnemy();
            return StartLeap(from != null ? from : target);
        }

        return false;
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

    /// <summary>Ближайший живой противник (от него отпрыгивает Скачок). null — нет.</summary>
    private Fighter NearestEnemy()
    {
        Fighter best = null;
        float bestDist = float.MaxValue;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsAlive) continue;
            float d = Vector2.Distance(e.Position, self.Position);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    /// <summary>Скачок: кувырок от врага (неуязвим на время кувырка).</summary>
    private bool StartLeap(Fighter target)
    {
        if (!BeginAction(KitPose.Roll)) return false;
        StartCooldown(Leap);
        castingBarrier = false;
        actionLength = leapTime;
        self.SetInvulnerable(leapTime);
        float away = -Mathf.Sign(target.Position.x - self.Position.x);
        if (away == 0f) away = -self.Movement.Facing;
        float speed = Mathf.Sqrt(2f * KnockDrag * leapDistance); // проедет ровно leapDistance, пока движение гаснет
        self.Movement.FaceTowards(target.Position.x);
        self.Movement.ApplyKnockback(new Vector2(away * speed, 0f));
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
        Pop("БАРЬЕР", new Color(0.4f, 0.9f, 0.5f));
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
                    power = self.Stats.attack * volleyDamage,
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
