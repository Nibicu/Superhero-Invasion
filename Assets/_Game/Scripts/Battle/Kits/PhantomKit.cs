using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фантом (злодей, класс Боец, ближний бой). Навыки:
///  ПАССИВКА "Пятый удар" — каждая 5-я авто атака наносит двойной урон.
///  1) "Клоны" (ур. 1) — выпускает 3 своих клона: полноценные бойцы (без скилов) с 30% его характеристик, тёмные.
///     Перезарядка (20 с) начинается, когда погибнет последний клон. Фантом выбыл — клоны исчезают.
///  2) "Энерго шар" (ур. 2) — шар летит во врага: урон от Атаки, сбивает с ног. Мерцает красно-розовым.
///  3) "Телепорт" (ур. 4) — когда HP меньше 20%: переносится в случайную точку неподалёку, подальше от врагов.
///  4) "Тень" (ур. 5, пассивный) — Защита и Спец. защита +25.
/// Перезарядки — в файле героя (Data/Villains/Villain_Phantom). Кадры — в Data/Characters/Sprites_Phantom.
/// </summary>
public class PhantomKit : HeroKit
{
    private const int Clones = 0, Orb = 1, Teleport = 2, Shadow = 3; // номера скилов

    [Header("Пятый удар (пассивка)")]
    [SerializeField] private int everyNthAuto = 5;          // Каждая какая авто атака усилена
    [SerializeField] private float nthMultiplier = 2f;      // Во сколько раз больше урон

    [Header("Клоны")]
    [SerializeField] private int cloneCount = 3;            // Сколько клонов
    [SerializeField] private float cloneStats = 0.3f;       // Характеристики клона — доля характеристик Фантома
    [SerializeField] private float cloneSpread = 1.6f;      // Насколько далеко от Фантома появляются клоны
    [SerializeField] private float cloneCastTime = 0.3f;    // Когда появляются клоны (после начала анимации)
    [SerializeField] private float cloneActionTime = 0.5f;  // Сколько длится применение

    [Header("Энерго шар")]
    [SerializeField] private float orbDamage = 1.2f;        // Урон (доля Атаки)
    [SerializeField] private float orbRange = 8f;           // Дальность
    [SerializeField] private float orbMinRange = 1.5f;      // Ближе — бьёт рукой
    [SerializeField] private float orbSpeed = 9f;           // Скорость шара
    [SerializeField] private float orbCastTime = 0.25f;     // Когда вылетает шар
    [SerializeField] private float orbActionTime = 0.45f;   // Сколько длится применение
    [SerializeField] private Color orbColor = new Color(1f, 0.15f, 0.3f);  // Красный
    [SerializeField] private Color orbFlicker = new Color(1f, 0.55f, 0.85f); // Розовый

    [Header("Телепорт")]
    [SerializeField] private float teleportHp = 0.2f;       // Работает, когда HP меньше этой доли
    [SerializeField] private float teleportRadius = 7f;     // Насколько далеко по арене может перенестись
    [SerializeField] private float teleportCastTime = 0.05f; // Когда переносится (почти сразу)
    [SerializeField] private float teleportActionTime = 0.3f; // Сколько длится применение (всё это время неуязвим)

    [Header("Тень (пассивка)")]
    [SerializeField] private int shadowDefense = 25;        // Сколько Защиты и Спец. защиты добавляет

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для шара
    private const int Knockdown = 99;         // "Очки сбивания" — сразу с ног

    private int current = -1;         // Какой скил сейчас применяется (-1 — никакой)
    private bool done;                // Клоны выпущены / шар вылетел / перенёсся (в текущем действии)
    private float actionLength;       // Сколько длится текущее действие
    private Fighter orbTarget;        // В кого летит шар
    private int autoCount;            // Счётчик авто атак (для Пятого удара)
    private readonly List<Fighter> clones = new List<Fighter>(); // Живые клоны

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Пассивки ----------

    /// <summary>Пятый удар: каждая 5-я авто атака — двойной урон.</summary>
    public override float ModifyAutoDamage(float power)
    {
        autoCount++;
        if (autoCount % Mathf.Max(1, everyNthAuto) != 0) return power;
        if (self.View != null) self.View.Pop("×2", new Color(0.85f, 0.6f, 1f));
        return power * nthMultiplier;
    }

    /// <summary>Тень: Защита и Спец. защита +25.</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (!IsUnlocked(Shadow)) return s;
        s.defense += shadowDefense;
        s.specialDefense += shadowDefense;
        return s;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 3) Телепорт — мало здоровья
        if (IsReady(Teleport) && self.Health.Fraction < teleportHp)
            return StartTeleport();

        // 1) Клоны — если их сейчас нет (перезарядка начнётся, когда погибнет последний)
        if (IsReady(Clones) && clones.Count == 0 && Vector2.Distance(target.Position, self.Position) <= self.detectionRadius)
            return StartClones(target);

        // 2) Энерго шар — цель на одной линии, не вплотную
        if (IsReady(Orb) && target.IsTargetable && absY <= AimTolerance && absX >= orbMinRange && absX <= orbRange)
            return StartOrb(target);

        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Начать действие скила skill.</summary>
    private bool Begin(int skill, float length)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        current = skill;
        done = false;
        actionLength = length;
        return true;
    }

    /// <summary>Клоны: перезарядка пока не идёт — начнётся, когда погибнет последний клон.</summary>
    private bool StartClones(Fighter target)
    {
        if (!Begin(Clones, cloneActionTime)) return false;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Энерго шар.</summary>
    private bool StartOrb(Fighter target)
    {
        if (!Begin(Orb, orbActionTime)) return false;
        StartCooldown(Orb);
        orbTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Телепорт.</summary>
    private bool StartTeleport()
    {
        if (!Begin(Teleport, teleportActionTime)) return false;
        StartCooldown(Teleport);
        self.SetInvulnerable(teleportActionTime + 0.2f); // растворяется — удары не проходят
        return true;
    }

    /// <summary>Выпустить клонов вокруг Фантома.</summary>
    private void SpawnClones()
    {
        BattleManager bm = BattleManager.Instance;
        HeroStats s = self.Stats.Scaled(cloneStats);
        for (int i = 0; i < cloneCount; i++)
        {
            float angle = (i + 0.5f) / cloneCount * Mathf.PI * 2f;
            Vector3 p = self.transform.position + new Vector3(Mathf.Cos(angle) * cloneSpread, Mathf.Sin(angle) * cloneSpread * 0.4f, 0f);
            Fighter c = bm.SpawnClone(self, s, p);
            c.Movement.FaceTowards(self.Position.x + self.Movement.Facing);
            clones.Add(c);
        }
        if (self.View != null) self.View.Pop("КЛОНЫ", new Color(0.7f, 0.6f, 1f));
    }

    /// <summary>Выпустить энерго шар.</summary>
    private void FireOrb()
    {
        if (orbTarget != null && orbTarget.IsAlive) self.Movement.FaceTowards(orbTarget.Position.x);
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.attack * orbDamage,
            vsSpecial = false,
            speed = orbSpeed,
            range = orbRange,
            size = 1.1f,
            stagger = Knockdown,
            knockback = 5f,
            color = orbColor,
            flicker = orbFlicker
        });
    }

    /// <summary>Перенестись в случайную точку неподалёку — ту, что дальше всех от врагов.</summary>
    private void DoTeleport()
    {
        BattleManager bm = BattleManager.Instance;
        Vector3 best = self.transform.position;
        float bestDist = NearestEnemyDistance(best);
        for (int i = 0; i < 12; i++)
        {
            Vector3 p = bm.RandomPointNear(self, teleportRadius);
            float dist = NearestEnemyDistance(p);
            if (dist > bestDist) { bestDist = dist; best = p; }
        }
        self.transform.position = best;
        if (self.View != null) self.View.Pop("ТЕЛЕПОРТ", new Color(0.7f, 0.6f, 1f));
    }

    /// <summary>Расстояние от точки p до ближайшего живого противника.</summary>
    private float NearestEnemyDistance(Vector3 p)
    {
        float best = float.MaxValue;
        foreach (Fighter f in BattleManager.Instance.GetOpponents(self.Faction))
            if (f.IsAlive) best = Mathf.Min(best, Vector2.Distance(f.Position, p));
        return best;
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
        if (!done && current == Orb && ActionTime >= orbCastTime) { done = true; FireOrb(); }
        if (!done && current == Teleport && ActionTime >= teleportCastTime) { done = true; DoTeleport(); }

        if (ActionTime >= actionLength)
        {
            current = -1;
            orbTarget = null;
            EndAction();
        }
    }

    /// <summary>
    /// Клоны: Фантом выбыл — клоны исчезают; погиб последний клон — начинается перезарядка (20 с).
    /// </summary>
    private void UpdateClones()
    {
        if (clones.Count == 0) return;
        if (!self.IsAlive)
            foreach (Fighter c in clones)
                if (c != null && c.IsAlive) c.Vanish();
        clones.RemoveAll(c => c == null || !c.IsAlive);
        if (clones.Count == 0) StartCooldown(Clones);
    }
}
