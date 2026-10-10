using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Таймер событий под верхней панелью:
///  затишье — "СОБЫТИЕ ЧЕРЕЗ 4:12 • Объект";
///  событие "Объект" — "СОБЫТИЕ: БАНК 3:20";
///  событие "Портал" — "ПОРТАЛ 3:20 • мы 1/3, враг 0/3".
/// Полоска показывает, сколько прошло времени. Клик — сообщение, что это за событие.
/// </summary>
public class EventTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;   // Надпись
    [SerializeField] private Image fill;      // Полоска (Image Type = Filled)
    [SerializeField] private Image background; // Фон (во время события — ярче)
    [SerializeField] private Button button;   // Клик по таймеру

    [Header("Цвета")]
    [SerializeField] private Color quietColor = new Color(0.1f, 0.12f, 0.17f, 0.9f);
    [SerializeField] private Color eventColor = new Color(0.45f, 0.2f, 0.55f, 0.95f);

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    /// <summary>Каждый кадр — текст и полоска.</summary>
    private void Update()
    {
        EventManager em = EventManager.Instance;
        if (em == null || text == null) return;
        string time = Format(em.TimeLeft);

        if (!em.IsRunning)
            text.text = $"СОБЫТИЕ ЧЕРЕЗ <color=#FFD84A>{time}</color>  <size=80%><color=#9AA4B5>• {em.KindName(em.Kind)}</color></size>";
        else if (em.Kind == GameEventKind.Object && em.EventObject != null)
            text.text = $"СОБЫТИЕ: {em.EventObject.Data.displayName.ToUpper()}  <color=#FFD84A>{time}</color>";
        else if (em.Kind == GameEventKind.Portal && em.Portal != null)
        {
            Portal p = em.Portal;
            text.text = p.IsClosed
                ? $"ПОРТАЛ ЗАКРЫТ {(p.ClosedBy == Team.Player ? "<color=#7FB8FF>НАМИ</color>" : "<color=#FF7A7A>ВРАГОМ</color>")}"
                : $"ПОРТАЛ  <color=#FFD84A>{time}</color>  <size=80%><color=#7FB8FF>мы {p.GetProgress(Team.Player)}/{p.TerritoryCount}</color> " +
                  $"<color=#FF7A7A>враг {p.GetProgress(Team.Enemy)}/{p.TerritoryCount}</color></size>";
        }
        if (fill != null) fill.fillAmount = em.Progress;
        if (background != null) background.color = em.IsRunning ? eventColor : quietColor;
    }

    /// <summary>"4:05".</summary>
    private static string Format(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>Клик — подсказка о событии.</summary>
    private void OnClicked()
    {
        EventManager em = EventManager.Instance;
        if (em == null) return;
        if (!em.IsRunning)
            ToastUI.Show(em.Kind == GameEventKind.Portal
                ? "Следующее событие — портал в центре карты: закройте его, иначе база получит урон"
                : "Следующее событие — на карте откроется объект: последний владелец получит артефакт");
        else if (em.Kind == GameEventKind.Object && em.EventObject != null)
            ToastUI.Show($"«{em.EventObject.Data.displayName}» открыт! Владелец к концу события получит артефакт");
        else
            ToastUI.Show("Портал открыт! Закройте его раньше врага");
    }
}
