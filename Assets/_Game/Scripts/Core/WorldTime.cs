using UnityEngine;

/// <summary>
/// "Время глобальной карты". Во время ручного боя карта ставится на паузу:
/// не идёт доход, не едут фишки, не тикают захваты и миссии, враг не думает.
/// Скрипты карты используют WorldTime.DeltaTime вместо Time.deltaTime.
/// Сам бой идёт в обычном времени (Time.deltaTime).
/// </summary>
public static class WorldTime
{
    /// <summary>Стоит ли карта на паузе.</summary>
    public static bool Paused { get; set; }

    /// <summary>Сколько времени прошло на карте за кадр (0, если пауза).</summary>
    public static float DeltaTime => Paused ? 0f : Time.deltaTime;
}
