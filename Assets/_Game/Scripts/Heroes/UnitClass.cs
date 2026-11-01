using UnityEngine;

/// <summary>Класс бойца: определяет характеристики и поведение в бою. Скилы есть только у героев со своим набором (HeroKit).</summary>
public enum UnitClass
{
    Tank,    // Танк: больше HP и Защиты; медленный, мало энергии, слабая авто атака. Ближний бой
    Fighter, // Боец: сильная Сила атаки и Защита, быстрый; слабая Спец. защита. Ближний бой
    Mage,    // Маг: сильная Магия, энергия и Спец. защита; мало HP и Защиты, медленный. Ближний бой
    Shooter, // Стрелок: сильная Сила атаки, Реген HP, быстрый; слабая Защита и авто атака. Дальний бой
    Support  // Поддержка: много энергии, Регена энергии и Регена HP; мало HP и авто атаки. Ближний бой
}

/// <summary>От какого параметра считается урон скила (и какая защита цели его ослабляет).</summary>
public enum DamageType
{
    Attack,  // От Силы атаки, ослабляется Защитой цели
    Special  // От Магии, ослабляется Спец. защитой цели
}

/// <summary>Как боец ведёт бой.</summary>
public enum CombatStyle
{
    Melee, // Ближний: подходит вплотную и бьёт авто атаками
    Ranged // Дальний: держит дистанцию и стреляет авто атаками (вплотную — бьёт рукой)
}

/// <summary>Настройки одного класса (поведение и дальности).</summary>
public class ClassProfile
{
    public string name;              // Название класса для интерфейса
    public CombatStyle style;        // Как ведёт бой
    public float retreatAt;          // При какой доле HP отступает (0.1 = 10%)
    public float autoRange;          // Дальность авто атаки (0 — вплотную, дальность удара бойца)
    public float preferredRange;     // На каком расстоянии от цели держится (0 — вплотную)
    public DamageType defaultDamage; // Тип урона скилов по умолчанию
}

/// <summary>
/// Таблица классов: поведение и "шаблоны" характеристик.
/// Характеристики: у всех классов одна "норма"; сильные стороны класса на 20% выше нормы, слабые — на 20% ниже.
/// Все цифры классов — здесь, их можно менять.
/// </summary>
public static class UnitClasses
{
    /// <summary>Отступивший боец возвращается в бой, когда HP выше этой доли.</summary>
    public const float ResumeAt = 0.35f;

    /// <summary>Насколько сильная сторона класса выше нормы (+20%).</summary>
    public const float Strong = 1.2f;

    /// <summary>Насколько слабая сторона класса ниже нормы (−20%).</summary>
    public const float Weak = 0.8f;

    private static readonly ClassProfile[] profiles =
    {
        new ClassProfile { name = "Танк", style = CombatStyle.Melee, retreatAt = 0.10f, autoRange = 0f, preferredRange = 0f, defaultDamage = DamageType.Attack },
        new ClassProfile { name = "Боец", style = CombatStyle.Melee, retreatAt = 0.20f, autoRange = 0f, preferredRange = 0f, defaultDamage = DamageType.Attack },
        new ClassProfile { name = "Маг", style = CombatStyle.Melee, retreatAt = 0.25f, autoRange = 0f, preferredRange = 0f, defaultDamage = DamageType.Special },
        new ClassProfile { name = "Стрелок", style = CombatStyle.Ranged, retreatAt = 0.30f, autoRange = 7.5f, preferredRange = 6.0f, defaultDamage = DamageType.Attack },
        new ClassProfile { name = "Поддержка", style = CombatStyle.Melee, retreatAt = 0.20f, autoRange = 0f, preferredRange = 0f, defaultDamage = DamageType.Special },
    };

    /// <summary>Настройки класса.</summary>
    public static ClassProfile Get(UnitClass c) => profiles[(int)c];

    /// <summary>Название класса.</summary>
    public static string Name(UnitClass c) => Get(c).name;

    /// <summary>Название типа урона скилов.</summary>
    public static string DamageName(DamageType t) => t == DamageType.Attack ? "Сила атаки" : "Магия";

    /// <summary>
    /// "Норма" — характеристики обычного бойца 1★ 1 уровня. От неё считаются все классы.
    /// Сила атаки и Магия одинаковые: у каждого класса работает только та, от которой считаются его скилы.
    /// </summary>
    public static HeroStats Norm => new HeroStats
    {
        hp = 700, hpRegen = 12, autoAttack = 22, attack = 100, specialAttack = 100,
        defense = 45, specialDefense = 45, energy = 100, energyRegen = 12, speed = 65
    };

    /// <summary>
    /// Шаблон характеристик класса для героя 1★ 1 уровня: норма, сильные стороны ×1.2, слабые ×0.8.
    /// Используется кнопкой "Заполнить характеристики по классу" в файле героя.
    /// </summary>
    public static HeroStats Template(UnitClass c)
    {
        HeroStats s = Norm;
        switch (c)
        {
            case UnitClass.Tank:
                s.hp = Up(s.hp); s.defense = Up(s.defense);
                s.speed = Down(s.speed); s.energy = Down(s.energy); s.autoAttack = Down(s.autoAttack);
                break;
            case UnitClass.Fighter:
                s.attack = Up(s.attack); s.defense = Up(s.defense); s.speed = Up(s.speed);
                s.specialDefense = Down(s.specialDefense);
                break;
            case UnitClass.Mage:
                s.specialAttack = Up(s.specialAttack); s.energy = Up(s.energy); s.specialDefense = Up(s.specialDefense);
                s.hp = Down(s.hp); s.defense = Down(s.defense); s.speed = Down(s.speed);
                break;
            case UnitClass.Shooter:
                s.attack = Up(s.attack); s.hpRegen = Up(s.hpRegen); s.speed = Up(s.speed);
                s.defense = Down(s.defense); s.autoAttack = Down(s.autoAttack);
                break;
            default: // Поддержка
                s.energy = Up(s.energy); s.energyRegen = Up(s.energyRegen); s.hpRegen = Up(s.hpRegen);
                s.hp = Down(s.hp); s.autoAttack = Down(s.autoAttack);
                break;
        }
        return s;
    }

    /// <summary>Сильная сторона: +20%.</summary>
    private static int Up(int v) => Mathf.RoundToInt(v * Strong);

    /// <summary>Слабая сторона: −20%.</summary>
    private static int Down(int v) => Mathf.RoundToInt(v * Weak);

    /// <summary>
    /// Характеристики по шаблону класса, увеличенные в power раз (1 — обычный герой 1★, 2.2 — 2★, 4.2 — 3★).
    /// Скорость почти не растёт, энергия растёт медленнее остального.
    /// Если тип урона скилов не "родной" для класса — Сила атаки и Магия меняются местами.
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
