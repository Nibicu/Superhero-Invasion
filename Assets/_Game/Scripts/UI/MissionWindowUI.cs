using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно миссии: название, описание, время выполнения, сколько миссия ещё провисит,
/// и (только с Радаром) рекомендуемая сила и награда.
/// Кнопка "ОТПРАВИТЬ КОМАНДУ" открывает список команд.
/// Это же окно показывает общую миссию ("!"): описание, награду, охрану (с Радаром).
/// </summary>
public class MissionWindowUI : WindowUI
{
    /// <summary>Единственное окно миссии в сцене.</summary>
    public static MissionWindowUI Instance { get; private set; }

    [Header("Миссия")]
    [SerializeField] private Image header;          // Полоса заголовка (цвет миссии)
    [SerializeField] private TMP_Text titleText;    // Название
    [SerializeField] private TMP_Text headerIcon;   // Знак в заголовке: "?" — миссия, "!" — общая миссия
    [SerializeField] private TMP_Text descText;     // Описание
    [SerializeField] private TMP_Text detailsText;  // Сила и награда (с Радаром) или "???"
    [SerializeField] private TMP_Text statusText;   // Таймер / прогресс
    [SerializeField] private Button sendButton;     // "ОТПРАВИТЬ КОМАНДУ"
    [SerializeField] private TMP_Text sendText;
    [SerializeField] private SquadPickerUI picker;  // Окно выбора команды

    private MissionMarker current;        // Какая миссия показана (или null)
    private GlobalMission currentGlobal;  // Какая общая миссия показана (или null)
    private Portal currentPortal;         // Показан портал (или null)
    private float portalRefresh;          // Когда перерисовать таймер портала

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        sendButton.onClick.AddListener(OnSendClicked);
        AttackableSite.AttackChanged += OnAttackChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        AttackableSite.AttackChanged -= OnAttackChanged;
    }

    /// <summary>На общую миссию или портал напали — окно закрывается до конца боя.</summary>
    private void OnAttackChanged(AttackableSite site)
    {
        if (!IsOpen || site == null || (site != currentGlobal && site != currentPortal)) return;
        if (!site.IsUnderAttack) { Refresh(); return; }
        Close();
    }

    /// <summary>Пока окно открыто — обновляем таймер и прогресс.</summary>
    protected override void Update()
    {
        base.Update();
        if (IsOpen && current != null) RefreshStatus();
        if (IsOpen && currentPortal != null)
        {
            portalRefresh -= Time.unscaledDeltaTime;
            if (portalRefresh <= 0f) { portalRefresh = 0.5f; RefreshPortal(); }
        }
    }

    /// <summary>Открыть окно миссии.</summary>
    public void Open(MissionMarker marker)
    {
        current = marker;
        currentGlobal = null;
        currentPortal = null;
        Open();
    }

    /// <summary>Открыть окно общей миссии.</summary>
    public void OpenGlobal(GlobalMission mission)
    {
        current = null;
        currentGlobal = mission;
        currentPortal = null;
        Open();
    }

    /// <summary>Открыть окно портала.</summary>
    public void OpenPortal(Portal portal)
    {
        current = null;
        currentGlobal = null;
        currentPortal = portal;
        Open();
    }

    /// <summary>Соперник уже готовится к бою у портала — сразу список команд (битва за флаг).</summary>
    public void OpenJoinPicker(Portal portal) => picker.OpenFor(portal, null);

    /// <summary>Портал исчез — закрываем окно и список команд, если они про него.</summary>
    public void OnPortalHidden(Portal portal)
    {
        if (currentPortal != portal) return;
        currentPortal = null;
        if (picker.IsOpen) picker.Close();
        if (IsOpen) Close();
    }

    /// <summary>Соперник уже готовится к бою на общей миссии — сразу список команд (битва за флаг).</summary>
    public void OpenJoinPicker(GlobalMission mission) => picker.OpenFor(mission, null);

    /// <summary>Общая миссия исчезла — закрываем окно и список команд, если они про неё.</summary>
    public void OnGlobalRemoved(GlobalMission mission)
    {
        if (currentGlobal != mission) return;
        currentGlobal = null;
        if (picker.IsOpen) picker.Close();
        if (IsOpen) Close();
    }

    /// <summary>Миссия исчезла с карты — если она открыта в окне, закрываем окно.</summary>
    public void OnMissionRemoved(MissionMarker marker)
    {
        if (current != marker) return;
        current = null;
        if (picker.IsOpen) picker.Close(); // список команд для исчезнувшей миссии тоже закрываем
        if (IsOpen) Close();
    }

    /// <summary>Перерисовать окно.</summary>
    public override void Refresh()
    {
        if (currentPortal != null) { RefreshPortal(); return; }
        if (currentGlobal != null) { RefreshGlobal(); return; }
        if (current == null) return;
        MissionData d = current.Data;
        header.color = d.color;
        if (headerIcon != null) headerIcon.text = "?";
        titleText.text = d.title;
        descText.text = d.description;

        MainBase myBase = MainBase.Get(Team.Player);
        bool radar = myBase != null && myBase.HasRadar;
        detailsText.text = radar
            ? $"Рекомендуемая сила команды: <color=#FFFFFF>{d.requiredPower}</color>\n" +
              $"Время выполнения: <color=#FFFFFF>{d.duration:0} с</color>\n" +
              $"Награда: {d.GetRewardText()}"
            : $"Время выполнения: <color=#FFFFFF>{d.duration:0} с</color>\n" +
              "Сложность и награда: <color=#8792A6>??? (постройте Радар)</color>";
        RefreshStatus();
    }

    /// <summary>Обновить строку статуса и кнопку.</summary>
    private void RefreshStatus()
    {
        if (current.IsInProgress)
        {
            statusText.text = $"Выполняется: {Mathf.FloorToInt(current.Progress * 100)}%";
            sendButton.interactable = false;
            sendText.text = "ВЫПОЛНЯЕТСЯ";
        }
        else if (current.AssignedSquad != null)
        {
            statusText.text = $"Команда {current.AssignedSquad.Number} в пути";
            sendButton.interactable = false;
            sendText.text = "КОМАНДА В ПУТИ";
        }
        else
        {
            statusText.text = $"Исчезнет через {Mathf.CeilToInt(current.LifeLeft)} с";
            sendButton.interactable = true;
            sendText.text = "ОТПРАВИТЬ КОМАНДУ";
        }
    }

    /// <summary>Перерисовать окно общей миссии.</summary>
    private void RefreshGlobal()
    {
        GlobalMissionData d = currentGlobal.Data;
        header.color = d.color;
        if (headerIcon != null) headerIcon.text = d.iconLetter;
        titleText.text = $"ОБЩАЯ МИССИЯ: {d.title}";
        descText.text = d.description;

        MainBase myBase = MainBase.Get(Team.Player);
        string guard = myBase != null && myBase.HasRadar
            ? $"<color=#FFFFFF>{d.GetGuardText()}</color>"
            : "<color=#8792A6>??? (нужен Радар)</color>";
        detailsText.text = $"Награда: {d.GetRewardText()}\n" +
                           $"Подготовка к бою: <color=#FFFFFF>{currentGlobal.PrepTime:0} с</color>\n" +
                           $"Охрана: {guard}";
        statusText.text = "<size=85%>Видят обе стороны. Если за подготовку приедет враг — битва за флаг</size>";
        sendButton.interactable = !currentGlobal.IsUnderAttack;
        sendText.text = "ОТПРАВИТЬ КОМАНДУ";
    }

    /// <summary>Перерисовать окно портала: таймер, прогресс сторон, охрана, правила.</summary>
    private void RefreshPortal()
    {
        Portal p = currentPortal;
        PortalData d = p.Data;
        header.color = d.color;
        if (headerIcon != null) headerIcon.text = d.iconLetter;
        titleText.text = $"СОБЫТИЕ: {d.title.ToUpper()}";
        descText.text = d.description;

        MainBase myBase = MainBase.Get(Team.Player);
        string guard = myBase != null && myBase.HasRadar
            ? $"<color=#FFFFFF>{d.TerritoryCount} территории, осталось пройти — сила {p.RemainingPower(Team.Player)}</color>"
            : "<color=#8792A6>??? (нужен Радар)</color>";
        detailsText.text = $"Пройдено: <color=#7FB8FF>мы {p.GetProgress(Team.Player)}/{p.TerritoryCount}</color>   <color=#FF7A7A>враг {p.GetProgress(Team.Enemy)}/{p.TerritoryCount}</color>\n" +
                           $"Охрана: {guard}\n" +
                           $"Закрывший получает артефакт, база соперника — {d.baseDamage} урона.\n" +
                           $"Никто не закроет — {d.baseDamage} урона обеим базам.";
        float t = p.TimeLeft;
        statusText.text = p.IsClosed ? "Портал закрыт"
            : $"<size=85%>Портал исчезнет через <color=#FFD84A>{Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}</color>. Прогресс по территориям сохраняется</size>";
        sendButton.interactable = !p.IsUnderAttack && !p.IsClosed;
        sendText.text = "ОТПРАВИТЬ КОМАНДУ";
    }

    /// <summary>Нажата "ОТПРАВИТЬ КОМАНДУ".</summary>
    private void OnSendClicked()
    {
        if (currentPortal != null) { picker.OpenFor(currentPortal, Close); return; }
        if (currentGlobal != null) { picker.OpenFor(currentGlobal, Close); return; }
        if (current != null) picker.OpenFor(current, Close);
    }
}
