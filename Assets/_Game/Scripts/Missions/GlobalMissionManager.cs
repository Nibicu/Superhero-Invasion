using UnityEngine;

/// <summary>
/// Создаёт общие миссии ("!"), которые видят обе стороны. На карте одновременно — не больше одной.
/// Когда появляется миссия, решает расписание событий (EventManager): посередине затишья
/// между событиями. Во время события новая общая миссия не появляется.
/// Все настройки — в инспекторе (объект Managers).
/// </summary>
public class GlobalMissionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static GlobalMissionManager Instance { get; private set; }

    [Header("Общие миссии")]
    [Tooltip("Все возможные общие миссии (файлы GlobalMissionData)")]
    [SerializeField] private GlobalMissionData[] pool;
    [Tooltip("Префаб круга с '!' (Assets/_Game/Prefabs/GlobalMissionMarker)")]
    [SerializeField] private GlobalMission markerPrefab;

    [Header("Отладка")]
    [Tooltip("Клавиша M — сразу создать общую миссию (для проверки)")]
    [SerializeField] private bool debugCheats = true;

    private GlobalMission current;   // Миссия на карте (или null)
    private GlobalMissionData last;  // Прошлая миссия (чтобы не повторялась подряд)

    /// <summary>Общая миссия на карте сейчас (или null).</summary>
    public GlobalMission Current => current;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отладка: клавиша M — общая миссия сразу.</summary>
    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.M) && current == null) Spawn();
    }

    /// <summary>Создать случайную общую миссию в свободном месте карты.</summary>
    public GlobalMission Spawn()
    {
        if (current != null || markerPrefab == null || pool == null || pool.Length == 0) return null;
        if (MissionManager.Instance == null || !MissionManager.Instance.TryFindFreePoint(out Vector3 pos)) return null;

        GlobalMissionData data = pool[Random.Range(0, pool.Length)];
        if (data == last && pool.Length > 1) data = pool[(System.Array.IndexOf(pool, data) + 1) % pool.Length];
        last = data;

        current = Instantiate(markerPrefab, pos, Quaternion.identity, transform);
        current.name = "GlobalMission_" + data.title;
        current.Init(data, this);
        ToastUI.Show($"Общая миссия «{data.title}»! Её видят обе стороны. Награда: {StripTags(data.GetRewardText())}");
        return current;
    }

    /// <summary>Миссию выполнили — убираем её (следующая — в следующем затишье).</summary>
    public void OnMissionCompleted(GlobalMission m)
    {
        if (m != current) return;
        current = null;
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnGlobalRemoved(m);
        Destroy(m.gameObject);
    }

    /// <summary>Убрать теги цвета из текста (для всплывающего сообщения).</summary>
    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("   ", ", ");
}
