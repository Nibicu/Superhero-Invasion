using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно перед боем. Открывается, когда наша команда приезжает захватывать объект.
/// Слева — наша команда и её сила, справа — охрана объекта и её сила,
/// по центру — прогноз ("Победа с небольшими потерями" / "Силы равны" / "Вы точно проиграете")
/// и отсчёт 10 секунд. Не выбрали — запускается автобой.
/// "АВТОБОЙ" — идёт таймер захвата, итог по прогнозу.
/// "НАЧАТЬ БОЙ" — переход на арену (BattleManager).
/// Если команды приехали одновременно, окна показываются по очереди.
/// </summary>
public class BattlePrepWindowUI : MonoBehaviour
{
    /// <summary>Единственное окно в сцене.</summary>
    public static BattlePrepWindowUI Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [Header("Наша команда")]
    [SerializeField] private TMP_Text ourPowerText;
    [SerializeField] private Transform ourList;
    [Header("Защитники")]
    [SerializeField] private TMP_Text enemyPowerText;
    [SerializeField] private Transform enemyList;
    [SerializeField] private BattleUnitRowUI rowTemplate;
    [Header("Прогноз и выбор")]
    [SerializeField] private TMP_Text verdictText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Button autoButton;
    [SerializeField] private Button fightButton;
    [Tooltip("Сколько секунд даётся на выбор")]
    [SerializeField] private float decisionTime = 10f;

    /// <summary>Запрос на бой: какой объект и какая команда.</summary>
    private class Request
    {
        public MapObject target;
        public SquadUnit unit;
    }

    private readonly Queue<Request> queue = new Queue<Request>();
    private readonly List<GameObject> rows = new List<GameObject>();
    private Request current;
    private BattleForecast forecast;
    private float timer;

    private void Awake()
    {
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
        autoButton.onClick.AddListener(ChooseAuto);
        fightButton.onClick.AddListener(ChooseFight);
        root.SetActive(false);
    }

    private void OnEnable() => BattleManager.BattleEnded += ShowNextIfIdle;
    private void OnDisable() => BattleManager.BattleEnded -= ShowNextIfIdle;

    /// <summary>Команда приехала к объекту — поставить бой в очередь.</summary>
    public void RequestBattle(MapObject target, SquadUnit unit)
    {
        queue.Enqueue(new Request { target = target, unit = unit });
        ShowNextIfIdle();
    }

    /// <summary>Отсчёт времени на выбор.</summary>
    private void Update()
    {
        if (current == null) return;
        timer -= Time.deltaTime;
        countdownText.text = $"Автобой через <color=#FFFFFF>{Mathf.CeilToInt(Mathf.Max(0f, timer))}</color> с";
        if (timer <= 0f) ChooseAuto();
    }

    /// <summary>Показать следующий запрос, если окно свободно и бой не идёт.</summary>
    private void ShowNextIfIdle()
    {
        if (current != null) return;
        if (BattleManager.Instance != null && BattleManager.Instance.IsRunning) return;
        while (queue.Count > 0)
        {
            Request r = queue.Dequeue();
            if (r.target == null || r.unit == null) continue;
            Show(r);
            return;
        }
        root.SetActive(false);
    }

    /// <summary>Заполнить окно.</summary>
    private void Show(Request r)
    {
        current = r;
        timer = decisionTime;
        Squad squad = r.unit.Squad;
        MapObjectData data = r.target.Data;

        int our = BattleCalculator.SquadPower(squad);
        int their = BattleCalculator.GarrisonPower(data);
        forecast = BattleCalculator.Forecast(our, their);

        titleText.text = $"БИТВА ЗА ОБЪЕКТ: {data.displayName.ToUpper()}";
        ourPowerText.text = $"Сила: <color=#7FB8FF>{our}</color>";
        enemyPowerText.text = $"Сила: <color=#FF7A7A>{their}</color>";
        verdictText.text = BattleCalculator.ForecastTitle(forecast);
        hintText.text = BattleCalculator.ForecastHint(forecast);

        foreach (GameObject g in rows) Destroy(g);
        rows.Clear();
        int hpPercent = Mathf.RoundToInt(squad.HpFraction * 100);
        foreach (HeroInstance h in squad.AllHeroes)
            AddRow(ourList, h.Data, $"Ур. {h.Level}  •  HP {hpPercent}%  •  сила {BattleCalculator.StatsPower(h.Stats)}");
        if (data.waves != null)
            for (int w = 0; w < data.waves.Length; w++)
                foreach (GuardEntry g in data.waves[w].guards)
                    if (g != null && g.unit != null)
                        AddRow(enemyList, g.unit, $"Территория {w + 1}  •  Ур. {g.level}  •  сила {BattleCalculator.StatsPower(BattleCalculator.GuardStats(g))}");

        root.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void AddRow(Transform parent, HeroData data, string info)
    {
        BattleUnitRowUI row = Instantiate(rowTemplate, parent);
        row.gameObject.SetActive(true);
        row.Setup(data, info);
        rows.Add(row.gameObject);
    }

    /// <summary>Автобой: объект начинает обычный захват по таймеру, итог — по прогнозу.</summary>
    private void ChooseAuto()
    {
        if (current == null) return;
        Request r = current;
        Close();
        r.target.BeginAutoCapture(r.unit, forecast);
        ShowNextIfIdle();
    }

    /// <summary>Начать бой самому — переход на арену.</summary>
    private void ChooseFight()
    {
        if (current == null) return;
        Request r = current;
        Close();
        r.target.OnManualBattleStarted();
        BattleManager.Instance.StartBattle(r.target.Data, r.unit.Squad,
            (win, hp) => r.target.OnManualBattleFinished(r.unit, win, hp));
    }

    private void Close()
    {
        current = null;
        root.SetActive(false);
    }
}
