using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Большое окно тактической паузы (игра стоит 2 минуты между фазами):
///  слева — ваши герои по иерархии (перетаскиванием меняется порядок; кто дальше лимита — в запасе),
///  в центре — база (HP, уровень, деньги) и кнопки: купить героев, инвентарь, база,
///  справа — задания фракции (выбрать одно), внизу — "ГОТОВ" (закончить паузу раньше).
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

    [Header("Задания фракции")]
    [SerializeField] private TMP_Text missionsHeader;          // "ЗАДАНИЯ ФРАКЦИИ: Трон"
    [SerializeField] private TMP_Text missionsStatus;          // Что сейчас идёт (наше и врага)
    [SerializeField] private Transform missionList;            // Карточки заданий
    [SerializeField] private FactionMissionCardUI cardTemplate; // Шаблон карточки (выключен)

    [Header("Окна")]
    [SerializeField] private RosterWindowUI roster;     // Покупка героев
    [SerializeField] private HeroInfoWindowUI heroInfo; // Карточка героя
    [SerializeField] private BaseWindowUI baseWindow;   // Окно базы

    private readonly List<GameObject> spawned = new List<GameObject>(); // Созданные строки и карточки

    /// <summary>Открыто ли окно паузы.</summary>
    public bool IsOpen => root != null && root.activeSelf;

    /// <summary>Подписать кнопки, спрятать шаблоны.</summary>
    private void Awake()
    {
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
        cardTemplate.gameObject.SetActive(false);
        readyButton.onClick.AddListener(() => { if (PhaseManager.Instance != null) PhaseManager.Instance.Ready(); });
        buyButton.onClick.AddListener(() => roster.Open());
        inventoryButton.onClick.AddListener(() => { if (ArtifactWindowUI.Instance != null) ArtifactWindowUI.Instance.OpenInventory(); });
        baseButton.onClick.AddListener(() => { MainBase b = MainBase.Get(Team.Player); if (b != null) baseWindow.Open(b); });
        root.SetActive(false);
    }

    /// <summary>Подписаться на изменения героев, денег и заданий.</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnChanged;
        ResourceManager.Instance.ResourcesChanged += OnChanged;
        if (GlobalMissionManager.Instance != null) GlobalMissionManager.Instance.Changed += RefreshIfOpen;
    }

    /// <summary>Отписаться.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnChanged;
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnChanged;
        if (GlobalMissionManager.Instance != null) GlobalMissionManager.Instance.Changed -= RefreshIfOpen;
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
            if (hm.IsReserve(heroes[i])) row.MarkReserve();
        }

        // База
        MainBase b = MainBase.Get(Team.Player);
        string baseInfo = b != null ? $"HP: <color=#6EE07A>{b.Hp} / {b.MaxHp}</color>   •   Уровень базы: {b.Level}" : "";
        baseText.text = $"{baseInfo}\n<color=#FFD84A>{rm.GetGold(Team.Player)} золота</color>   •   <color=#C58BFF>{rm.GetPlutonium(Team.Player)} плутония</color>\n" +
                        "<size=80%><color=#9AA4B5>Покупать героев можно только сейчас (в паузе)</color></size>";

        // Задания фракции
        GlobalMissionManager gmm = GlobalMissionManager.Instance;
        string faction = GameSetup.PlayerFaction != null ? GameSetup.PlayerFaction.displayName : (hm.GetSide(Team.Player) == Side.Heroes ? "Герои" : "Злодеи");
        missionsHeader.text = $"ЗАДАНИЯ ФРАКЦИИ\n<size=70%><color=#C9D3E6>{faction}</color></size>";
        if (gmm == null) return;
        GlobalMission mine = gmm.LaunchedBy(Team.Player), theirs = gmm.LaunchedBy(Team.Enemy);
        missionsStatus.text =
            (mine != null ? $"Ваше задание: <color=#7FB8FF>{mine.Title}</color>" : "Ваше задание: <color=#9AA4B5>не выбрано</color>") + "\n" +
            (theirs != null ? $"Враг выполняет: <color=#FF7A7A>{theirs.Title}</color>" : "Враг: <color=#9AA4B5>задания нет</color>");
        foreach (GlobalMissionData d in gmm.Pool)
        {
            FactionMissionCardUI card = Instantiate(cardTemplate, missionList);
            card.gameObject.SetActive(true);
            spawned.Add(card.gameObject);
            bool can = gmm.CanLaunch(Team.Player, d, out _);
            string state = mine != null && mine.Data == d ? "Ваше — идёт"
                : theirs != null && theirs.Data == d ? "Идёт у врага"
                : mine != null ? "Одно задание за раз" : "Недоступно";
            GlobalMissionData captured = d;
            card.Setup(d, can, state, () => gmm.Launch(Team.Player, captured));
        }
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
