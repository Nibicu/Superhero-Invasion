using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Большое окно тактической паузы (игра стоит между фазами):
///  сверху — золото, плутоний, очки фракции, таймер и что будет дальше;
///  слева — вкладки "Фракция", "Герои", "База"; в центре — содержимое вкладки;
///  справа внизу — "КОНЕЦ ХОДА" (закончить паузу раньше).
/// Вкладка "Фракция": 5 иконок, открываются по порядку за очки фракции (FactionManager).
/// Вкладка "Герои": 3★ герой фракции (нанять — после всех 5 иконок), лидер и остальные герои
///  (перетаскиванием меняется порядок; перетащите героя на лидера — он станет лидером).
/// Вкладка "База": 5 ячеек построек (встроенное окно базы).
/// </summary>
public class TacticalPauseUI : MonoBehaviour
{
    /// <summary>Единственное окно в сцене.</summary>
    public static TacticalPauseUI Instance { get; private set; }

    /// <summary>Вкладки окна.</summary>
    private enum Tab { Faction, Heroes, Base }

    [Header("Окно")]
    [SerializeField] private GameObject root;        // Всё окно
    [SerializeField] private TMP_Text timerText;     // "1:45"
    [SerializeField] private TMP_Text nextText;      // "Дальше: событие «Портал»"
    [SerializeField] private Button readyButton;     // "КОНЕЦ ХОДА"

    [Header("Ресурсы сверху")]
    [SerializeField] private TMP_Text goldText;      // Золото
    [SerializeField] private TMP_Text plutoniumText; // Плутоний
    [SerializeField] private TMP_Text factionText;   // Очки фракции

    [Header("Вкладки")]
    [SerializeField] private Button factionTabButton; // "Фракция"
    [SerializeField] private Button heroesTabButton;  // "Герои"
    [SerializeField] private Button baseTabButton;    // "База"
    [SerializeField] private GameObject factionPage;  // Содержимое вкладки "Фракция"
    [SerializeField] private GameObject heroesPage;   // Содержимое вкладки "Герои"

    [Header("Фракция")]
    [SerializeField] private TMP_Text factionTitle;      // Название фракции
    [SerializeField] private TMP_Text factionHint;       // Подсказка под иконками
    [SerializeField] private FactionIconUI[] icons;      // 5 иконок по порядку
    [SerializeField] private Image[] links;              // Линии между иконками (links[i] — между i и i+1)

    [Header("Герои")]
    [SerializeField] private TMP_Text heroesHeader;      // "ВАШИ ГЕРОИ 5 / 6"
    [SerializeField] private Transform heroList;         // Список остальных героев (розовая область)
    [SerializeField] private DraftRowUI rowTemplate;     // Шаблон строки героя (выключен)
    [SerializeField] private Button factionHeroButton;   // Голубой овал: 3★ герой фракции
    [SerializeField] private HeroPortraitUI factionHeroPortrait;
    [SerializeField] private TMP_Text factionHeroText;   // Имя и статус 3★ героя
    [SerializeField] private CanvasGroup factionHeroFade; // Бледный, пока нанять нельзя
    [SerializeField] private Button leaderButton;        // Синий овал: лидер
    [SerializeField] private RectTransform leaderSlot;   // Область лидера (сюда можно бросить героя)
    [SerializeField] private HeroPortraitUI leaderPortrait;
    [SerializeField] private TMP_Text leaderText;        // Имя лидера
    [SerializeField] private Button buyButton;           // "КУПИТЬ ГЕРОЕВ"
    [SerializeField] private Button inventoryButton;     // "ИНВЕНТАРЬ"

    [Header("Побеждённые герои")]

    [Header("Окна")]
    [SerializeField] private RosterWindowUI roster;     // Покупка героев
    [SerializeField] private HeroInfoWindowUI heroInfo; // Карточка героя
    [SerializeField] private BaseWindowUI baseTab;      // Вкладка "База" (встроенное окно базы)

    private readonly List<GameObject> spawned = new List<GameObject>(); // Созданные строки
    private Tab tab = Tab.Faction; // Открытая вкладка

    /// <summary>Открыто ли окно паузы.</summary>
    public bool IsOpen => root != null && root.activeSelf;

    /// <summary>Подписать кнопки, спрятать шаблоны.</summary>
    private void Awake()
    {
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
        readyButton.onClick.AddListener(() => { if (PhaseManager.Instance != null) PhaseManager.Instance.Ready(); });
        buyButton.onClick.AddListener(() => roster.Open());
        inventoryButton.onClick.AddListener(() => { if (ArtifactWindowUI.Instance != null) ArtifactWindowUI.Instance.OpenInventory(); });
        factionTabButton.onClick.AddListener(() => SetTab(Tab.Faction));
        heroesTabButton.onClick.AddListener(() => SetTab(Tab.Heroes));
        baseTabButton.onClick.AddListener(() => SetTab(Tab.Base));
        foreach (FactionIconUI icon in icons)
            icon.Button.onClick.AddListener(OnIconClicked);
        factionHeroButton.onClick.AddListener(() =>
        {
            FactionData f = FactionManager.GetFaction(Team.Player);
            if (f != null && f.factionHero != null) heroInfo.ShowHero(f.factionHero); // карточка с кнопкой найма
        });
        leaderButton.onClick.AddListener(() =>
        {
            HeroInstance leader = HeroManager.Instance.GetLeader(Team.Player);
            if (leader != null) heroInfo.ShowHero(leader.Data);
        });
        root.SetActive(false);
    }

    /// <summary>Подписаться на изменения героев, денег и фракции.</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnChanged;
        ResourceManager.Instance.ResourcesChanged += OnChanged;
        if (FactionManager.Instance != null) FactionManager.Instance.Changed += OnChanged;
    }

    /// <summary>Отписаться.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnChanged;
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnChanged;
        if (FactionManager.Instance != null) FactionManager.Instance.Changed -= OnChanged;
    }

    /// <summary>Что-то изменилось у игрока — перерисовать окно (если открыто).</summary>
    private void OnChanged(Team team)
    {
        if (team == Team.Player && IsOpen) Refresh();
    }

    /// <summary>Показать окно паузы (вызывает PhaseManager).</summary>
    public void Show()
    {
        root.SetActive(true);
        transform.SetAsLastSibling();
        SetTab(tab);
    }

    /// <summary>Спрятать окно паузы и окна, открытые из него.</summary>
    public void Hide()
    {
        root.SetActive(false);
        if (baseTab != null) baseTab.Close();
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

    // ---------- Вкладки ----------

    /// <summary>Открыть вкладку: подсветить её кнопку и показать содержимое.</summary>
    private void SetTab(Tab t)
    {
        tab = t;
        factionPage.SetActive(t == Tab.Faction);
        heroesPage.SetActive(t == Tab.Heroes);
        if (t == Tab.Base)
        {
            MainBase b = MainBase.Get(Team.Player);
            if (b != null) baseTab.Open(b);
        }
        else baseTab.Close();
        Paint(factionTabButton, t == Tab.Faction);
        Paint(heroesTabButton, t == Tab.Heroes);
        Paint(baseTabButton, t == Tab.Base);
        Refresh();
    }

    /// <summary>Кнопка вкладки: выбранная — белая с тёмным текстом, остальные — тёмные.</summary>
    private static void Paint(Button b, bool selected)
    {
        b.GetComponent<Image>().color = selected ? new Color(0.96f, 0.93f, 0.85f) : new Color(0.16f, 0.13f, 0.11f, 0.95f);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.color = selected ? new Color(0.15f, 0.1f, 0.05f) : Color.white;
    }

    // ---------- Перерисовка ----------

    /// <summary>Пересобрать окно.</summary>
    private void Refresh()
    {
        ResourceManager rm = ResourceManager.Instance;
        PhaseManager pm = PhaseManager.Instance;
        FactionManager fm = FactionManager.Instance;

        // Ресурсы сверху
        goldText.text = rm.GetGold(Team.Player).ToString();
        plutoniumText.text = rm.GetPlutonium(Team.Player).ToString();
        factionText.text = fm != null ? fm.GetPoints(Team.Player).ToString() : "—";

        // Что дальше
        if (pm != null && EventManager.Instance != null)
            nextText.text = pm.NextPhase == GamePhase.Event
                ? $"Дальше: фаза события «{EventManager.Instance.KindName(EventManager.Instance.Kind)}»"
                : "Дальше: фаза миссий";

        if (tab == Tab.Faction) RefreshFaction();
        if (tab == Tab.Heroes) RefreshHeroes();
    }

    /// <summary>Вкладка "Фракция": иконки по порядку и подсказка.</summary>
    private void RefreshFaction()
    {
        FactionManager fm = FactionManager.Instance;
        FactionData f = FactionManager.GetFaction(Team.Player);
        string heroName = f != null && f.factionHero != null ? f.factionHero.displayName : "3-звёздочного героя";
        factionTitle.text = f != null
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(f.color)}>{f.displayName.ToUpper()}</color>\n<size=55%><color=#C9D3E6>{f.description}</color></size>"
            : "ФРАКЦИЯ";
        if (fm == null) { factionHint.text = "Нет FactionManager на сцене"; return; }

        int opened = fm.GetUnlocked(Team.Player);
        int points = fm.GetPoints(Team.Player);
        for (int i = 0; i < icons.Length; i++)
        {
            bool last = i == icons.Length - 1;
            string caption = last ? $"{heroName} <color=#FFD84A>3 зв.</color>" : "";
            if (i < opened) icons[i].ShowOpened(last ? caption : "Открыта");
            else if (i == opened) icons[i].ShowNext(fm.IconCost, points >= fm.IconCost, caption);
            else icons[i].ShowLocked(i + 1, caption);
        }
        for (int i = 0; i < links.Length; i++)
            links[i].color = i + 1 < opened ? new Color(0.3f, 0.85f, 0.4f) : new Color(0.05f, 0.05f, 0.06f);

        factionHint.text = opened >= fm.IconCount
            ? $"Фракция прокачана! Во вкладке «Герои» можно нанять <color=#7FE0FF>{heroName}</color> (3 зв.)"
            : $"Каждая иконка стоит <color=#C77DFF>{fm.IconCost} очков фракции</color>, открываются по порядку ({opened}/{fm.IconCount}).\n" +
              $"Очки дают миссии, общие миссии, объекты и порталы. Откройте все — сможете нанять <color=#7FE0FF>{heroName}</color> (3 зв.)";
    }

    /// <summary>Нажата иконка фракции — открыть следующую.</summary>
    private void OnIconClicked()
    {
        FactionManager fm = FactionManager.Instance;
        if (fm == null) return;
        if (fm.TryUnlockNext(Team.Player))
            ToastUI.Show(FactionManager.IsComplete(Team.Player)
                ? "Фракция прокачана! Теперь можно нанять 3-звёздочного героя фракции"
                : $"Иконка фракции открыта ({fm.GetUnlocked(Team.Player)}/{fm.IconCount})");
        else if (fm.CanUnlockNext(Team.Player, out string reason) == false) ToastUI.Show(reason);
    }

    /// <summary>Вкладка "Герои": 3★ герой фракции, лидер и остальные герои.</summary>
    private void RefreshHeroes()
    {
        foreach (GameObject go in spawned) Destroy(go);
        spawned.Clear();
        HeroManager hm = HeroManager.Instance;

        // Голубой овал — 3★ герой фракции
        FactionData f = FactionManager.GetFaction(Team.Player);
        HeroData fh = f != null ? f.factionHero : null;
        factionHeroButton.gameObject.SetActive(fh != null);
        if (fh != null)
        {
            factionHeroPortrait.Show(fh);
            bool hired = hm.FindHero(Team.Player, fh) != null;
            bool can = hm.CanHire(Team.Player, fh, out string reason);
            factionHeroFade.alpha = hired || can ? 1f : 0.45f;
            int opened = FactionManager.Instance != null ? FactionManager.Instance.GetUnlocked(Team.Player) : 0;
            int total = FactionManager.Instance != null ? FactionManager.Instance.IconCount : 5;
            string status = hired ? "<color=#6EE07A>В команде</color>"
                : can ? $"<color=#FFD84A>НАНЯТЬ\n{fh.hireCost}</color>"
                : reason == "Фракция не прокачана" ? $"<color=#9AA4B5>Фракция\n{opened}/{total}</color>"
                : $"<color=#9AA4B5>{reason}</color>";
            factionHeroText.text = $"{fh.displayName}\n<size=80%>{status}</size>";
        }

        // Синий овал — лидер (первый в иерархии)
        IReadOnlyList<HeroInstance> heroes = hm.GetHeroes(Team.Player);
        HeroInstance leader = heroes.Count > 0 ? heroes[0] : null;
        leaderPortrait.gameObject.SetActive(leader != null);
        if (leader != null) leaderPortrait.Show(leader.Data);
        leaderText.text = leader != null ? $"ЛИДЕР\n<size=80%>{leader.Data.displayName}</size>" : "ЛИДЕР\n<size=80%><color=#9AA4B5>нет</color></size>";

        // Розовая область — остальные герои по иерархии
        int limit = hm.GetHeroLimit(Team.Player);
        heroesHeader.text = $"ВАШИ ГЕРОИ  <size=75%><color=#C9D3E6>{Mathf.Min(heroes.Count, limit)} / {limit} в команде{(heroes.Count > limit ? $"  •  {heroes.Count - limit} в запасе" : "")}</color></size>";
        for (int i = 1; i < heroes.Count; i++)
        {
            DraftRowUI row = Instantiate(rowTemplate, heroList);
            row.gameObject.SetActive(true);
            spawned.Add(row.gameObject);
            HeroData data = heroes[i].Data;
            row.Setup(i, heroes[i], true, OnRowDropped, () => heroInfo.ShowHero(data));
            if (hm.IsReserve(heroes[i])) row.MarkReserve();
            if (hm.StarReady(heroes[i])) row.MarkStarReady(); // 5 уровень — можно повысить звезду
        }
    }

    /// <summary>
    /// Отпустили строку героя: над овалом лидера — герой становится лидером;
    /// иначе новое место — по высоте курсора среди остальных строк.
    /// </summary>
    private void OnRowDropped(DraftRowUI row, float pointerY)
    {
        Vector2 pointer = Input.mousePosition;
        if (leaderSlot != null && RectTransformUtility.RectangleContainsScreenPoint(leaderSlot, pointer, null))
        {
            HeroManager.Instance.MoveHero(Team.Player, row.Hero, 0);
            ToastUI.Show($"{row.Hero.Data.displayName} — новый лидер");
            return;
        }
        int target = 1; // место 0 — у лидера
        foreach (Transform child in heroList)
        {
            if (child == row.transform || !child.gameObject.activeSelf || child.GetComponent<DraftRowUI>() == null) continue;
            if (child.position.y > pointerY) target++;
        }
        HeroManager.Instance.MoveHero(Team.Player, row.Hero, target);
    }
}
