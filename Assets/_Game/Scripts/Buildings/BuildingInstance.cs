/// <summary>
/// Постройка, которая уже стоит в ячейке базы.
/// BuildingData — это "чертёж" (одинаковый для всех), а BuildingInstance —
/// конкретное здание с текущим уровнем и владельцем.
/// Сама приносит доход, поэтому реализует IIncomeSource.
/// </summary>
public class BuildingInstance : IIncomeSource
{
    /// <summary>Чертёж постройки (название, цены, бонусы).</summary>
    public BuildingData Data { get; }

    /// <summary>Текущий уровень (начинается с 1).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Чья постройка.</summary>
    public Team Owner { get; }

    /// <summary>Параметры текущего уровня.</summary>
    public BuildingLevel Current => Data.GetLevel(Level);

    /// <summary>Есть ли куда улучшать.</summary>
    public bool IsMaxLevel => Level >= Data.MaxLevel;

    // Доход для ResourceManager
    public int GoldIncome => Current.goldIncome;
    public int PlutoniumIncome => Current.plutoniumIncome;
    public string SourceName => Data.displayName;

    /// <summary>Создать постройку уровня 1.</summary>
    public BuildingInstance(BuildingData data, Team owner)
    {
        Data = data;
        Owner = owner;
    }

    /// <summary>Поднять уровень на 1 (проверки цены делает MainBase).</summary>
    public void LevelUp()
    {
        if (!IsMaxLevel) Level++;
    }
}
