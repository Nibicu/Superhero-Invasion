using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно итога битвы: "ПОБЕДА!" / "ПОРАЖЕНИЕ", что произошло ("Объект «Банк» теперь ваш!")
/// и кнопка "ПРОДОЛЖИТЬ". Показывается после каждого боя, в котором участвовал игрок:
///  - после ручного боя — прямо на арене, а по кнопке игра возвращается на карту;
///  - после автобоя — поверх карты.
/// Если итогов несколько (например, два автобоя подряд), они показываются по очереди.
/// Лежит на отдельном Canvas, чтобы быть видимым и на арене, и на карте.
/// Вызов из любого скрипта: BattleResultWindowUI.Show(победа, текст).
/// </summary>
public class BattleResultWindowUI : MonoBehaviour
{
    private static BattleResultWindowUI instance; // Единственное окно в сцене

    [SerializeField] private GameObject root;       // Всё окно (затемнение + панель)
    [SerializeField] private Image header;          // Полоса заголовка (зелёная/красная)
    [SerializeField] private TMP_Text titleText;    // "ПОБЕДА!" / "ПОРАЖЕНИЕ"
    [SerializeField] private TMP_Text messageText;  // Что произошло
    [SerializeField] private Button continueButton; // "ПРОДОЛЖИТЬ"

    [Header("Цвета")]
    [SerializeField] private Color winColor = new Color(0.25f, 0.64f, 0.3f);
    [SerializeField] private Color loseColor = new Color(0.72f, 0.22f, 0.23f);

    /// <summary>Один итог в очереди.</summary>
    private struct Entry
    {
        public bool win;
        public string message;
    }

    private readonly Queue<Entry> queue = new Queue<Entry>(); // Итоги, ждущие показа

    /// <summary>Открыто ли сейчас окно итога (BattleManager ждёт, пока его закроют).</summary>
    public static bool IsShowing => instance != null && instance.root.activeSelf;

    private void Awake()
    {
        instance = this;
        continueButton.onClick.AddListener(Continue);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Показать итог битвы (или поставить в очередь, если окно уже открыто).</summary>
    public static void Show(bool playerWon, string message)
    {
        if (instance == null) { Debug.Log($"{(playerWon ? "ПОБЕДА" : "ПОРАЖЕНИЕ")}: {message}"); return; }
        instance.queue.Enqueue(new Entry { win = playerWon, message = message });
        if (!instance.root.activeSelf) instance.ShowNext();
    }

    /// <summary>Показать следующий итог из очереди или закрыть окно.</summary>
    private void ShowNext()
    {
        if (queue.Count == 0)
        {
            root.SetActive(false);
            return;
        }
        Entry e = queue.Dequeue();
        header.color = e.win ? winColor : loseColor;
        titleText.text = e.win ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
        messageText.text = e.message;
        root.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>Кнопка "ПРОДОЛЖИТЬ".</summary>
    private void Continue() => ShowNext();
}
