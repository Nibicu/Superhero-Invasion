using UnityEngine;

/// <summary>
/// Рейнджер (герой, класс Стрелок, урон от Силы атаки). Навыки:
///  1) "Мощный выстрел" (ур. 1) — летит до конца арены, пробивает всех на пути и сбивает с ног: 150% Силы атаки.
///  2) "Ястреб" (ур. 3) — ястреб раз в 3 с пикирует на ближайшего врага: 60% Силы атаки, враг шатается (гроги).
///     Бьёт 3 раза, потом улетает; перезарядка начинается после 3-го удара.
///  3) "Энерго стрела" (ур. 5, пассивный) — каждая 5-я авто атака выпускает ещё и энерго стрелу: 50% Силы атаки.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Reyndger). Кадры — в Data/Characters/Sprites_Reyndger.
/// </summary>
public class ReyndgerKit : HeroKit
{
    private const int PowerShot = 0, Hawk = 1, EnergyArrow = 2; // номера скилов

    [Header("Мощный выстрел")]
    [SerializeField] private float shotDamage = 1.5f;        // Урон (доля Силы атаки)
    [SerializeField] private float shotSpeed = 16f;          // Скорость
    [SerializeField] private float shotKnockback = 6f;       // Сила отбрасывания
    [SerializeField] private float shotMaxRange = 12f;       // Стреляет, когда враг на линии ближе этого

    [Header("Ястреб")]
    [SerializeField] private int hawkStrikes = 3;            // Сколько раз пикирует
    [SerializeField] private float hawkInterval = 3f;        // Раз во сколько секунд
    [SerializeField] private float hawkDamage = 0.6f;        // Урон удара (доля Силы атаки)
    [SerializeField] private float hawkRange = 10f;          // На каких врагов пикирует (от Рейнджера)
    [SerializeField] private float hawkDiveTime = 0.25f;     // Сколько летит вниз до удара
    [SerializeField] private Color hawkColor = new Color(0.55f, 0.38f, 0.2f); // Коричневый

    [Header("Энерго стрела (пассивка)")]
    [SerializeField] private int arrowEveryNth = 5;          // Каждая какая авто атака
    [SerializeField] private float arrowDamage = 0.5f;       // Урон (доля Силы атаки)

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели
    private const int Knockdown = 99;         // "Очки сбивания" — сразу с ног

    private bool casting;            // Идёт выстрел
    private bool fired;              // Пуля вылетела
    private Fighter shotTarget;      // По кому выстрел
    private int autoCount;           // Счётчик авто атак (для энерго стрелы)

    private int hawkLeft;            // Сколько ударов ястреба осталось (0 — ястреба нет)
    private float hawkTimer;         // До следующего пике
    private Fighter diveTarget;      // На кого пикирует сейчас
    private float diveTimer;         // До удара (идёт пике)
    private SkillFx hawkFx;          // Ястреб кружит над Рейнджером

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive && casting ? PowerShot : -1;

    // ---------- Пассивка ----------

    /// <summary>Энерго стрела: каждая 5-я авто атака — ещё и стрела в сторону взгляда.</summary>
    public override void OnAutoAttack()
    {
        if (!IsUnlocked(EnergyArrow)) return;
        autoCount++;
        if (autoCount % Mathf.Max(1, arrowEveryNth) != 0) return;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.attack * arrowDamage,
            vsSpecial = false,
            speed = 14f,
            range = 9f,
            size = 0.55f,
            stagger = 1,
            knockback = 2f,
            color = new Color(0.4f, 0.95f, 1f),
            flicker = new Color(0.8f, 1f, 1f)
        });
        Pop("ЭНЕРГО СТРЕЛА", new Color(0.5f, 0.95f, 1f));
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;

        // 2) Ястреб — если его ещё нет и враг недалеко
        if (IsReady(Hawk) && hawkLeft == 0 && d.magnitude <= hawkRange) { SummonHawk(); return false; } // ястреб летит сам, Рейнджер не стоит

        // 1) Мощный выстрел — цель на линии
        if (IsReady(PowerShot) && target.IsTargetable && Mathf.Abs(d.y) <= AimTolerance && Mathf.Abs(d.x) <= shotMaxRange)
        {
            if (!BeginAction(KitPose.Aim)) return false;
            StartCooldown(PowerShot);
            casting = true;
            fired = false;
            shotTarget = target;
            self.Movement.FaceTowards(target.Position.x);
            return true;
        }
        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Мощный выстрел: до конца арены, пробивает и сбивает всех на пути.</summary>
    private void FirePowerShot()
    {
        if (shotTarget != null && shotTarget.IsAlive) self.Movement.FaceTowards(shotTarget.Position.x);
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.attack * shotDamage,
            vsSpecial = false,
            speed = shotSpeed,
            range = 200f, // до края арены
            size = 0.8f,
            pierce = true,
            stagger = Knockdown,
            knockback = shotKnockback,
            color = new Color(1f, 0.85f, 0.3f)
        });
        Pop("МОЩНЫЙ ВЫСТРЕЛ", new Color(1f, 0.85f, 0.3f));
    }

    /// <summary>Позвать ястреба: кружит над Рейнджером, первое пике — почти сразу.</summary>
    private void SummonHawk()
    {
        hawkLeft = hawkStrikes;
        hawkTimer = 0.5f;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            hawkFx = SkillFx.Spawn(circle, self.transform.position, hawkColor, new Vector2(0.6f, 0.3f), new Vector2(0.6f, 0.3f), 60f, 80)
                .Follow(self.transform, new Vector3(0f, 2.8f, 0f));
        Pop("ЯСТРЕБ", hawkColor);
    }

    /// <summary>Ястреб: раз в 3 с пикирует на ближайшего врага; после 3-го удара улетает (перезарядка).</summary>
    private void UpdateHawk(float dt)
    {
        if (hawkLeft <= 0) return;
        if (!self.IsAlive) { EndHawk(); return; }

        if (diveTarget != null)
        {
            diveTimer -= dt;
            if (diveTimer > 0f) return;
            Strike(diveTarget);
            diveTarget = null;
            hawkLeft--;
            hawkTimer = hawkInterval;
            if (hawkLeft <= 0) EndHawk();
            return;
        }

        hawkTimer -= dt;
        if (hawkTimer > 0f) return;
        Fighter t = NearestTarget();
        if (t == null) { hawkTimer = 0.3f; return; }
        diveTarget = t;
        diveTimer = hawkDiveTime;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, t.transform.position + new Vector3(0f, 3.5f, 0f), hawkColor, new Vector2(0.6f, 0.3f), new Vector2(0.5f, 0.35f), hawkDiveTime, 80)
                .Move(new Vector3(0f, -2.6f / hawkDiveTime, 0f));
    }

    /// <summary>Удар ястреба: урон и гроги (шатается).</summary>
    private void Strike(Fighter t)
    {
        if (t == null || !t.IsAlive) return;
        if (HitEnemy(t, self.Stats.attack * hawkDamage, false, Vector2.zero, 0) && t.CanBecomeDizzy) t.BecomeDizzy();
    }

    /// <summary>Ближайший враг, на которого можно пикировать (null — нет).</summary>
    private Fighter NearestTarget()
    {
        Fighter best = null;
        float bestDist = hawkRange;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsTargetable) continue;
            float d = Vector2.Distance(e.Position, self.Position);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    /// <summary>Ястреб улетел — перезарядка пошла.</summary>
    private void EndHawk()
    {
        hawkLeft = 0;
        diveTarget = null;
        if (hawkFx != null) hawkFx.Kill();
        StartCooldown(Hawk);
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (self != null) UpdateHawk(dt);

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; casting = false; return; } // по Рейнджеру попали — выстрел прерван
        ActionTime += dt;
        if (casting && !fired && ActionTime >= 0.25f) { fired = true; FirePowerShot(); }
        if (ActionTime >= 0.45f)
        {
            casting = false;
            shotTarget = null;
            EndAction();
        }
    }

    private void OnDestroy()
    {
        if (hawkFx != null) hawkFx.Kill();
    }
}
