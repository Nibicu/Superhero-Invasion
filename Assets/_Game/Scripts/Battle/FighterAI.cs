using UnityEngine;

/// <summary>
/// ИИ бойца на арене (управляет всеми: нашими героями, врагами и охраной — бой мы только смотрим).
/// Раз в небольшую случайную задержку ("время реакции") принимает решение. Порядок важности:
///
/// 1) ОТСТУПЛЕНИЕ. HP упало ниже порога класса (Танк 10%, Боец 20%, Маг 25%, Стрелок 30%, Поддержка 20%) —
///    боец убегает от врагов (к своему краю территории или к аптечке) и лечится регеном.
///    Убежать можно, только если его сейчас не бьют; иначе дерётся, пока не появится шанс.
///    Когда HP выше 35% — снова идёт в бой.
/// 2) ЛЕЧЕНИЕ (Поддержка): союзник (или сама) ранен — лечит.
/// 3) ПРЕДМЕТЫ: врагов рядом нет (или коробка/бафф совсем близко) — идёт разбить коробку или подобрать бафф.
/// 4) БОЙ по стилю класса:
///    - ближний (Танк, Боец): подходит вплотную, в основном авто атаки, скил иногда;
///    - средний (Маг, Поддержка): держит среднюю дистанцию, больше полагается на скилы;
///    - дальний (Стрелок): держится далеко, в основном авто атаки (пули), скил иногда.
/// 5) Противников рядом нет — команды идут к точке сбора (следующая территория или флаг), охрана стоит.
/// </summary>
[DefaultExecutionOrder(-10)] // решает раньше, чем двигается FighterMovement
[RequireComponent(typeof(Fighter))]
public class FighterAI : MonoBehaviour
{
    [Tooltip("Время реакции: ИИ пересматривает решение раз в столько секунд (случайно в диапазоне)")]
    [SerializeField] private Vector2 reactionTime = new Vector2(0.15f, 0.35f);
    [Tooltip("На какой доле дальности удара боец встаёт от цели (ближний бой)")]
    [SerializeField, Range(0.3f, 1f)] private float preferredRangeFactor = 0.7f;
    [Tooltip("Насколько точно нужно встать на одну линию с целью, чтобы стрелять")]
    [SerializeField] private float aimTolerance = 0.3f;
    [Tooltip("Не толпиться: союзники ближе этого расстояния отталкиваются")]
    [SerializeField] private float separationRadius = 0.7f;

    [Header("Отступление")]
    [Tooltip("Боец считается 'под атакой' (не может убежать), если его били меньше столько секунд назад")]
    [SerializeField] private float underAttackTime = 1.2f;
    [Tooltip("...или если противник ближе этого расстояния")]
    [SerializeField] private float underAttackDistance = 1.3f;

    [Header("Предметы")]
    [Tooltip("Как далеко боец замечает коробки и баффы, когда врагов рядом нет")]
    [SerializeField] private float itemSearchRadius = 12f;
    [Tooltip("Коробку или бафф ближе этого расстояния боец берёт даже рядом с врагами")]
    [SerializeField] private float itemNearDistance = 2.5f;

    private Fighter self;
    private Fighter target;       // Цель-противник
    private Fighter healTarget;   // Кого лечим (Поддержка)
    private Vector2 aimPoint;     // Куда повернуться перед атакой (противник или коробка)
    private Vector2 destination;  // Куда идём
    private bool hasDestination;
    private bool wantAuto;        // Решили сделать авто атаку
    private bool wantSkill;       // Решили применить скил
    private bool retreating;      // Режим отступления
    private float thinkTimer;

    /// <summary>Отступает ли сейчас (для отладки и интерфейса).</summary>
    public bool IsRetreating => retreating;

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
        if (wantSkill || wantAuto)
        {
            self.Movement.FaceTowards(aimPoint.x);
            bool done = wantSkill ? self.Combat.TrySkill(healTarget) : self.Combat.TryAuto();
            if (done) { wantSkill = false; wantAuto = false; }
            return;
        }
        if (hasDestination)
        {
            Vector2 dir = destination - self.Position;
            if (dir.magnitude < 0.08f) hasDestination = false;
            else self.Movement.Move(dir.normalized + Separation(bm) * 0.6f);
        }
    }

    // ---------- Решение ----------

    /// <summary>Принять решение: отступить, лечить, взять предмет или драться.</summary>
    private void Think(BattleManager bm)
    {
        wantAuto = false;
        wantSkill = false;
        healTarget = null;
        ClassProfile profile = self.Profile;

        if (target == null || !target.IsTargetable || Vector2.Distance(target.Position, self.Position) > self.detectionRadius * 1.5f)
            target = FindTarget(bm);

        // 1) Отступление
        float hp = self.Health.Fraction;
        if (!retreating && hp <= profile.retreatAt) retreating = true;
        if (retreating && hp >= UnitClasses.ResumeAt) retreating = false;
        bool pressed = IsUnderAttack(bm);
        if (retreating && !pressed)
        {
            if (self.Class == UnitClass.Support && TryHeal(bm, true)) return; // Поддержка лечит себя на бегу
            BattleBuffItem medkit = FindBuff(bm, true, itemSearchRadius);
            destination = medkit != null ? medkit.Position : bm.GetRetreatPoint(self);
            hasDestination = (destination - self.Position).magnitude > 0.2f;
            return;
        }

        // 2) Лечение
        if (self.Class == UnitClass.Support && TryHeal(bm, false)) return;

        // 3) Предметы: врагов рядом нет — или предмет совсем рядом, а цель далеко
        // (охрана за предметами не ходит — она стережёт свою территорию)
        bool isTeam = self.Faction == BattleFaction.Heroes || self.Faction == BattleFaction.Rivals;
        float targetDist = target != null ? Vector2.Distance(target.Position, self.Position) : float.MaxValue;
        if (isTeam && (target == null || targetDist > self.Combat.AutoRange + 1.5f))
        {
            float radius = target == null ? itemSearchRadius : itemNearDistance;
            if (TryTakeItem(bm, radius)) return;
        }

        // 4) Нет противников — к точке сбора (охрана стоит на месте)
        if (target == null)
        {
            if (bm.TryGetRallyPoint(self, out Vector2 rally))
            {
                destination = rally;
                hasDestination = (destination - self.Position).magnitude > 0.3f;
            }
            else hasDestination = false;
            return;
        }

        // 5) Бой по стилю класса
        if (profile.style == CombatStyle.Melee) FightMelee(bm, profile);
        else FightRanged(profile);
    }

    /// <summary>Ближний бой (Танк, Боец): вплотную, в основном авто атаки, скил иногда.</summary>
    private void FightMelee(BattleManager bm, ClassProfile profile)
    {
        Vector2 d = target.Position - self.Position;
        aimPoint = target.Position;

        // Танк: удар по земле, когда вокруг двое и больше врагов (или иногда по одному)
        if (self.Class == UnitClass.Tank && self.Combat.CanSkill)
        {
            int around = 0;
            foreach (Fighter f in bm.GetOpponents(self.Faction))
                if (f.IsTargetable && self.Combat.InSlamZone(f.Position)) around++;
            if (around >= 2 || (around == 1 && Random.value < profile.skillChance * 0.5f))
            {
                wantSkill = true;
                hasDestination = false;
                return;
            }
        }

        // В зоне удара — бьём (Боец иногда мощным ударом)
        if (Mathf.Abs(d.x) <= self.meleeRange && Mathf.Abs(d.y) <= self.depthTolerance * 0.8f)
        {
            hasDestination = false;
            if (self.Class == UnitClass.Fighter && self.Combat.CanSkill && Random.value < profile.skillChance) wantSkill = true;
            else wantAuto = true;
            return;
        }

        // Подходим вплотную, выравниваясь по глубине
        float side = d.x >= 0 ? -1f : 1f;
        destination = new Vector2(target.Position.x + side * self.meleeRange * preferredRangeFactor, target.Position.y);
        hasDestination = true;
    }

    /// <summary>
    /// Средний и дальний бой (Маг, Поддержка, Стрелок): держит свою дистанцию,
    /// встаёт на одну линию с целью и стреляет — авто атакой или скилом.
    /// </summary>
    private void FightRanged(ClassProfile profile)
    {
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);
        aimPoint = target.Position;
        bool aligned = absY <= aimTolerance;

        if (aligned)
        {
            // Скил: Маг — часто, Стрелок — иногда (Поддержка лечит в другом месте)
            if (self.Class != UnitClass.Support && self.Combat.CanSkill && absX <= self.Combat.SkillRange
                && Random.value < profile.skillChance)
            {
                wantSkill = true;
                hasDestination = false;
                return;
            }
            if (absX <= self.Combat.AutoRange && self.Combat.CanAuto)
            {
                wantAuto = true;
                hasDestination = false;
                return;
            }
        }

        // Встаём на свою дистанцию и на одну глубину с целью (слишком близко — отходим)
        float side = d.x >= 0 ? -1f : 1f;
        destination = new Vector2(target.Position.x + side * profile.preferredRange, target.Position.y);
        hasDestination = (destination - self.Position).magnitude > 0.15f;
    }

    // ---------- Лечение ----------

    /// <summary>
    /// Поддержка: найти раненого союзника (меньше 75% HP, или себя при отступлении) в радиусе лечения и вылечить.
    /// Вернёт true, если решили лечить (или идём к раненому).
    /// </summary>
    private bool TryHeal(BattleManager bm, bool selfOnly)
    {
        if (!self.Combat.CanSkill) return false;
        Fighter best = null;
        float bestHp = selfOnly ? 0.9f : 0.75f;
        foreach (Fighter ally in bm.GetAllies(self.Faction))
        {
            if (!ally.IsAlive || (selfOnly && ally != self)) continue;
            if (ally.Health.Fraction >= bestHp) continue;
            if (Vector2.Distance(ally.Position, self.Position) > self.Combat.SkillRange * 1.5f) continue;
            bestHp = ally.Health.Fraction;
            best = ally;
        }
        if (best == null) return false;

        if (Vector2.Distance(best.Position, self.Position) <= self.Combat.SkillRange)
        {
            healTarget = best;
            aimPoint = best.Position;
            wantSkill = true;
            hasDestination = false;
        }
        else
        {
            destination = best.Position; // подходим ближе к раненому
            hasDestination = true;
        }
        return true;
    }

    // ---------- Предметы ----------

    /// <summary>Пойти к баффу или коробке в радиусе radius (если до них можно дойти). Вернёт true, если нашли.</summary>
    private bool TryTakeItem(BattleManager bm, float radius)
    {
        // Бафф: лечение — только если ранен, усиление — всегда
        BattleBuffItem buff = FindBuff(bm, false, radius);
        if (buff != null)
        {
            destination = buff.Position;
            hasDestination = true;
            return true;
        }

        BattleBox box = FindBox(bm, radius);
        if (box == null) return false;
        Vector2 d = box.Position - self.Position;
        aimPoint = box.Position;
        bool aligned = Mathf.Abs(d.y) <= (self.Combat.IsMeleeAuto ? self.depthTolerance * 0.8f : aimTolerance);
        if (aligned && Mathf.Abs(d.x) <= self.Combat.AutoRange && Mathf.Abs(d.x) > 0.3f)
        {
            if (self.Combat.CanAuto) wantAuto = true;
            hasDestination = false;
            return true;
        }
        // Встаём сбоку от коробки на дистанцию авто атаки (ближние — вплотную)
        float side = d.x >= 0 ? -1f : 1f;
        float range = self.Combat.IsMeleeAuto ? self.meleeRange * preferredRangeFactor : Mathf.Min(self.Combat.AutoRange * 0.6f, 3f);
        destination = new Vector2(box.Position.x + side * range, box.Position.y);
        hasDestination = true;
        return true;
    }

    /// <summary>Ближайший бафф в радиусе. healOnly — только лечение (для отступающих).</summary>
    private BattleBuffItem FindBuff(BattleManager bm, bool healOnly, float radius)
    {
        BattleBuffItem best = null;
        float bestDist = radius;
        foreach (BattleBuffItem b in bm.Buffs)
        {
            if (b == null || b.Taken) continue;
            if (healOnly && !b.IsHeal) continue;
            if (b.IsHeal && self.Health.Fraction > 0.9f) continue; // здоровому аптечка не нужна
            float dist = Vector2.Distance(b.Position, self.Position);
            if (dist < bestDist && bm.IsReachable(self, b.Position)) { bestDist = dist; best = b; }
        }
        return best;
    }

    /// <summary>Ближайшая целая коробка в радиусе (до которой можно дойти).</summary>
    private BattleBox FindBox(BattleManager bm, float radius)
    {
        BattleBox best = null;
        float bestDist = radius;
        foreach (BattleBox b in bm.Boxes)
        {
            if (b == null || b.IsBroken) continue;
            float dist = Vector2.Distance(b.Position, self.Position);
            if (dist < bestDist && bm.IsReachable(self, b.Position)) { bestDist = dist; best = b; }
        }
        return best;
    }

    // ---------- Помощники ----------

    /// <summary>Бойца сейчас атакуют: недавно ударили или противник вплотную.</summary>
    private bool IsUnderAttack(BattleManager bm)
    {
        if (self.Health.TimeSinceHit < underAttackTime) return true;
        foreach (Fighter f in bm.GetOpponents(self.Faction))
            if (f.IsAlive && f.CanAct && Vector2.Distance(f.Position, self.Position) < underAttackDistance) return true;
        return false;
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
