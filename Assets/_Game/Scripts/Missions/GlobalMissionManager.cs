using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Задания фракции (бывшие общие миссии, "!" на карте). Задание выбирает сторона сама
/// (игрок — в тактической паузе, ИИ — тоже в паузе), и оно появляется на своём месте карты.
/// Видят его обе стороны, но каждая — под своим названием (GlobalMissionData.TitleFor);
/// соперник может приехать помешать (битва за флаг).
/// У каждой стороны одновременно — не больше одного своего задания; одно и то же задание дважды не идёт.
/// Все настройки — в инспекторе (объект Managers).
/// </summary>
public class GlobalMissionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static GlobalMissionManager Instance { get; private set; }

    [Header("Задания фракции")]
    [Tooltip("Все задания фракции (файлы GlobalMissionData)")]
    [SerializeField] private GlobalMissionData[] pool;
    [Tooltip("Префаб круга с '!' (Assets/_Game/Prefabs/GlobalMissionMarker)")]
    [SerializeField] private GlobalMission markerPrefab;

    [Header("Отладка")]
    [Tooltip("Клавиша M — сразу начать задание фракции игрока (для проверки)")]
    [SerializeField] private bool debugCheats = true;

    private readonly List<GlobalMission> active = new List<GlobalMission>(); // Задания на карте

    /// <summary>Задания фракции на карте сейчас.</summary>
    public IReadOnlyList<GlobalMission> Active => active;

    /// <summary>Все задания фракции, которые можно начать.</summary>
    public IReadOnlyList<GlobalMissionData> Pool => pool ?? new GlobalMissionData[0];

    /// <summary>Задание игрока или врага изменилось (началось / выполнено).</summary>
    public event System.Action Changed;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отладка: клавиша M — начать первое доступное задание игрока.</summary>
    private void Update()
    {
        if (!debugCheats || !Input.GetKeyDown(KeyCode.M)) return;
        foreach (GlobalMissionData d in Pool)
            if (CanLaunch(Team.Player, d, out _)) { Launch(Team.Player, d); return; }
    }

    /// <summary>Идёт ли уже задание, начатое этой стороной.</summary>
    public bool HasLaunched(Team team)
    {
        foreach (GlobalMission m in active)
            if (m.LaunchedBy == team && !m.IsCompleted) return true;
        return false;
    }

    /// <summary>Задание этой стороны на карте (или null).</summary>
    public GlobalMission LaunchedBy(Team team)
    {
        foreach (GlobalMission m in active)
            if (m.LaunchedBy == team && !m.IsCompleted) return m;
        return null;
    }

    /// <summary>Идёт ли это задание (кем бы оно ни было начато).</summary>
    public bool IsActive(GlobalMissionData data)
    {
        foreach (GlobalMission m in active)
            if (m.Data == data && !m.IsCompleted) return true;
        return false;
    }

    /// <summary>Можно ли стороне начать задание. Если нельзя — reason объясняет почему.</summary>
    public bool CanLaunch(Team team, GlobalMissionData data, out string reason)
    {
        reason = null;
        if (markerPrefab == null || data == null) { reason = "Нет задания"; return false; }
        if (HasLaunched(team)) { reason = "Уже идёт ваше задание"; return false; }
        if (IsActive(data)) { reason = "Уже идёт"; return false; }
        return true;
    }

    /// <summary>Начать задание фракции стороны team: круг "!" появляется на месте задания.</summary>
    public GlobalMission Launch(Team team, GlobalMissionData data)
    {
        if (!CanLaunch(team, data, out string reason)) { if (team == Team.Player) ToastUI.Show(reason); return null; }
        Vector3 pos = new Vector3(data.mapPosition.x, data.mapPosition.y, 0f);
        GlobalMission m = Instantiate(markerPrefab, pos, Quaternion.identity, transform);
        m.name = "FactionMission_" + data.name;
        m.LaunchedBy = team;
        m.Init(data, this);
        active.Add(m);
        if (team == Team.Player)
            ToastUI.Show($"Задание фракции «{m.Title}» началось! Награда: {StripTags(data.GetRewardText())}");
        else
            ToastUI.Show($"Враг начал своё задание — для нас это «{m.Title}». Можно помешать!");
        Changed?.Invoke();
        return m;
    }

    /// <summary>Задание выполнили — убираем его.</summary>
    public void OnMissionCompleted(GlobalMission m)
    {
        if (!active.Remove(m)) return;
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnGlobalRemoved(m);
        Destroy(m.gameObject);
        Changed?.Invoke();
    }

    /// <summary>Убрать теги цвета из текста (для всплывающего сообщения).</summary>
    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("   ", ", ");
}
