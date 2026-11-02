using UnityEngine;

/// <summary>
/// Аква (героиня, класс Танк, урон от Магии). Навыки:
///  1) "Водяная тюрьма" (ур. 1) — шар воды: попал — 70% Магии, враг заперт в пузыре и оглушён на 3 с.
///  2) "Проклятый шлем" (ур. 3, пассивный) — Защита +30.
///  3) "Взрывной пузырь" (ур. 5, пассивный) — улучшает Водяную тюрьму: когда 3 с стана проходят, пузырь лопается —
///     враги рядом отлетают, а пленник подлетает вверх. И ещё Спец. защита +30 (к шлему).
/// Перезарядки — в файле героя (Data/Heroes/Hero_Aqua). Кадры — в Data/Characters/Sprites_Aqua.
/// </summary>
public class AquaKit : HeroKit
{
    private const int Prison = 0, Helmet = 1, Burst = 2; // номера скилов

    [Header("Водяная тюрьма")]
    [SerializeField] private float prisonDamage = 0.7f;     // Урон (доля Магии)
    [SerializeField] private float prisonStun = 3f;         // Сколько заперт (стан)
    [SerializeField] private float prisonRange = 8f;        // Дальность шара
    [SerializeField] private float prisonMinRange = 1.5f;   // Ближе — бьёт рукой
    [SerializeField] private float prisonSpeed = 8f;        // Скорость шара
    [SerializeField] private Color waterColor = new Color(0.25f, 0.7f, 1f); // Цвет воды

    [Header("Проклятый шлем (пассивка)")]
    [SerializeField] private int helmetDefense = 30;        // +Защита
    [SerializeField] private int burstSpecialDefense = 30;  // +Спец. защита (когда открыт 3-й навык)

    [Header("Взрывной пузырь")]
    [SerializeField] private float burstRadius = 2.2f;      // Кого отбрасывает взрыв (от пленника)
    [SerializeField] private float burstKnockback = 7f;     // Сила отбрасывания
    [SerializeField] private float burstLaunch = 9f;        // Как высоко подлетает пленник

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для шара
    private const int Knockdown = 99;         // "Очки сбивания" — сразу с ног

    private bool casting;            // Идёт бросок шара
    private bool fired;              // Шар уже вылетел
    private Fighter skillTarget;     // В кого бросает
    private Fighter prisoner;        // Кто сейчас заперт в пузыре
    private float prisonTimer;       // Сколько ещё заперт
    private SkillFx bubble;          // Картинка пузыря вокруг пленника

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive && casting ? Prison : -1;

    // ---------- Пассивки ----------

    /// <summary>Проклятый шлем: Защита +30; с 3-м навыком — ещё Спец. защита +30.</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (IsUnlocked(Helmet)) s.defense += helmetDefense;
        if (IsUnlocked(Burst)) s.specialDefense += burstSpecialDefense;
        return s;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive || prisoner != null) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);
        if (!IsReady(Prison) || !target.IsTargetable || absY > AimTolerance || absX < prisonMinRange || absX > prisonRange) return false;
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Prison);
        casting = true;
        fired = false;
        skillTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Шар воды: попал — урон и тюрьма.</summary>
    private void FirePrison()
    {
        if (skillTarget != null && skillTarget.IsAlive) self.Movement.FaceTowards(skillTarget.Position.x);
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * prisonDamage,
            vsSpecial = true,
            speed = prisonSpeed,
            range = prisonRange + 0.5f,
            size = 0.9f,
            stagger = 0,
            knockback = 0f,
            color = waterColor,
            flicker = new Color(0.7f, 0.95f, 1f),
            onHit = Imprison
        });
    }

    /// <summary>Запереть врага в пузыре: стан 3 с, вокруг — водяной шар.</summary>
    private void Imprison(Fighter e)
    {
        if (e == null || !e.IsAlive) return;
        e.ApplyStun(prisonStun);
        prisoner = e;
        prisonTimer = prisonStun;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            bubble = SkillFx.Spawn(circle, e.transform.position, new Color(waterColor.r, waterColor.g, waterColor.b, 0.35f),
                new Vector2(2f, 2f), new Vector2(2.1f, 2.1f), prisonStun, 60).Follow(e.transform, new Vector3(0f, 0.85f, 0f));
        Pop("ТЮРЬМА", waterColor);
    }

    /// <summary>Пузырь: держит пленника; время вышло — лопается (с 3-м навыком — взрыв).</summary>
    private void UpdatePrison(float dt)
    {
        if (prisoner == null) return;
        prisonTimer -= dt;
        if (prisoner.IsAlive && prisonTimer > 0f) return;
        Fighter p = prisoner;
        prisoner = null;
        if (bubble != null) bubble.Kill();
        if (!p.IsAlive || !IsUnlocked(Burst)) return;

        // Взрыв: враги рядом отлетают, пленник подлетает вверх
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, p.transform.position + new Vector3(0f, 0.85f, 0f), new Color(waterColor.r, waterColor.g, waterColor.b, 0.6f),
                new Vector2(1.5f, 1.5f), new Vector2(burstRadius * 2.2f, burstRadius * 1.6f), 0.35f, 60).Fade(0.6f, 0f);
        foreach (Fighter e in Enemies)
        {
            if (e == p || !e.IsAlive || Vector2.Distance(e.Position, p.Position) > burstRadius) continue;
            float dir = Mathf.Sign(e.Position.x - p.Position.x + 0.001f);
            HitEnemy(e, 0f, true, new Vector2(dir * burstKnockback, 0f), Knockdown);
        }
        p.Health.TakeHit(new HitInfo
        {
            attacker = self,
            attackId = FighterCombat.NewAttackId(),
            damage = 0f,
            launch = burstLaunch,
            special = true
        });
        Pop("ВЗРЫВ ПУЗЫРЯ", waterColor);
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        UpdatePrison(dt);

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; casting = false; return; } // по Акве попали — бросок прерван
        ActionTime += dt;
        if (casting && !fired && ActionTime >= 0.2f) { fired = true; FirePrison(); }
        if (ActionTime >= 0.4f)
        {
            casting = false;
            skillTarget = null;
            EndAction();
        }
    }

    private void OnDestroy()
    {
        if (bubble != null) bubble.Kill();
    }
}
