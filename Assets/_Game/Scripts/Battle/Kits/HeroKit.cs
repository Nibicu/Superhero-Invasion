using System.Collections.Generic;
using UnityEngine;

/// <summary>Поза бойца во время личного скила (для внешнего вида).</summary>
public enum KitPose
{
    None, // Обычные позы
    Aim,  // Целится и стреляет (обе руки вперёд)
    Roll, // Рывок или кувырок (кадры бега)
    Cast, // Применяет что-то руками (щит, волна, бросок)
    Block // Блок (Стажёр)
}

/// <summary>
/// Личные навыки героя в бою: 3 скила (открываются на 1, 3 и 5 уровне; скил может быть пассивным).
/// Каждый герой со своими навыками — отдельный класс-наследник (например, SoldierKit).
/// У героя без своих навыков (HeroData.kit = None) скилов нет — он бьётся авто атаками.
///
/// Как это работает:
///  - FighterAI каждое решение сначала спрашивает Think(...): если навык применён — ИИ ждёт;
///  - PreferredRange(...) — на какой дистанции держаться (0 — как у класса);
///  - ModifyStats(...), RegenAlways, SpeedMultiplier — пассивные эффекты;
///  - TryBlock(...) — навык может отменить попавший по герою удар (блок, уворот);
///  - OnAutoHit(...) — авто атака героя попала (например, кровотечение);
///  - длинные действия (залп, рывок) идут в состоянии Attacking, удар по бойцу их прерывает.
/// Перезарядка, название, описание и числа скилов (урон, шансы, длительности) — в файле героя (HeroData.skills).
/// Числа читаются в LoadParams (список чисел и значения по умолчанию — в KitParams).
/// </summary>
[RequireComponent(typeof(Fighter))]
public abstract class HeroKit : MonoBehaviour
{
    protected Fighter self;
    private readonly float[] cooldowns = new float[HeroData.SkillUnlockLevels.Length]; // Сколько секунд до готовности каждого скила

    /// <summary>Поза для внешнего вида (во время скила).</summary>
    public KitPose Pose { get; protected set; } = KitPose.None;

    /// <summary>Сколько секунд длится текущее действие скила (для позы).</summary>
    public float ActionTime { get; protected set; }

    /// <summary>
    /// Идёт действие, которое обычный удар не прерывает (залп, рывок, блок).
    /// Урон проходит, а сбить с ног — по-прежнему можно.
    /// </summary>
    public bool Unstoppable => ActionUnstoppable && self != null && self.State == FighterState.Attacking;

    /// <summary>Какие действия обычный удар не прерывает (залп, рывок, блок; наследник может добавить свои).</summary>
    protected virtual bool ActionUnstoppable => Pose == KitPose.Aim || Pose == KitPose.Roll || Pose == KitPose.Block;

    /// <summary>Какой скил (0..2) сейчас показывать кадрами персонажа (-1 — никакой: обычные кадры).</summary>
    public virtual int AnimSkill => -1;

    /// <summary>Счётчик повторов анимации скила: изменился — кадры скила играют сначала (каждая пуля Залпа).</summary>
    public int AnimCycle { get; protected set; }

    /// <summary>Сейчас идёт блок: любой урон, отбрасывание, стан и шарм не проходят.</summary>
    public virtual bool IsBlocking => false;

    /// <summary>Реген HP идёт всегда, даже под ударами (Геном Солдата).</summary>
    public virtual bool RegenAlways => false;

    /// <summary>Множитель скорости ходьбы (дым Сойки — ×1.3).</summary>
    public virtual float SpeedMultiplier => 1f;

    /// <summary>Множитель скорости авто атак (бубны Тени — следующие 3 удара быстрее).</summary>
    public virtual float AttackSpeedMultiplier => 1f;

    /// <summary>
    /// Смертельный удар: навык может спасти бойца (монетка Тени). Вернёт true и долю HP, с которой он остаётся;
    /// false — боец выбывает как обычно.
    /// </summary>
    public virtual bool TryCheatDeath(out float hpFraction)
    {
        hpFraction = 0f;
        return false;
    }

    /// <summary>Создать навыки нужного героя (null — своих навыков нет).</summary>
    public static HeroKit Create(Fighter f, HeroKitType type)
    {
        switch (type)
        {
            case HeroKitType.Soldier: return f.gameObject.AddComponent<SoldierKit>();
            case HeroKitType.Stajor: return f.gameObject.AddComponent<StajorKit>();
            case HeroKitType.Phantom: return f.gameObject.AddComponent<PhantomKit>();
            case HeroKitType.Soyka: return f.gameObject.AddComponent<SoykaKit>();
            case HeroKitType.Sukub: return f.gameObject.AddComponent<SukubKit>();
            case HeroKitType.Charovnica: return f.gameObject.AddComponent<CharovnicaKit>();
            case HeroKitType.Aqua: return f.gameObject.AddComponent<AquaKit>();
            case HeroKitType.Derevo: return f.gameObject.AddComponent<DerevoKit>();
            case HeroKitType.Motelek: return f.gameObject.AddComponent<MotelekKit>();
            case HeroKitType.Reyndger: return f.gameObject.AddComponent<ReyndgerKit>();
            case HeroKitType.FaerGerl: return f.gameObject.AddComponent<FaerGerlKit>();
            case HeroKitType.Protatp28: return f.gameObject.AddComponent<Protatp28Kit>();
            case HeroKitType.America: return f.gameObject.AddComponent<AmericaKit>();
            case HeroKitType.ZedRock: return f.gameObject.AddComponent<ZedRockKit>();
            case HeroKitType.Ten: return f.gameObject.AddComponent<TenKit>();
            case HeroKitType.Shtorm: return f.gameObject.AddComponent<ShtormKit>();
            case HeroKitType.Mozg: return f.gameObject.AddComponent<MozgKit>();
            case HeroKitType.Frizen: return f.gameObject.AddComponent<FrizenKit>();
            default: return null;
        }
    }

    /// <summary>Подготовить (вызывает Fighter.SetKit). В бой — с небольшой случайной задержкой скилов.</summary>
    public virtual void Setup(Fighter f)
    {
        self = f;
        LoadParams();
        for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Random.Range(0.3f, 1.5f);
    }

    /// <summary>Взять числа навыков из файла героя (HeroData → Skills → Values; список чисел — KitParams).</summary>
    protected virtual void LoadParams() { }

    /// <summary>Число навыка skill по ключу key (проценты уже превращены в долю: 40 → 0.4).</summary>
    protected float Num(int skill, string key) => KitParams.Get(self.Data, skill, key);

    /// <summary>Целое число навыка (количество: выстрелы, клоны, удары).</summary>
    protected int Count(int skill, string key) => Mathf.Max(0, Mathf.RoundToInt(Num(skill, key)));

    /// <summary>Открыт ли скил i на уровне героя (и придуман ли он).</summary>
    public bool IsUnlocked(int i) =>
        i >= 0 && i < HeroData.SkillUnlockLevels.Length && self.Level >= HeroData.SkillUnlockLevels[i]
        && !self.Data.GetSkill(i).IsEmpty;

    /// <summary>Можно ли применить скил i прямо сейчас (открыт, перезарядился, нет немоты, боец свободен).</summary>
    public bool IsReady(int i) => IsUnlocked(i) && cooldowns[i] <= 0f && !self.IsSilenced && self.CanAct;

    /// <summary>Скил i открыт, но перезаряжается.</summary>
    public bool IsOnCooldown(int i) => IsUnlocked(i) && cooldowns[i] > 0f;

    /// <summary>Запустить перезарядку скила i (из файла героя).</summary>
    protected void StartCooldown(int i) => cooldowns[i] = Mathf.Max(0.5f, self.Data.GetSkill(i).cooldown);

    /// <summary>Пассивка: изменить характеристики бойца (по умолчанию — без изменений).</summary>
    public virtual HeroStats ModifyStats(HeroStats s) => s;

    /// <summary>
    /// Пришёл удар: вернёт true, если навык его остановил (блок, уворот) — тогда урона нет.
    /// hit.auto — удар авто атакой (иначе скилом).
    /// </summary>
    public virtual bool TryBlock(HitInfo hit) => false;

    /// <summary>Авто атака героя попала по target (по умолчанию — ничего).</summary>
    public virtual void OnAutoHit(Fighter target) { }

    /// <summary>Герой сделал авто атаку — удар или выстрел (по умолчанию — ничего). Например, каждая 5-я — доп. стрела.</summary>
    public virtual void OnAutoAttack() { }

    /// <summary>
    /// Решение ИИ: применить скил против target (retreating — боец отступает).
    /// Вернёт true, если скил применён (ИИ в этот раз больше ничего не делает).
    /// </summary>
    public virtual bool Think(Fighter target, bool retreating) => false;

    /// <summary>На каком расстоянии держаться от цели (0 — как у класса).</summary>
    public virtual float PreferredRange(Fighter target) => 0f;

    /// <summary>Перезарядка скилов.</summary>
    protected virtual void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < cooldowns.Length; i++)
            if (cooldowns[i] > 0f) cooldowns[i] -= dt;
    }

    /// <summary>Начать длинное действие скила (боец стоит, ИИ ждёт). Вернёт false, если боец занят.</summary>
    protected bool BeginAction(KitPose pose)
    {
        if (!self.SetState(FighterState.Attacking)) return false;
        Pose = pose;
        ActionTime = 0f;
        return true;
    }

    /// <summary>Закончить действие скила.</summary>
    protected void EndAction()
    {
        Pose = KitPose.None;
        if (self.State == FighterState.Attacking) self.SetState(FighterState.Idle);
    }

    /// <summary>Действие ещё идёт (боец не получил удар и не упал).</summary>
    protected bool ActionAlive => Pose != KitPose.None && self.State == FighterState.Attacking;

    // ---------- Помощники для скилов ----------

    /// <summary>Противники героя.</summary>
    protected IReadOnlyList<Fighter> Enemies => BattleManager.Instance.GetOpponents(self.Faction);

    /// <summary>Короткая надпись над головой героя.</summary>
    protected void Pop(string text, Color color)
    {
        if (self.View != null) self.View.Pop(text, color);
    }

    /// <summary>Расстояние от точки p до ближайшего живого противника.</summary>
    protected float NearestEnemyDistance(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (Fighter f in Enemies)
            if (f.IsAlive) best = Mathf.Min(best, Vector2.Distance(f.Position, p));
        return best;
    }

    /// <summary>Живые противники в радиусе radius от точки p (кроме except).</summary>
    protected List<Fighter> EnemiesNear(Vector2 p, float radius, Fighter except = null)
    {
        var list = new List<Fighter>();
        foreach (Fighter f in Enemies)
            if (f != except && f.IsAlive && Vector2.Distance(f.Position, p) <= radius) list.Add(f);
        return list;
    }

    /// <summary>Точка неподалёку (до radius по арене), которая дальше всех от противников (безопасное место).</summary>
    protected Vector3 SafePoint(float radius)
    {
        BattleManager bm = BattleManager.Instance;
        Vector3 best = self.transform.position;
        float bestDist = NearestEnemyDistance(best);
        for (int i = 0; i < 12; i++)
        {
            Vector3 p = bm.RandomPointNear(self, radius);
            float dist = NearestEnemyDistance(p);
            if (dist > bestDist) { bestDist = dist; best = p; }
        }
        return best;
    }

    /// <summary>Перенестись в безопасное место неподалёку (до radius по арене).</summary>
    protected void TeleportAway(float radius) => self.transform.position = SafePoint(radius);

    /// <summary>Ударить врага скилом с подбрасыванием (launch — скорость вверх). Вернёт true, если удар прошёл.</summary>
    protected bool LaunchEnemy(Fighter e, float power, bool vsSpecial, float launch, Vector2 knockback)
    {
        return e.Health.TakeHit(new HitInfo
        {
            attacker = self,
            attackId = FighterCombat.NewAttackId(),
            damage = FighterCombat.CalcDamage(power, vsSpecial ? e.Stats.specialDefense : e.Stats.defense),
            knockback = knockback,
            launch = launch,
            special = vsSpecial
        });
    }

    /// <summary>Гроги: враг шатается (если сейчас может — не лежит, не в воздухе, не выбыл).</summary>
    protected static void MakeDizzy(Fighter e)
    {
        if (e == null || !e.IsAlive || !e.CanBecomeDizzy) return;
        if (e.State == FighterState.KnockedDown || e.State == FighterState.Airborne || e.State == FighterState.Jumping) return;
        e.BecomeDizzy();
    }

    /// <summary>Ударить врага скилом (урон power × сила; vsSpecial — против Спец. защиты).</summary>
    protected bool HitEnemy(Fighter e, float power, bool vsSpecial, Vector2 knockback, int stagger)
    {
        return e.Health.TakeHit(new HitInfo
        {
            attacker = self,
            attackId = FighterCombat.NewAttackId(),
            damage = FighterCombat.CalcDamage(power, vsSpecial ? e.Stats.specialDefense : e.Stats.defense),
            knockback = knockback,
            stagger = stagger,
            special = vsSpecial
        });
    }
}
