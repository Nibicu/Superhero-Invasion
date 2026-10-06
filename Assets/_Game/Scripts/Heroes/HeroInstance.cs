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
/// а HeroInstance — конкретный нанятый герой со своим уровнем и статусом.
/// </summary>
public class HeroInstance
{
    /// <summary>На сколько растут характеристики за каждый уровень (0.1 = +10%).</summary>
    public const float StatGrowthPerLevel = 0.1f;

    /// <summary>Описание героя (имя, звёзды, базовые характеристики).</summary>
    public HeroData Data { get; }

    /// <summary>Кому принадлежит герой.</summary>
    public Team Owner { get; }

    /// <summary>Уровень героя (растёт при прокачке).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Чем занят герой.</summary>
    public HeroStatus Status { get; set; } = HeroStatus.Free;

    /// <summary>Создать нанятого героя 1 уровня.</summary>
    public HeroInstance(HeroData data, Team owner)
    {
        Data = data;
        Owner = owner;
    }

    /// <summary>Текущие характеристики с учётом уровня.</summary>
    public HeroStats Stats => Data.baseStats.Scaled(1f + StatGrowthPerLevel * (Level - 1));

    /// <summary>Открыт ли навык на текущем уровне.</summary>
    public bool IsSkillUnlocked(HeroSkill skill) => Level >= skill.unlockLevel;

    /// <summary>Поднять уровень на 1 (цену и максимум проверяет HeroManager).</summary>
    public void LevelUp() => Level++;

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
