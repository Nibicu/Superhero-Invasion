using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Создаёт миссии ("?") в случайных местах карты.
/// У каждой стороны свои миссии: миссии героев видит и выполняет только игрок,
/// миссии злодеев — только враг (игроку они не видны). Каждые interval секунд
/// каждой стороне приходит новая миссия (если их на карте меньше maxActivePerSide).
/// Миссии не появляются поверх баз, объектов, общих миссий и друг друга.
/// Все настройки — в инспекторе (объект Managers).
/// </summary>
public class MissionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static MissionManager Instance { get; private set; }

    [Header("Миссии")]
    [Tooltip("Все возможные миссии героев и злодеев (файлы MissionData, у каждой указано поле Side)")]
    [SerializeField] private MissionData[] missionPool;
    [Tooltip("Префаб круга с '?' (Assets/_Game/Prefabs/MissionMarker)")]
    [SerializeField] private MissionMarker markerPrefab;

    [Header("Появление")]
    [Tooltip("Через сколько секунд после начала игры появится первая миссия")]
    [SerializeField] private float firstDelay = 8f;
    [Tooltip("Раз в сколько секунд каждой стороне приходит новая миссия")]
    [SerializeField] private float interval = 30f;
    [Tooltip("Сколько миссий одной стороны может быть на карте одновременно")]
    [SerializeField] private int maxActivePerSide = 3;

    [Header("Где появляются")]
    [Tooltip("Левый нижний угол области появления")]
    [SerializeField] private Vector2 areaMin = new Vector2(-11.5f, -5.3f);
    [Tooltip("Правый верхний угол области появления")]
    [SerializeField] private Vector2 areaMax = new Vector2(11.5f, 7.6f);
    [Tooltip("Не ближе этого к главной дороге (y = 0)")]
    [SerializeField] private float minDistanceFromRoad = 1.8f;
    [Tooltip("Не ближе этого к объектам карты и другим миссиям")]
    [SerializeField] private float minDistanceFromObjects = 3f;

    private readonly List<MissionMarker> active = new List<MissionMarker>(); // Миссии на карте (обеих сторон)
    private readonly HashSet<(Team, MissionData)> completedOnce = new HashSet<(Team, MissionData)>(); // Выполненные одноразовые миссии (по сторонам)
    private readonly Dictionary<Team, float> timers = new Dictionary<Team, float>(); // Секунд до следующей миссии каждой стороны

    /// <summary>Миссии на карте сейчас (обеих сторон).</summary>
    public IReadOnlyList<MissionMarker> Active => active;

    private void Awake()
    {
        Instance = this;
        timers[Team.Player] = firstDelay;
        timers[Team.Enemy] = firstDelay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отсчитываем время до следующей миссии каждой стороны.</summary>
    private void Update()
    {
        foreach (Team t in new[] { Team.Player, Team.Enemy })
        {
            timers[t] -= WorldTime.DeltaTime;
            if (timers[t] > 0f) continue;
            timers[t] = interval;
            if (CountActive(t) < maxActivePerSide) SpawnMission(t);
        }
    }

    /// <summary>Сколько миссий стороны сейчас на карте.</summary>
    public int CountActive(Team side)
    {
        int n = 0;
        foreach (MissionMarker m in active)
            if (m.Side == side) n++;
        return n;
    }

    /// <summary>Создать случайную миссию стороны side в случайном свободном месте.</summary>
    public MissionMarker SpawnMission(Team side)
    {
        MissionData data = PickMission(side);
        if (data == null || markerPrefab == null) return null;
        if (!TryFindFreePoint(out Vector3 pos)) return null;

        MissionMarker m = Instantiate(markerPrefab, pos, Quaternion.identity, transform);
        m.name = (side == Team.Player ? "Mission_" : "EnemyMission_") + data.title;
        m.Init(data, this, side);
        active.Add(m);
        return m;
    }

    /// <summary>Убрать миссию с карты (вызывает сама миссия: выполнена, провалена или истекла).</summary>
    public void RemoveMission(MissionMarker m, bool success)
    {
        if (success && m.Data.oneTime) completedOnce.Add((m.Side, m.Data));
        active.Remove(m);
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnMissionRemoved(m);
        Destroy(m.gameObject);
    }

    /// <summary>
    /// Выбрать миссию стороны: подходящую ей по полю Side, не одноразовую уже выполненную
    /// и не такую же, как уже висит на карте.
    /// </summary>
    private MissionData PickMission(Team side)
    {
        var options = new List<MissionData>();
        if (missionPool != null)
            foreach (MissionData d in missionPool)
            {
                if (d == null || d.SideTeam != side || completedOnce.Contains((side, d))) continue;
                if (active.Exists(m => m.Data == d)) continue;
                options.Add(d);
            }
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }

    /// <summary>Найти точку, далёкую от дороги, объектов, баз и других миссий (её берёт и общая миссия).</summary>
    public bool TryFindFreePoint(out Vector3 pos)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            pos = new Vector3(Random.Range(areaMin.x, areaMax.x), Random.Range(areaMin.y, areaMax.y), 0f);
            if (Mathf.Abs(pos.y) < minDistanceFromRoad) continue;
            if (IsNear(pos)) continue;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    /// <summary>Есть ли рядом объект, база, миссия или общая миссия.</summary>
    private bool IsNear(Vector3 pos)
    {
        foreach (MapObject o in MapObject.All)
            if (Vector2.Distance(o.transform.position, pos) < minDistanceFromObjects) return true;
        foreach (MissionMarker m in active)
            if (Vector2.Distance(m.transform.position, pos) < minDistanceFromObjects) return true;
        GlobalMission g = GlobalMissionManager.Instance != null ? GlobalMissionManager.Instance.Current : null;
        if (g != null && Vector2.Distance(g.transform.position, pos) < minDistanceFromObjects + 1f) return true;
        foreach (Team t in new[] { Team.Player, Team.Enemy })
        {
            MainBase b = MainBase.Get(t);
            if (b != null && Vector2.Distance(b.transform.position, pos) < 4f) return true;
        }
        return false;
    }
}
