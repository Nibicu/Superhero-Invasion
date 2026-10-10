using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Вид события.</summary>
public enum GameEventKind
{
    Object, // На карте открывается случайный объект (Банк, Завод...) — за него можно бороться
    Portal  // В центре карты открывается портал — его нужно закрыть
}

/// <summary>
/// Расписание событий (объект Managers). Игра идёт по кругу:
///  затишье (quietTime, 5 мин) → событие (eventDuration, 5 мин) → затишье → ...
///  Посередине затишья (через 2:30) появляется общая миссия (GlobalMissionManager).
/// События идут по порядку из списка order: "объект, объект, портал" и снова сначала.
/// Событие "Объект": открывается случайный неактивный объект; по окончании последний
///  владелец получает артефакт, а объект снова ничей и неактивен.
/// Событие "Портал": открывается портал; закрыл один — урон базе другого (это делает сам Portal);
///  никто не закрыл — урон обеим базам.
/// Если время вышло, а за объект/портал ещё идёт бой — событие ждёт итога боя.
/// </summary>
public class EventManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static EventManager Instance { get; private set; }

    [Header("Расписание")]
    [Tooltip("Порядок событий (повторяется по кругу)")]
    [SerializeField] private GameEventKind[] order = { GameEventKind.Object, GameEventKind.Object, GameEventKind.Portal };
    [Tooltip("Сколько секунд затишья перед первым событием")]
    [SerializeField] private float firstDelay = 300f;
    [Tooltip("Сколько секунд затишья между событиями")]
    [SerializeField] private float quietTime = 300f;
    [Tooltip("Сколько секунд длится событие")]
    [SerializeField] private float eventDuration = 300f;
    [Tooltip("Создавать общую миссию посередине затишья")]
    [SerializeField] private bool spawnGlobalMission = true;

    [Header("Портал")]
    [Tooltip("Портал в центре карты (объект Portal в сцене)")]
    [SerializeField] private Portal portal;

    [Header("Отладка")]
    [Tooltip("Клавиша E — сразу начать следующее событие (или закончить текущее)")]
    [SerializeField] private bool debugCheats = true;

    private bool running;           // Идёт событие
    private float timer;            // Секунд до события (затишье) или до конца события
    private float quietLength;      // Длина текущего затишья (для полоски)
    private int index;              // Какое событие из order следующее/текущее
    private bool globalSpawned;     // Общая миссия в этом затишье уже была
    private MapObject eventObject;  // Объект текущего события
    private GameEventKind current;  // Вид текущего события

    /// <summary>Событие началось или закончилось.</summary>
    public event Action Changed;

    /// <summary>Идёт ли сейчас событие.</summary>
    public bool IsRunning => running;

    /// <summary>Сколько секунд до события (затишье) или до его конца (событие).</summary>
    public float TimeLeft => Mathf.Max(0f, timer);

    /// <summary>Доля прошедшего времени (затишья или события) 0..1 — для полоски.</summary>
    public float Progress => 1f - Mathf.Clamp01(timer / (running ? eventDuration : quietLength));

    /// <summary>Вид текущего (если идёт) или следующего события.</summary>
    public GameEventKind Kind => running ? current : order[index % order.Length];

    /// <summary>Объект текущего события (или null).</summary>
    public MapObject EventObject => running && current == GameEventKind.Object ? eventObject : null;

    /// <summary>Портал на карте.</summary>
    public Portal Portal => portal;

    private void Awake()
    {
        Instance = this;
        timer = quietLength = firstDelay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отсчёт затишья и событий.</summary>
    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.E)) SkipTimer();

        timer -= WorldTime.DeltaTime;

        if (!running)
        {
            // Посередине затишья — общая миссия
            if (spawnGlobalMission && !globalSpawned && timer <= quietLength / 2f)
            {
                globalSpawned = true;
                if (GlobalMissionManager.Instance != null && GlobalMissionManager.Instance.Current == null)
                    GlobalMissionManager.Instance.Spawn();
            }
            if (timer <= 0f) StartEvent();
            return;
        }

        UpdateEvent();
    }

    /// <summary>Отладка: затишье — сразу начать событие; событие — сразу закончить (дождётся конца боя).</summary>
    public void SkipTimer()
    {
        if (running) timer = 0f;
        else StartEvent();
    }

    // ---------- Начало события ----------

    /// <summary>Начать следующее событие по порядку.</summary>
    private void StartEvent()
    {
        GameEventKind kind = order[index % order.Length];
        index++;

        if (kind == GameEventKind.Object)
        {
            eventObject = PickObject();
            if (eventObject == null) { BeginQuiet(); return; } // все объекты заняты — пропускаем
            current = kind;
            running = true;
            timer = eventDuration;
            eventObject.EventTimeLeft = eventDuration;
            eventObject.Activate();
            ToastUI.Show($"СОБЫТИЕ: открыт объект «{eventObject.Data.displayName}»! {Mathf.RoundToInt(eventDuration / 60f)} мин на захват — последний владелец получит артефакт");
        }
        else
        {
            if (portal == null) { BeginQuiet(); return; }
            current = kind;
            running = true;
            timer = eventDuration;
            portal.Open(eventDuration);
            ToastUI.Show($"СОБЫТИЕ: открылся портал! Закройте его за {Mathf.RoundToInt(eventDuration / 60f)} мин, иначе база получит {portal.Data.baseDamage} урона");
        }
        Changed?.Invoke();
    }

    /// <summary>Случайный неактивный объект, на который сейчас никто не нападает.</summary>
    private static MapObject PickObject()
    {
        var options = new List<MapObject>();
        foreach (MapObject o in MapObject.All)
            if (!o.IsActive && !o.IsUnderAttack) options.Add(o);
        return options.Count > 0 ? options[UnityEngine.Random.Range(0, options.Count)] : null;
    }

    // ---------- Ход события ----------

    /// <summary>Таймер события; конец — когда время вышло и бой за цель закончился (или портал закрыт).</summary>
    private void UpdateEvent()
    {
        if (current == GameEventKind.Object)
        {
            eventObject.EventTimeLeft = Mathf.Max(0f, timer);
            if (timer <= 0f && !eventObject.IsUnderAttack) EndObjectEvent();
        }
        else
        {
            portal.TimeLeft = Mathf.Max(0f, timer);
            if (portal.IsClosed && !portal.IsUnderAttack) EndPortalEvent();
            else if (timer <= 0f && !portal.IsUnderAttack) EndPortalEvent();
        }
    }

    /// <summary>Конец события "Объект": артефакт последнему владельцу, объект снова неактивен.</summary>
    private void EndObjectEvent()
    {
        MapObject o = eventObject;
        eventObject = null;
        string name = o.Data.displayName;
        if (o.Deactivate(out Team owner))
        {
            ArtifactData a = ArtifactManager.Instance != null ? ArtifactManager.Instance.GiveRandom(owner) : null;
            string art = a != null ? $"<color=#FF9EE6>артефакт «{a.displayName}»</color>" : "артефакт";
            if (owner == Team.Player)
                BattleResultWindowUI.Show(true, $"Событие окончено!\n«{name}» был нашим до конца — получен {art}.\n<size=80%>Объект снова неактивен.</size>");
            else
                BattleResultWindowUI.Show(false, $"Событие окончено.\n«{name}» остался за врагом — враг получил артефакт.\n<size=80%>Объект снова неактивен.</size>");
        }
        else ToastUI.Show($"Событие окончено: «{name}» никто не захватил. Объект снова неактивен");
        BeginQuiet();
    }

    /// <summary>Конец события "Портал": если никто не закрыл — урон обеим базам.</summary>
    private void EndPortalEvent()
    {
        if (!portal.IsClosed)
        {
            int dmg = portal.Data.baseDamage;
            foreach (Team t in new[] { Team.Player, Team.Enemy })
            {
                MainBase b = MainBase.Get(t);
                if (b != null) b.TakeDamage(dmg, "Портал не закрыт");
            }
            BattleResultWindowUI.Show(false, $"Портал никто не закрыл!\nОбе базы получили {dmg} урона.");
        }
        portal.Hide();
        BeginQuiet();
    }

    /// <summary>Начать затишье до следующего события.</summary>
    private void BeginQuiet()
    {
        running = false;
        timer = quietLength = quietTime;
        globalSpawned = false;
        Changed?.Invoke();
    }

    /// <summary>Название события для интерфейса.</summary>
    public string KindName(GameEventKind kind) => kind == GameEventKind.Portal ? "Портал" : "Объект";
}
