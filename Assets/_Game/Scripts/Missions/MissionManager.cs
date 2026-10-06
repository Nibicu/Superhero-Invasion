using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Создаёт миссии ("?") в случайных местах карты через случайные промежутки времени.
/// Миссии не появляются поверх баз, объектов и друг друга.
/// Все настройки — в инспекторе (объект Managers).
/// </summary>
public class MissionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static MissionManager Instance { get; private set; }

    [Header("Миссии")]
    [Tooltip("Все возможные миссии (файлы MissionData)")]
    [SerializeField] private MissionData[] missionPool;
    [Tooltip("Префаб круга с '?' (Assets/_Game/Prefabs/MissionMarker)")]
    [SerializeField] private MissionMarker markerPrefab;

    [Header("Появление")]
    [Tooltip("Через сколько секунд после начала игры появится первая миссия")]
    [SerializeField] private float firstDelay = 8f;
    [Tooltip("Минимальный промежуток между миссиями (сек)")]
    [SerializeField] private float minInterval = 20f;
    [Tooltip("Максимальный промежуток между миссиями (сек)")]
    [SerializeField] private float maxInterval = 40f;
    [Tooltip("Сколько миссий может быть на карте одновременно")]
    [SerializeField] private int maxActive = 3;

    [Header("Где появляются")]
    [Tooltip("Левый нижний угол области появления")]
    [SerializeField] private Vector2 areaMin = new Vector2(-11.5f, -5.3f);
    [Tooltip("Правый верхний угол области появления")]
    [SerializeField] private Vector2 areaMax = new Vector2(11.5f, 7.6f);
    [Tooltip("Не ближе этого к главной дороге (y = 0)")]
    [SerializeField] private float minDistanceFromRoad = 1.8f;
    [Tooltip("Не ближе этого к объектам карты и другим миссиям")]
    [SerializeField] private float minDistanceFromObjects = 3f;

    private readonly List<MissionMarker> active = new List<MissionMarker>();     // Миссии на карте
    private readonly HashSet<MissionData> completedOnce = new HashSet<MissionData>(); // Выполненные одноразовые миссии
    private float timer; // Секунд до следующей миссии

    /// <summary>Миссии на карте сейчас.</summary>
    public IReadOnlyList<MissionMarker> Active => active;

    private void Awake()
    {
        Instance = this;
        timer = firstDelay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отсчитываем время до следующей миссии.</summary>
    private void Update()
    {
        timer -= WorldTime.DeltaTime;
        if (timer > 0f) return;
        timer = Random.Range(minInterval, maxInterval);
        if (active.Count < maxActive) SpawnMission();
    }

    /// <summary>Создать случайную миссию в случайном свободном месте.</summary>
    public MissionMarker SpawnMission()
    {
        MissionData data = PickMission();
        if (data == null || markerPrefab == null) return null;
        if (!FindFreePoint(out Vector3 pos)) return null;

        MissionMarker m = Instantiate(markerPrefab, pos, Quaternion.identity, transform);
        m.name = "Mission_" + data.title;
        m.Init(data, this);
        active.Add(m);
        return m;
    }

    /// <summary>Убрать миссию с карты (вызывает сама миссия: выполнена, провалена или истекла).</summary>
    public void RemoveMission(MissionMarker m, bool success)
    {
        if (success && m.Data.oneTime) completedOnce.Add(m.Data);
        active.Remove(m);
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnMissionRemoved(m);
        Destroy(m.gameObject);
    }

    /// <summary>Выбрать миссию: не одноразовую уже выполненную и не такую же, как уже висит на карте.</summary>
    private MissionData PickMission()
    {
        var options = new List<MissionData>();
        if (missionPool != null)
            foreach (MissionData d in missionPool)
            {
                if (d == null || completedOnce.Contains(d)) continue;
                if (active.Exists(m => m.Data == d)) continue;
                options.Add(d);
            }
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }

    /// <summary>Найти точку, далёкую от дороги, объектов, баз и других миссий.</summary>
    private bool FindFreePoint(out Vector3 pos)
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

    /// <summary>Есть ли рядом объект, база или другая миссия.</summary>
    private bool IsNear(Vector3 pos)
    {
        foreach (MapObject o in MapObject.All)
            if (Vector2.Distance(o.transform.position, pos) < minDistanceFromObjects) return true;
        foreach (MissionMarker m in active)
            if (Vector2.Distance(m.transform.position, pos) < minDistanceFromObjects) return true;
        foreach (Team t in new[] { Team.Player, Team.Enemy })
        {
            MainBase b = MainBase.Get(t);
            if (b != null && Vector2.Distance(b.transform.position, pos) < 4f) return true;
        }
        return false;
    }
}
