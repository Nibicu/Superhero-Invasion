using UnityEngine;

/// <summary>Поза бойца во время личного скила (для внешнего вида).</summary>
public enum KitPose
{
    None, // Обычные позы
    Aim,  // Целится и стреляет (обе руки вперёд)
    Roll, // Кувырок
    Cast  // Применяет что-то руками (щит, оковы)
}

/// <summary>
/// Личные навыки героя в бою: 1 пассивный и 4 скила (открываются на 1, 2, 4 и 5 уровне).
/// Каждый герой со своими навыками — отдельный класс-наследник (например, SoldierKit).
/// Пока у героя нет своих навыков (HeroData.kit = None), он дерётся скилом своего класса.
///
/// Как это работает:
///  - FighterAI каждое решение сначала спрашивает Think(...): если навык применён — ИИ ждёт;
///  - PreferredRange(...) — на какой дистанции держаться (0 — как у класса);
///  - ModifyAutoDamage(...) — пассивка может менять урон авто атак;
///  - длинные действия (залп, кувырок) идут в состоянии Attacking, удар по бойцу их прерывает.
/// Перезарядка, название и описание скилов — в файле героя (HeroData.skills).
/// </summary>
[RequireComponent(typeof(Fighter))]
public abstract class HeroKit : MonoBehaviour
{
    protected Fighter self;
    private readonly float[] cooldowns = new float[4]; // Сколько секунд до готовности каждого скила

    /// <summary>Поза для внешнего вида (во время скила).</summary>
    public KitPose Pose { get; protected set; } = KitPose.None;

    /// <summary>Сколько секунд длится текущее действие скила (для позы).</summary>
    public float ActionTime { get; protected set; }

    /// <summary>
    /// Идёт действие, которое обычный удар не прерывает (залп, кувырок).
    /// Урон проходит, а сбить с ног — по-прежнему можно.
    /// </summary>
    public bool Unstoppable => (Pose == KitPose.Aim || Pose == KitPose.Roll) && self != null && self.State == FighterState.Attacking;

    /// <summary>Создать навыки нужного героя (null — своих навыков пока нет).</summary>
    public static HeroKit Create(Fighter f, HeroKitType type)
    {
        switch (type)
        {
            case HeroKitType.Soldier: return f.gameObject.AddComponent<SoldierKit>();
            default: return null;
        }
    }

    /// <summary>Подготовить (вызывает Fighter.SetKit). В бой — с небольшой случайной задержкой скилов.</summary>
    public virtual void Setup(Fighter f)
    {
        self = f;
        for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Random.Range(0.3f, 1.5f);
    }

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

    /// <summary>Пассивка: изменить силу авто атаки (по умолчанию — без изменений).</summary>
    public virtual float ModifyAutoDamage(float power) => power;

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
}
