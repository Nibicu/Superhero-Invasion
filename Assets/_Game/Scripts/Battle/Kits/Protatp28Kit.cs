using UnityEngine;

/// <summary>
/// Протатип 28 (злодей, класс Танк, урон от Силы атаки). Навыки:
///  1) "Прыжок ярости" (ур. 1) — прыгает на врага (в прыжке неуязвим к ударам и контролю): приземлился — враги рядом
///     получают 120% Силы атаки и подлетают вверх.
///  2) "Подземный удар" (ур. 3) — бьёт в землю: из-под ближайшего врага — удар 100% Силы атаки, враг подлетает вверх.
///  3) "Ремонт" (ур. 5, пассивный) — реген HP идёт без перерыва, даже под ударами; пока HP меньше 50% — Реген HP больше на 50%.
/// Перезарядки — в файле героя (Data/Villains/Villain_Protatp28). Кадры — в Data/Characters/Sprites_Protatp28.
/// </summary>
public class Protatp28Kit : HeroKit
{
    private const int RageLeap = 0, Underground = 1, Repair = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        leapDamage = Num(RageLeap, "damage");
        undergroundDamage = Num(Underground, "damage");
        repairHp = Num(Repair, "hp");
        repairBonus = Num(Repair, "bonus");
    }

    [Header("Прыжок ярости")]
    [SerializeField] private float leapDamage = 1.2f;        // Урон (доля Силы атаки)
    [SerializeField] private float leapUp = 10f;             // Скорость прыжка вверх
    [SerializeField] private float leapMaxSpeed = 9f;        // Самая большая скорость прыжка по арене
    [SerializeField] private Vector2 leapRange = new Vector2(2.5f, 7f); // На какого врага прыгает (расстояние от и до)
    [SerializeField] private float leapRadius = 1.4f;        // Кого задевает приземление
    [SerializeField] private float leapLaunch = 9f;          // Как высоко подлетают враги

    [Header("Подземный удар")]
    [SerializeField] private float undergroundDamage = 1f;   // Урон (доля Силы атаки)
    [SerializeField] private float undergroundRange = 6f;    // Достаёт врага ближе этого
    [SerializeField] private float undergroundLaunch = 9f;   // Как высоко подлетает враг

    [Header("Ремонт (пассивка)")]
    [SerializeField] private float repairHp = 0.5f;          // Работает, пока HP меньше этой доли
    [SerializeField] private float repairBonus = 0.5f;       // На сколько больше Реген HP (+50%)

    private bool leaping;            // Идёт Прыжок ярости (ждём приземления)
    private bool striking;           // Идёт Подземный удар
    private bool struck;             // Удар из-под земли уже нанесён
    private Fighter strikeTarget;    // Под кем удар

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive && striking ? Underground : -1;

    // ---------- Пассивка ----------

    /// <summary>Ремонт: реген HP идёт без перерыва — даже когда Протатипа бьют (обычно удары его останавливают).</summary>
    public override bool RegenAlways => self != null && IsUnlocked(Repair);

    /// <summary>Ремонт: пока HP меньше 50%, Реген HP +50%.</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (IsUnlocked(Repair) && self.Health != null && self.Health.Fraction < repairHp)
            s.hpRegen = Mathf.RoundToInt(s.hpRegen * (1f + repairBonus));
        return s;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive || leaping) return false;
        Vector2 d = target.Position - self.Position;
        float dist = d.magnitude;

        // 1) Прыжок ярости — враг на расстоянии прыжка
        if (IsReady(RageLeap) && !retreating && target.IsTargetable && dist >= leapRange.x && dist <= leapRange.y)
            return StartLeap(target);

        // 2) Подземный удар — ближайший враг в досягаемости
        if (IsReady(Underground) && target.IsTargetable && dist <= undergroundRange)
        {
            if (!BeginAction(KitPose.Cast)) return false;
            StartCooldown(Underground);
            striking = true;
            struck = false;
            strikeTarget = target;
            self.Movement.FaceTowards(target.Position.x);
            return true;
        }
        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Прыжок ярости: дугой прямо на врага (удар в прыжке не нужен — урон при приземлении).</summary>
    private bool StartLeap(Fighter target)
    {
        float airTime = 2f * leapUp / Mathf.Max(1f, self.Movement.Gravity);
        Vector2 ground = (target.Position - self.Position) / airTime;
        if (ground.magnitude > leapMaxSpeed) ground = ground.normalized * leapMaxSpeed;
        self.Movement.FaceTowards(target.Position.x);
        if (!self.StartJump(leapUp, ground)) return false;
        self.Combat.SkipJumpAttack();
        // В прыжке неуязвим к ударам и к контролю (стан, шарм, подброс, захват) — сбить нельзя
        self.SetInvulnerable(airTime + 0.1f);
        self.SetControlImmune(airTime + 0.1f);
        StartCooldown(RageLeap);
        leaping = true;
        Pop("ПРЫЖОК ЯРОСТИ", new Color(1f, 0.4f, 0.2f));
        return true;
    }

    /// <summary>Приземление Прыжка ярости: урон и подбрасывание всех рядом, волна пыли.</summary>
    private void Land()
    {
        leaping = false;
        if (!self.IsAlive) return;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, self.transform.position + new Vector3(0f, 0.2f, 0f), new Color(0.75f, 0.6f, 0.4f, 0.7f),
                new Vector2(0.8f, 0.3f), new Vector2(leapRadius * 2.4f, leapRadius * 0.9f), 0.35f, 2).Fade(0.7f, 0f);
        foreach (Fighter e in EnemiesNear(self.Position, leapRadius))
            Launch(e, self.Stats.attack * leapDamage, leapLaunch);
    }

    /// <summary>Подземный удар: столб земли под врагом — урон и подбрасывание.</summary>
    private void StrikeFromBelow()
    {
        Fighter t = strikeTarget != null && strikeTarget.IsAlive && Vector2.Distance(strikeTarget.Position, self.Position) <= undergroundRange
            ? strikeTarget : NearestEnemy();
        if (t == null) return;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, t.transform.position + new Vector3(0f, 0.4f, 0f), new Color(0.5f, 0.35f, 0.2f, 0.95f),
                new Vector2(0.4f, 0.3f), new Vector2(0.8f, 1.8f), 0.4f, 5).Fade(0.95f, 0.1f);
        Launch(t, self.Stats.attack * undergroundDamage, undergroundLaunch);
        Pop("ПОДЗЕМНЫЙ УДАР", new Color(0.8f, 0.6f, 0.35f));
    }

    /// <summary>Удар с подбрасыванием: урон power (против Защиты), враг подлетает со скоростью launch.</summary>
    private void Launch(Fighter e, float power, float launch)
    {
        e.Health.TakeHit(new HitInfo
        {
            attacker = self,
            attackId = FighterCombat.NewAttackId(),
            damage = FighterCombat.CalcDamage(power, e.Stats.defense),
            launch = launch,
            special = false
        });
    }

    /// <summary>Ближайший живой враг в досягаемости Подземного удара (null — нет).</summary>
    private Fighter NearestEnemy()
    {
        Fighter best = null;
        float bestDist = undergroundRange;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsTargetable) continue;
            float d = Vector2.Distance(e.Position, self.Position);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        // Прыжок ярости: приземлились (или сбили в прыжке) — удар по земле
        if (leaping && self.State != FighterState.Jumping)
        {
            if (self.State == FighterState.Idle || self.State == FighterState.Moving) Land();
            else leaping = false; // сбили в воздухе — удара нет
        }

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; striking = false; return; } // по Протатипу попали — удар прерван
        ActionTime += Time.deltaTime;
        if (striking && !struck && ActionTime >= 0.25f) { struck = true; StrikeFromBelow(); }
        if (ActionTime >= 0.45f)
        {
            striking = false;
            strikeTarget = null;
            EndAction();
        }
    }
}
