using UnityEngine;

/// <summary>
/// Мотылёк (героиня, класс Поддержка, урон от Магии). Навыки:
///  1) "Зачарованный снаряд" (ур. 1) — очередью 2–5 магических шаров (летят прямо, чуть доворачивают к цели) (сколько хватит энергии, каждый — 15 энергии),
///     каждый — 45% Магии (как звёздочки Фантома).
///  2) "Волшебная пыль" (ур. 3) — пыль вокруг Мотылька (4 с): союзник, зашедший в неё, лечится на 30% макс. HP (MotelekDust).
///  3) "Выброс энергии" (ур. 5) — волна во все стороны: враги рядом получают 50% Силы атаки и отлетают.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Motelek). Кадры — в Data/Characters/Sprites_Motelek.
/// </summary>
public class MotelekKit : HeroKit
{
    private const int Orbs = 0, Dust = 1, Release = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        orbEnergy = Num(Orbs, "energy");
        orbDamage = Num(Orbs, "damage");
        orbsMin = Count(Orbs, "min");
        orbsMax = Mathf.Max(orbsMin, Count(Orbs, "max"));
        dustHeal = Num(Dust, "heal");
        dustTime = Num(Dust, "time");
        releaseDamage = Num(Release, "damage");
        releaseRadius = Num(Release, "radius");
    }

    [Header("Зачарованный снаряд")]
    [SerializeField] private int orbsMin = 2;               // Меньше этого не бросает (ждёт энергию)
    [SerializeField] private int orbsMax = 5;               // Больше этого не бросает
    [SerializeField] private float orbEnergy = 15f;         // Цена одного шара (энергия)
    [SerializeField] private float orbDamage = 0.45f;       // Урон одного шара (доля Магии)
    [SerializeField] private float orbInterval = 0.25f;     // Интервал между шарами
    [SerializeField] private float orbSpeed = 10f;          // Скорость
    [SerializeField] private float orbRange = 9f;           // Дальность
    [SerializeField] private float orbMinRange = 1.5f;      // Ближе — бьёт рукой
    [SerializeField] private Color orbColor = new Color(0.75f, 0.5f, 1f); // Сиреневый

    [Header("Волшебная пыль")]
    [SerializeField] private float dustHurtHp = 0.7f;       // Сыплет, когда кто-то рядом (или сам) ранен ниже этого
    [SerializeField] private float dustAllyRadius = 2.5f;   // Каких союзников считать "рядом"

    private float dustHeal = 0.3f;     // Лечение пыли — доля макс. HP
    private float dustTime = 4f;       // Сколько держится пыль

    [Header("Выброс энергии")]
    [SerializeField] private float releaseDamage = 0.5f;    // Урон (доля Силы атаки)
    [SerializeField] private float releaseRadius = 2.5f;    // Радиус волны
    [SerializeField] private float releaseKnockback = 7f;   // Сила отбрасывания

    private const int Knockdown = 99;          // "Очки сбивания" — сразу с ног

    private int current = -1;        // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;      // Сколько длится действие
    private int orbsTotal;           // Сколько шаров в этом броске
    private int orbsThrown;          // Сколько уже брошено
    private Fighter orbTarget;       // В кого бросает

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 2) Пыль — кто-то из своих рядом ранен
        if (IsReady(Dust) && SomeoneHurtNear()) return StartDust();

        // 3) Выброс энергии — враги вплотную (двое и больше, или один, а Мотылёк ранен)
        if (IsReady(Release))
        {
            int near = EnemiesNear(self.Position, releaseRadius * 0.8f).Count;
            if (near >= 2 || (near == 1 && self.Health.Fraction < 0.6f)) return StartRelease();
        }

        // 1) Шары — цель не вплотную, примерно на одной линии, энергии хотя бы на 2
        if (IsReady(Orbs) && target.IsTargetable && absY <= 0.8f && absX >= orbMinRange && absX <= orbRange
            && self.Combat.HasEnergy(orbEnergy * orbsMin))
            return StartOrbs(target);

        return false;
    }

    /// <summary>Кто-то из союзников рядом (или сам Мотылёк) ранен ниже dustHurtHp.</summary>
    private bool SomeoneHurtNear()
    {
        foreach (Fighter a in BattleManager.Instance.GetAllies(self.Faction))
            if (a.IsAlive && a.Health.Fraction < dustHurtHp && Vector2.Distance(a.Position, self.Position) <= dustAllyRadius) return true;
        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Начать действие скила skill длиной length.</summary>
    private bool Begin(int skill, float length)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        actionLength = length;
        return true;
    }

    /// <summary>Шары: сколько хватает энергии (от 2 до 5), по одному через orbInterval.</summary>
    private bool StartOrbs(Fighter target)
    {
        orbsTotal = Mathf.Clamp(Mathf.FloorToInt(self.Combat.Energy / orbEnergy), orbsMin, orbsMax);
        if (!Begin(Orbs, 0.1f + orbInterval * orbsTotal + 0.15f)) return false;
        orbsThrown = 0;
        orbTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Бросить один шар (очередь: прямо, с доворотом к цели).</summary>
    private void ThrowOrb()
    {
        // Очередь (как в Little Fighter 2 при зажатой кнопке): кончилась энергия или цель выбыла — очередь обрывается
        if (orbTarget == null || !orbTarget.IsAlive || !self.Combat.TrySpendEnergy(orbEnergy)) { orbsTotal = orbsThrown; actionLength = Mathf.Min(actionLength, ActionTime + 0.15f); return; }
        if (orbTarget != null && orbTarget.IsAlive) self.Movement.FaceTowards(orbTarget.Position.x);
        orbsThrown++;
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * orbDamage,
            vsSpecial = true,
            speed = orbSpeed,
            range = orbRange,
            size = 0.5f,
            stagger = 1,
            knockback = 1.5f,
            steerTarget = orbTarget, // летит прямо, но чуть доворачивает к цели
            color = orbColor,
            flicker = new Color(1f, 0.85f, 1f)
        });
    }

    /// <summary>Пыль: облако вокруг Мотылька.</summary>
    private bool StartDust()
    {
        if (!Begin(Dust, 0.4f)) return false;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        MotelekDust.Spread(self, self.transform.position, circle, dustHeal, dustTime);
        Pop("ВОЛШЕБНАЯ ПЫЛЬ", new Color(1f, 0.95f, 0.55f));
        return true;
    }

    /// <summary>Выброс энергии: кольцо во все стороны, враги рядом — урон и отлёт.</summary>
    private bool StartRelease()
    {
        if (!Begin(Release, 0.4f)) return false;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, self.transform.position + new Vector3(0f, 0.6f, 0f), new Color(1f, 0.95f, 0.7f, 0.7f),
                new Vector2(0.6f, 0.4f), new Vector2(releaseRadius * 2f, releaseRadius * 1.2f), 0.35f, 60).Fade(0.7f, 0f);
        foreach (Fighter e in EnemiesNear(self.Position, releaseRadius))
        {
            float dir = Mathf.Sign(e.Position.x - self.Position.x + 0.001f);
            HitEnemy(e, self.Stats.attack * releaseDamage, false, new Vector2(dir * releaseKnockback, 0f), Knockdown);
        }
        Pop("ВЫБРОС ЭНЕРГИИ", new Color(1f, 0.95f, 0.7f));
        return true;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Мотыльку попали — действие прервано
        ActionTime += Time.deltaTime;

        if (current == Orbs && orbsThrown < orbsTotal && ActionTime >= 0.1f + orbsThrown * orbInterval) ThrowOrb();

        if (ActionTime >= actionLength)
        {
            current = -1;
            orbTarget = null;
            EndAction();
        }
    }
}
