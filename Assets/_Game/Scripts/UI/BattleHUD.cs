using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Интерфейс во время боя (отдельный Canvas):
/// - сверху: название объекта, номер территории, сколько врагов осталось;
/// - слева: "ВАША КОМАНДА" — HP и энергия каждого героя;
/// - справа сверху: кнопка "ОТСТУПИТЬ";
/// - по центру: крупные сообщения ("ВПЕРЁД!") и итог боя (ПОБЕДА / ПОРАЖЕНИЕ).
/// </summary>
public class BattleHUD : MonoBehaviour
{
    [SerializeField] private GameObject root;              // Весь интерфейс боя
    [SerializeField] private TMP_Text titleText;           // Название объекта
    [SerializeField] private TMP_Text progressText;        // "Территория 1/3 • Врагов: 2"
    [SerializeField] private Transform teamList;           // Куда складывать строки героев
    [SerializeField] private BattleHeroHpUI rowTemplate;   // Шаблон строки (выключен)
    [SerializeField] private Button retreatButton;         // "ОТСТУПИТЬ"
    [SerializeField] private TMP_Text bigMessage;          // Крупное сообщение по центру
    [SerializeField] private CanvasGroup bigMessageGroup;  // Для плавного исчезания
    [SerializeField] private GameObject resultPanel;       // Панель итога
    [SerializeField] private TMP_Text resultTitle;         // "ПОБЕДА!" / "ПОРАЖЕНИЕ"
    [SerializeField] private TMP_Text resultText;          // Подробности

    private readonly List<BattleHeroHpUI> rows = new List<BattleHeroHpUI>();
    private BattleManager manager;
    private float messageTimer;

    private void Awake()
    {
        rowTemplate.gameObject.SetActive(false);
        retreatButton.onClick.AddListener(() => { if (manager != null) manager.Retreat(); });
        root.SetActive(false);
    }

    /// <summary>Показать интерфейс боя для команды players.</summary>
    public void Show(BattleManager m, string title, IReadOnlyList<Fighter> players)
    {
        manager = m;
        root.SetActive(true);
        resultPanel.SetActive(false);
        retreatButton.interactable = true;
        titleText.text = $"БИТВА: {title.ToUpper()}";
        foreach (BattleHeroHpUI r in rows) Destroy(r.gameObject);
        rows.Clear();
        foreach (Fighter f in players)
        {
            BattleHeroHpUI row = Instantiate(rowTemplate, teamList);
            row.gameObject.SetActive(true);
            row.Bind(f);
            rows.Add(row);
        }
        // Высота панели — по числу героев
        if (teamList.parent is RectTransform panel)
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 62f + players.Count * 66f);
    }

    /// <summary>Спрятать интерфейс боя.</summary>
    public void Hide()
    {
        root.SetActive(false);
        manager = null;
    }

    /// <summary>Обновить строку прогресса.</summary>
    public void SetProgress(int territory, int total, int enemiesLeft) =>
        progressText.text = $"Территория {territory} / {total}   •   Врагов: {enemiesLeft}";

    /// <summary>Крупное сообщение по центру на пару секунд.</summary>
    public void ShowMessage(string text, float duration = 2f)
    {
        bigMessage.text = text;
        messageTimer = duration;
        bigMessageGroup.alpha = 1f;
    }

    /// <summary>Показать итог боя.</summary>
    public void ShowResult(bool win, string details)
    {
        messageTimer = 0f;
        bigMessageGroup.alpha = 0f;
        resultPanel.SetActive(true);
        retreatButton.interactable = false;
        resultTitle.text = win ? "<color=#6EE07A>ПОБЕДА!</color>" : "<color=#FF6B6B>ПОРАЖЕНИЕ</color>";
        resultText.text = $"{details}\n<size=75%><color=#9AA4B5>Возвращение на карту...</color></size>";
    }

    /// <summary>Плавное исчезание сообщения.</summary>
    private void Update()
    {
        if (messageTimer <= 0f) return;
        messageTimer -= Time.deltaTime;
        bigMessageGroup.alpha = Mathf.Clamp01(messageTimer / 0.5f);
    }
}
