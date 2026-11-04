using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна иконка фракции во вкладке "Фракция" тактической паузы (кружок на линии).
/// Состояния: открыта (зелёная, с галочкой), следующая (жёлтая, кнопка "открыть"), закрыта (серая).
/// </summary>
public class FactionIconUI : MonoBehaviour
{
    [SerializeField] private Image ring;       // Обводка кружка
    [SerializeField] private Image fill;       // Заливка внутри
    [SerializeField] private GameObject check; // Галочка (видна, когда иконка открыта)
    [SerializeField] private TMP_Text label;   // Текст внутри: номер или цена
    [SerializeField] private TMP_Text caption; // Подпись под кружком
    [SerializeField] private Button button;    // Нажатие — открыть иконку

    private bool pulse; // Мягко мигать (иконку можно открыть прямо сейчас)

    /// <summary>Кнопка иконки (окно паузы подписывается на нажатие).</summary>
    public Button Button => button;

    /// <summary>Иконка уже открыта: зелёная, с галочкой.</summary>
    public void ShowOpened(string text)
    {
        pulse = false;
        ring.color = new Color(0.3f, 0.85f, 0.4f);
        fill.color = new Color(0.12f, 0.35f, 0.16f);
        check.SetActive(true);
        label.gameObject.SetActive(false);
        caption.text = text;
        button.interactable = false;
    }

    /// <summary>Следующая иконка: показать цену; afford — хватает очков (мигает, можно нажать).</summary>
    public void ShowNext(int cost, bool afford, string text)
    {
        pulse = afford;
        ring.color = afford ? new Color(1f, 0.82f, 0.25f) : new Color(0.7f, 0.58f, 0.25f);
        fill.color = new Color(0.16f, 0.14f, 0.1f);
        check.SetActive(false);
        label.gameObject.SetActive(true);
        label.text = $"<size=60%>ОТКРЫТЬ</size>\n<color={(afford ? "#C77DFF" : "#FF6B6B")}>{cost}</color>";
        caption.text = text;
        button.interactable = afford;
    }

    /// <summary>Иконка закрыта (сначала нужно открыть предыдущие).</summary>
    public void ShowLocked(int number, string text)
    {
        pulse = false;
        ring.color = new Color(0.4f, 0.42f, 0.47f);
        fill.color = new Color(0.12f, 0.13f, 0.16f);
        check.SetActive(false);
        label.gameObject.SetActive(true);
        label.text = $"<color=#7C8496>{number}</color>";
        caption.text = text;
        button.interactable = false;
    }

    /// <summary>Мигание обводки, когда иконку можно открыть (пауза идёт по реальному времени).</summary>
    private void Update()
    {
        if (!pulse) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        ring.color = Color.Lerp(new Color(1f, 0.82f, 0.25f), Color.white, k * 0.6f);
    }
}
