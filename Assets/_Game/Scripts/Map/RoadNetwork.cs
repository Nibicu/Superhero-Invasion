using System.Collections.Generic;
using UnityEngine;

/// <summary>Отрезок дороги: горизонтальный (по X на высоте Y) или вертикальный (по Y на позиции X).</summary>
[System.Serializable]
public struct RoadLine
{
    [Tooltip("Для горизонтальной — высота Y, для вертикальной — позиция X")]
    public float at;
    [Tooltip("Начало дороги (X у горизонтальной, Y у вертикальной)")]
    public float from;
    [Tooltip("Конец дороги")]
    public float to;
}

/// <summary>
/// Сеть дорог карты: горизонтальные и вертикальные улицы.
/// Команды ездят по ней (SquadUnit.BuildPath): от точки — к ближайшей горизонтальной улице,
/// по ней — до вертикальной улицы, которая ведёт к улице цели, потом к цели.
/// Стоит на объекте Map, данные заполняются вместе с картой.
/// </summary>
public class RoadNetwork : MonoBehaviour
{
    /// <summary>Сеть дорог на карте (или null — тогда команды ездят по главной дороге y = 0).</summary>
    public static RoadNetwork Instance { get; private set; }

    [Tooltip("Горизонтальные улицы (главная — y = 0)")]
    [SerializeField] private RoadLine[] horizontal = new RoadLine[0];
    [Tooltip("Вертикальные улицы (соединяют горизонтальные)")]
    [SerializeField] private RoadLine[] vertical = new RoadLine[0];

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Записать дороги (вызывает построитель карты в редакторе).</summary>
    public void SetRoads(RoadLine[] h, RoadLine[] v)
    {
        horizontal = h;
        vertical = v;
    }

    /// <summary>Ближайшая к точке горизонтальная улица, которая проходит над/под этой точкой по X.</summary>
    private RoadLine NearestHorizontal(Vector3 p)
    {
        RoadLine best = horizontal[0];
        float bestD = float.MaxValue;
        foreach (RoadLine h in horizontal)
        {
            float d = Mathf.Abs(p.y - h.at);
            if (p.x < h.from - 0.01f || p.x > h.to + 0.01f) d += 1000f; // улица не доходит до этого X
            if (d < bestD) { bestD = d; best = h; }
        }
        return best;
    }

    /// <summary>
    /// Маршрут по улицам от from до to (точки, через которые ехать, включая to).
    /// </summary>
    public List<Vector3> BuildPath(Vector3 from, Vector3 to)
    {
        var path = new List<Vector3>();
        if (horizontal == null || horizontal.Length == 0)
        {
            path.Add(new Vector3(from.x, 0f, 0f));
            path.Add(new Vector3(to.x, 0f, 0f));
            path.Add(to);
            return path;
        }

        RoadLine ha = NearestHorizontal(from), hb = NearestHorizontal(to);
        path.Add(new Vector3(from.x, ha.at, 0f));
        if (!Mathf.Approximately(ha.at, hb.at))
        {
            // Вертикальная улица, соединяющая обе горизонтальные, с самым коротким объездом
            float bestX = from.x, bestCost = float.MaxValue;
            float lo = Mathf.Min(ha.at, hb.at), hi = Mathf.Max(ha.at, hb.at);
            foreach (RoadLine v in vertical)
            {
                if (v.from > lo + 0.01f || v.to < hi - 0.01f) continue;
                float cost = Mathf.Abs(from.x - v.at) + Mathf.Abs(to.x - v.at);
                if (cost < bestCost) { bestCost = cost; bestX = v.at; }
            }
            path.Add(new Vector3(bestX, ha.at, 0f));
            path.Add(new Vector3(bestX, hb.at, 0f));
        }
        path.Add(new Vector3(to.x, hb.at, 0f));
        path.Add(to);
        return path;
    }

    /// <summary>Длина маршрута по улицам (для оценки времени в пути).</summary>
    public float PathLength(Vector3 from, Vector3 to)
    {
        float len = 0f;
        Vector3 prev = from;
        foreach (Vector3 p in BuildPath(from, to)) { len += Vector3.Distance(prev, p); prev = p; }
        return len;
    }
}
