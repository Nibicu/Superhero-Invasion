using System;
using UnityEngine;

/// <summary>Фаза игры.</summary>
public enum GamePhase
{
    Draft,    // Драфт перед игрой (покупка героев)
    Missions, // Фаза миссий: обычные миссии и задания фракции
    Pause,    // Тактическая пауза между фазами (игра стоит)
    Event     // Фаза события: закрытие портала или битва за объект
}

/// <summary>
/// Фазы игры (объект Managers): Драфт → Миссии (2 мин) → тактическая пауза (2 мин) → Событие (2 мин) →
/// пауза → Миссии → ... по кругу.
/// В тактической паузе карта стоит: можно покупать героев (только в паузе и на драфте), надевать вещи,
/// прокачивать героев, выбирать задание фракции. Кнопка "ГОТОВ" заканчивает паузу раньше.
/// Событие (портал или объект) запускает EventManager; фаза события кончается, когда вышло время
/// и бой за цель события закончился.
/// </summary>
[DefaultExecutionOrder(-50)]
public class PhaseManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static PhaseManager Instance { get; private set; }

    [Header("Длительность фаз (секунды)")]
    [Tooltip("Фаза миссий")]
    [SerializeField] private float missionsDuration = 120f;
    [Tooltip("Фаза события")]
    [SerializeField] private float eventDuration = 120f;
    [Tooltip("Тактическая пауза между фазами")]
    [SerializeField] private float pauseDuration = 120f;

    [Tooltip("За сколько секунд до конца фазы нельзя отправлять команды (защищаться можно всегда)")]
    [SerializeField] private float dispatchLockTime = 20f;

    [Header("Отладка")]
    [Tooltip("Клавиша E — сразу закончить текущую фазу")]
    [SerializeField] private bool debugCheats = true;

    private bool eventStarted; // В фазе события событие действительно началось (был свободный объект / портал)

    /// <summary>Текущая фаза.</summary>
    public GamePhase Phase { get; private set; } = GamePhase.Draft;

    /// <summary>Какая фаза будет после тактической паузы.</summary>
    public GamePhase NextPhase { get; private set; } = GamePhase.Missions;

    /// <summary>Сколько секунд осталось до конца фазы.</summary>
    public float TimeLeft { get; private set; }

    /// <summary>Длина текущей фазы (для полоски).</summary>
    public float Duration { get; private set; } = 1f;

    /// <summary>Доля прошедшего времени фазы 0..1.</summary>
    public float Progress => 1f - Mathf.Clamp01(TimeLeft / Mathf.Max(1f, Duration));

    /// <summary>Можно ли сейчас нанимать героев (драфт или тактическая пауза).</summary>
    public bool CanHire => Phase == GamePhase.Draft || Phase == GamePhase.Pause;

    /// <summary>Строить и улучшать базу можно только в тактической паузе (и на драфте).</summary>
    public bool CanBuild => Phase == GamePhase.Draft || Phase == GamePhase.Pause;

    /// <summary>
    /// Время фазы вышло, но команды ещё не вернулись: фаза ждёт, пока все герои и злодеи будут дома.
    /// </summary>
    public bool WaitingForSquads { get; private set; }

    /// <summary>Нельзя отправлять команды: до конца фазы меньше 20 с (или фаза уже ждёт возвращения команд).</summary>
    public bool DispatchLocked => (Phase == GamePhase.Missions || Phase == GamePhase.Event) && TimeLeft <= dispatchLockTime
                                  || Phase == GamePhase.Pause || Phase == GamePhase.Draft;

    /// <summary>Фаза сменилась.</summary>
    public event Action Changed;

    /// <summary>Запомнить себя.</summary>
    private void Awake() => Instance = this;

    /// <summary>Убрать ссылку.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Отсчёт фаз.</summary>
    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.E)) Skip();

        switch (Phase)
        {
            case GamePhase.Draft:
                if (!DraftUI.IsRunning) BeginMissions(); // драфт закончился (или выключен)
                break;
            case GamePhase.Missions:
                TimeLeft = Mathf.Max(0f, TimeLeft - WorldTime.DeltaTime);
                WaitingForSquads = TimeLeft <= 0f && !AllHome();
                if (TimeLeft <= 0f && !WaitingForSquads) BeginPause(GamePhase.Event);
                break;
            case GamePhase.Event:
                TimeLeft = Mathf.Max(0f, TimeLeft - WorldTime.DeltaTime);
                bool eventOver = !eventStarted || EventManager.Instance == null || !EventManager.Instance.IsRunning;
                WaitingForSquads = TimeLeft <= 0f && (!eventOver || !AllHome());
                if (TimeLeft <= 0f && !WaitingForSquads) BeginPause(GamePhase.Missions);
                break;
            case GamePhase.Pause:
                TimeLeft -= Time.unscaledDeltaTime;
                if (TimeLeft <= 0f) EndPause();
                break;
        }
    }

    /// <summary>Все команды обеих сторон дома и нигде не идёт нападение.</summary>
    private static bool AllHome()
    {
        SquadManager sm = SquadManager.Instance;
        if (sm != null)
            foreach (Team t in new[] { Team.Player, Team.Enemy })
                foreach (Squad s in sm.GetSquads(t))
                    if (s.Status != SquadStatus.AtBase) return false;
        return !MainBase.AnyUnderAttack(out _);
    }

    /// <summary>Начать фазу миссий.</summary>
    private void BeginMissions()
    {
        if (Phase != GamePhase.Draft && HeroManager.Instance != null) HeroManager.Instance.OnPhaseStarted();
        Phase = GamePhase.Missions;
        TimeLeft = Duration = missionsDuration;
        ToastUI.Show($"ФАЗА МИССИЙ — {Mathf.RoundToInt(missionsDuration / 60f)} мин. Выполняйте миссии и задания фракции");
        Changed?.Invoke();
    }

    /// <summary>Начать фазу события (портал или объект — по очереди).</summary>
    private void BeginEvent()
    {
        if (HeroManager.Instance != null) HeroManager.Instance.OnPhaseStarted();
        Phase = GamePhase.Event;
        TimeLeft = Duration = eventDuration;
        eventStarted = EventManager.Instance != null && EventManager.Instance.StartNext(eventDuration);
        if (!eventStarted) ToastUI.Show("ФАЗА СОБЫТИЯ — свободных целей для события нет");
        Changed?.Invoke();
    }

    /// <summary>Тактическая пауза: карта стоит, ИИ делает свои покупки, открывается окно паузы.</summary>
    private void BeginPause(GamePhase next)
    {
        WaitingForSquads = false;
        Phase = GamePhase.Pause;
        NextPhase = next;
        TimeLeft = Duration = pauseDuration;
        WorldTime.Paused = true;
        EnemyAI ai = FindAnyObjectByType<EnemyAI>();
        if (ai != null) ai.OnTacticalPause();
        if (TacticalPauseUI.Instance != null) TacticalPauseUI.Instance.Show();
        Changed?.Invoke();
    }

    /// <summary>Конец паузы: карта оживает, начинается следующая фаза.</summary>
    private void EndPause()
    {
        if (Phase != GamePhase.Pause) return;
        WorldTime.Paused = false;
        if (TacticalPauseUI.Instance != null) TacticalPauseUI.Instance.Hide();
        if (GlobalMissionManager.Instance != null) GlobalMissionManager.Instance.OnPauseEnded(); // "перезарядка" заданий фракции
        if (NextPhase == GamePhase.Event) BeginEvent();
        else BeginMissions();
    }

    /// <summary>Кнопка "ГОТОВ" в тактической паузе.</summary>
    public void Ready() => EndPause();

    /// <summary>Отладка: сразу закончить текущую фазу.</summary>
    public void Skip()
    {
        switch (Phase)
        {
            case GamePhase.Missions: TimeLeft = 0f; break;
            case GamePhase.Event:
                TimeLeft = 0f;
                if (EventManager.Instance != null && EventManager.Instance.IsRunning) EventManager.Instance.SkipTimer();
                break;
            case GamePhase.Pause: EndPause(); break;
        }
    }

    /// <summary>Название фазы для интерфейса.</summary>
    public static string PhaseName(GamePhase p)
    {
        switch (p)
        {
            case GamePhase.Draft: return "Драфт";
            case GamePhase.Missions: return "Миссии";
            case GamePhase.Pause: return "Тактическая пауза";
            default: return "Событие";
        }
    }
}
