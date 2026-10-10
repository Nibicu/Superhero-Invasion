using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно перед боем. Открывается, когда закончилась подготовка к бою (20 с) за объект или базу,
/// если в бою участвует игрок:
/// - НАПАДЕНИЕ (наша команда напала): слева — наша команда, справа — защитники;
/// - ЗАЩИТА (враг напал на наш объект или базу): слева — наши защитники
///   (охрана объекта или гарнизон базы), справа — нападающая команда врага.
/// По центру — прогноз ("Победа с небольшими потерями" / "Силы равны" / "Вы точно проиграете")
/// и отсчёт 10 секунд. Не выбрали — автобой.
/// "АВТОБОЙ" — итог сразу, по прогнозу.
/// "НАЧАТЬ БОЙ" — переход на арену (BattleManager).
/// - БИТВА ЗА ФЛАГ (наша и вражеская команды напали на объект одновременно):
///   только "НАЧАТЬ БОЙ" — автобоя нет, через 10 с бой начнётся сам.
/// Если бои начались одновременно, окна показываются по очереди.
/// </summary>
public class BattlePrepWindowUI : MonoBehaviour
{
    /// <summary>Единственное окно в сцене.</summary>
    public static BattlePrepWindowUI Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [Header("Наша сторона")]
    [Tooltip("Подпись над левым списком (\"ВАША КОМАНДА\" / \"ВАШИ ЗАЩИТНИКИ\")")]
    [SerializeField] private TMP_Text ourLabelText;
    [SerializeField] private TMP_Text ourPowerText;
    [SerializeField] private Transform ourList;
    [Header("Противник")]
    [Tooltip("Подпись над правым списком (\"ЗАЩИТНИКИ\" / \"НАПАДАЮЩИЕ\")")]
    [SerializeField] private TMP_Text enemyLabelText;
    [SerializeField] private TMP_Text enemyPowerText;
    [SerializeField] private Transform enemyList;
    [SerializeField] private BattleUnitRowUI rowTemplate;
    [Header("Прогноз и выбор")]
    [SerializeField] private TMP_Text verdictText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Button autoButton;
    [SerializeField] private Button fightButton;
    [Tooltip("\"ОТСТУПИТЬ\": нападали — команда уходит домой без боя; защищались — объект/база остаются без защиты")]
    [SerializeField] private Button retreatButton;
    [Tooltip("Сколько секунд даётся на выбор")]
    [SerializeField] private float decisionTime = 10f;

    private readonly Queue<AttackableSite> queue = new Queue<AttackableSite>(); // Места, где ждут решения игрока
    private readonly List<GameObject> rows = new List<GameObject>(); // Созданные строки бойцов
    private AttackableSite current; // Какой бой сейчас показан
    private float timer;            // Сколько секунд осталось на выбор

    private void Awake()
    {
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
        autoButton.onClick.AddListener(ChooseAuto);
        fightButton.onClick.AddListener(ChooseFight);
        if (retreatButton != null) retreatButton.onClick.AddListener(ChooseRetreat);
        root.SetActive(false);
    }

    private void OnEnable() => BattleManager.BattleEnded += ShowNextIfIdle;
    private void OnDisable() => BattleManager.BattleEnded -= ShowNextIfIdle;

    /// <summary>Подготовка к бою закончилась — поставить бой в очередь.</summary>
    public void RequestBattle(AttackableSite site)
    {
        queue.Enqueue(site);
        ShowNextIfIdle();
    }

    /// <summary>Отсчёт времени на выбор.</summary>
    private void Update()
    {
        if (current == null) return;
        timer -= Time.deltaTime;
        int left = Mathf.CeilToInt(Mathf.Max(0f, timer));
        if (current.IsContested)
        {
            // В битве за флаг автобоя нет — по истечении времени бой начинается сам
            countdownText.text = $"Бой начнётся через <color=#FFFFFF>{left}</color> с";
            if (timer <= 0f) ChooseFight();
            return;
        }
        countdownText.text = $"Автобой через <color=#FFFFFF>{left}</color> с";
        if (timer <= 0f) ChooseAuto();
    }

    /// <summary>Показать следующий запрос, если окно свободно и бой не идёт.</summary>
    private void ShowNextIfIdle()
    {
        if (current != null) return;
        if (BattleManager.Instance != null && BattleManager.Instance.IsRunning) return;
        while (queue.Count > 0)
        {
            AttackableSite s = queue.Dequeue();
            if (s == null || s.Phase != AttackPhase.Deciding) continue; // нападение уже закончилось
            Show(s);
            return;
        }
        root.SetActive(false);
    }

    /// <summary>Заполнить окно.</summary>
    private void Show(AttackableSite site)
    {
        current = site;
        timer = decisionTime;
        foreach (GameObject g in rows) Destroy(g);
        rows.Clear();
        autoButton.gameObject.SetActive(!site.IsContested); // в битве за флаг автобоя нет

        if (site.IsContested)
        {
            ShowContested(site);
            return;
        }
        bool defending = site.PlayerDefends;

        // "Наши" — нападающая команда или защитники, в зависимости от того, кто напал
        int attackerPower = BattleCalculator.SquadPower(site.Attacker.Squad);
        int our = defending ? site.DefenderPower : attackerPower;
        int their = defending ? attackerPower : site.DefenderPower;
        BattleForecast forecast = BattleCalculator.Forecast(our, their);

        titleText.text = defending ? $"ЗАЩИТА: {site.SiteName.ToUpper()}" : $"БИТВА: {site.SiteName.ToUpper()}";
        if (ourLabelText != null) ourLabelText.text = defending ? "ВАШИ ЗАЩИТНИКИ" : "ВАША КОМАНДА";
        if (enemyLabelText != null) enemyLabelText.text = defending ? "НАПАДАЮЩИЕ" : "ЗАЩИТНИКИ";
        ourPowerText.text = $"Сила: <color=#7FB8FF>{our}</color>";
        enemyPowerText.text = $"Сила: <color=#FF7A7A>{their}</color>";
        verdictText.text = BattleCalculator.ForecastTitle(forecast);
        hintText.text = defending ? BattleCalculator.DefenseHint(forecast) : BattleCalculator.ForecastHint(forecast);

        foreach (BattleUnit u in OurUnits(site)) AddRow(ourList, u.data, u.info);
        foreach (List<BattleUnit> wave in TheirWaves(site))
            foreach (BattleUnit u in wave)
                AddRow(enemyList, u.data, u.info);

        root.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Битва за флаг: слева наша команда, справа команда врага и охрана
    /// (у каждой команды своя копия охраны). Только "НАЧАТЬ БОЙ".
    /// </summary>
    private void ShowContested(AttackableSite site)
    {
        Squad mine = site.PlayerContestant.Squad;
        Squad theirs = site.EnemyContestant.Squad;
        int our = BattleCalculator.SquadPower(mine);
        int their = BattleCalculator.SquadPower(theirs);

        titleText.text = $"БИТВА ЗА ФЛАГ: {site.SiteName.ToUpper()}";
        if (ourLabelText != null) ourLabelText.text = "ВАША КОМАНДА";
        if (enemyLabelText != null) enemyLabelText.text = "КОМАНДА ВРАГА";
        ourPowerText.text = $"Сила: <color=#7FB8FF>{our}</color>";
        enemyPowerText.text = $"Сила: <color=#FF7A7A>{their}</color>";
        verdictText.text = "<color=#FFD84A>ДВЕ КОМАНДЫ — БИТВА ЗА ФЛАГ!</color>";

        int guardsCount = 0;
        List<List<BattleUnit>> waves = site.GetDefenderWaves();
        foreach (List<BattleUnit> wave in waves) guardsCount += wave.Count;
        hintText.text = $"Охрана: {waves.Count} территории, {guardsCount} бойцов, сила {site.DefenderPower} — у каждой команды своя копия " +
                        "(вы — слева, враг — справа). Пройдите свои территории быстрее врага и удержите флаг в центре 10 с.";

        foreach (BattleUnit u in AttackableSite.GetSquadUnits(site.PlayerContestant)) AddRow(ourList, u.data, u.info);
        foreach (BattleUnit u in AttackableSite.GetSquadUnits(site.EnemyContestant)) AddRow(enemyList, u.data, u.info);

        root.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>Наши бойцы: своя команда или (при защите) все защитники одним списком.</summary>
    private static List<BattleUnit> OurUnits(AttackableSite site)
    {
        if (!site.PlayerDefends) return site.GetAttackerUnits();
        var list = new List<BattleUnit>();
        foreach (List<BattleUnit> wave in site.GetDefenderWaves()) list.AddRange(wave);
        return list;
    }

    /// <summary>Противники по территориям: охрана места или (при защите) нападающая команда одной волной.</summary>
    private static List<List<BattleUnit>> TheirWaves(AttackableSite site)
    {
        if (!site.PlayerDefends) return site.GetDefenderWaves();
        return new List<List<BattleUnit>> { site.GetAttackerUnits() };
    }

    private void AddRow(Transform parent, HeroData data, string info)
    {
        BattleUnitRowUI row = Instantiate(rowTemplate, parent);
        row.gameObject.SetActive(true);
        row.Setup(data, info);
        rows.Add(row.gameObject);
    }

    /// <summary>Автобой: итог сразу, по прогнозу.</summary>
    private void ChooseAuto()
    {
        if (current == null || current.IsContested) return; // в битве за флаг автобоя нет
        AttackableSite site = current;
        Close();
        site.ResolveAuto();
        ShowNextIfIdle();
    }

    /// <summary>Начать бой самому — переход на арену.</summary>
    private void ChooseFight()
    {
        if (current == null) return;
        AttackableSite site = current;
        Close();
        if (site.IsContested)
        {
            // Битва за флаг: наша команда, команда врага и охрана (BattleManager продублирует её для каждой команды)
            List<BattleUnit> mine = AttackableSite.GetSquadUnits(site.PlayerContestant);
            List<BattleUnit> rival = AttackableSite.GetSquadUnits(site.EnemyContestant);
            // У каждой стороны своя копия охраны (у портала — без уже пройденных территорий)
            List<List<BattleUnit>> guardWaves = site.GetContestWaves(Team.Player);
            List<List<BattleUnit>> rivalWaves = site.GetContestWaves(Team.Enemy);
            site.OnManualBattleStarted();
            BattleManager.Instance.StartContestedBattle(site.SiteName, site.SiteColor, mine, rival, guardWaves, site.OnContestedBattleFinished, rivalWaves);
            return;
        }
        if (site.PlayerDefends && site.HasReinforcement)
        {
            // Защита с подкреплением: наша команда — на своей территории, охрана объекта — перед ней,
            // враг сначала прорывается через охрану
            var squad = new List<BattleUnit>();
            var guardUnits = new List<BattleUnit>();
            foreach (List<BattleUnit> wave in site.GetDefenderWaves())
                foreach (BattleUnit u in wave)
                    (u.reinforcement ? squad : guardUnits).Add(u);
            List<BattleUnit> attackers = site.GetAttackerUnits();
            site.OnManualBattleStarted();
            BattleManager.Instance.StartDefenseBattle(site.SiteName, site.SiteColor, squad, attackers, guardUnits, site.OnManualBattleFinished);
            return;
        }
        List<BattleUnit> ours = OurUnits(site);
        List<List<BattleUnit>> theirs = TheirWaves(site);
        site.OnManualBattleStarted();
        BattleManager.Instance.StartBattle(site.SiteName, site.SiteColor, ours, theirs, site.OnManualBattleFinished);
    }

    /// <summary>
    /// "ОТСТУПИТЬ": боя не будет. Нападали — команда уходит домой без потерь;
    /// битва за флаг — наша команда уходит, враг бьётся с охраной сам;
    /// защищались — нападающие побеждают без боя.
    /// </summary>
    private void ChooseRetreat()
    {
        if (current == null) return;
        AttackableSite site = current;
        Close();
        site.PlayerWithdraw();
        ShowNextIfIdle();
    }

    private void Close()
    {
        current = null;
        root.SetActive(false);
    }
}
