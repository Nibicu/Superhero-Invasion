using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Большое окно тактической паузы (игра стоит 2 минуты между фазами):
///  слева — ваши герои по иерархии (перетаскиванием меняется порядок; кто дальше лимита — в запасе),
///  в центре — база (HP, уровень, деньги) и кнопки: купить героев, инвентарь, база,
///  внизу — "ГОТОВ" (закончить паузу раньше).
/// Покупать героев можно только в паузе (и на драфте). Клик по герою — его карточка (прокачка, вещи).
/// </summary>
public class TacticalPauseUI : MonoBehaviour
{
    /// <summary>Единственное окно в сцене.</summary>
    public static TacticalPauseUI Instance { get; private set; }

    [Header("Окно")]
    [SerializeField] private GameObject root;        // Всё окно
    [SerializeField] private TMP_Text timerText;     // "1:45"
    [SerializeField] private TMP_Text nextText;      // "Дальше: событие «Портал»"
    [SerializeField] private Button readyButton;     // "ГОТОВ"

    [Header("Герои")]
    [SerializeField] private TMP_Text heroesHeader;  // "ВАШИ ГЕРОИ 5 / 6"
    [SerializeField] private Transform heroList;     // Список героев
    [SerializeField] private DraftRowUI rowTemplate; // Шаблон строки (выключен)

    [Header("База")]
    [SerializeField] private TMP_Text baseText;      // HP, уровень, деньги
    [SerializeField] private Button buyButton;       // "КУПИТЬ ГЕРОЕВ"
    [SerializeField] private Button inventoryButton; // "ИНВЕНТАРЬ"
    [SerializeField] private Button baseButton;      // "БАЗА"

    [Header("Побеждённые герои")]
    [SerializeField] private TMP_Text defeatedHeader;     // "ПОБЕЖДЁННЫЕ ГЕРОИ"
    [SerializeField] private Transform defeatedList;      // Строки побеждённых
    [SerializeField] private GameObject defeatedTemplate; // Шаблон строки: текст + кнопка (выключен)

    [Header("Окна")]
    [SerializeField] private RosterWindowUI roster;     // Покупка героев
    [SerializeField] private HeroInfoWindowUI heroInfo; // Карточка героя
    [SerializeField] private BaseWindowUI baseWindow;   // Окно базы

    private readonly List<GameObject> spawned = new List<GameObject>(); // Созданные строки

    /// <summary>Открыто ли окно паузы.</summary>
    public bool IsOpen => root != null && root.activeSelf;

    /// <summary>Подписать кнопки, спрятать шаблоны.</summary>
    private void Awake()
    {
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
        if (defeatedTemplate != null) defeatedTemplate.SetActive(false);
        readyButton.onClick.AddListener(() => { if (PhaseManager.Instance != null) PhaseManager.Instance.Ready(); });
        buyButton.onClick.AddListener(() => roster.Open());
        inventoryButton.onClick.AddListener(() => { if (ArtifactWindowUI.Instance != null) ArtifactWindowUI.Instance.OpenInventory(); });
        baseButton.onClick.AddListener(() => { MainBase b = MainBase.Get(Team.Player); if (b != null) baseWindow.Open(b); });
        root.SetActive(false);
    }

    /// <summary>Подписаться на изменения героев и денег.</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnChanged;
        ResourceManager.Instance.ResourcesChanged += OnChanged;
    }

    /// <summary>Отписаться.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnChanged;
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnChanged;
    }

    private void OnChanged(Team team)
    {
        if (team == Team.Player) RefreshIfOpen();
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    /// <summary>Показать окно паузы (вызывает PhaseManager).</summary>
    public void Show()
    {
        root.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    /// <summary>Спрятать окно паузы и окна, открытые из него.</summary>
    public void Hide()
    {
        root.SetActive(false);
        if (baseWindow != null) baseWindow.Close();
        if (roster != null && roster.IsOpen) roster.Close();
        if (heroInfo != null && heroInfo.IsOpen) heroInfo.Close();
    }

    /// <summary>Таймер паузы.</summary>
    private void Update()
    {
        if (!IsOpen || PhaseManager.Instance == null) return;
        int s = Mathf.Max(0, Mathf.CeilToInt(PhaseManager.Instance.TimeLeft));
        timerText.text = $"{s / 60}:{s % 60:00}";
    }

    /// <summary>Пересобрать окно.</summary>
    private void Refresh()
    {
        foreach (GameObject go in spawned) Destroy(go);
        spawned.Clear();
        HeroManager hm = HeroManager.Instance;
        ResourceManager rm = ResourceManager.Instance;
        PhaseManager pm = PhaseManager.Instance;

        // Что дальше
        if (pm != null && EventManager.Instance != null)
            nextText.text = pm.NextPhase == GamePhase.Event
                ? $"Дальше: фаза события «{EventManager.Instance.KindName(EventManager.Instance.Kind)}» (2 мин)"
                : "Дальше: фаза миссий (2 мин)";

        // Герои
        IReadOnlyList<HeroInstance> heroes = hm.GetHeroes(Team.Player);
        int limit = hm.GetHeroLimit(Team.Player);
        heroesHeader.text = $"ВАШИ ГЕРОИ\n<size=70%><color=#C9D3E6>{Mathf.Min(heroes.Count, limit)} / {limit} в команде{(heroes.Count > limit ? $"  •  {heroes.Count - limit} в запасе" : "")}</color></size>";
        for (int i = 0; i < heroes.Count; i++)
        {
            DraftRowUI row = Instantiate(rowTemplate, heroList);
            row.gameObject.SetActive(true);
            spawned.Add(row.gameObject);
            HeroData data = heroes[i].Data;
            row.Setup(i, heroes[i], true, OnRowDropped, () => heroInfo.ShowHero(data));
            if (i == 0) row.MarkLeader(); // первый в иерархии — лидер (перетащите героя наверх, чтобы сменить)
            if (hm.IsReserve(heroes[i])) row.MarkReserve();
            if (hm.StarReady(heroes[i])) row.MarkStarReady(); // 5 уровень — можно повысить звезду
        }

        // Побеждённые герои: ждут 2 фазы, потом их можно восстановить за двойную цену
        IReadOnlyList<HeroManager.DefeatedHero> defeated = hm.GetDefeated(Team.Player);
        if (defeatedHeader != null) defeatedHeader.gameObject.SetActive(defeated.Count > 0);
        if (defeatedTemplate != null)
            foreach (HeroManager.DefeatedHero d in defeated)
            {
                GameObject go = Instantiate(defeatedTemplate, defeatedList);
                go.SetActive(true);
                spawned.Add(go);
                bool can = hm.CanRestore(d, out string reason);
                int cost = hm.RestoreCost(d);
                go.GetComponentInChildren<TMP_Text>().text = $"{d.hero.Data.displayName}  <size=80%><color=#9AA4B5>ур. {d.hero.Level}</color></size>";
                Button restore = go.GetComponentInChildren<Button>();
                restore.interactable = can;
                restore.GetComponentInChildren<TMP_Text>().text = can ? $"ВОССТАНОВИТЬ {cost}" : reason.ToUpper();
                HeroManager.DefeatedHero captured = d;
                restore.onClick.AddListener(() =>
                {
                    if (HeroManager.Instance.TryRestore(captured, out string err)) ToastUI.Show($"{captured.hero.Data.displayName} снова в строю!");
                    else ToastUI.Show(err);
                });
            }

        // База
        MainBase b = MainBase.Get(Team.Player);
        string baseInfo = b != null ? $"HP: <color=#6EE07A>{b.Hp} / {b.MaxHp}</color>   •   Уровень базы: {b.Level}" : "";
        baseText.text = $"{baseInfo}\n<color=#FFD84A>{rm.GetGold(Team.Player)} золота</color>   •   <color=#C58BFF>{rm.GetPlutonium(Team.Player)} плутония</color>\n" +
                        "<size=80%><color=#9AA4B5>Покупать героев можно только сейчас (в паузе)</color></size>";
    }

    /// <summary>Отпустили строку героя: новое место — по высоте курсора среди остальных строк.</summary>
    private void OnRowDropped(DraftRowUI row, float pointerY)
    {
        int target = 0;
        foreach (Transform child in heroList)
        {
            if (child == row.transform || !child.gameObject.activeSelf) continue;
            if (child.position.y > pointerY) target++;
        }
        HeroManager.Instance.MoveHero(Team.Player, row.Hero, target);
    }
}
