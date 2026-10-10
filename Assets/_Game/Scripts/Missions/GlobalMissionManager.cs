using UnityEngine;

/// <summary>
/// Создаёт общие миссии ("!"), которые видят обе стороны. На карте одновременно — не больше одной.
/// Первая появляется через firstDelay секунд, следующая — через interval секунд после того,
/// как предыдущую выполнили. (Шаг 5: расписание будет связано с событиями.)
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

    [Header("Появление")]
    [Tooltip("Через сколько секунд после начала игры появится первая общая миссия")]
    [SerializeField] private float firstDelay = 150f;
    [Tooltip("Через сколько секунд после выполнения появится следующая")]
    [SerializeField] private float interval = 150f;

    [Header("Отладка")]
    [Tooltip("Клавиша M — сразу создать общую миссию (для проверки)")]
    [SerializeField] private bool debugCheats = true;

    private GlobalMission current;   // Миссия на карте (или null)
    private GlobalMissionData last;  // Прошлая миссия (чтобы не повторялась подряд)
    private float timer;             // Секунд до следующей

    /// <summary>Общая миссия на карте сейчас (или null).</summary>
    public GlobalMission Current => current;

    /// <summary>Сколько секунд до следующей общей миссии (0 — если миссия уже на карте).</summary>
    public float TimeToNext => current != null ? 0f : Mathf.Max(0f, timer);

    private void Awake()
    {
        Instance = this;
        timer = firstDelay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отсчёт до следующей миссии.</summary>
    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.M) && current == null) Spawn();

        if (current != null) return;
        timer -= WorldTime.DeltaTime;
        if (timer <= 0f) Spawn();
    }

    /// <summary>Создать случайную общую миссию в свободном месте карты.</summary>
    public GlobalMission Spawn()
    {
        timer = interval;
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

    /// <summary>Миссию выполнили — убираем её и начинаем отсчёт до следующей.</summary>
    public void OnMissionCompleted(GlobalMission m)
    {
        if (m != current) return;
        current = null;
        timer = interval;
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnGlobalRemoved(m);
        Destroy(m.gameObject);
    }

    /// <summary>Убрать теги цвета из текста (для всплывающего сообщения).</summary>
    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("   ", ", ");
}
