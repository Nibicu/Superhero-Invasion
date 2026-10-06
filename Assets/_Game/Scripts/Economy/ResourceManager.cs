using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Главный "кошелёк" игры. Хранит золото и плутоний игрока и врага
/// и раз в incomeInterval секунд (по GDD — 10) начисляет доход
/// со всех зарегистрированных источников (IIncomeSource).
///
/// Как пользоваться из других скриптов:
///   ResourceManager.Instance.GetGold(Team.Player)
///   ResourceManager.Instance.TrySpend(Team.Player, 500, 0)  // купить что-то за 500 золота
///   ResourceManager.Instance.Add(Team.Player, 200, 10)      // выдать награду
/// </summary>
[DefaultExecutionOrder(-100)] // запускается раньше остальных скриптов, чтобы источники могли сразу зарегистрироваться
public class ResourceManager : MonoBehaviour
{
    /// <summary>Ресурсы одной стороны. Видны в инспекторе во время игры.</summary>
    [Serializable]
    public class Wallet
    {
        public int gold;      // Золото (деньги)
        public int plutonium; // Плутоний (фиолетовый кристалл)
    }

    /// <summary>Единственный экземпляр менеджера — доступен из любого скрипта.</summary>
    public static ResourceManager Instance { get; private set; }

    [Header("Стартовые ресурсы (у обеих сторон одинаковые)")]
    [SerializeField] private int startGold = 1000;
    [SerializeField] private int startPlutonium = 0;

    [Header("Доход")]
    [Tooltip("Раз во сколько секунд начисляется доход")]
    [SerializeField] private float incomeInterval = 10f;

    [Header("Отладка")]
    [Tooltip("Читы для проверки: G = +1000 золота, P = +50 плутония")]
    [SerializeField] private bool debugCheats = true;

    [Header("Текущие ресурсы (только смотреть)")]
    [SerializeField] private Wallet player = new Wallet(); // Кошелёк игрока
    [SerializeField] private Wallet enemy = new Wallet();  // Кошелёк врага

    private readonly List<IIncomeSource> sources = new List<IIncomeSource>(); // Все источники дохода в игре
    private float timer; // Сколько секунд прошло с прошлого начисления

    /// <summary>Вызывается, когда у стороны изменились ресурсы (для обновления UI).</summary>
    public event Action<Team> ResourcesChanged;

    /// <summary>Вызывается при начислении дохода: сторона, золото, плутоний.</summary>
    public event Action<Team, int, int> IncomeReceived;

    /// <summary>Интервал начисления дохода в секундах.</summary>
    public float IncomeInterval => incomeInterval;

    /// <summary>Сколько секунд осталось до следующего начисления.</summary>
    public float TimeToNextIncome => Mathf.Max(0f, incomeInterval - timer);

    /// <summary>Прогресс до следующего начисления: 0 — только что начислили, 1 — сейчас начислим.</summary>
    public float IncomeProgress => Mathf.Clamp01(timer / incomeInterval);

    /// <summary>Запоминаем себя как единственный экземпляр и выдаём стартовые ресурсы.</summary>
    private void Awake()
    {
        Instance = this;
        player.gold = enemy.gold = startGold;
        player.plutonium = enemy.plutonium = startPlutonium;
    }

    /// <summary>Сообщаем UI стартовые значения.</summary>
    private void Start()
    {
        ResourcesChanged?.Invoke(Team.Player);
        ResourcesChanged?.Invoke(Team.Enemy);
    }

    /// <summary>При выходе из игры убираем ссылку на себя.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Каждый кадр двигаем таймер дохода и проверяем читы.</summary>
    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= incomeInterval)
        {
            timer -= incomeInterval;
            GiveIncome(Team.Player);
            GiveIncome(Team.Enemy);
        }

        if (debugCheats)
        {
            if (Input.GetKeyDown(KeyCode.G)) Add(Team.Player, 1000, 0);
            if (Input.GetKeyDown(KeyCode.P)) Add(Team.Player, 0, 50);
        }
    }

    // ---------- Источники дохода ----------

    /// <summary>Добавить источник дохода (вызывают сами источники при появлении).</summary>
    public void RegisterSource(IIncomeSource source)
    {
        if (source != null && !sources.Contains(source))
            sources.Add(source);
    }

    /// <summary>Убрать источник дохода (например, объект захватил враг).</summary>
    public void UnregisterSource(IIncomeSource source)
    {
        sources.Remove(source);
    }

    /// <summary>Посчитать, сколько сторона получит за одно начисление со всех источников.</summary>
    public void GetIncomePerTick(Team team, out int gold, out int plutonium)
    {
        gold = 0;
        plutonium = 0;
        foreach (IIncomeSource source in sources)
        {
            if (source.Owner != team) continue;
            gold += source.GoldIncome;
            plutonium += source.PlutoniumIncome;
        }
    }

    /// <summary>Начислить стороне доход со всех её источников.</summary>
    private void GiveIncome(Team team)
    {
        GetIncomePerTick(team, out int gold, out int plutonium);
        Add(team, gold, plutonium);
        IncomeReceived?.Invoke(team, gold, plutonium);
    }

    // ---------- Работа с кошельком ----------

    /// <summary>Получить кошелёк нужной стороны.</summary>
    private Wallet GetWallet(Team team) => team == Team.Player ? player : enemy;

    /// <summary>Сколько золота у стороны.</summary>
    public int GetGold(Team team) => GetWallet(team).gold;

    /// <summary>Сколько плутония у стороны.</summary>
    public int GetPlutonium(Team team) => GetWallet(team).plutonium;

    /// <summary>Хватает ли стороне ресурсов на покупку.</summary>
    public bool CanAfford(Team team, int gold, int plutonium = 0)
    {
        Wallet w = GetWallet(team);
        return w.gold >= gold && w.plutonium >= plutonium;
    }

    /// <summary>Попробовать потратить ресурсы. Вернёт false и ничего не спишет, если не хватает.</summary>
    public bool TrySpend(Team team, int gold, int plutonium = 0)
    {
        if (!CanAfford(team, gold, plutonium)) return false;
        Wallet w = GetWallet(team);
        w.gold -= gold;
        w.plutonium -= plutonium;
        ResourcesChanged?.Invoke(team);
        return true;
    }

    /// <summary>Выдать стороне ресурсы (доход, награда за миссию и т.п.).</summary>
    public void Add(Team team, int gold, int plutonium = 0)
    {
        Wallet w = GetWallet(team);
        w.gold += gold;
        w.plutonium += plutonium;
        ResourcesChanged?.Invoke(team);
    }
}
