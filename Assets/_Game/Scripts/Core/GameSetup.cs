/// <summary>
/// Что игрок выбрал перед игрой в Песочнице: карта, сторона конфликта и фракция.
/// Заполняется в главном меню и читается сценой игры (сцена меняется, а эти данные остаются).
/// Если игру запустили сразу со сцены карты (без меню) — IsSet = false, работают настройки сцены.
/// </summary>
public static class GameSetup
{
    /// <summary>Выбор сделан в меню.</summary>
    public static bool IsSet { get; private set; }

    /// <summary>Выбранная карта.</summary>
    public static MapData Map { get; private set; }

    /// <summary>Сторона игрока (герои или злодеи). Враг — противоположная.</summary>
    public static Side PlayerSide { get; private set; } = Side.Heroes;

    /// <summary>Фракция игрока.</summary>
    public static FactionData PlayerFaction { get; private set; }

    /// <summary>Фракция врага (случайная фракция противоположной стороны).</summary>
    public static FactionData EnemyFaction { get; private set; }

    /// <summary>Запомнить выбор игрока перед запуском карты.</summary>
    public static void Set(MapData map, Side side, FactionData playerFaction, FactionData enemyFaction)
    {
        Map = map;
        PlayerSide = side;
        PlayerFaction = playerFaction;
        EnemyFaction = enemyFaction;
        IsSet = true;
    }
}
