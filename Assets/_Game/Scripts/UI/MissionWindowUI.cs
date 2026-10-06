using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно миссии: название, описание, время выполнения, сколько миссия ещё провисит,
/// и (только с Радаром) рекомендуемая сила и награда.
/// Кнопка "ОТПРАВИТЬ КОМАНДУ" открывает список команд.
/// </summary>
public class MissionWindowUI : WindowUI
{
    /// <summary>Единственное окно миссии в сцене.</summary>
    public static MissionWindowUI Instance { get; private set; }

    [Header("Миссия")]
    [SerializeField] private Image header;          // Полоса заголовка (цвет миссии)
    [SerializeField] private TMP_Text titleText;    // Название
    [SerializeField] private TMP_Text descText;     // Описание
    [SerializeField] private TMP_Text detailsText;  // Сила и награда (с Радаром) или "???"
    [SerializeField] private TMP_Text statusText;   // Таймер / прогресс
    [SerializeField] private Button sendButton;     // "ОТПРАВИТЬ КОМАНДУ"
    [SerializeField] private TMP_Text sendText;
    [SerializeField] private SquadPickerUI picker;  // Окно выбора команды

    private MissionMarker current; // Какая миссия показана

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        sendButton.onClick.AddListener(OnSendClicked);
    }

    /// <summary>Пока окно открыто — обновляем таймер и прогресс.</summary>
    protected override void Update()
    {
        base.Update();
        if (IsOpen && current != null) RefreshStatus();
    }

    /// <summary>Открыть окно миссии.</summary>
    public void Open(MissionMarker marker)
    {
        current = marker;
        Open();
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
        if (current == null) return;
        MissionData d = current.Data;
        header.color = d.color;
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

    /// <summary>Нажата "ОТПРАВИТЬ КОМАНДУ".</summary>
    private void OnSendClicked()
    {
        if (current != null) picker.OpenFor(current, Close);
    }
}
