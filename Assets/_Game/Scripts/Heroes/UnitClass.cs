using UnityEngine;

/// <summary>Класс бойца: определяет характеристики, поведение в бою и скил.</summary>
public enum UnitClass
{
    Tank,    // Танк: много HP и защиты, медленный, мало энергии. Ближний бой
    Fighter, // Боец: сильная атака и защита, слабая спец. защита. Ближний бой
    Mage,    // Маг: сильная спец. атака и энергия, медленный, слабая защита. Средний бой
    Shooter, // Стрелок: сильная атака и реген, слабая защита и авто атака. Дальний бой
    Support  // Поддержка: много энергии и регена, мало HP и авто атаки. Средний бой, лечит
}

/// <summary>От какого параметра считается урон скила (и какая защита цели его ослабляет).</summary>
public enum DamageType
{
    Attack,  // От Атаки, ослабляется Защитой цели
    Special  // От Спец. атаки, ослабляется Спец. защитой цели
}

/// <summary>Как боец ведёт бой.</summary>
public enum CombatStyle
{
    Melee, // Ближний: в основном авто атаки вплотную, скилы иногда
    Mid,   // Средний: держит среднюю дистанцию, больше полагается на скилы
    Ranged // Дальний: в основном авто атаки издалека, скилы иногда
}

/// <summary>Настройки одного класса (поведение, дальности, скил).</summary>
public class ClassProfile
{
    public string name;             // Название класса для интерфейса
    public CombatStyle style;       // Как ведёт бой
    public float retreatAt;         // При какой доле HP отступает (0.1 = 10%)
    public float autoRange;         // Дальность авто атаки (0 — вплотную, дальность удара бойца)
    public float preferredRange;    // На каком расстоянии от цели держится (0 — вплотную)
    public DamageType defaultDamage; // Тип урона скилов по умолчанию
    public string skillName;        // Название скила
    public string skillDescription; // Что делает скил
    public float skillCost;         // Сколько энергии тратит скил
    public float skillCooldown;     // Перезарядка скила (сек)
    public float skillChance;       // Шанс применить скил, когда он готов и есть цель (за одно решение)
}

/// <summary>
/// Таблица классов: поведение, скилы и "шаблоны" характеристик.
/// Все цифры классов — здесь, их можно менять.
/// </summary>
public static class UnitClasses
{
    /// <summary>Отступивший боец возвращается в бой, когда HP выше этой доли.</summary>
    public const float ResumeAt = 0.35f;

    private static readonly ClassProfile[] profiles =
    {
        new ClassProfile // Танк
        {
            name = "Танк", style = CombatStyle.Melee, retreatAt = 0.10f,
            autoRange = 0f, preferredRange = 0f, defaultDamage = DamageType.Attack,
            skillName = "Удар по земле", skillDescription = "Бьёт по земле: урон всем врагам вокруг и отбрасывает их",
            skillCost = 50f, skillCooldown = 6f, skillChance = 0.3f
        },
        new ClassProfile // Боец
        {
            name = "Боец", style = CombatStyle.Melee, retreatAt = 0.20f,
            autoRange = 0f, preferredRange = 0f, defaultDamage = DamageType.Attack,
            skillName = "Мощный удар", skillDescription = "Удар двойной силы, сбивает врага с ног",
            skillCost = 40f, skillCooldown = 4f, skillChance = 0.3f
        },
        new ClassProfile // Маг
        {
            name = "Маг", style = CombatStyle.Mid, retreatAt = 0.25f,
            autoRange = 4.5f, preferredRange = 4.0f, defaultDamage = DamageType.Special,
            skillName = "Энергетический снаряд", skillDescription = "Большой снаряд: сильный урон, отбрасывает врага",
            skillCost = 40f, skillCooldown = 2.5f, skillChance = 0.75f
        },
        new ClassProfile // Стрелок
        {
            name = "Стрелок", style = CombatStyle.Ranged, retreatAt = 0.30f,
            autoRange = 7.5f, preferredRange = 6.0f, defaultDamage = DamageType.Attack,
            skillName = "Прицельный выстрел", skillDescription = "Быстрая пуля большой силы, пробивает врагов насквозь",
            skillCost = 50f, skillCooldown = 5f, skillChance = 0.25f
        },
        new ClassProfile // Поддержка
        {
            name = "Поддержка", style = CombatStyle.Mid, retreatAt = 0.20f,
            autoRange = 4.5f, preferredRange = 4.5f, defaultDamage = DamageType.Special,
            skillName = "Лечение", skillDescription = "Лечит союзника с самым низким здоровьем (или себя)",
            skillCost = 50f, skillCooldown = 5f, skillChance = 1f
        },
    };

    /// <summary>Настройки класса.</summary>
    public static ClassProfile Get(UnitClass c) => profiles[(int)c];

    /// <summary>Название класса.</summary>
    public static string Name(UnitClass c) => Get(c).name;

    /// <summary>Название типа урона скилов.</summary>
    public static string DamageName(DamageType t) => t == DamageType.Attack ? "Атака" : "Спец. атака";

    /// <summary>
    /// Шаблон характеристик класса для героя 1★ 1 уровня (сила около 200–240).
    /// Используется кнопкой "Заполнить характеристики по классу" в файле героя.
    /// </summary>
    public static HeroStats Template(UnitClass c)
    {
        switch (c)
        {
            case UnitClass.Tank:
                return new HeroStats { hp = 900, hpRegen = 10, autoAttack = 20, attack = 45, specialAttack = 15, defense = 70, specialDefense = 55, energy = 70, energyRegen = 6, speed = 55 };
            case UnitClass.Fighter:
                return new HeroStats { hp = 750, hpRegen = 10, autoAttack = 28, attack = 70, specialAttack = 20, defense = 55, specialDefense = 25, energy = 70, energyRegen = 8, speed = 75 };
            case UnitClass.Mage:
                return new HeroStats { hp = 620, hpRegen = 8, autoAttack = 18, attack = 15, specialAttack = 90, defense = 25, specialDefense = 50, energy = 150, energyRegen = 14, speed = 60 };
            case UnitClass.Shooter:
                return new HeroStats { hp = 650, hpRegen = 18, autoAttack = 18, attack = 85, specialAttack = 15, defense = 25, specialDefense = 35, energy = 100, energyRegen = 16, speed = 75 };
            default: // Поддержка
                return new HeroStats { hp = 550, hpRegen = 18, autoAttack = 14, attack = 15, specialAttack = 75, defense = 35, specialDefense = 50, energy = 160, energyRegen = 18, speed = 70 };
        }
    }

    /// <summary>
    /// Характеристики по шаблону класса, увеличенные в power раз (1 — обычный герой 1★, 2.2 — 2★, 4.2 — 3★).
    /// Скорость почти не растёт, энергия растёт медленнее остального.
    /// Если тип урона скилов не "родной" для класса — атака и спец. атака меняются местами.
    /// </summary>
    public static HeroStats ScaledTemplate(UnitClass c, DamageType damage, float power, int stars)
    {
        HeroStats t = Template(c);
        if (damage != Get(c).defaultDamage)
        {
            int a = t.attack; t.attack = t.specialAttack; t.specialAttack = a;
        }
        float e = Mathf.Pow(power, 0.6f); // энергия растёт медленнее
        return new HeroStats
        {
            hp = Mathf.RoundToInt(t.hp * power),
            hpRegen = Mathf.RoundToInt(t.hpRegen * power),
            autoAttack = Mathf.RoundToInt(t.autoAttack * power),
            attack = Mathf.RoundToInt(t.attack * power),
            specialAttack = Mathf.RoundToInt(t.specialAttack * power),
            defense = Mathf.RoundToInt(t.defense * power),
            specialDefense = Mathf.RoundToInt(t.specialDefense * power),
            energy = Mathf.RoundToInt(t.energy * e),
            energyRegen = Mathf.Max(1, Mathf.RoundToInt(t.energyRegen * e)),
            speed = t.speed + (Mathf.Clamp(stars, 1, 3) - 1) * 10
        };
    }

    /// <summary>Во сколько раз герой N звёзд сильнее героя 1★.</summary>
    public static float StarPower(int stars) => stars >= 3 ? 4.2f : stars == 2 ? 2.2f : 1f;
}
