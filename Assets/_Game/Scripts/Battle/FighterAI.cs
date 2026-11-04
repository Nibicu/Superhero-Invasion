using UnityEngine;

/// <summary>
/// ИИ бойца на арене (управляет всеми: нашими героями, врагами и охраной — бой мы только смотрим).
/// Раз в небольшую случайную задержку ("время реакции") принимает решение. Порядок важности:
///
/// 0) ШАРМ (Суккуб): зачарованный бьёт авто атаками ближайшего своего союзника, скилы не применяет.
/// 1) ОТСТУПЛЕНИЕ. HP упало ниже порога класса (Танк 10%, Боец 20%, Маг 25%, Стрелок 30%, Поддержка 20%) —
///    боец отходит НЕДАЛЕКО (держится в kiteDistance от врага) и всё время двигается;
///    увидел зелье — бежит к нему. Прижали вплотную — отбивается.
///    Когда HP выше 35% — снова идёт в бой. Охрана объектов (нейтральные юниты) не отступает никогда.
/// 2) ПРЕДМЕТЫ: врагов рядом нет (или коробка/бафф совсем близко) — идёт разбить коробку или подобрать бафф.
/// 3) БОЙ по стилю класса:
///    - ближний (Танк, Боец, Маг, Поддержка): подходит вплотную и бьёт авто атаками;
///    - дальний (Стрелок): держится далеко и стреляет; враг подошёл вплотную — бьёт рукой, пока он не отлетит.
/// 4) Противников рядом нет — идёт к ближайшему противнику на арене или к точке сбора (флаг), охрана стоит.
/// Скилы есть только у героев с личными навыками (HeroKit): каждое решение сначала спрашивает их —
/// навык применён — ждём его конца; навык хочет держать дистанцию (PreferredRange) — встаём на неё.
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

    [Header("Захват")]
    [Tooltip("Шанс схватить беззащитного (шатается или оглушён) врага вплотную — за одно решение")]
    [SerializeField, Range(0f, 1f)] private float grabChance = 0.5f;

    [Header("Прыжок")]
    [Tooltip("Подброшенный враг ближе этого (по горизонтали) — подпрыгнуть и добить в воздухе")]
    [SerializeField] private float jumpJuggleRange = 3.5f;
    [Tooltip("Иногда прыгает с ударом на врага на этом расстоянии (от и до)")]
    [SerializeField] private Vector2 jumpAttackRange = new Vector2(2f, 3f);
    [Tooltip("Шанс прыгнуть с ударом (за одно решение), когда враг на таком расстоянии")]
    [SerializeField, Range(0f, 1f)] private float jumpChance = 0.2f;

    [Header("Отступление")]
    [Tooltip("Боец считается 'под атакой' (не может убежать), если его били меньше столько секунд назад")]
    [SerializeField] private float underAttackTime = 1.2f;
    [Tooltip("...или если противник ближе этого расстояния")]
    [SerializeField] private float underAttackDistance = 1.3f;
    [Tooltip("На каком расстоянии от ближайшего врага держится отступающий (недалеко, чтобы не потерять бой)")]
    [SerializeField] private float kiteDistance = 5f;

    [Header("Бег")]
    [Tooltip("Бежать можно, только если ближайший враг дальше этого (рядом с врагом — шагом). Отступающий бежит всегда")]
    [SerializeField] private float runSafeDistance = 5f;
    [Tooltip("Бежать, только если до точки, куда идём, дальше этого")]
    [SerializeField] private float runMinDistance = 2f;

    [Header("Предметы")]
    [Tooltip("Как далеко боец замечает коробки и баффы, когда врагов рядом нет")]
    [SerializeField] private float itemSearchRadius = 12f;
    [Tooltip("Коробку или бафф ближе этого расстояния боец берёт даже рядом с врагами")]
    [SerializeField] private float itemNearDistance = 2.5f;

    private Fighter self;
    private Fighter target;       // Цель-противник (у зачарованного — союзник)
    private Vector2 aimPoint;     // Куда повернуться перед атакой (противник или коробка)
    private Vector2 destination;  // Куда идём
    private bool hasDestination;
    private bool wantAuto;        // Решили сделать авто атаку
    private bool wantGrab;        // Решили схватить цель
    private bool wantDash;        // Решили ударить с разбега
    private bool wantJump;        // Решили прыгнуть (с ударом в прыжке)
    private bool autoClose;       // Авто атака вплотную (Стрелок бьёт рукой)
    private bool retreating;      // Режим отступления
    private bool wantRun;         // Решили бежать к точке (а не идти)
    private bool dashApproach;    // Подбегаем к цели для удара с разбега (бежим даже на короткой дистанции)
    private float thinkTimer;

    /// <summary>Отступает ли сейчас (для отладки и интерфейса).</summary>
    public bool IsRetreating => retreating;

    private float wavePhase; // Свой ритм покачивания при отступлении (чтобы не двигались одинаково)

    private void Awake()
    {
        self = GetComponent<Fighter>();
        wavePhase = Random.Range(0f, 10f);
    }

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
        if (wantGrab)
        {
            wantGrab = false;
            if (target != null && self.Combat.TryGrab(target)) return;
        }
        if (wantJump)
        {
            wantJump = false;
            if (target != null && self.Combat.TryJump(target)) return;
        }
        if (wantDash && target != null && self.Combat.CanDashAttack(target))
        {
            wantDash = false;
            if (self.Combat.TryDashAttack(target)) return;
        }
        if (wantAuto)
        {
            self.Movement.FaceTowards(aimPoint.x);
            if (self.Combat.TryAuto(autoClose)) wantAuto = false;
            return;
        }
        if (hasDestination)
        {
            Vector2 dir = destination - self.Position;
            if (dir.magnitude < 0.08f) hasDestination = false;
            else self.Movement.Move(dir.normalized + Separation(bm) * 0.6f, wantRun && (dir.magnitude > runMinDistance || dashApproach));
        }
    }

    // ---------- Решение ----------

    /// <summary>Принять решение: отступить, взять предмет или драться.</summary>
    private void Think(BattleManager bm)
    {
        wantAuto = false;
        wantGrab = false;
        wantDash = false;
        wantJump = false;
        autoClose = false;
        ClassProfile profile = self.Profile;
        try { ThinkInner(bm, profile); }
        finally
        {
            wantRun = ShouldRun(bm);
            dashApproach = wantRun && !retreating && target != null && target.Faction != self.Faction
                           && self.Combat.DashReady && Vector2.Distance(target.Position, self.Position) > self.meleeRange;
        }
    }

    /// <summary>
    /// Бежать или идти: рядом враг — шагом; отступает с низким HP — бегом (оторваться и восстановиться);
    /// иначе (врагов рядом нет, или ближний боец подбегает к далёкому врагу) — бегом, пока есть шкала бега.
    /// </summary>
    private bool ShouldRun(BattleManager bm)
    {
        if (!hasDestination || !self.Movement.CanRun) return false;
        if (retreating) return true;
        // Удар с разбега готов — к цели подбегаем бегом (чтобы ударить с разбега)
        if (target != null && target.Faction != self.Faction && self.Combat.DashReady && !self.IsCharmed) return true;
        Fighter near = NearestOpponent(bm, runSafeDistance);
        return near == null;
    }

    /// <summary>Само решение (без выбора "бежать или идти").</summary>
    private void ThinkInner(BattleManager bm, ClassProfile profile)
    {
        // 0) Шарм: бьём ближайшего своего союзника авто атаками (скилы и отступление — нет)
        if (self.IsCharmed)
        {
            target = NearestCharmTarget(bm);
            if (target == null) { hasDestination = false; return; }
            if (profile.style == CombatStyle.Melee) FightMelee();
            else FightRanged(profile);
            return;
        }
        if (target != null && target.Faction == self.Faction) target = null; // шарм прошёл — союзник больше не цель

        // Цель: старую бросаем, если она недоступна (за барьером), лежит или ушла далеко;
        // переключаемся на противника, который заметно ближе (кто рядом бьёт — тот и цель)
        if (target != null && (!target.IsTargetable || !bm.IsReachable(self, target.Position)
                               || Vector2.Distance(target.Position, self.Position) > self.detectionRadius * 1.5f))
            target = null;
        Fighter nearest = FindTarget(bm);
        if (target == null || (nearest != null && nearest != target
            && Vector2.Distance(nearest.Position, self.Position) + 2f < Vector2.Distance(target.Position, self.Position)))
            target = nearest;

        // 1) Отступление: недалеко, всё время в движении, со скилами; увидел зелье — бежит к нему
        float hp = self.Health.Fraction;
        if (!retreating && hp <= profile.retreatAt && !self.IsGuard) retreating = true; // охрана объектов не отступает
        if (retreating && hp >= UnitClasses.ResumeAt) retreating = false;

        // Личные навыки героя (если есть): применили — ждём, пока навык закончится
        if (self.Kit != null && target != null && self.Kit.Think(target, retreating))
        {
            hasDestination = false;
            return;
        }
        if (retreating)
        {
            HealSpot spot = FindHealSpot(bm, itemSearchRadius); // цветок Дерева, пыль Мотылька
            if (spot != null)
            {
                destination = spot.Position;
                hasDestination = true;
                return;
            }
            BattleBuffItem potion = FindBuff(bm, true, itemSearchRadius);
            if (potion != null)
            {
                destination = potion.Position;
                hasDestination = true;
                return;
            }
            // Прижали вплотную и бьют — отбивается, пока не появится шанс отойти
            Fighter threat = NearestOpponent(bm, self.detectionRadius * 1.5f);
            if (threat != null && IsUnderAttack(bm) && Vector2.Distance(threat.Position, self.Position) <= self.meleeRange * 1.3f)
            {
                target = threat;
                if (profile.style == CombatStyle.Melee) FightMelee();
                else FightRanged(profile);
                return;
            }
            destination = KitePoint(bm, threat);
            hasDestination = true;
            return;
        }

        // 2) Предметы: врагов рядом нет — или предмет совсем рядом, а цель далеко
        // (охрана за предметами не ходит — она стережёт свою территорию)
        bool isTeam = self.Faction == BattleFaction.Heroes || self.Faction == BattleFaction.Rivals;
        float targetDist = target != null ? Vector2.Distance(target.Position, self.Position) : float.MaxValue;
        if (isTeam && (target == null || targetDist > self.Combat.AutoRange + 1.5f))
        {
            float radius = target == null ? itemSearchRadius : itemNearDistance;
            if (TryTakeItem(bm, radius)) return;
        }

        // 3) Рядом противников нет. Если они есть дальше на арене — идём к ближайшему, чтобы бой не замирал
        //    (команды разошлись дальше радиуса обнаружения). Команда у флага держит флаг, охрана стоит на месте.
        //    Иначе — к точке сбора.
        if (target == null)
        {
            if (isTeam && !bm.IsHoldingFlag(self))
            {
                Fighter far = NearestReachableOpponent(bm);
                if (far != null)
                {
                    destination = far.Position;
                    hasDestination = true;
                    return;
                }
            }
            if (bm.TryGetRallyPoint(self, out Vector2 rally))
            {
                destination = rally;
                hasDestination = (destination - self.Position).magnitude > 0.3f;
            }
            else hasDestination = false;
            return;
        }

        // 4) Личный навык хочет держать дистанцию (например, Солдат с готовым Залпом) — встаём на неё,
        //    но если враг уже вплотную — дерёмся как обычно
        if (self.Kit != null)
        {
            float keep = self.Kit.PreferredRange(target);
            Vector2 dd = target.Position - self.Position;
            bool inMelee = Mathf.Abs(dd.x) <= self.meleeRange * 1.1f && Mathf.Abs(dd.y) <= self.depthTolerance * 0.8f;
            if (keep > 0f && !inMelee)
            {
                aimPoint = target.Position;
                float side = dd.x >= 0 ? -1f : 1f;
                destination = new Vector2(target.Position.x + side * keep, target.Position.y);
                hasDestination = (destination - self.Position).magnitude > 0.15f;
                if (!hasDestination) self.Movement.FaceTowards(target.Position.x);
                return;
            }
        }

        // 5) Прыжок: подброшенного врага — добить в воздухе; иногда — прыжок с ударом на подходе
        if (WantJump())
        {
            wantJump = true;
            hasDestination = false;
            return;
        }

        // 6) Бой по стилю класса
        if (profile.style == CombatStyle.Melee) FightMelee();
        else FightRanged(profile);
    }

    /// <summary>Ближний бой (Танк, Боец, Маг, Поддержка): подходит вплотную и бьёт авто атаками (с разбега — сильный удар).</summary>
    private void FightMelee()
    {
        Vector2 d = target.Position - self.Position;
        aimPoint = target.Position;

        // Бежим к цели и она рядом — удар с разбега (решение держится, пока ИИ думает)
        if (self.Combat.CanDashAttack(target))
        {
            wantDash = true;
            hasDestination = true;
        }

        // В зоне удара — бьём (беззащитного — иногда хватаем)
        if (Mathf.Abs(d.x) <= self.meleeRange && Mathf.Abs(d.y) <= self.depthTolerance * 0.8f)
        {
            hasDestination = false;
            if (WantGrab()) wantGrab = true;
            else wantAuto = true;
            return;
        }

        // Подходим вплотную, выравниваясь по глубине
        float side = d.x >= 0 ? -1f : 1f;
        destination = new Vector2(target.Position.x + side * self.meleeRange * preferredRangeFactor, target.Position.y);
        hasDestination = true;
    }

    /// <summary>
    /// Дальний бой (Стрелок): держит свою дистанцию, встаёт на одну линию с целью и стреляет авто атакой.
    /// Враг вплотную — бьёт рукой.
    /// </summary>
    private void FightRanged(ClassProfile profile)
    {
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);
        aimPoint = target.Position;

        // Враг вплотную — бьём рукой (пока не отлетит); беззащитного — иногда хватаем
        if (absX <= self.meleeRange * 1.1f && absY <= self.depthTolerance * 0.8f)
        {
            hasDestination = false;
            if (WantGrab()) { wantGrab = true; return; }
            wantAuto = true;
            autoClose = true;
            return;
        }

        if (absY <= aimTolerance && absX <= self.Combat.AutoRange && self.Combat.CanAuto)
        {
            wantAuto = true;
            hasDestination = false;
            return;
        }

        // Встаём на свою дистанцию и на одну глубину с целью (слишком близко — отходим)
        float side = d.x >= 0 ? -1f : 1f;
        float keep = profile.preferredRange > 0f ? profile.preferredRange : self.meleeRange * preferredRangeFactor;
        destination = new Vector2(target.Position.x + side * keep, target.Position.y);
        hasDestination = (destination - self.Position).magnitude > 0.15f;
    }

    // ---------- Отступление ----------

    /// <summary>
    /// Куда отходить раненому: недалеко (kiteDistance от ближайшего врага, чтобы его не теряли из виду)
    /// и всё время двигаясь — покачиваясь по глубине. Прижали к краю — уходит вбок.
    /// </summary>
    private Vector2 KitePoint(BattleManager bm, Fighter threat)
    {
        if (threat == null) return bm.GetRetreatPoint(self);
        float away = Mathf.Sign(self.Position.x - threat.Position.x);
        if (away == 0f) away = 1f;
        float wave = Mathf.Sin(Time.time * 1.7f + wavePhase); // у каждого свой ритм
        Vector3 want = new Vector3(threat.Position.x + away * kiteDistance, self.Position.y + wave * 1.2f, 0f);
        Vector3 p = bm.ClampToArena(self, want);
        if (Mathf.Abs(p.x - want.x) > 1f) // упёрлись в край или барьер — уходим по глубине
            p = bm.ClampToArena(self, new Vector3(self.Position.x, self.Position.y + (wave > 0f ? 1.5f : -1.5f), 0f));
        return p;
    }

    /// <summary>Ближайший живой противник в радиусе (даже недоступный — от него и отходим).</summary>
    private Fighter NearestOpponent(BattleManager bm, float radius)
    {
        Fighter best = null;
        float bestDist = radius;
        foreach (Fighter f in bm.GetOpponents(self.Faction))
        {
            if (!f.IsAlive) continue;
            float dist = Vector2.Distance(f.Position, self.Position);
            if (dist < bestDist) { bestDist = dist; best = f; }
        }
        return best;
    }

    // ---------- Предметы ----------

    /// <summary>Пойти к лечебному месту, баффу или коробке в радиусе radius (если до них можно дойти). Вернёт true, если нашли.</summary>
    private bool TryTakeItem(BattleManager bm, float radius)
    {
        // Лечебное место своей стороны (цветок, пыль) — если ранен
        if (self.Health.Fraction < 0.85f)
        {
            HealSpot spot = FindHealSpot(bm, radius);
            if (spot != null)
            {
                destination = spot.Position;
                hasDestination = true;
                return true;
            }
        }

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

    /// <summary>Ближайшее лечебное место своей стороны в радиусе, где можно вылечиться (null — нет).</summary>
    private HealSpot FindHealSpot(BattleManager bm, float radius)
    {
        HealSpot best = null;
        float bestDist = radius;
        foreach (HealSpot s in HealSpot.All)
        {
            if (s == null || !s.CanHeal(self)) continue;
            float dist = Vector2.Distance(s.Position, self.Position);
            if (dist < bestDist && bm.IsReachable(self, s.Position)) { bestDist = dist; best = s; }
        }
        return best;
    }

    /// <summary>Ближайший бафф в радиусе. healOnly — только лечение (для отступающих).</summary>
    private BattleBuffItem FindBuff(BattleManager bm, bool healOnly, float radius)
    {
        BattleBuffItem best = null;
        float bestDist = radius;
        foreach (BattleBuffItem b in bm.Buffs)
        {
            if (b == null || !b.Available) continue;
            if (healOnly && !b.IsHeal) continue;
            if (b.IsHeal && self.Health.Fraction > 0.9f) continue; // здоровому аптечка не нужна
            if (b.IsEnergy && (self.Kit == null || self.Combat.Energy > self.Combat.MaxEnergy * 0.8f)) continue; // энергия нужна только тем, у кого есть скилы
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
            if (b == null || !b.CanBeHit) continue;
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

    /// <summary>
    /// Ближайший противник в радиусе обнаружения, которого можно атаковать и до которого можно дойти
    /// (враги за закрытым барьером не считаются — иначе боец стоял бы у барьера и ничего не делал).
    /// </summary>
    private Fighter FindTarget(BattleManager bm)
    {
        Fighter best = null;
        float bestDist = self.detectionRadius;
        foreach (Fighter f in bm.GetOpponents(self.Faction))
        {
            if (!f.IsTargetable || !bm.IsReachable(self, f.Position)) continue;
            float dist = Vector2.Distance(f.Position, self.Position);
            if (dist < bestDist) { bestDist = dist; best = f; }
        }
        return best;
    }

    /// <summary>
    /// Прыгать ли сейчас: цель подброшена и недалеко (добить в воздухе) или иногда —
    /// враг на расстоянии прыжка с ударом. Не прыгает при отступлении и под шармом.
    /// </summary>
    private bool WantJump()
    {
        if (target == null || retreating || self.IsCharmed || !self.Combat.CanJump) return false;
        Vector2 d = target.Position - self.Position;
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        if (ay > self.depthTolerance * 1.5f) return false;
        if (target.State == FighterState.Airborne) return ax <= jumpJuggleRange;
        return ax >= jumpAttackRange.x && ax <= jumpAttackRange.y && Random.value < jumpChance;
    }

    /// <summary>Хватать ли цель сейчас: она беззащитна и вплотную, и выпал шанс.</summary>
    private bool WantGrab() => self.Combat.CanGrab(target) && Random.value < grabChance;

    /// <summary>Шарм: ближайший живой союзник (его бьёт зачарованный). null — союзников нет.</summary>
    private Fighter NearestCharmTarget(BattleManager bm)
    {
        Fighter best = null;
        float bestDist = float.MaxValue;
        foreach (Fighter f in bm.FoesOf(self))
        {
            if (!f.IsTargetable) continue;
            float dist = Vector2.Distance(f.Position, self.Position);
            if (dist < bestDist) { bestDist = dist; best = f; }
        }
        return best;
    }

    /// <summary>Ближайший противник на всей арене (без ограничения радиусом), до которого можно дойти. null — таких нет.</summary>
    private Fighter NearestReachableOpponent(BattleManager bm)
    {
        Fighter best = null;
        float bestDist = float.MaxValue;
        foreach (Fighter f in bm.GetOpponents(self.Faction))
        {
            if (!f.IsAlive || !bm.IsReachable(self, f.Position)) continue;
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
