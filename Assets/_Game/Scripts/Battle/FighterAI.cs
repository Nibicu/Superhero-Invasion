using UnityEngine;

/// <summary>
/// Простой ИИ бойца на арене (управляет и нашими героями, и охраной — бой мы только смотрим).
/// Раз в небольшую случайную задержку ("время реакции"):
///  1) ищет ближайшего живого противника в радиусе обнаружения;
///  2) подходит к нему, выравниваясь по глубине (Y) и вставая на дистанцию удара;
///  3) в радиусе удара — бьёт; на одной линии издалека — стреляет снарядом
///     ("маги" — у кого спец. атака выше обычной — держат дистанцию и стреляют чаще);
///  4) цель погибла или сбита с ног — ищет другую;
///  5) противников рядом нет — команды идут к точке сбора (следующая территория или флаг), охрана стоит.
/// Не знает ничего лишнего и не атакует через всю арену.
/// </summary>
[DefaultExecutionOrder(-10)] // решает раньше, чем двигается FighterMovement
[RequireComponent(typeof(Fighter))]
public class FighterAI : MonoBehaviour
{
    [Tooltip("Время реакции: ИИ пересматривает решение раз в столько секунд (случайно в диапазоне)")]
    [SerializeField] private Vector2 reactionTime = new Vector2(0.15f, 0.35f);
    [Tooltip("На какой доле дальности удара боец встаёт от цели")]
    [SerializeField, Range(0.3f, 1f)] private float preferredRangeFactor = 0.7f;
    [Tooltip("Минимальная дистанция для выстрела снарядом")]
    [SerializeField] private float projectileMinRange = 2.5f;
    [Tooltip("Максимальная дистанция для выстрела снарядом")]
    [SerializeField] private float projectileMaxRange = 7.5f;
    [Tooltip("Шанс выстрелить, когда выстрел возможен (за одно решение)")]
    [SerializeField, Range(0f, 1f)] private float projectileChance = 0.5f;
    [Tooltip("На каком расстоянии держится 'маг' (у кого спец. атака больше обычной), пока есть энергия на выстрел")]
    [SerializeField] private float casterRange = 4.5f;
    [Tooltip("Не толпиться: союзники ближе этого расстояния отталкиваются")]
    [SerializeField] private float separationRadius = 0.7f;

    private Fighter self;
    private Fighter target;       // Текущая цель
    private Vector2 destination;  // Куда идём
    private bool hasDestination;
    private bool wantAttack;      // Решили ударить
    private float thinkTimer;

    private void Awake() => self = GetComponent<Fighter>();

    private void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || !bm.IsRunning || !self.IsAlive || !self.CanAct) return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = Random.Range(reactionTime.x, reactionTime.y);
            Think(bm);
        }

        // Выполняем решение
        if (wantAttack && target != null)
        {
            self.Movement.FaceTowards(target.Position.x);
            if (self.Combat.TryMelee()) wantAttack = false;
            return;
        }
        if (hasDestination)
        {
            Vector2 dir = destination - self.Position;
            if (dir.magnitude < 0.08f) hasDestination = false;
            else self.Movement.Move(dir.normalized + Separation(bm) * 0.6f);
        }
    }

    /// <summary>Принять решение: цель, куда идти, бить или стрелять.</summary>
    private void Think(BattleManager bm)
    {
        wantAttack = false;
        if (target == null || !target.IsTargetable || Vector2.Distance(target.Position, self.Position) > self.detectionRadius * 1.5f)
            target = FindTarget(bm);

        if (target == null)
        {
            // Противников рядом нет: команды идут к точке сбора (следующая территория или флаг), охрана ждёт
            if (bm.TryGetRallyPoint(self, out Vector2 rally))
            {
                destination = rally;
                hasDestination = (destination - self.Position).magnitude > 0.3f;
            }
            else hasDestination = false;
            return;
        }

        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // В зоне удара — бьём
        if (absX <= self.meleeRange && absY <= self.depthTolerance * 0.8f)
        {
            hasDestination = false;
            wantAttack = true;
            return;
        }

        // "Маг" — спец. атака сильнее обычной: предпочитает стрелять издалека
        bool caster = self.Stats.specialAttack > self.Stats.attack;
        bool hasEnergy = self.Combat.Energy >= self.specialEnergyCost;

        // На одной линии и на подходящей дистанции — стреляем (маг — всегда, боец — иногда)
        float minRange = caster ? self.meleeRange * 0.7f : projectileMinRange;
        if (absY <= 0.3f && absX >= minRange && absX <= projectileMaxRange
            && self.Combat.CanSpecial && (caster || Random.value < projectileChance))
        {
            self.Movement.FaceTowards(target.Position.x);
            if (self.Combat.TrySpecial()) { hasDestination = false; return; }
        }

        // Куда встать: маг с энергией — на дистанцию выстрела, остальные — на дистанцию удара.
        // Глубина (Y) — как у цели, чтобы попадать.
        float side = d.x >= 0 ? -1f : 1f; // с какой стороны от цели вставать
        float range = caster && hasEnergy ? casterRange : self.meleeRange * preferredRangeFactor;
        destination = new Vector2(target.Position.x + side * range, target.Position.y);
        hasDestination = true;
    }

    /// <summary>Ближайший противник, которого можно атаковать, в радиусе обнаружения.</summary>
    private Fighter FindTarget(BattleManager bm)
    {
        Fighter best = null;
        float bestDist = self.detectionRadius;
        foreach (Fighter f in bm.GetOpponents(self.Faction))
        {
            if (!f.IsTargetable) continue;
            float dist = Vector2.Distance(f.Position, self.Position);
            if (dist < bestDist) { bestDist = dist; best = f; }
        }
        return best;
    }

    /// <summary>Небольшой толчок от союзников, чтобы не стоять в одной точке.</summary>
    private Vector2 Separation(BattleManager bm)
    {
        Vector2 push = Vector2.zero;
        foreach (Fighter ally in bm.GetAllies(self.Faction))
        {
            if (ally == self || !ally.IsAlive) continue;
            Vector2 d = self.Position - ally.Position;
            float dist = d.magnitude;
            if (dist < separationRadius && dist > 0.001f) push += d / dist * (1f - dist / separationRadius);
        }
        return push;
    }
}
