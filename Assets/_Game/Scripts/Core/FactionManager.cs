using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прокачка фракции (объект Managers): очки фракции и 5 иконок у игрока и у врага.
/// Очки фракции дают миссии, общие миссии, объекты и порталы (сколько — в их файлах, поле "Очки фракции").
/// Иконки открываются по порядку в тактической паузе, каждая стоит iconCost очков.
/// Когда открыты все иконки — можно нанять 3-звёздочного героя своей фракции (FactionData → Faction Hero).
///
/// Как пользоваться из других скриптов:
///   FactionManager.AddPoints(Team.Player, 150)       // выдать очки (безопасно, даже если менеджера нет)
///   FactionManager.Instance.TryUnlockNext(Team.Player) // открыть следующую иконку
///   FactionManager.IsComplete(Team.Player)           // открыты ли все иконки
/// </summary>
[DefaultExecutionOrder(-90)]
public class FactionManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static FactionManager Instance { get; private set; }

    [Header("Иконки фракции")]
    [Tooltip("Сколько иконок у фракции")]
    [SerializeField, Min(1)] private int iconCount = 5;
    [Tooltip("Сколько очков фракции стоит одна иконка")]
    [SerializeField, Min(0)] private int iconCost = 1000;

    [Header("Фракции, если игра запущена без меню")]
    [Tooltip("Все фракции (Data/Factions). Без меню игрок получает первую фракцию своей стороны, враг — первую фракцию другой стороны")]
    [SerializeField] private FactionData[] factions = new FactionData[0];

    [Header("Отладка")]
    [Tooltip("Клавиша F — +500 очков фракции игроку")]
    [SerializeField] private bool debugCheats = true;

    [Header("Сейчас (только смотреть)")]
    [SerializeField] private int playerPoints;   // Очки фракции игрока
    [SerializeField] private int playerUnlocked; // Сколько иконок открыл игрок
    [SerializeField] private int enemyPoints;    // Очки фракции врага
    [SerializeField] private int enemyUnlocked;  // Сколько иконок открыл враг

    /// <summary>Изменились очки или иконки стороны (для интерфейса).</summary>
    public event Action<Team> Changed;

    /// <summary>Сколько иконок у фракции.</summary>
    public int IconCount => iconCount;

    /// <summary>Цена одной иконки в очках фракции.</summary>
    public int IconCost => iconCost;

    /// <summary>Запомнить себя.</summary>
    private void Awake() => Instance = this;

    /// <summary>Убрать ссылку.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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

    // ---------- Очки ----------

    /// <summary>Сколько очков фракции у стороны.</summary>
    public int GetPoints(Team team) => team == Team.Player ? playerPoints : enemyPoints;

    /// <summary>Сколько иконок открыла сторона (0..IconCount).</summary>
    public int GetUnlocked(Team team) => team == Team.Player ? playerUnlocked : enemyUnlocked;

    /// <summary>Выдать стороне очки фракции (если менеджера нет — ничего не делает).</summary>
    public static void AddPoints(Team team, int amount)
    {
        if (Instance == null || amount <= 0) return;
        if (team == Team.Player) Instance.playerPoints += amount;
        else Instance.enemyPoints += amount;
        Instance.Changed?.Invoke(team);
        PointsAdded?.Invoke(team, amount);
    }

    /// <summary>Стороне выдали очки фракции: сторона, сколько. Для итогов фазы.</summary>
    public static event Action<Team, int> PointsAdded;

    // ---------- Иконки ----------

    /// <summary>Открыты ли все иконки (можно нанять 3-звёздочного героя фракции).</summary>
    public static bool IsComplete(Team team) => Instance != null && Instance.GetUnlocked(team) >= Instance.iconCount;

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
        if (team == Team.Player) { playerPoints -= iconCost; playerUnlocked++; }
        else { enemyPoints -= iconCost; enemyUnlocked++; }
        Changed?.Invoke(team);
        if (HeroManager.Instance != null) HeroManager.Instance.NotifyChanged(team); // найм 3★ мог открыться
        return true;
    }

    /// <summary>Текст очков для сообщений: "+150 очков фракции" (с цветом или без).</summary>
    public static string PointsText(int amount, bool color = true) =>
        amount <= 0 ? "" : color ? $"<color=#C77DFF>+{amount} очков фракции</color>" : $"+{amount} очков фракции";

    /// <summary>Все фракции из списка (для редактора и отладки).</summary>
    public IReadOnlyList<FactionData> Factions => factions;
}
