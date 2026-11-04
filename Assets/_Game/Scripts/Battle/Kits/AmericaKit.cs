using UnityEngine;

/// <summary>
/// Америка (героиня, класс Боец, урон от Силы атаки). Навыки:
///  1) "Энергетический бумеранг" (ур. 1) — кольцо летит вперёд на среднее расстояние и возвращается к Америке;
///     каждого врага на пути туда и каждого на пути обратно ранит (60% Силы атаки) и вводит в гроги (шатается).
///  2) "Энерго-апперкот" (ур. 3) — апперкот всем врагам вплотную перед ней: урон 90% Силы атаки и подбрасывает.
///  3) "Контратака" (ур. 5, пассивный) — в ближнем бою пропущенный удар не проходит, а Америка сразу бьёт
///     ударившего мощным ударом (100% Силы атаки) — он отлетает и падает. Потом перезарядка.
/// Перезарядки — в файле героя (Data/Heroes/Hero_America). Кадры — в Data/Characters/Sprites_America.
/// </summary>
public class AmericaKit : HeroKit
{
    private const int Boomerang = 0, Uppercut = 1, Counter = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        boomerangDamage = Num(Boomerang, "damage");
        boomerangRange = Num(Boomerang, "range");
        uppercutDamage = Num(Uppercut, "damage");
        uppercutReach = Num(Uppercut, "radius");
        counterDamage = Num(Counter, "damage");
    }

    [Header("Энергетический бумеранг")]
    [SerializeField] private float boomerangDamage = 0.6f;  // Урон за попадание (доля Силы атаки)
    [SerializeField] private float boomerangRange = 5f;     // Как далеко летит
    [SerializeField] private float boomerangSpeed = 8f;     // Скорость полёта

    [Header("Энерго-апперкот")]
    [SerializeField] private float uppercutDamage = 0.9f;   // Урон (доля Силы атаки)
    [SerializeField] private float uppercutReach = 1.6f;    // Досягаемость перед Америкой
    [SerializeField] private float uppercutLaunch = 8f;     // Как высоко подлетают враги

    [Header("Контратака (пассивка)")]
    [SerializeField] private float counterDamage = 1f;      // Урон (доля Силы атаки)
    [SerializeField] private float counterKnock = 5f;       // Как сильно отлетает ударивший

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;    // Сколько длится действие
    private bool fired;            // Бумеранг брошен / апперкот нанесён

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive || !target.IsTargetable) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 2) Апперкот — враг вплотную
        if (IsReady(Uppercut) && absX <= uppercutReach && absY <= 0.7f)
            return Begin(Uppercut, 0.4f, target);

        // 1) Бумеранг — враг впереди на средней дистанции, примерно на одной глубине
        if (IsReady(Boomerang) && absX <= boomerangRange + 0.5f && absX >= 1f && absY <= 0.6f)
            return Begin(Boomerang, 0.35f, target);
        return false;
    }

    /// <summary>Начать действие скила skill длиной length против target.</summary>
    private bool Begin(int skill, float length, Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        actionLength = length;
        fired = false;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Бросить бумеранг вперёд.</summary>
    private void ThrowBoomerang()
    {
        int dir = self.Movement.Facing;
        Vector3 start = self.transform.position + new Vector3(dir * 0.5f, 0f, 0f);
        EnergyBoomerang.Throw(self, start, dir, self.Stats.attack * boomerangDamage, boomerangRange, boomerangSpeed,
                              self.View != null ? self.View.CircleSprite : null);
        Pop("БУМЕРАНГ", new Color(0.4f, 0.9f, 1f));
    }

    /// <summary>Апперкот всем врагам вплотную перед Америкой (урон и подбрасывание).</summary>
    private void DoUppercut()
    {
        int dir = self.Movement.Facing;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsAlive || e.IsInvulnerable) continue;
            Vector2 d = e.Position - self.Position;
            if (d.x * dir < -0.3f || Mathf.Abs(d.x) > uppercutReach || Mathf.Abs(d.y) > 0.7f) continue; // только перед собой
            LaunchEnemy(e, self.Stats.attack * uppercutDamage, false, uppercutLaunch, new Vector2(dir * 1.5f, 0f));
        }
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, self.transform.position + new Vector3(dir * 0.7f, 0.9f, 0f), new Color(0.4f, 0.9f, 1f),
                new Vector2(0.4f, 0.8f), new Vector2(0.9f, 1.6f), 0.3f, 60).Fade(0.8f, 0f).Move(new Vector3(0f, 2f, 0f));
        Pop("АППЕРКОТ", new Color(0.4f, 0.9f, 1f));
    }

    // ---------- Контратака ----------

    /// <summary>
    /// Контратака: удар в ближнем бою не проходит, а Америка сразу бьёт ударившего (он отлетает и падает).
    /// Работает, когда скил открыт и перезарядился, а Америка на ногах.
    /// </summary>
    public override bool TryBlock(HitInfo hit)
    {
        if (!IsUnlocked(Counter) || IsOnCooldown(Counter) || !self.IsAlive || self.IsStunned) return false;
        FighterState st = self.State;
        if (st == FighterState.KnockedDown || st == FighterState.Airborne || st == FighterState.Held
            || st == FighterState.Dizzy || st == FighterState.Jumping) return false;
        Fighter a = hit.attacker;
        if (a == null || !a.IsAlive || a.Faction == self.Faction) return false;
        Vector2 d = a.Position - self.Position;
        if (Mathf.Abs(d.x) > self.meleeRange * 1.6f || Mathf.Abs(d.y) > 0.7f) return false; // только ближний бой

        StartCooldown(Counter);
        self.Movement.FaceTowards(a.Position.x);
        int dir = d.x >= 0f ? 1 : -1;
        a.Health.TakeHit(new HitInfo
        {
            attacker = self,
            attackId = FighterCombat.NewAttackId(),
            damage = FighterCombat.CalcDamage(self.Stats.attack * counterDamage, a.Stats.defense),
            knockback = new Vector2(dir * counterKnock, 0f),
            stagger = Fighter.KnockdownStagger // мощный удар — сбивает с ног
        });
        Pop("КОНТРАТАКА", new Color(1f, 0.85f, 0.3f));
        return true;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Америке попали — действие прервано
        ActionTime += Time.deltaTime;

        if (!fired && ActionTime >= 0.15f)
        {
            fired = true;
            if (current == Boomerang) ThrowBoomerang();
            else if (current == Uppercut) DoUppercut();
        }

        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }
}
