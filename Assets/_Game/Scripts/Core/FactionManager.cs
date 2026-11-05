using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прокачка фракции (объект Managers): 5 иконок у игрока и у врага.
/// ОЧКИ ФРАКЦИИ — главный ресурс игры (бывшее золото): их дают миссии, общие миссии, объекты и доход базы,
/// на них нанимают героев и открывают иконки. Хранятся в кошельке ResourceManager (поле gold — это очки фракции).
/// Иконки открываются по порядку в тактической паузе, каждая стоит iconCost очков, и дают:
///   1 — Радар (видно охрану, награды миссий, базу и команды врага);
///   2 — резерв найма: лимит героев +reserveBonus (2);
///   3 — найм героев 2★;
///   4 — пока ничего (придумаем позже);
///   5 — найм 3★ героя своей фракции (FactionData → Faction Hero).
/// Строительства на базе больше нет — всё это раньше давали постройки.
///
/// Как пользоваться из других скриптов:
///   FactionManager.AddPoints(Team.Player, 150)         // выдать очки (безопасно, даже если менеджера нет)
///   FactionManager.Instance.TryUnlockNext(Team.Player) // открыть следующую иконку
///   FactionManager.HasRadar(Team.Player)               // открыт ли Радар
/// </summary>
[DefaultExecutionOrder(-90)]
public class FactionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static FactionManager Instance { get; private set; }

    /// <summary>Номера иконок (с 1): что открывает каждая.</summary>
    public const int RadarIcon = 1, ReserveIcon = 2, TwoStarIcon = 3, FactionHeroIcon = 5;

    [Header("Иконки фракции")]
    [Tooltip("Сколько иконок у фракции")]
    [SerializeField, Min(1)] private int iconCount = 5;
    [Tooltip("Сколько очков фракции стоит одна иконка")]
    [SerializeField, Min(0)] private int iconCost = 2000;
    [Tooltip("Иконка 2 (резерв найма): на сколько больше лимит героев")]
    [SerializeField, Min(0)] private int reserveBonus = 2;

    [Header("Фракции, если игра запущена без меню")]
    [Tooltip("Все фракции (Data/Factions). Без меню игрок получает первую фракцию своей стороны, враг — первую фракцию другой стороны")]
    [SerializeField] private FactionData[] factions = new FactionData[0];

    [Header("Отладка")]
    [Tooltip("Клавиша F — +500 очков фракции игроку")]
    [SerializeField] private bool debugCheats = true;

    [Header("Сейчас (только смотреть)")]
    [SerializeField] private int playerUnlocked; // Сколько иконок открыл игрок
    [SerializeField] private int enemyUnlocked;  // Сколько иконок открыл враг

    /// <summary>Изменились очки или иконки стороны (для интерфейса).</summary>
    public event Action<Team> Changed;

    /// <summary>Сколько иконок у фракции.</summary>
    public int IconCount => iconCount;

    /// <summary>Цена одной иконки в очках фракции.</summary>
    public int IconCost => iconCost;

    /// <summary>Запомнить себя.</summary>
    private void Awake() => Instance = this;

    /// <summary>Очки лежат в кошельке ResourceManager — его изменения = изменения очков.</summary>
    private void Start()
    {
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged += OnResourcesChanged;
    }

    /// <summary>Убрать ссылку и отписаться.</summary>
    private void OnDestroy()
    {
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnResourcesChanged;
        if (Instance == this) Instance = null;
    }

    private void OnResourcesChanged(Team team) => Changed?.Invoke(team);

    /// <summary>Чит для проверки: F — +500 очков.</summary>
    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.F)) AddPoints(Team.Player, 500);
    }

    // ---------- Фракция стороны ----------

    /// <summary>Фракция стороны: выбранная в меню, а без меню — первая подходящая из списка factions.</summary>
    public static FactionData GetFaction(Team team)
    {
        if (GameSetup.IsSet)
            return team == Team.Player ? GameSetup.PlayerFaction : GameSetup.EnemyFaction;
        if (Instance == null || HeroManager.Instance == null) return null;
        Side side = HeroManager.Instance.GetSide(team);
        foreach (FactionData f in Instance.factions)
            if (f != null && f.side == side) return f;
        return null;
    }

    // ---------- Очки (кошелёк ResourceManager) ----------

    /// <summary>Сколько очков фракции у стороны.</summary>
    public int GetPoints(Team team) => ResourceManager.Instance != null ? ResourceManager.Instance.GetGold(team) : 0;

    /// <summary>Сколько иконок открыла сторона (0..IconCount).</summary>
    public int GetUnlocked(Team team) => team == Team.Player ? playerUnlocked : enemyUnlocked;

    /// <summary>Выдать стороне очки фракции (в кошелёк; если менеджера нет — ничего не делает).</summary>
    public static void AddPoints(Team team, int amount)
    {
        if (ResourceManager.Instance == null || amount <= 0) return;
        ResourceManager.Instance.Add(team, amount, 0);
    }

    // ---------- Что дают иконки ----------

    /// <summary>Открыта ли иконка с номером number (с 1).</summary>
    public static bool IsOpen(Team team, int number) => Instance != null && Instance.GetUnlocked(team) >= number;

    /// <summary>Открыты ли все иконки (можно нанять 3-звёздочного героя фракции).</summary>
    public static bool IsComplete(Team team) => Instance != null && Instance.GetUnlocked(team) >= Instance.iconCount;

    /// <summary>Иконка 1: Радар.</summary>
    public static bool HasRadar(Team team) => IsOpen(team, RadarIcon);

    /// <summary>Иконка 2: на сколько больше лимит героев.</summary>
    public static int ReserveBonus(Team team) => IsOpen(team, ReserveIcon) ? Instance.reserveBonus : 0;

    /// <summary>Иконка 3: героев скольких звёзд можно нанимать (без неё — только 1★; 3★ — отдельно, иконка 5).</summary>
    public static int MaxHireStars(Team team) => IsOpen(team, TwoStarIcon) ? 2 : 1;

    /// <summary>Подпись иконки number: что она даёт (для окна паузы).</summary>
    public string IconCaption(int number, string factionHero)
    {
        switch (number)
        {
            case RadarIcon: return "Радар";
            case ReserveIcon: return $"+{reserveBonus} к лимиту героев";
            case TwoStarIcon: return "Найм 2 зв.";
            case FactionHeroIcon: return factionHero;
            default: return "Скоро";
        }
    }

    // ---------- Открытие иконок ----------

    /// <summary>Можно ли открыть следующую иконку (хватает очков, есть что открывать, сейчас пауза).</summary>
    public bool CanUnlockNext(Team team, out string reason)
    {
        reason = null;
        if (GetUnlocked(team) >= iconCount) { reason = "Все иконки открыты"; return false; }
        if (PhaseManager.Instance != null && PhaseManager.Instance.Phase != GamePhase.Pause && PhaseManager.Instance.Phase != GamePhase.Draft)
        { reason = "Только в тактической паузе"; return false; }
        if (GetPoints(team) < iconCost) { reason = $"Нужно {iconCost} очков фракции"; return false; }
        return true;
    }

    /// <summary>Открыть следующую иконку за iconCost очков. Вернёт false, если нельзя.</summary>
    public bool TryUnlockNext(Team team)
    {
        if (!CanUnlockNext(team, out _)) return false;
        if (!ResourceManager.Instance.TrySpend(team, iconCost)) return false;
        if (team == Team.Player) playerUnlocked++;
        else enemyUnlocked++;
        Changed?.Invoke(team);
        if (HeroManager.Instance != null) HeroManager.Instance.NotifyChanged(team); // найм 2★/3★ и лимит могли открыться
        return true;
    }

    /// <summary>Текст очков для сообщений: "+150 очков фракции" (с цветом или без).</summary>
    public static string PointsText(int amount, bool color = true) =>
        amount <= 0 ? "" : color ? $"<color=#C77DFF>+{amount} очков фракции</color>" : $"+{amount} очков фракции";

    /// <summary>Цена в очках фракции для кнопок: "300 очков" (красным, если не хватает).</summary>
    public static string CostText(int amount, bool afford) =>
        $"<color={(afford ? "#C77DFF" : "#FF6B6B")}>{amount} очков</color>";

    /// <summary>Все фракции из списка (для редактора и отладки).</summary>
    public IReadOnlyList<FactionData> Factions => factions;
}
