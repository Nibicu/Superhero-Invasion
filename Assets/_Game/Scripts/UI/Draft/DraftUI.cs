using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Драфт перед игрой (ещё не игра): карта стоит, обе стороны покупают героев на стартовый капитал.
/// Слева — герои игрока (их можно перетаскивать — иерархия), справа — герои врага (ИИ покупает в это же время),
/// в центре — кого можно купить. Длится 2 минуты, кнопка "ГОТОВ" начинает игру раньше.
/// Если игрок нажал "ГОТОВ" раньше, ИИ сразу докупает героев, чтобы не отставать.
/// </summary>
public class DraftUI : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Проводить драфт в начале игры (выключите для быстрых тестов)")]
    [SerializeField] private bool draftEnabled = true;
    [Tooltip("Сколько секунд длится драфт")]
    [SerializeField] private float duration = 120f;
    [Tooltip("Как часто ИИ покупает героя (секунды, случайно между двумя числами)")]
    [SerializeField] private Vector2 enemyBuyInterval = new Vector2(3f, 7f);

    [Header("Окно")]
    [SerializeField] private GameObject root;         // Всё окно драфта
    [SerializeField] private TMP_Text timerText;      // "1:59"
    [SerializeField] private TMP_Text goldText;       // "1 500 золота"
    [SerializeField] private TMP_Text myHeader;       // "ВАШИ ГЕРОИ • Трон • 2/6"
    [SerializeField] private TMP_Text enemyHeader;    // "ВРАГ • Разрушители • 3"
    [SerializeField] private Transform myList;        // Колонка героев игрока
    [SerializeField] private Transform enemyList;     // Колонка героев врага
    [SerializeField] private DraftRowUI rowTemplate;  // Шаблон строки (выключен)
    [SerializeField] private Transform grid;          // Сетка героев для покупки
    [SerializeField] private HeroCardUI cardTemplate; // Шаблон карточки (выключен)
    [SerializeField] private Button readyButton;      // "ГОТОВ"
    [SerializeField] private HeroInfoWindowUI heroInfo; // Подробная карточка героя

    private float timeLeft;        // Сколько осталось до конца драфта
    private float enemyBuyTimer;   // Когда ИИ купит следующего героя
    private bool enemyDone;        // ИИ больше некого купить
    private bool running;          // Драфт идёт
    private readonly List<GameObject> spawned = new List<GameObject>(); // Созданные строки и карточки

    /// <summary>Идёт ли драфт (карта стоит, игра ещё не началась).</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>Подписать кнопку, спрятать шаблоны.</summary>
    private void Awake()
    {
        readyButton.onClick.AddListener(Finish);
        rowTemplate.gameObject.SetActive(false);
        cardTemplate.gameObject.SetActive(false);
        root.SetActive(false);
        IsRunning = false;
    }

    /// <summary>Начать драфт (после того, как все менеджеры готовы).</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnChanged;
        ResourceManager.Instance.ResourcesChanged += OnChanged;
        if (!draftEnabled) return;
        running = true;
        IsRunning = true;
        WorldTime.Paused = true; // карта стоит, пока идёт драфт
        timeLeft = duration;
        enemyBuyTimer = Random.Range(enemyBuyInterval.x, enemyBuyInterval.y);
        root.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    /// <summary>Отписаться.</summary>
    private void OnDestroy()
    {
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnChanged;
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnChanged;
        IsRunning = false;
    }

    /// <summary>Таймер драфта и покупки ИИ.</summary>
    private void Update()
    {
        if (!running) return;
        float dt = Time.unscaledDeltaTime;
        timeLeft -= dt;
        int sec = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
        timerText.text = $"{sec / 60}:{sec % 60:00}";
        timerText.color = timeLeft <= 15f ? new Color(1f, 0.45f, 0.4f) : Color.white;

        enemyBuyTimer -= dt;
        if (!enemyDone && enemyBuyTimer <= 0f)
        {
            enemyBuyTimer = Random.Range(enemyBuyInterval.x, enemyBuyInterval.y);
            EnemyBuy();
        }
        if (timeLeft <= 0f) Finish();
    }

    /// <summary>ИИ покупает одного героя (если некого — больше не пытается).</summary>
    private void EnemyBuy()
    {
        EnemyAI ai = FindAnyObjectByType<EnemyAI>();
        if (ai == null || !ai.DraftHire()) enemyDone = true;
    }

    /// <summary>Конец драфта: ИИ докупает героев, карта оживает, начинается игра.</summary>
    private void Finish()
    {
        if (!running) return;
        running = false;
        IsRunning = false;
        for (int i = 0; i < 20 && !enemyDone; i++) EnemyBuy(); // не отставать, если игрок нажал "ГОТОВ" раньше
        root.SetActive(false);
        if (heroInfo != null && heroInfo.IsOpen) heroInfo.Close();
        WorldTime.Paused = false;
        ToastUI.Show("Игра началась!");
    }

    /// <summary>Деньги или герои изменились — обновить окно.</summary>
    private void OnChanged(Team team)
    {
        if (running) Refresh();
    }

    /// <summary>Пересобрать колонки и сетку покупки.</summary>
    private void Refresh()
    {
        foreach (GameObject go in spawned) Destroy(go);
        spawned.Clear();
        HeroManager hm = HeroManager.Instance;
        ResourceManager rm = ResourceManager.Instance;

        goldText.text = $"{rm.GetGold(Team.Player):N0} золота".Replace(',', ' ');
        string myFaction = GameSetup.PlayerFaction != null ? GameSetup.PlayerFaction.displayName : SideName(hm.GetSide(Team.Player));
        string enemyFaction = GameSetup.EnemyFaction != null ? GameSetup.EnemyFaction.displayName : SideName(hm.GetSide(Team.Enemy));
        myHeader.text = $"ВАШИ {SideName(hm.GetSide(Team.Player)).ToUpper()}\n<size=70%><color=#C9D3E6>{myFaction}  •  {hm.GetHeroes(Team.Player).Count} / {hm.GetHeroLimit(Team.Player)}</color></size>";
        enemyHeader.text = $"ВРАГ: {SideName(hm.GetSide(Team.Enemy)).ToUpper()}\n<size=70%><color=#C9D3E6>{enemyFaction}  •  {hm.GetHeroes(Team.Enemy).Count}</color></size>";

        // Колонки
        IReadOnlyList<HeroInstance> mine = hm.GetHeroes(Team.Player);
        for (int i = 0; i < mine.Count; i++) AddRow(myList, i, mine[i], true);
        IReadOnlyList<HeroInstance> theirs = hm.GetHeroes(Team.Enemy);
        for (int i = 0; i < theirs.Count; i++) AddRow(enemyList, i, theirs[i], false);

        // Кого можно купить
        foreach (HeroData data in hm.GetRoster(Team.Player))
        {
            if (hm.IsHired(Team.Player, data)) continue;
            HeroCardUI card = Instantiate(cardTemplate, grid);
            card.gameObject.SetActive(true);
            spawned.Add(card.gameObject);
            bool canHire = hm.CanHire(Team.Player, data, out string reason);
            bool afford = rm.CanAfford(Team.Player, data.hireCost);
            string info = $"<color=#C9D3E6>{UnitClasses.Name(data.unitClass)}</color>  •  <color={(afford ? "#FFD84A" : "#FF6B6B")}>{data.hireCost} золота</color>";
            string action = !canHire ? reason.ToUpper() : afford ? "КУПИТЬ" : "НЕ ХВАТАЕТ";
            HeroData captured = data;
            card.Setup(data, info, action, canHire && afford, () => Buy(captured), () => heroInfo.ShowHero(captured));
        }
    }

    /// <summary>Добавить строку героя в колонку.</summary>
    private void AddRow(Transform list, int index, HeroInstance hero, bool mine)
    {
        DraftRowUI row = Instantiate(rowTemplate, list);
        row.gameObject.SetActive(true);
        spawned.Add(row.gameObject);
        HeroData data = hero.Data;
        row.Setup(index, hero, mine, mine ? OnRowDropped : null, () => heroInfo.ShowHero(data));
    }

    /// <summary>Купить героя.</summary>
    private void Buy(HeroData data)
    {
        if (HeroManager.Instance.TryHire(Team.Player, data, out string error)) ToastUI.Show($"{data.displayName} в вашей команде!");
        else ToastUI.Show(error);
    }

    /// <summary>Отпустили строку героя: новое место — по высоте курсора среди остальных строк.</summary>
    private void OnRowDropped(DraftRowUI row, float pointerY)
    {
        int target = 0;
        foreach (Transform child in myList)
        {
            if (child == row.transform || !child.gameObject.activeSelf) continue;
            if (child.position.y > pointerY) target++; // строки выше курсора — перед героем
        }
        HeroManager.Instance.MoveHero(Team.Player, row.Hero, target); // окно обновится само (HeroesChanged)
    }

    /// <summary>Название стороны по-русски.</summary>
    private static string SideName(Side s) => s == Side.Heroes ? "Герои" : "Злодеи";
}
