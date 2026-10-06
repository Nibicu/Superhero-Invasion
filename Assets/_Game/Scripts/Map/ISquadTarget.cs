using UnityEngine;

/// <summary>
/// "Цель задания" — куда можно отправить команду: объект карты (Шаг 5)
/// или миссия (Шаг 6). Команда едет к ApproachPoint, а когда приезжает —
/// цель получает вызов OnSquadArrived и сама решает, что делать дальше
/// (таймер захвата, награда), после чего отправляет команду домой.
/// </summary>
public interface ISquadTarget
{
    /// <summary>Название цели (для сообщений).</summary>
    string TargetName { get; }

    /// <summary>Точка, куда подъезжает команда.</summary>
    Vector3 ApproachPoint { get; }

    /// <summary>Можно ли отправить сюда эту команду. Если нет — reason объясняет почему.</summary>
    bool CanAccept(Squad squad, out string reason);

    /// <summary>Команда приехала.</summary>
    void OnSquadArrived(SquadUnit unit);
}
