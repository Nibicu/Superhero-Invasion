using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно объекта карты (Банк, Завод...): название, владелец, описание, бонусы,
/// время захвата, охрана (видна только с Радаром) и кнопка "ЗАХВАТИТЬ",
/// которая открывает список команд для отправки.
/// </summary>
public class MapObjectWindowUI : WindowUI
{
    /// <summary>Единственное окно объекта в сцене.</summary>
    public static MapObjectWindowUI Instance { get; private set; }

    [Header("Объект")]
    [SerializeField] private Image header;          // Полоса заголовка (цвет объекта)
    [SerializeField] private Image iconBackground;  // Плашка иконки (цвет объекта)
    [SerializeField] private Image icon;            // Спрайт иконки
    [SerializeField] private TMP_Text iconLetter;   // Буква, если спрайта нет
    [SerializeField] private TMP_Text titleText;    // Название
    [SerializeField] private TMP_Text ownerText;    // "Владелец: Нейтральный"
    [SerializeField] private TMP_Text descText;     // Описание
    [SerializeField] private TMP_Text bonusText;    // Бонусы
    [SerializeField] private TMP_Text infoText;     // Время захвата и охрана
    [SerializeField] private TMP_Text statusText;   // Прогресс захвата

    [Header("Захват")]
    [SerializeField] private Button captureButton;  // "ЗАХВАТИТЬ"
    [SerializeField] private TMP_Text captureText;  // Текст на кнопке
    [SerializeField] private SquadPickerUI picker;  // Окно выбора команды

    private MapObject current; // Какой объект показан

    /// <summary>Запоминаем себя, подписываем кнопку.</summary>
    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        captureButton.onClick.AddListener(OnCaptureClicked);
    }

    /// <summary>Пока окно открыто — обновляем прогресс захвата каждый кадр.</summary>
    protected override void Update()
    {
        base.Update();
        if (IsOpen && current != null) RefreshStatus();
    }

    /// <summary>Открыть окно объекта.</summary>
    public void Open(MapObject obj)
    {
        current = obj;
        Open();
    }

    /// <summary>Перерисовать окно целиком.</summary>
    public override void Refresh()
    {
        if (current == null) return;
        MapObjectData d = current.Data;

        header.color = d.color;
        iconBackground.color = d.icon != null ? Color.white : d.color;
        icon.gameObject.SetActive(d.icon != null);
        icon.sprite = d.icon;
        iconLetter.gameObject.SetActive(d.icon == null);
        iconLetter.text = d.iconLetter;
        titleText.text = d.displayName;
        descText.text = d.description;
        bonusText.text = d.GetBonusText();

        MainBase myBase = MainBase.Get(Team.Player);
        string guard = myBase != null && myBase.HasRadar
            ? $"<color=#FFFFFF>{d.guardDescription}</color>"
            : "<color=#8792A6>??? (нужен Радар)</color>";
        infoText.text = $"Время захвата: <color=#FFFFFF>{d.captureTime:0} с</color>\nОхрана: {guard}";

        RefreshStatus();
    }

    /// <summary>Обновить строку владельца, прогресс и кнопку (меняются во время захвата).</summary>
    private void RefreshStatus()
    {
        ownerText.text = $"Владелец: {current.OwnerText}";

        if (current.IsCapturing)
        {
            string who = current.CapturingTeam == Team.Player ? "<color=#7FB8FF>наша команда</color>" : "<color=#FF7A7A>враг</color>";
            statusText.text = $"Идёт захват ({who}): {Mathf.FloorToInt(current.CaptureProgress * 100)}%";
        }
        else statusText.text = "";

        if (current.IsOwnedBy(Team.Player))
        {
            captureButton.interactable = false;
            captureText.text = "ОБЪЕКТ НАШ";
        }
        else if (current.IsCapturing && current.CapturingTeam == Team.Player)
        {
            captureButton.interactable = false;
            captureText.text = "ИДЁТ ЗАХВАТ";
        }
        else
        {
            captureButton.interactable = true;
            captureText.text = "ЗАХВАТИТЬ";
        }
    }

    /// <summary>Нажата "ЗАХВАТИТЬ" — открываем список команд.</summary>
    private void OnCaptureClicked()
    {
        if (current != null) picker.OpenFor(current, Close);
    }
}
