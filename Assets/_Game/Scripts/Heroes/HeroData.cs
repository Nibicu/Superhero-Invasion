using System;
using UnityEngine;

/// <summary>Сторона конфликта: супергерои или суперзлодеи.</summary>
public enum Side
{
    Heroes,  // Супергерои
    Villains // Суперзлодеи
}

/// <summary>
/// Характеристики героя (по GDD). Используются в карточке героя,
/// а позже — в битвах.
/// </summary>
[Serializable]
public struct HeroStats
{
    public int hp;             // Здоровье
    public int hpRegen;        // Реген здоровья (HP в секунду в бою, когда героя не бьют)
    public int autoAttack;     // Авто атака — урон обычных атак
    public int attack;         // Атака — урон скилов (у кого тип урона "Атака")
    public int specialAttack;  // Специальная атака — урон скилов (у кого тип урона "Спец. атака")
    public int defense;        // Защита
    public int specialDefense; // Специальная защита
    public int energy;         // Энергия
    public int energyRegen;    // Реген энергии
    public int speed;          // Скорость

    /// <summary>Названия характеристик для интерфейса (в том же порядке, что ToArray).</summary>
    public static readonly string[] Names =
    {
        "Здоровье", "Реген HP", "Авто атака", "Атака", "Спец. атака", "Защита",
        "Спец. защита", "Энергия", "Реген энергии", "Скорость"
    };

    /// <summary>Максимумы для полосок в карточке героя (полоска заполнена целиком при таком значении).</summary>
    public static readonly int[] BarMax = { 4000, 80, 150, 400, 400, 400, 400, 400, 40, 160 };

    /// <summary>Все значения массивом — удобно выводить в цикле.</summary>
    public int[] ToArray() => new[]
    {
        hp, hpRegen, autoAttack, attack, specialAttack, defense, specialDefense, energy, energyRegen, speed
    };

    /// <summary>Сумма характеристик (например, герой + прибавка от надетой вещи).</summary>
    public HeroStats Plus(HeroStats b) => new HeroStats
    {
        hp = hp + b.hp,
        hpRegen = hpRegen + b.hpRegen,
        autoAttack = autoAttack + b.autoAttack,
        attack = attack + b.attack,
        specialAttack = specialAttack + b.specialAttack,
        defense = defense + b.defense,
        specialDefense = specialDefense + b.specialDefense,
        energy = energy + b.energy,
        energyRegen = energyRegen + b.energyRegen,
        speed = speed + b.speed,
    };

    /// <summary>Характеристики с одним усиленным параметром (временный бафф в бою).</summary>
    public HeroStats WithBoost(BoostStat stat, float mult)
    {
        HeroStats s = this;
        switch (stat)
        {
            case BoostStat.AutoAttack: s.autoAttack = Mathf.RoundToInt(autoAttack * mult); break;
            case BoostStat.Attack: s.attack = Mathf.RoundToInt(attack * mult); break;
            case BoostStat.SpecialAttack: s.specialAttack = Mathf.RoundToInt(specialAttack * mult); break;
            case BoostStat.Defense: s.defense = Mathf.RoundToInt(defense * mult); break;
            case BoostStat.SpecialDefense: s.specialDefense = Mathf.RoundToInt(specialDefense * mult); break;
            case BoostStat.Speed: s.speed = Mathf.RoundToInt(speed * mult); break;
        }
        return s;
    }

    /// <summary>Характеристики, умноженные на коэффициент (для прокачки уровня).</summary>
    public HeroStats Scaled(float k) => new HeroStats
    {
        hp = Mathf.RoundToInt(hp * k),
        hpRegen = Mathf.RoundToInt(hpRegen * k),
        autoAttack = Mathf.RoundToInt(autoAttack * k),
        attack = Mathf.RoundToInt(attack * k),
        specialAttack = Mathf.RoundToInt(specialAttack * k),
        defense = Mathf.RoundToInt(defense * k),
        specialDefense = Mathf.RoundToInt(specialDefense * k),
        energy = Mathf.RoundToInt(energy * k),
        energyRegen = Mathf.RoundToInt(energyRegen * k),
        speed = Mathf.RoundToInt(speed * k),
    };
}

/// <summary>Навык героя. Открывается, когда герой достигает нужного уровня.</summary>
[Serializable]
public class HeroSkill
{
    [Tooltip("Название навыка")]
    public string name = "Навык";

    [Tooltip("Что делает навык (используется в бою)")]
    [TextArea(1, 3)] public string description = "";

    [Tooltip("С какого уровня героя навык открыт")]
    [Min(1)] public int unlockLevel = 1;
}

/// <summary>
/// Описание героя или злодея (ScriptableObject — файл-ассет).
/// Создать нового: ПКМ в Project → Create → Супергеройское бюро → Герой.
/// Чтобы герой появился в игре — добавьте его в список Hero Pool у HeroManager.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Герой", fileName = "Hero_")]
public class HeroData : ScriptableObject
{
    [Header("Внешний вид")]
    [Tooltip("Имя героя")]
    public string displayName = "Герой";

    [Tooltip("Портрет. Если пусто — рисуется цветной круг с инициалами")]
    public Sprite portrait;

    [Tooltip("Цвет круга-портрета (если нет спрайта)")]
    public Color color = Color.gray;

    [Tooltip("Описание / история героя")]
    [TextArea(2, 5)] public string description = "";

    [Header("Найм")]
    [Tooltip("Герой или злодей")]
    public Side side = Side.Heroes;

    [Tooltip("Звёзды: 1, 2 или 3. Чем больше — тем сильнее и дороже")]
    [Range(1, 3)] public int stars = 1;

    [Tooltip("Цена найма в золоте")]
    public int hireCost = 300;

    [Tooltip("Можно ли нанять в РОСТЕРЕ. Выключено — это моб-охранник (только для охраны объектов)")]
    public bool hireable = true;

    [Header("Класс")]
    [Tooltip("Танк, Боец, Маг, Стрелок или Поддержка — определяет поведение в бою и скил")]
    public UnitClass unitClass = UnitClass.Fighter;

    [Tooltip("От чего считается урон скилов: Атака (против Защиты) или Спец. атака (против Спец. защиты)")]
    public DamageType skillDamage = DamageType.Attack;

    [Header("Характеристики (на 1 уровне)")]
    public HeroStats baseStats;

    /// <summary>Настройки класса (поведение, скил).</summary>
    public ClassProfile Profile => UnitClasses.Get(unitClass);

    /// <summary>
    /// Кнопка в меню компонента (три точки справа сверху в Инспекторе):
    /// заполнить характеристики по шаблону класса с учётом звёзд. Потом их можно подправить руками.
    /// </summary>
    [ContextMenu("Заполнить характеристики по классу")]
    private void FillStatsFromClass()
    {
        baseStats = UnitClasses.ScaledTemplate(unitClass, skillDamage, UnitClasses.StarPower(stars), stars);
    }

    [Header("Навыки и бонус командира")]
    public HeroSkill[] skills = { new HeroSkill() };

    [Tooltip("Бонус команде, если этот герой — капитан")]
    [TextArea(1, 3)] public string captainBonus = "";

    /// <summary>Цвет рамки по звёздам: 1 — синеватый, 2 — бирюзовый, 3 — золотой.</summary>
    public static Color StarColor(int stars)
    {
        switch (stars)
        {
            case 3: return new Color(0.96f, 0.77f, 0.26f);
            case 2: return new Color(0.36f, 0.82f, 0.86f);
            default: return new Color(0.55f, 0.62f, 0.75f);
        }
    }

    /// <summary>Инициалы для временного портрета: "Титан" → "Т", "Синяя Молния" → "СМ".</summary>
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "?";
            string[] parts = displayName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                : parts[0].Substring(0, 1).ToUpper();
        }
    }
}
