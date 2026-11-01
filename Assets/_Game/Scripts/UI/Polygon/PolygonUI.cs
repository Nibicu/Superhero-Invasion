using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Полигон «Битва» (сцена Polygon) — испытание баланса.
/// 1) Выбор арены: обычная (бой до последнего бойца) или битва за флаг.
/// 2) Команды: любые герои, злодеи и мобы (можно повторять), до 4 в команде A и B, уровень 1–5.
/// 3) Автобой на арене. Жизни и опыт не тратятся.
/// 4) Итог: таблица — урон, лечение, полученный урон, добивания, кто выжил.
///    «ЗАНОВО» — тот же состав, «НОВЫЕ КОМАНДЫ» — к выбору, «СЕРИЯ ×20» — 20 боёв подряд на ускорении
///    с итогом «A выиграла 14 из 20» и средними цифрами по бойцам.
/// </summary>
public class PolygonUI : MonoBehaviour
{
    /// <summary>Сколько бойцов в команде.</summary>
    public const int TeamSize = 4;

    /// <summary>Максимальный уровень бойца.</summary>
    public const int MaxLevel = 5;

    /// <summary>Боец, выбранный в команду: кто и какого уровня.</summary>
    private class Pick
    {
        public HeroData data; // Кто
        public int level = 1; // Уровень 1–5
    }

    /// <summary>Сумма цифр одного бойца за серию боёв (или за один бой).</summary>
    private class Tally
    {
        public float damage;   // Нанёс урона
        public float healing;  // Вылечил
        public float taken;    // Получил урона
        public int kills;      // Добил
        public int survived;   // Сколько раз дожил до конца боя
        public float hpLeft;   // Доля здоровья в конце (сумма по боям)
        public float deathTime; // Когда выбыл (последний бой; −1 — выжил)
    }

    [Header("Данные")]
    [Tooltip("Все бойцы, которых можно поставить в команды (заполняет сборщик: герои, злодеи, мобы)")]
    [SerializeField] private HeroData[] units;
    [Tooltip("Цвет арены (флаги, таблички)")]
    [SerializeField] private Color arenaColor = new Color(0.55f, 0.6f, 0.75f);

    [Header("Экраны")]
    [SerializeField] private GameObject arenaPanel;  // Выбор арены
    [SerializeField] private GameObject setupPanel;  // Выбор команд
    [SerializeField] private GameObject resultPanel; // Итог боя / серии
    [SerializeField] private GameObject seriesPanel; // Плашка серии во время боёв

    [Header("Выбор арены")]
    [SerializeField] private Button normalArenaButton; // "ОБЫЧНАЯ АРЕНА"
    [SerializeField] private Button flagArenaButton;   // "БИТВА ЗА ФЛАГ"
    [SerializeField] private Button arenaMenuButton;   // "В МЕНЮ"

    [Header("Команды")]
    [SerializeField] private Transform unitGrid;               // Сетка карточек бойцов
    [SerializeField] private PolygonUnitCardUI cardTemplate;   // Шаблон карточки (выключен)
    [SerializeField] private PolygonSlotUI[] slotsA;           // 4 ячейки команды A
    [SerializeField] private PolygonSlotUI[] slotsB;           // 4 ячейки команды B
    [SerializeField] private TMP_Text powerAText;              // "Сила: 1234"
    [SerializeField] private TMP_Text powerBText;              // "Сила: 1234"
    [SerializeField] private TMP_Text arenaText;               // "Арена: обычная"
    [SerializeField] private TMP_Text hintText;                // Подсказка внизу ("Команда A заполнена")
    [SerializeField] private Button fightButton;               // "В БОЙ!"
    [SerializeField] private Button seriesButton;              // "СЕРИЯ ×20"
    [SerializeField] private Button changeArenaButton;         // "СМЕНИТЬ АРЕНУ"
    [SerializeField] private Button setupMenuButton;           // "В МЕНЮ"
    [SerializeField] private Button clearAButton;              // "ОЧИСТИТЬ" у команды A
    [SerializeField] private Button clearBButton;              // "ОЧИСТИТЬ" у команды B

    [Header("Итог")]
    [SerializeField] private TMP_Text resultTitle;          // "ПОБЕДА КОМАНДЫ A" / "СЕРИЯ: 20 БОЁВ"
    [SerializeField] private TMP_Text resultSubtitle;       // Длина боя, счёт серии
    [SerializeField] private TMP_Text tableHeader;          // Заголовки столбцов
    [SerializeField] private Transform tableRoot;           // Строки таблицы
    [SerializeField] private PolygonStatRowUI rowTemplate;  // Шаблон строки (выключен)
    [SerializeField] private Button againButton;            // "ЗАНОВО"
    [SerializeField] private Button newTeamsButton;         // "НОВЫЕ КОМАНДЫ"
    [SerializeField] private Button resultSeriesButton;     // "СЕРИЯ ×20"
    [SerializeField] private Button resultMenuButton;       // "В МЕНЮ"

    [Header("Серия")]
    [SerializeField] private TMP_Text seriesText;   // "Серия: бой 7 / 20 ..."
    [SerializeField] private Button stopSeriesButton; // "СТОП"
    [Tooltip("Сколько боёв в серии")]
    [SerializeField] private int seriesCount = 20;
    [Tooltip("Во сколько раз ускорить игру во время серии")]
    [SerializeField] private float seriesSpeed = 4f;

    private const string MenuScene = "MainMenu"; // Сцена главного меню

    private readonly List<Pick> teamA = new List<Pick>(); // Команда A (слева)
    private readonly List<Pick> teamB = new List<Pick>(); // Команда B (справа)
    private bool flagArena;          // Выбрана арена битвы за флаг
    private bool inSeries;           // Идёт серия боёв
    private bool stopRequested;      // В серии нажали "СТОП"
    private int fightsDone;          // Сколько боёв серии сыграно
    private int winsA, winsB, draws; // Счёт серии
    private float totalDuration;     // Суммарная длина боёв серии
    private BattleResult lastResult; // Итог последнего боя
    private readonly List<Tally> talliesA = new List<Tally>(); // Цифры бойцов команды A (по ячейкам)
    private readonly List<Tally> talliesB = new List<Tally>(); // Цифры бойцов команды B
    private readonly List<GameObject> tableRows = new List<GameObject>(); // Созданные строки таблицы
    private Coroutine hintRoutine;   // Таймер подсказки

    // ---------- Запуск ----------

    /// <summary>Подписать кнопки, заполнить список бойцов и открыть выбор арены.</summary>
    private void Awake()
    {
        Time.timeScale = 1f;
        cardTemplate.gameObject.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        hintText.text = "";

        foreach (HeroData d in units)
        {
            if (d == null) continue;
            PolygonUnitCardUI card = Instantiate(cardTemplate, unitGrid);
            card.gameObject.SetActive(true);
            HeroData captured = d;
            card.Setup(d, toA => Add(toA ? teamA : teamB, captured, toA ? "A" : "B"));
        }

        normalArenaButton.onClick.AddListener(() => ChooseArena(false));
        flagArenaButton.onClick.AddListener(() => ChooseArena(true));
        arenaMenuButton.onClick.AddListener(ToMenu);
        fightButton.onClick.AddListener(() => StartRun(false));
        seriesButton.onClick.AddListener(() => StartRun(true));
        changeArenaButton.onClick.AddListener(() => Show(arenaPanel));
        setupMenuButton.onClick.AddListener(ToMenu);
        clearAButton.onClick.AddListener(() => { teamA.Clear(); RefreshTeams(); });
        clearBButton.onClick.AddListener(() => { teamB.Clear(); RefreshTeams(); });
        againButton.onClick.AddListener(() => StartRun(false));
        newTeamsButton.onClick.AddListener(() => Show(setupPanel));
        resultSeriesButton.onClick.AddListener(() => StartRun(true));
        resultMenuButton.onClick.AddListener(ToMenu);
        stopSeriesButton.onClick.AddListener(StopSeries);

        BattleManager.BattleEnded += OnBattleEnded;
        RefreshTeams();
        Show(arenaPanel);
    }

    private void OnDestroy()
    {
        BattleManager.BattleEnded -= OnBattleEnded;
        Time.timeScale = 1f;
    }

    /// <summary>Показать один экран (null — все спрятаны, идёт бой).</summary>
    private void Show(GameObject panel)
    {
        arenaPanel.SetActive(panel == arenaPanel);
        setupPanel.SetActive(panel == setupPanel);
        resultPanel.SetActive(panel == resultPanel);
        if (panel == setupPanel) RefreshTeams();
    }

    /// <summary>Выбрана арена — переходим к выбору команд.</summary>
    private void ChooseArena(bool flag)
    {
        flagArena = flag;
        arenaText.text = flag ? "Арена: <color=#FFD84A>битва за флаг</color>" : "Арена: <color=#FFD84A>обычная</color>";
        Show(setupPanel);
    }

    /// <summary>Вернуться в главное меню.</summary>
    private void ToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }

    // ---------- Команды ----------

    /// <summary>Добавить бойца в команду (если в ней есть место).</summary>
    private void Add(List<Pick> team, HeroData data, string teamName)
    {
        if (team.Count >= TeamSize) { ShowHint($"В команде {teamName} уже {TeamSize} бойца"); return; }
        team.Add(new Pick { data = data, level = 1 });
        RefreshTeams();
    }

    /// <summary>Перерисовать обе команды, их силу и доступность кнопок боя.</summary>
    private void RefreshTeams()
    {
        FillSlots(slotsA, teamA);
        FillSlots(slotsB, teamB);
        powerAText.text = $"Сила: {TeamPower(teamA)}";
        powerBText.text = $"Сила: {TeamPower(teamB)}";
        bool ready = teamA.Count > 0 && teamB.Count > 0;
        fightButton.interactable = ready;
        seriesButton.interactable = ready;
    }

    /// <summary>Заполнить ячейки команды.</summary>
    private void FillSlots(PolygonSlotUI[] slots, List<Pick> team)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i >= team.Count) { slots[i].Show(null, 0, MaxLevel, null, null); continue; }
            Pick p = team[i];
            slots[i].Show(p.data, p.level, MaxLevel,
                delta => { p.level = Mathf.Clamp(p.level + delta, 1, MaxLevel); RefreshTeams(); },
                () => { team.Remove(p); RefreshTeams(); });
        }
    }

    /// <summary>Сила команды (как в игре: по характеристикам с учётом уровня).</summary>
    private static int TeamPower(List<Pick> team)
    {
        int sum = 0;
        foreach (Pick p in team) sum += BattleCalculator.UnitPower(p.data, StatsFor(p));
        return sum;
    }

    /// <summary>Характеристики бойца на его уровне (+5% за уровень, как у героев в игре).</summary>
    private static HeroStats StatsFor(Pick p) =>
        p.data.baseStats.Scaled(1f + HeroInstance.StatGrowthPerLevel * (p.level - 1));

    /// <summary>Команда для боя. Мобы (нельзя нанять) ведут себя как охрана — не отступают.</summary>
    private static List<BattleUnit> ToUnits(List<Pick> team)
    {
        var list = new List<BattleUnit>();
        foreach (Pick p in team)
            list.Add(new BattleUnit { data = p.data, stats = StatsFor(p), hpFraction = 1f, level = p.level, guard = !p.data.hireable });
        return list;
    }

    // ---------- Бой и серия ----------

    /// <summary>Начать один бой (series = false) или серию из seriesCount боёв.</summary>
    private void StartRun(bool series)
    {
        if (teamA.Count == 0 || teamB.Count == 0) { Show(setupPanel); ShowHint("Нужен хотя бы один боец в каждой команде"); return; }
        inSeries = series;
        stopRequested = false;
        fightsDone = winsA = winsB = draws = 0;
        totalDuration = 0f;
        ResetTallies(talliesA, teamA.Count);
        ResetTallies(talliesB, teamB.Count);
        Time.timeScale = series ? seriesSpeed : 1f;
        StartFight();
    }

    /// <summary>Выпустить команды на арену.</summary>
    private void StartFight()
    {
        Show(null);
        seriesPanel.SetActive(inSeries);
        UpdateSeriesText();
        BattleManager.Instance.StartDuelBattle("Полигон", arenaColor, ToUnits(teamA), ToUnits(teamB), flagArena, OnFightFinished);
    }

    /// <summary>
    /// Итог боя (бойцы ещё на арене): записать цифры каждого бойца и счёт.
    /// Бой, остановленный кнопкой «СТОП» в серии, не считается.
    /// </summary>
    private void OnFightFinished(BattleResult r)
    {
        lastResult = r;
        if (inSeries && stopRequested) return;
        fightsDone++;
        totalDuration += r.duration;
        if (r.draw) draws++;
        else if (r.win) winsA++;
        else winsB++;
        Collect(BattleManager.Instance.LeftTeam, talliesA);
        Collect(BattleManager.Instance.RightTeam, talliesB);
        UpdateSeriesText();
    }

    /// <summary>Арена очищена: следующий бой серии или таблица итогов.</summary>
    private void OnBattleEnded()
    {
        if (inSeries && !stopRequested && fightsDone < seriesCount)
        {
            StartCoroutine(NextFightNextFrame());
            return;
        }
        Time.timeScale = 1f;
        seriesPanel.SetActive(false);
        ShowResults();
    }

    /// <summary>Следующий бой — кадром позже (BattleManager должен до конца убрать прошлый бой).</summary>
    private IEnumerator NextFightNextFrame()
    {
        yield return null;
        StartFight();
    }

    /// <summary>Остановить серию: текущий бой прерывается и не считается.</summary>
    private void StopSeries()
    {
        if (!inSeries) return;
        stopRequested = true;
        BattleManager.Instance.Retreat();
    }

    /// <summary>Текст плашки серии.</summary>
    private void UpdateSeriesText()
    {
        if (seriesText == null) return;
        seriesText.text = $"СЕРИЯ: бой {Mathf.Min(fightsDone + 1, seriesCount)} / {seriesCount}   •   " +
                          $"<color=#7FB8FF>A: {winsA}</color>   <color=#FF7A7A>B: {winsB}</color>   ничьи: {draws}   •   ускорение ×{seriesSpeed:0}";
    }

    // ---------- Цифры ----------

    /// <summary>Завести пустые счётчики для count бойцов.</summary>
    private static void ResetTallies(List<Tally> list, int count)
    {
        list.Clear();
        for (int i = 0; i < count; i++) list.Add(new Tally());
    }

    /// <summary>Добавить цифры бойцов команды (по порядку ячеек; клоны не считаются — их урон уже у хозяина).</summary>
    private static void Collect(IReadOnlyList<Fighter> fighters, List<Tally> tallies)
    {
        int i = 0;
        foreach (Fighter f in fighters)
        {
            if (f == null || f.IsClone) continue;
            if (i >= tallies.Count) break;
            Tally t = tallies[i++];
            t.damage += f.DamageDealt;
            t.healing += f.HealingDone;
            t.taken += f.DamageTaken;
            t.kills += f.Kills;
            if (f.IsAlive) { t.survived++; t.hpLeft += f.Health.Fraction; t.deathTime = -1f; }
            else t.deathTime = f.DeathTime;
        }
    }

    /// <summary>Показать итог: один бой — победитель и цифры; серия — счёт и средние цифры за бой.</summary>
    private void ShowResults()
    {
        Show(resultPanel);
        int n = Mathf.Max(1, fightsDone);
        if (inSeries)
        {
            resultTitle.text = fightsDone == 0 ? "СЕРИЯ ОСТАНОВЛЕНА" : $"СЕРИЯ: {fightsDone} {Fights(fightsDone)}";
            resultSubtitle.text = $"<color=#7FB8FF>Команда A: {winsA} {Wins(winsA)} ({Percent(winsA, n)})</color>   •   " +
                                  $"<color=#FF7A7A>Команда B: {winsB} {Wins(winsB)} ({Percent(winsB, n)})</color>   •   " +
                                  $"Ничьи: {draws}   •   Средняя длина боя: {totalDuration / n:0} с";
            tableHeader.text = "Средние цифры за один бой";
        }
        else
        {
            resultTitle.text = lastResult.draw ? "<color=#FFD84A>НИЧЬЯ</color>"
                : lastResult.win ? "<color=#7FB8FF>ПОБЕДА КОМАНДЫ A</color>" : "<color=#FF7A7A>ПОБЕДА КОМАНДЫ B</color>";
            resultSubtitle.text = $"{lastResult.details}   •   бой длился {lastResult.duration:0} с";
            tableHeader.text = "Цифры каждого бойца";
        }

        foreach (GameObject g in tableRows) Destroy(g);
        tableRows.Clear();
        AddRow(true, "Ком.", "Боец", "Ур.", "Урон", "Лечение", "Получил", "Добил", inSeries ? "Выжил" : "Итог", header: true);
        AddTeamRows(teamA, talliesA, true, n);
        AddTeamRows(teamB, talliesB, false, n);
    }

    /// <summary>Строки одной команды.</summary>
    private void AddTeamRows(List<Pick> team, List<Tally> tallies, bool isA, int n)
    {
        for (int i = 0; i < team.Count && i < tallies.Count; i++)
        {
            Tally t = tallies[i];
            string last = inSeries
                ? Percent(t.survived, n)
                : t.deathTime < 0f ? $"<color=#8CE08C>жив, {t.hpLeft * 100f:0}% HP</color>" : $"<color=#FF9A8A>выбыл на {t.deathTime:0} с</color>";
            AddRow(isA, isA ? "A" : "B", team[i].data.displayName, team[i].level.ToString(),
                $"{t.damage / n:0}", $"{t.healing / n:0}", $"{t.taken / n:0}",
                inSeries ? $"{(float)t.kills / n:0.0}" : t.kills.ToString(), last);
        }
    }

    /// <summary>Добавить строку таблицы.</summary>
    private void AddRow(bool isA, string team, string name, string level, string dmg, string heal, string taken, string kills, string last, bool header = false)
    {
        PolygonStatRowUI row = Instantiate(rowTemplate, tableRoot);
        row.gameObject.SetActive(true);
        row.Set(isA, team, name, level, dmg, heal, taken, kills, last);
        if (header) row.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 0.95f);
        tableRows.Add(row.gameObject);
    }

    private static string Percent(int part, int total) => $"{100f * part / Mathf.Max(1, total):0}%";

    /// <summary>"бой" / "боя" / "боёв".</summary>
    private static string Fights(int n) => Plural(n, "БОЙ", "БОЯ", "БОЁВ");

    /// <summary>"победа" / "победы" / "побед".</summary>
    private static string Wins(int n) => Plural(n, "победа", "победы", "побед");

    /// <summary>Русское окончание по числу: 1 бой, 2 боя, 5 боёв.</summary>
    private static string Plural(int n, string one, string few, string many)
    {
        int m10 = n % 10, m100 = n % 100;
        if (m10 == 1 && m100 != 11) return one;
        if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
        return many;
    }

    // ---------- Подсказка ----------

    /// <summary>Короткая подсказка внизу экрана выбора команд.</summary>
    private void ShowHint(string text)
    {
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(HintRoutine(text));
    }

    private IEnumerator HintRoutine(string text)
    {
        hintText.text = text;
        yield return new WaitForSecondsRealtime(2.5f);
        hintText.text = "";
    }
}
