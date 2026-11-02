using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сойка (героиня, класс Боец, ближний бой). Навыки:
///  1) "Ловкость" (ур. 1) — рывок к врагу: кого задела — получает 70% Силы атаки и отлетает.
///     Потом рывок к другому врагу неподалёку — и так, пока рядом есть враги и хватает энергии (каждый рывок — энергия).
///  2) "Дымовая завеса" (ур. 3) — дымовая шашка под себя: фиолетовый дым на 4 с. Враг, стоящий в дыму,
///     промахивается по Сойке с шансом 40%. Сойка в дыму бегает на 30% быстрее. На Сойку дым не действует.
///  3) "Перчатка с лезвиями" (ур. 5, пассивный) — 15% шанс, что авто атака вызовет кровотечение:
///     10% макс. HP цели за 5 с. Не складывается и не продлевается (над целью — капля крови).
/// Перезарядки — в файле героя (Data/Heroes/Hero_Soyka). Кадры — в Data/Characters/Sprites_Soyka.
/// </summary>
public class SoykaKit : HeroKit
{
    private const int Agility = 0, Smoke = 1, Blades = 2; // номера скилов

    [Header("Ловкость")]
    [SerializeField] private float dashEnergy = 15f;        // Цена одного рывка (энергия)
    [SerializeField] private float dashDamage = 0.7f;       // Урон (доля Силы атаки)
    [SerializeField] private float dashStartRange = 5f;     // Первый рывок — к врагу не дальше этого
    [SerializeField] private float dashMinRange = 1.2f;     // Ближе — бьёт рукой, а не рывком
    [SerializeField] private float dashChainRange = 4f;     // Следующий рывок — к врагу не дальше этого (небольшая дальность)
    [SerializeField] private float dashSpeed = 8f;          // Скорость рывка (видно, как она перескакивает от врага к врагу)
    [SerializeField] private float dashMaxTime = 0.8f;      // Дольше этого один рывок не длится
    [SerializeField] private float dashPause = 0.2f;        // Пауза между рывками
    [SerializeField] private float dashHitRadius = 0.8f;    // Кого задевает по пути (по горизонтали)
    [SerializeField] private float dashKnockback = 6f;      // Сила отбрасывания
    [SerializeField] private int dashMaxJumps = 8;          // Больше рывков за раз не бывает

    [Header("Дымовая завеса")]
    [SerializeField] private float smokeTime = 4f;          // Сколько держится дым
    [SerializeField] private Vector2 smokeRadius = new Vector2(2.2f, 1.1f); // Размер облака (по горизонтали и глубине)
    [SerializeField] private float smokeMissChance = 0.4f;  // Шанс промаха врага, стоящего в дыму
    [SerializeField] private float smokeSpeedBonus = 1.3f;  // Скорость Сойки в дыму
    [SerializeField] private float smokeEnemyRange = 2.5f;  // Бросает дым, когда враг ближе этого
    [SerializeField] private Color smokeColor = new Color(0.55f, 0.25f, 0.8f, 0.16f); // Фиолетовый (облака накладываются — бойцов в дыму видно)

    [Header("Перчатка с лезвиями (пассивка)")]
    [SerializeField] private float bleedChance = 0.15f;     // Шанс кровотечения от авто атаки
    [SerializeField] private float bleedHpPart = 0.1f;      // Сколько HP отнимает (доля макс. HP цели)
    [SerializeField] private float bleedTime = 5f;          // За сколько секунд

    private int current = -1;            // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;          // Сколько длится текущее действие (дым)
    private Fighter dashTarget;          // К кому сейчас рывок
    private Fighter lastStruck;          // Кого ударила последним (следующий рывок — к другому)
    private float dashClock;             // Сколько длится текущий рывок
    private float pauseTimer;            // Пауза перед следующим рывком (сек; 0 — летим)
    private int jumps;                   // Сколько рывков уже сделано
    private readonly List<Fighter> hitThisDash = new List<Fighter>(); // Кого задела этим рывком

    private Vector2 smokeCenter;         // Где дым
    private float smokeTimer;            // Сколько ещё держится дым
    private GameObject smokeCloud;       // Картинка дыма

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Пассивки и дым ----------

    /// <summary>В дыму Сойка быстрее на 30%.</summary>
    public override float SpeedMultiplier => smokeTimer > 0f && InSmoke(self.Position) ? smokeSpeedBonus : 1f;

    /// <summary>Враг, стоящий в дыму, промахивается с шансом 40%.</summary>
    public override bool TryBlock(HitInfo hit)
    {
        if (smokeTimer <= 0f || hit.attacker == null || !InSmoke(hit.attacker.Position)) return false;
        if (Random.value >= smokeMissChance) return false;
        Pop("ПРОМАХ", new Color(0.8f, 0.6f, 1f));
        return true;
    }

    /// <summary>Перчатка с лезвиями: 15% — кровотечение (если цель ещё не кровоточит).</summary>
    public override void OnAutoHit(Fighter target)
    {
        if (!IsUnlocked(Blades) || target == null || !target.IsAlive || Random.value >= bleedChance) return;
        if (target.ApplyBleed(target.Health.Max * bleedHpPart, bleedTime, self))
            Pop("КРОВОТЕЧЕНИЕ", new Color(1f, 0.35f, 0.35f));
    }

    /// <summary>Стоит ли точка в облаке дыма.</summary>
    private bool InSmoke(Vector2 p)
    {
        Vector2 d = p - smokeCenter;
        float ex = d.x / smokeRadius.x, ey = d.y / smokeRadius.y;
        return ex * ex + ey * ey <= 1f;
    }

    // ---------- Решение ----------

    /// <summary>Рывок обычный удар не прерывает.</summary>
    protected override bool ActionUnstoppable => base.ActionUnstoppable || current == Agility;

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        float dist = Vector2.Distance(target.Position, self.Position);
        float nearest = NearestEnemyDistance(self.Position);

        // 2) Дым — враг рядом, а Сойку бьют (или здоровья меньше 60%)
        if (IsReady(Smoke) && smokeTimer <= 0f && nearest <= smokeEnemyRange
            && (self.Health.TimeSinceHit < 1.5f || self.Health.Fraction < 0.6f))
            return StartSmoke();

        // 1) Ловкость — враг недалеко, но не вплотную, и хватает энергии на рывок
        if (IsReady(Agility) && !retreating && target.IsTargetable && dist >= dashMinRange && dist <= dashStartRange
            && Mathf.Abs(target.Position.y - self.Position.y) <= 1.5f && self.Combat.HasEnergy(dashEnergy))
            return StartAgility(target);

        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Ловкость: первый рывок.</summary>
    private bool StartAgility(Fighter target)
    {
        if (!BeginAction(KitPose.Roll)) return false;
        StartCooldown(Agility);
        current = Agility;
        jumps = 0;
        lastStruck = null;
        pauseTimer = 0f;
        Pop("ЛОВКОСТЬ", new Color(0.6f, 0.85f, 1f));
        if (!NextDash(target)) EndAgility();
        return true;
    }

    /// <summary>Начать рывок к target (тратит энергию). Вернёт false, если энергии нет.</summary>
    private bool NextDash(Fighter target)
    {
        if (target == null || jumps >= dashMaxJumps || !self.Combat.TrySpendEnergy(dashEnergy)) return false;
        jumps++;
        dashTarget = target;
        dashClock = 0f;
        hitThisDash.Clear();
        AnimCycle++;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    /// <summary>Ближайший живой враг рядом (не тот, кого ударила последним) — цель следующего рывка.</summary>
    private Fighter NextDashTarget()
    {
        Fighter best = null;
        float bestDist = dashChainRange;
        foreach (Fighter e in Enemies)
        {
            if (e == lastStruck || !e.IsTargetable) continue;
            float d = Vector2.Distance(e.Position, self.Position);
            if (d < bestDist && BattleManager.Instance.IsReachable(self, e.Position)) { bestDist = d; best = e; }
        }
        return best;
    }

    /// <summary>Рывок: летим к цели, задеваем всех по пути; долетели — следующий рывок или конец.</summary>
    private void UpdateDash(float dt)
    {
        // Пауза между рывками: Сойка приземлилась — оглядывается и прыгает к следующему
        if (pauseTimer > 0f)
        {
            pauseTimer -= dt;
            if (pauseTimer <= 0f && !NextDash(NextDashTarget())) EndAgility();
            return;
        }
        BattleManager bm = BattleManager.Instance;
        dashClock += dt;
        Vector2 to = dashTarget != null ? dashTarget.Position - self.Position : Vector2.zero;
        bool arrived = dashTarget == null || !dashTarget.IsAlive || to.magnitude <= self.meleeRange * 0.6f || dashClock >= dashMaxTime;
        if (!arrived)
        {
            Vector2 step = to.normalized * Mathf.Min(dashSpeed * dt, to.magnitude);
            self.transform.position = bm.ClampToArena(self, self.transform.position + (Vector3)step);
        }

        // Задели по пути — урон и отлёт
        int facing = self.Movement.Facing;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsAlive || hitThisDash.Contains(e)) continue;
            Vector2 d = e.Position - self.Position;
            if (Mathf.Abs(d.x) > dashHitRadius || Mathf.Abs(d.y) > self.depthTolerance) continue;
            hitThisDash.Add(e);
            lastStruck = e;
            HitEnemy(e, self.Stats.attack * dashDamage, false, new Vector2(facing * dashKnockback, Random.Range(-0.5f, 0.5f)), 2);
        }

        if (!arrived) return;
        if (dashTarget != null && !hitThisDash.Contains(dashTarget)) lastStruck = dashTarget; // не задели — всё равно к другому
        pauseTimer = dashPause; // следующий рывок — после короткой паузы
    }

    /// <summary>Закончить серию рывков.</summary>
    private void EndAgility()
    {
        current = -1;
        dashTarget = null;
        EndAction();
    }

    /// <summary>Дымовая завеса: облако под Сойкой.</summary>
    private bool StartSmoke()
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Smoke);
        current = Smoke;
        actionLength = 0.3f;
        smokeCenter = self.Position;
        smokeTimer = smokeTime;
        CreateSmokeCloud();
        Pop("ДЫМ", new Color(0.8f, 0.6f, 1f));
        return true;
    }

    /// <summary>Картинка дыма: несколько полупрозрачных фиолетовых кругов (временная графика).</summary>
    private void CreateSmokeCloud()
    {
        if (smokeCloud != null) Destroy(smokeCloud);
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle == null) return;
        BattleManager bm = BattleManager.Instance;
        smokeCloud = new GameObject("SmokeCloud");
        smokeCloud.transform.SetParent(bm.RuntimeRoot, false);
        smokeCloud.transform.position = new Vector3(smokeCenter.x, smokeCenter.y, 0f);
        float w = Mathf.Max(0.01f, circle.bounds.size.x);
        for (int i = 0; i < 7; i++)
        {
            var go = new GameObject("Puff");
            go.transform.SetParent(smokeCloud.transform, false);
            float a = i / 7f * Mathf.PI * 2f;
            float r = i == 0 ? 0f : 0.55f;
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * smokeRadius.x * r, Mathf.Sin(a) * smokeRadius.y * r + 0.7f, 0f);
            float size = (i == 0 ? 1.3f : 0.95f) * smokeRadius.x * 2f / w;
            go.transform.localScale = new Vector3(size, size * 0.75f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = smokeColor;
            sr.sortingOrder = bm.DepthSortingOrder(smokeCenter.y - smokeRadius.y) + 40; // поверх тех, кто в дыму
        }
    }

    /// <summary>Дым тает: прозрачность падает в последнюю секунду, потом облако исчезает.</summary>
    private void UpdateSmoke(float dt)
    {
        if (smokeTimer <= 0f) return;
        smokeTimer -= dt;
        if (smokeCloud == null) return;
        if (smokeTimer <= 0f) { Destroy(smokeCloud); return; }
        float fade = Mathf.Clamp01(smokeTimer); // последняя секунда — тает
        float wobble = 1f + Mathf.Sin(Time.time * 2f) * 0.04f;
        smokeCloud.transform.localScale = new Vector3(wobble, wobble, 1f);
        foreach (SpriteRenderer sr in smokeCloud.GetComponentsInChildren<SpriteRenderer>())
            sr.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, smokeColor.a * fade);
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        UpdateSmoke(dt);

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; dashTarget = null; return; } // сбили — действие прервано
        ActionTime += dt;

        if (current == Agility) { UpdateDash(dt); return; }
        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }

    private void OnDestroy()
    {
        if (smokeCloud != null) Destroy(smokeCloud);
    }
}
