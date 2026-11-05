using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фантом (злодей, класс Боец, ближний бой). Навыки:
///  1) "Клоны" (ур. 1) — выпускает 2 своих клона: бойцы без скилов с 30% его характеристик (скорость — как у Фантома), тёмные.
///     Перезарядка начинается, когда погибнет последний клон. Фантом выбыл — клоны исчезают.
///  2) "Звёздочки" (ур. 3) — метает очередью от 2 до 5 звёздочек (летят прямо, чуть доворачивают к цели) (сколько хватает энергии, каждая — 15 энергии),
///     каждая — 45% Силы атаки.
///  3) "Нематериальный" (ур. 5, пассивный) — 20% шанс увернуться от вражеской авто атаки.
/// Перезарядки — в файле героя (Data/Villains/Villain_Phantom). Кадры — в Data/Characters/Sprites_Phantom.
/// </summary>
public class PhantomKit : HeroKit
{
    private const int Clones = 0, Stars = 1, Immaterial = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        cloneCount = Count(Clones, "count");
        cloneStats = Num(Clones, "stats");
        starEnergy = Num(Stars, "energy");
        starDamage = Num(Stars, "damage");
        starsMin = Count(Stars, "min");
        starsMax = Mathf.Max(starsMin, Count(Stars, "max"));
        dodgeChance = Num(Immaterial, "dodge");
    }

    [Header("Клоны")]
    [SerializeField] private int cloneCount = 2;            // Сколько клонов
    [SerializeField] private float cloneStats = 0.3f;       // Характеристики клона — доля характеристик Фантома (кроме скорости)
    [SerializeField] private float cloneSpread = 1.6f;      // Насколько далеко от Фантома появляются клоны
    [SerializeField] private float cloneCastTime = 0.3f;    // Когда появляются клоны (после начала анимации)
    [SerializeField] private float cloneActionTime = 0.5f;  // Сколько длится применение

    [Header("Звёздочки")]
    [SerializeField] private int starsMin = 2;              // Меньше этого не метает (не хватает энергии — ждёт)
    [SerializeField] private int starsMax = 5;              // Больше этого не метает
    [SerializeField] private float starEnergy = 15f;        // Цена одной звёздочки (энергия)
    [SerializeField] private float starDamage = 0.45f;      // Урон одной звёздочки (доля Силы атаки)
    [SerializeField] private float starInterval = 0.25f;    // Интервал между звёздочками
    [SerializeField] private float starSpeed = 12f;         // Скорость полёта
    [SerializeField] private float starRange = 9f;          // Дальность
    [SerializeField] private float starMinRange = 1.5f;     // Ближе — бьёт рукой

    [Header("Нематериальный (пассивка)")]
    [SerializeField] private float dodgeChance = 0.2f;      // Шанс увернуться от авто атаки

    private int current = -1;         // Какой скил сейчас применяется (-1 — никакой)
    private bool done;                // Клоны выпущены (в текущем действии)
    private float actionLength;       // Сколько длится текущее действие
    private int starsTotal;           // Сколько звёздочек в этом броске
    private int starsThrown;          // Сколько уже брошено
    private Fighter starTarget;       // В кого бросает
    private readonly List<Fighter> clones = new List<Fighter>(); // Живые клоны

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Пассивка ----------

    /// <summary>Нематериальный: 20% — вражеская авто атака проходит сквозь Фантома.</summary>
    public override bool TryBlock(HitInfo hit)
    {
        if (!hit.auto || !IsUnlocked(Immaterial) || Random.value >= dodgeChance) return false;
        Pop("УВОРОТ", new Color(0.75f, 0.65f, 1f));
        return true;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 1) Клоны — если их сейчас нет (перезарядка начнётся, когда погибнет последний)
        if (IsReady(Clones) && clones.Count == 0 && Vector2.Distance(target.Position, self.Position) <= self.detectionRadius)
            return StartClones(target);

        // 2) Звёздочки — цель не вплотную, примерно на одной линии (звёздочки доворачивают), энергии хотя бы на 2
        if (IsReady(Stars) && target.IsTargetable && absY <= 0.8f && absX >= starMinRange && absX <= starRange
            && self.Combat.HasEnergy(starEnergy * starsMin))
            return StartStars(target);

        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Клоны: перезарядка пока не идёт — начнётся, когда погибнет последний клон.</summary>
    private bool StartClones(Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        SpendSkillEnergy(Clones); // энергия — сразу при вызове (перезарядка — когда погибнет последний клон)
        current = Clones;
        done = false;
        actionLength = cloneActionTime;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Звёздочки: сколько хватает энергии (от 2 до 5), по одной через starInterval.</summary>
    private bool StartStars(Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Stars);
        current = Stars;
        starsTotal = Mathf.Clamp(Mathf.FloorToInt(self.Combat.Energy / starEnergy), starsMin, starsMax);
        starsThrown = 0;
        starTarget = target;
        actionLength = 0.1f + starInterval * starsTotal + 0.15f;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Выпустить клонов вокруг Фантома (скорость — как у Фантома).</summary>
    private void SpawnClones()
    {
        BattleManager bm = BattleManager.Instance;
        HeroStats s = self.Stats.Scaled(cloneStats);
        s.speed = self.Stats.speed;
        for (int i = 0; i < cloneCount; i++)
        {
            float angle = (i + 0.5f) / cloneCount * Mathf.PI * 2f;
            Vector3 p = self.transform.position + new Vector3(Mathf.Cos(angle) * cloneSpread, Mathf.Sin(angle) * cloneSpread * 0.4f, 0f);
            Fighter c = bm.SpawnClone(self, s, p);
            c.Movement.FaceTowards(self.Position.x + self.Movement.Facing);
            clones.Add(c);
        }
        Pop("КЛОНЫ", new Color(0.7f, 0.6f, 1f));
    }

    /// <summary>Бросить одну звёздочку (очередь: прямо, с доворотом к цели).</summary>
    private void ThrowStar()
    {
        // Очередь (как в Little Fighter 2 при зажатой кнопке): кончилась энергия или цель выбыла — очередь обрывается
        if (starTarget == null || !starTarget.IsAlive || !self.Combat.TrySpendEnergy(starEnergy)) { starsTotal = starsThrown; actionLength = Mathf.Min(actionLength, ActionTime + 0.15f); return; }
        if (starTarget != null && starTarget.IsAlive) self.Movement.FaceTowards(starTarget.Position.x);
        starsThrown++;
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.attack * starDamage,
            vsSpecial = false,
            speed = starSpeed,
            range = starRange,
            size = 0.45f,
            stagger = 1,
            knockback = 1.5f,
            steerTarget = starTarget, // летит прямо, но чуть доворачивает к цели
            color = new Color(1f, 0.95f, 0.6f),
            flicker = new Color(0.8f, 0.85f, 1f)
        });
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        UpdateClones();

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Фантому попали — действие прервано
        ActionTime += Time.deltaTime;

        if (!done && current == Clones && ActionTime >= cloneCastTime) { done = true; SpawnClones(); }
        if (current == Stars && starsThrown < starsTotal && ActionTime >= 0.1f + starsThrown * starInterval) ThrowStar();

        if (ActionTime >= actionLength)
        {
            current = -1;
            starTarget = null;
            EndAction();
        }
    }

    /// <summary>
    /// Клоны: Фантом выбыл — клоны исчезают; погиб последний клон — начинается перезарядка.
    /// </summary>
    private void UpdateClones()
    {
        if (clones.Count == 0) return;
        if (!self.IsAlive)
            foreach (Fighter c in clones)
                if (c != null && c.IsAlive) c.Vanish();
        clones.RemoveAll(c => c == null || !c.IsAlive);
        if (clones.Count == 0) StartCooldown(Clones, false);
    }
}
