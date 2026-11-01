using UnityEngine;

/// <summary>
/// Чародейка (злодейка, класс Маг, урон от Магии, ближний бой). Навыки:
///  1) "Энерго стрела" (ур. 1) — быстрая стрела жёлтой энергии на жёлтой верёвке (средняя дистанция):
///     попала — враг получает 60% Магии, оглушён и подтянут вплотную к Чародейке.
///  2) "Мышь" (ур. 3) — самонаводящаяся летучая мышь фиолетовой энергии: цель получает 50% Магии и стан 2 с,
///     враги рядом с целью тоже оглушены на 2 с (без урона).
///  3) "Мираж" (ур. 5, пассивный) — когда HP падает до 20%, телепортируется подальше от врагов.
/// Перезарядки — в файле героя (Data/Villains/Villain_Charovnica). Кадры — в Data/Characters/Sprites_Charovnica.
/// </summary>
public class CharovnicaKit : HeroKit
{
    private const int Arrow = 0, Bat = 1, Mirage = 2; // номера скилов

    [Header("Энерго стрела")]
    [SerializeField] private float arrowDamage = 0.6f;       // Урон (доля Магии)
    [SerializeField] private float arrowRange = 7f;          // Дальность (средняя)
    [SerializeField] private float arrowMinRange = 2f;       // Ближе — бьёт рукой
    [SerializeField] private float arrowSpeed = 18f;         // Скорость (быстрая)
    [SerializeField] private float arrowStun = 1.2f;         // Стан цели (сек)
    [SerializeField] private float pullTime = 0.25f;         // За сколько секунд цель подтягивается
    [SerializeField] private Color ropeColor = new Color(1f, 0.85f, 0.15f); // Жёлтая верёвка

    [Header("Мышь")]
    [SerializeField] private float batDamage = 0.5f;         // Урон цели (доля Магии)
    [SerializeField] private float batStun = 2f;             // Стан цели и врагов рядом
    [SerializeField] private float batRadius = 2.5f;         // Кого из врагов рядом с целью тоже оглушает
    [SerializeField] private float batRange = 9f;            // Пускает, когда враг ближе этого
    [SerializeField] private float batSpeed = 6.5f;          // Скорость полёта
    [SerializeField] private float batLifetime = 5f;         // Сколько секунд летит

    [Header("Мираж (пассивка)")]
    [SerializeField] private float mirageHp = 0.2f;          // При какой доле HP телепортируется
    [SerializeField] private float mirageRadius = 7f;        // Насколько далеко по арене

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для стрелы

    private int current = -1;        // Какой скил сейчас применяется (-1 — никакой)
    private bool fired;              // Снаряд уже вылетел (в текущем действии)
    private float actionLength;      // Сколько длится текущее действие
    private Fighter skillTarget;     // В кого скил
    private Projectile arrowShot;    // Летящая стрела (к ней тянется верёвка)
    private Fighter pulled;          // Кого подтягиваем
    private float pullClock;         // Сколько уже тянем
    private Vector3 pullFrom;        // Откуда тянем цель
    private LineRenderer rope;       // Верёвка стрелы

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    public override void Setup(Fighter f)
    {
        base.Setup(f);
        CreateRope();
    }

    /// <summary>Верёвка: тонкая жёлтая линия от руки до стрелы (или до подтягиваемого врага).</summary>
    private void CreateRope()
    {
        var go = new GameObject("Rope");
        go.transform.SetParent(transform, false);
        rope = go.AddComponent<LineRenderer>();
        rope.positionCount = 2;
        rope.useWorldSpace = true;
        rope.startWidth = rope.endWidth = 0.06f;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) rope.material = new Material(shader);
        rope.startColor = rope.endColor = ropeColor;
        rope.sortingOrder = 30000;
        rope.enabled = false;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 1) Энерго стрела — цель на одной линии, средняя дистанция
        if (IsReady(Arrow) && target.IsTargetable && absY <= AimTolerance && absX >= arrowMinRange && absX <= arrowRange)
            return Begin(Arrow, target, 0.35f);

        // 2) Мышь — самонаводящаяся, линия не нужна
        if (IsReady(Bat) && target.IsTargetable && d.magnitude <= batRange)
            return Begin(Bat, target, 0.4f);

        return false;
    }

    /// <summary>Начать бросок скила skill в target.</summary>
    private bool Begin(int skill, Fighter target, float length)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        fired = false;
        skillTarget = target;
        actionLength = length;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Энерго стрела: быстрая, на верёвке; попала — урон, стан и подтягивание.</summary>
    private void FireArrow()
    {
        arrowShot = self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * arrowDamage,
            vsSpecial = true,
            speed = arrowSpeed,
            range = arrowRange + 0.5f,
            size = 0.55f,
            pierce = false,
            stagger = 0,
            knockback = 0f,
            stun = arrowStun,
            color = new Color(1f, 0.9f, 0.2f),
            onHit = StartPull
        });
    }

    /// <summary>Стрела попала: тянем цель вплотную к Чародейке.</summary>
    private void StartPull(Fighter e)
    {
        if (e == null || !e.IsAlive || !self.IsAlive) return;
        pulled = e;
        pullClock = 0f;
        pullFrom = e.transform.position;
        Pop("ПОПАЛАСЬ!", new Color(1f, 0.9f, 0.3f));
    }

    /// <summary>Подтягивание: цель плавно едет к Чародейке (на расстояние удара).</summary>
    private void UpdatePull(float dt)
    {
        if (pulled == null) return;
        if (!pulled.IsAlive || !self.IsAlive) { pulled = null; return; }
        pullClock += dt;
        float side = Mathf.Sign(pullFrom.x - self.transform.position.x);
        if (side == 0f) side = self.Movement.Facing;
        Vector3 to = self.transform.position + new Vector3(side * self.meleeRange * 0.9f, 0f, 0f);
        Vector3 p = Vector3.Lerp(pullFrom, to, Mathf.Clamp01(pullClock / pullTime));
        pulled.transform.position = BattleManager.Instance.ClampToArena(pulled, p);
        if (pullClock >= pullTime) pulled = null;
    }

    /// <summary>Мышь: самонаводящаяся, урон цели и стан ей и врагам рядом.</summary>
    private void FireBat()
    {
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * batDamage,
            vsSpecial = true,
            speed = batSpeed,
            range = 999f,
            lifetime = batLifetime,
            homing = skillTarget,
            size = 0.7f,
            stagger = 0,
            knockback = 1f,
            color = new Color(0.6f, 0.25f, 0.95f),
            flicker = new Color(0.85f, 0.6f, 1f),
            onHit = OnBatHit
        });
    }

    /// <summary>Мышь попала: стан цели и всем её союзникам рядом.</summary>
    private void OnBatHit(Fighter e)
    {
        if (e == null) return;
        e.ApplyStun(batStun);
        foreach (Fighter a in BattleManager.Instance.GetAllies(e.Faction))
            if (a != e && a.IsAlive && Vector2.Distance(a.Position, e.Position) <= batRadius) a.ApplyStun(batStun);
    }

    // ---------- Мираж ----------

    /// <summary>Мираж: HP упало до 20% — телепорт подальше от врагов (перезарядка — в файле героя).</summary>
    private void UpdateMirage()
    {
        if (!IsUnlocked(Mirage) || IsOnCooldown(Mirage) || !self.IsAlive) return;
        if (self.State == FighterState.KnockedDown || self.Health.Fraction > mirageHp) return;
        StartCooldown(Mirage);
        TeleportAway(mirageRadius);
        self.SetInvulnerable(0.3f);
        Pop("МИРАЖ", new Color(0.75f, 0.6f, 1f));
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        UpdateMirage();
        UpdatePull(dt);
        UpdateRope();

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Чародейке попали — действие прервано
        ActionTime += dt;

        if (!fired && ActionTime >= (current == Arrow ? 0.12f : 0.2f))
        {
            fired = true;
            if (skillTarget != null && skillTarget.IsAlive) self.Movement.FaceTowards(skillTarget.Position.x);
            if (current == Arrow) FireArrow();
            else if (current == Bat) FireBat();
        }

        if (ActionTime >= actionLength)
        {
            current = -1;
            skillTarget = null;
            EndAction();
        }
    }

    /// <summary>Верёвка: от руки до летящей стрелы или до подтягиваемого врага.</summary>
    private void UpdateRope()
    {
        if (rope == null) return;
        Vector3 hand = self.transform.position + new Vector3(self.Movement.Facing * 0.4f, 1f, 0f);
        bool flying = arrowShot != null; // уничтоженный снаряд Unity считает равным null
        if (pulled != null) SetRope(hand, pulled.transform.position + new Vector3(0f, 1f, 0f));
        else if (flying) SetRope(hand, arrowShot.VisualPosition);
        else if (rope.enabled) rope.enabled = false;
    }

    private void SetRope(Vector3 a, Vector3 b)
    {
        rope.enabled = true;
        rope.SetPosition(0, a);
        rope.SetPosition(1, b);
    }
}
