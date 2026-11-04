using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общие миссии ("!" на карте) — случайное событие фазы миссий: не больше одной за фазу.
/// В начале фазы миссий с шансом spawnChance выбирается момент (spawnWindow секунд от начала фазы),
/// и в этот момент появляется случайная общая миссия из pool (на своём месте карты или на случайной улице).
/// Видят её обе стороны; выполнить может любая (если приедут обе — битва за флаг).
/// Победитель получает награду, миссия исчезает. Не выполнили до конца фазы — миссия исчезает.
/// Все настройки — в инспекторе (объект Managers).
/// </summary>
public class GlobalMissionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static GlobalMissionManager Instance { get; private set; }

    [Header("Общие миссии")]
    [Tooltip("Все общие миссии (файлы GlobalMissionData)")]
    [SerializeField] private GlobalMissionData[] pool;
    [Tooltip("Префаб круга с '!' (Assets/_Game/Prefabs/GlobalMissionMarker)")]
    [SerializeField] private GlobalMission markerPrefab;

    [Header("Появление (случайное событие фазы миссий)")]
    [Tooltip("Шанс, что в фазе миссий появится общая миссия (не больше одной за фазу)")]
    [SerializeField, Range(0f, 1f)] private float spawnChance = 0.75f;
    [Tooltip("Когда может появиться: секунд от начала фазы миссий (от и до)")]
    [SerializeField] private Vector2 spawnWindow = new Vector2(15f, 75f);

    [Header("Отладка")]
    [Tooltip("Клавиша M — сразу создать общую миссию (для проверки)")]
    [SerializeField] private bool debugCheats = true;

    private readonly List<GlobalMission> active = new List<GlobalMission>(); // Общие миссии на карте
    private float spawnAt = -1f;  // Через сколько секунд от начала фазы появится миссия (-1 — в этой фазе не будет)
    private GamePhase lastPhase = GamePhase.Draft; // Фаза в прошлом кадре (чтобы поймать её смену)

    /// <summary>Общие миссии на карте сейчас.</summary>
    public IReadOnlyList<GlobalMission> Active => active;

    /// <summary>Все общие миссии, которые могут появиться.</summary>
    public IReadOnlyList<GlobalMissionData> Pool => pool ?? new GlobalMissionData[0];

    /// <summary>Общая миссия появилась или исчезла.</summary>
    public event System.Action Changed;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Следим за фазами: началась фаза миссий — выбрать момент; закончилась — убрать невыполненную миссию.</summary>
    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.M)) Spawn();

        PhaseManager pm = PhaseManager.Instance;
        if (pm == null) return;
        if (pm.Phase != lastPhase)
        {
            if (pm.Phase == GamePhase.Missions)
                spawnAt = Random.value < spawnChance ? Random.Range(spawnWindow.x, spawnWindow.y) : -1f;
            else if (lastPhase == GamePhase.Missions)
            {
                spawnAt = -1f;
                ClearUnfinished(); // фаза миссий кончилась — невыполненная миссия исчезает
            }
            lastPhase = pm.Phase;
        }

        // Пора — появляется миссия (если ещё можно отправлять команды)
        if (pm.Phase == GamePhase.Missions && spawnAt >= 0f && pm.Duration - pm.TimeLeft >= spawnAt)
        {
            spawnAt = -1f;
            if (!pm.DispatchLocked) Spawn();
        }
    }

    /// <summary>Создать случайную общую миссию (которой ещё нет на карте).</summary>
    public GlobalMission Spawn()
    {
        if (markerPrefab == null) return null;
        var options = new List<GlobalMissionData>();
        foreach (GlobalMissionData d in Pool)
            if (d != null && !active.Exists(m => m.Data == d)) options.Add(d);
        if (options.Count == 0) return null;
        GlobalMissionData data = options[Random.Range(0, options.Count)];

        // Своё место на карте, а если не задано — свободная точка на улице
        Vector3 pos = new Vector3(data.mapPosition.x, data.mapPosition.y, 0f);
        if (data.mapPosition == Vector2.zero && (MissionManager.Instance == null || !MissionManager.Instance.TryFindFreePoint(out pos)))
            return null;

        GlobalMission m = Instantiate(markerPrefab, pos, Quaternion.identity, transform);
        m.name = "GlobalMission_" + data.name;
        m.Init(data, this);
        active.Add(m);
        ToastUI.Show($"Общая миссия «{m.Title}»! Награда: {StripTags(data.GetRewardText())}");
        Changed?.Invoke();
        return m;
    }

    /// <summary>Миссию выполнили — убираем её.</summary>
    public void OnMissionCompleted(GlobalMission m) => Remove(m);

    /// <summary>Убрать невыполненные миссии (за которые сейчас не идёт бой).</summary>
    private void ClearUnfinished()
    {
        foreach (GlobalMission m in new List<GlobalMission>(active))
            if (!m.IsUnderAttack) Remove(m);
    }

    /// <summary>Убрать миссию с карты.</summary>
    private void Remove(GlobalMission m)
    {
        if (!active.Remove(m)) return;
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnGlobalRemoved(m);
        Destroy(m.gameObject);
        Changed?.Invoke();
    }

    /// <summary>Убрать теги цвета из текста (для всплывающего сообщения).</summary>
    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("   ", ", ");
}
