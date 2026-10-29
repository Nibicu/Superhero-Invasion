using UnityEngine;

/// <summary>Чем сейчас занят нанятый герой.</summary>
public enum HeroStatus
{
    Free,     // На базе, свободен
    InTeam,   // Состоит в команде (Шаг 4)
    OnMission // Команда героя на задании (Шаги 5-6)
}

/// <summary>
/// Нанятый герой. HeroData — это "карточка" героя (одинаковая для всех),
/// а HeroInstance — конкретный нанятый герой со своим уровнем, опытом, звездой, жизнями и статусом.
/// Рост: опыт за задания и бои → уровень (каждые 100 опыта, до 5) → на 5 уровне можно один раз
/// повысить звезду за плутоний (характеристики как у героя с этим числом звёзд; 3★ → 3.5★, +10%).
/// </summary>
public class HeroInstance
{
    /// <summary>На сколько растут характеристики за каждый уровень (0.05 = +5%).</summary>
    public const float StatGrowthPerLevel = 0.05f;

    /// <summary>Сколько опыта нужно на каждый следующий уровень.</summary>
    public const int XpPerLevel = 100;

    /// <summary>Сколько жизней у героя (сколько раз может выбыть в бою, прежде чем будет побеждён).</summary>
    public const int MaxLives = 3;

    /// <summary>Прибавка 3★ героя за повышение звезды (до 3.5★): +10% ко всем характеристикам.</summary>
    public const float HalfStarBonus = 0.1f;

    /// <summary>Описание героя (имя, звёзды, базовые характеристики).</summary>
    public HeroData Data { get; }

    /// <summary>Кому принадлежит герой.</summary>
    public Team Owner { get; }

    /// <summary>Уровень героя (растёт от опыта).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Опыт на текущем уровне (0..99).</summary>
    public int Xp { get; private set; }

    /// <summary>Звезда героя уже повышена (повысить можно только один раз).</summary>
    public bool StarUpgraded { get; private set; }

    /// <summary>Сколько жизней осталось (0 — побеждён).</summary>
    public int Lives { get; set; } = MaxLives;

    /// <summary>Чем занят герой.</summary>
    public HeroStatus Status { get; set; } = HeroStatus.Free;

    /// <summary>Доля здоровья героя 0..1 (раненый герой лечится, пока он дома).</summary>
    public float HpFraction { get; set; } = 1f;

    /// <summary>Герой дома (не в команде, которая уехала на задание).</summary>
    public bool IsAtHome => Status != HeroStatus.OnMission;

    /// <summary>Создать нанятого героя 1 уровня.</summary>
    public HeroInstance(HeroData data, Team owner)
    {
        Data = data;
        Owner = owner;
    }

    /// <summary>Звёзды героя с учётом повышения (3★ после повышения — 3.5★).</summary>
    public float Stars => Data.stars + (StarUpgraded ? (Data.stars >= 3 ? 0.5f : 1f) : 0f);

    /// <summary>Звёзды словами: "2★" / "3.5★" (для подписей; в шрифте нет ★ — пишем "зв.").</summary>
    public string StarsText => Stars % 1f > 0f ? $"{Stars:0.0} зв." : $"{Stars:0} зв.";

    /// <summary>Сколько ячеек для вещей-артефактов у героя.</summary>
    public const int SlotCount = 3;

    /// <summary>Вещи-артефакты, надетые на героя (null — ячейка пуста). Меняет их ArtifactManager.</summary>
    public ArtifactData[] Slots { get; } = new ArtifactData[SlotCount];

    /// <summary>
    /// Текущие характеристики: базовые (+ разница до следующей звезды, если звезда повышена),
    /// уровень (+5% за уровень), 3.5★ (+10%), плюс прибавки от надетых вещей.
    /// </summary>
    public HeroStats Stats
    {
        get
        {
            HeroStats b = Data.baseStats;
            if (StarUpgraded && Data.stars < 3) b = b.Plus(StarDifference(Data, Data.stars, Data.stars + 1));
            float k = 1f + StatGrowthPerLevel * (Level - 1) + (StarUpgraded && Data.stars >= 3 ? HalfStarBonus : 0f);
            HeroStats s = b.Scaled(k);
            foreach (ArtifactData a in Slots)
                if (a != null) s = s.Plus(a.bonus);
            return s;
        }
    }

    /// <summary>
    /// Насколько герой класса data с звёздами to сильнее такого же с звёздами from
    /// (по шаблонам классов — "как будто герой изначально был с таким числом звёзд").
    /// </summary>
    public static HeroStats StarDifference(HeroData data, int from, int to)
    {
        HeroStats a = UnitClasses.ScaledTemplate(data.unitClass, data.skillDamage, UnitClasses.StarPower(from), from);
        HeroStats b = UnitClasses.ScaledTemplate(data.unitClass, data.skillDamage, UnitClasses.StarPower(to), to);
        return new HeroStats
        {
            hp = b.hp - a.hp,
            hpRegen = b.hpRegen - a.hpRegen,
            autoAttack = b.autoAttack - a.autoAttack,
            attack = b.attack - a.attack,
            specialAttack = b.specialAttack - a.specialAttack,
            defense = b.defense - a.defense,
            specialDefense = b.specialDefense - a.specialDefense,
            energy = b.energy - a.energy,
            energyRegen = b.energyRegen - a.energyRegen,
            speed = b.speed - a.speed,
        };
    }

    /// <summary>Открыт ли навык на текущем уровне.</summary>
    public bool IsSkillUnlocked(int index) => index >= 0 && index < HeroData.SkillUnlockLevels.Length && Level >= HeroData.SkillUnlockLevels[index];

    /// <summary>
    /// Добавить опыт. Каждые 100 опыта — новый уровень (до maxLevel; на максимуме опыт не копится).
    /// Вернёт, сколько уровней получено.
    /// </summary>
    public int AddXp(int amount, int maxLevel)
    {
        if (amount <= 0 || Level >= maxLevel) return 0;
        Xp += amount;
        int gained = 0;
        while (Xp >= XpPerLevel && Level < maxLevel)
        {
            Xp -= XpPerLevel;
            Level++;
            gained++;
        }
        if (Level >= maxLevel) Xp = 0;
        return gained;
    }

    /// <summary>Повысить звезду (проверки и цену делает HeroManager).</summary>
    public void UpgradeStar() => StarUpgraded = true;

    /// <summary>Статус словами для интерфейса.</summary>
    public string StatusText
    {
        get
        {
            switch (Status)
            {
                case HeroStatus.InTeam: return "<color=#7FB8FF>В команде</color>";
                case HeroStatus.OnMission: return "<color=#FFB84A>На задании</color>";
                default: return "<color=#6EE07A>На базе</color>";
            }
        }
    }
}
