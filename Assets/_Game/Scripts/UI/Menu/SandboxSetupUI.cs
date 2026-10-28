using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Экран Песочницы: выбор карты, стороны конфликта (Герои / Злодеи) и фракции
/// (герои — Трон или Стражи, злодеи — Разрушители или Тайный порядок).
/// Враг получает противоположную сторону и случайную фракцию.
/// Кнопка "НАЧАТЬ" запоминает выбор (GameSetup) и загружает сцену карты.
/// </summary>
public class SandboxSetupUI : MonoBehaviour
{
    [Header("Данные")]
    [Tooltip("Карты для выбора (Data/Maps)")]
    [SerializeField] private MapData[] maps = new MapData[0];
    [Tooltip("Все фракции (Data/Factions)")]
    [SerializeField] private FactionData[] factions = new FactionData[0];

    [Header("Карта")]
    [SerializeField] private Image mapPreview;      // Картинка выбранной карты
    [SerializeField] private TMP_Text mapName;      // Название карты
    [SerializeField] private TMP_Text mapInfo;      // Описание карты
    [SerializeField] private Button mapPrev;        // Предыдущая карта
    [SerializeField] private Button mapNext;        // Следующая карта

    [Header("Сторона конфликта")]
    [SerializeField] private Button heroesButton;   // "ГЕРОИ"
    [SerializeField] private Button villainsButton; // "ЗЛОДЕИ"

    [Header("Фракция")]
    [SerializeField] private Button[] factionButtons = new Button[2]; // Две фракции выбранной стороны
    [SerializeField] private TMP_Text factionInfo;  // Описание выбранной фракции

    [Header("Итог")]
    [SerializeField] private TMP_Text summary;      // "Вы: ... Враг: злодеи (случайная фракция)"
    [SerializeField] private Button startButton;    // "НАЧАТЬ"

    [Header("Цвета")]
    [SerializeField] private Color heroesColor = new Color(0.184f, 0.482f, 0.878f);   // Цвет героев
    [SerializeField] private Color villainsColor = new Color(0.851f, 0.22f, 0.227f);  // Цвет злодеев
    [SerializeField] private Color idleColor = new Color(0.22f, 0.25f, 0.32f);        // Невыбранная кнопка

    private int mapIndex;                     // Выбранная карта
    private Side side = Side.Heroes;          // Выбранная сторона
    private int factionIndex;                 // Выбранная фракция (0 или 1 среди фракций стороны)
    private readonly List<FactionData> sideFactions = new List<FactionData>(); // Фракции выбранной стороны

    /// <summary>Подписать кнопки.</summary>
    private void Awake()
    {
        mapPrev.onClick.AddListener(() => StepMap(-1));
        mapNext.onClick.AddListener(() => StepMap(1));
        heroesButton.onClick.AddListener(() => SelectSide(Side.Heroes));
        villainsButton.onClick.AddListener(() => SelectSide(Side.Villains));
        for (int i = 0; i < factionButtons.Length; i++)
        {
            int k = i;
            factionButtons[i].onClick.AddListener(() => SelectFaction(k));
        }
        startButton.onClick.AddListener(StartGame);
    }

    /// <summary>При открытии экрана — показать текущий выбор.</summary>
    private void OnEnable()
    {
        StepMap(0);
        SelectSide(side);
    }

    /// <summary>Листать карты.</summary>
    private void StepMap(int dir)
    {
        if (maps.Length == 0) return;
        mapIndex = (mapIndex + dir + maps.Length) % maps.Length;
        MapData m = maps[mapIndex];
        mapPreview.sprite = m.preview;
        mapPreview.preserveAspect = true;
        mapName.text = m.displayName;
        mapInfo.text = m.description;
        bool many = maps.Length > 1;
        mapPrev.gameObject.SetActive(many);
        mapNext.gameObject.SetActive(many);
        RefreshSummary();
    }

    /// <summary>Выбрать сторону конфликта: фракции меняются на фракции этой стороны.</summary>
    private void SelectSide(Side s)
    {
        bool changed = s != side;
        side = s;
        sideFactions.Clear();
        foreach (FactionData f in factions)
            if (f != null && f.side == s) sideFactions.Add(f);
        Paint(heroesButton, s == Side.Heroes ? heroesColor : idleColor);
        Paint(villainsButton, s == Side.Villains ? villainsColor : idleColor);
        for (int i = 0; i < factionButtons.Length; i++)
        {
            bool has = i < sideFactions.Count;
            factionButtons[i].gameObject.SetActive(has);
            if (has) factionButtons[i].GetComponentInChildren<TMP_Text>().text = sideFactions[i].displayName.ToUpper();
        }
        SelectFaction(changed ? 0 : factionIndex); // та же сторона — фракция остаётся выбранной
    }

    /// <summary>Выбрать фракцию (номер среди фракций стороны).</summary>
    private void SelectFaction(int i)
    {
        factionIndex = Mathf.Clamp(i, 0, Mathf.Max(0, sideFactions.Count - 1));
        for (int k = 0; k < factionButtons.Length && k < sideFactions.Count; k++)
            Paint(factionButtons[k], k == factionIndex ? sideFactions[k].color : idleColor);
        factionInfo.text = sideFactions.Count > 0 ? sideFactions[factionIndex].description : "";
        RefreshSummary();
    }

    /// <summary>Строка-итог: за кого играем и против кого.</summary>
    private void RefreshSummary()
    {
        if (summary == null || sideFactions.Count == 0) return;
        string me = side == Side.Heroes ? "Герои" : "Злодеи";
        string foe = side == Side.Heroes ? "Злодеи" : "Герои";
        summary.text = $"Вы: <b>{me}</b> • {sideFactions[factionIndex].displayName}      Враг: <b>{foe}</b> • случайная фракция";
        startButton.interactable = maps.Length > 0;
    }

    /// <summary>Покрасить кнопку выбора.</summary>
    private static void Paint(Button b, Color c)
    {
        var img = b.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    /// <summary>Запомнить выбор и загрузить карту. Враг — противоположная сторона, случайная фракция.</summary>
    private void StartGame()
    {
        if (maps.Length == 0 || sideFactions.Count == 0) return;
        Side enemySide = side == Side.Heroes ? Side.Villains : Side.Heroes;
        var enemyOptions = new List<FactionData>();
        foreach (FactionData f in factions)
            if (f != null && f.side == enemySide) enemyOptions.Add(f);
        FactionData enemy = enemyOptions.Count > 0 ? enemyOptions[Random.Range(0, enemyOptions.Count)] : null;
        GameSetup.Set(maps[mapIndex], side, sideFactions[factionIndex], enemy);
        SceneManager.LoadScene(maps[mapIndex].sceneName);
    }
}
