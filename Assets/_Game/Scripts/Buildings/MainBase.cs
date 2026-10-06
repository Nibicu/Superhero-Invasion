using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Главная база (своя или вражеская). Если её потерять — поражение.
/// - Уровни 1–3, улучшаются за золото.
/// - 5 ячеек под постройки: на ур.1 открыто 3, на ур.2 — 4, на ур.3 — 5.
/// - Сама база приносит небольшой доход (зависит от уровня).
/// - Клик по базе на карте открывает окно базы (BaseWindowUI).
/// На объекте должен быть Collider2D, иначе клик не сработает.
/// </summary>
public class MainBase : MonoBehaviour, IIncomeSource
{
    /// <summary>Сколько всего ячеек под постройки.</summary>
    public const int MaxSlots = 5;

    [Header("Владелец")]
    [SerializeField] private Team owner = Team.Player;
    [Tooltip("Название базы на карте и в окне")]
    [SerializeField] private string baseName = "НАША БАЗА";

    [Header("Уровни базы (элемент 0 = уровень 1)")]
    [Tooltip("Цена улучшения: элемент 0 — до ур.2, элемент 1 — до ур.3")]
    [SerializeField] private int[] upgradeCosts = { 1500, 3000 };
    [Tooltip("Сколько ячеек открыто на каждом уровне")]
    [SerializeField] private int[] slotsPerLevel = { 3, 4, 5 };
    [Tooltip("Доход самой базы на каждом уровне (золото раз в 10 с)")]
    [SerializeField] private int[] goldIncomePerLevel = { 100, 150, 200 };

    [Header("Постройки")]
    [Tooltip("Все постройки, которые в принципе можно строить на этой базе")]
    [SerializeField] private BuildingData[] availableBuildings;
    [Tooltip("Постройки, которые уже стоят в начале игры (по порядку в ячейках)")]
    [SerializeField] private BuildingData[] startBuildings;

    [Header("Ссылки")]
    [Tooltip("Надпись над базой на карте")]
    [SerializeField] private TMP_Text label;

    private int level = 1;                                                   // Текущий уровень базы
    private readonly BuildingInstance[] slots = new BuildingInstance[MaxSlots]; // Постройки в ячейках (null = пусто)
    private readonly HashSet<BuildingData> unlocked = new HashSet<BuildingData>(); // Открытые "особые" постройки

    private static readonly List<MainBase> all = new List<MainBase>(); // Все базы в сцене

    /// <summary>Что-то изменилось (уровень, постройки) — UI должен перерисоваться.</summary>
    public event Action Changed;

    /// <summary>
    /// Захвачен ли Завод: разрешает улучшать доходные постройки до ур.2.
    /// Будет включаться объектом "Завод" на Шаге 5.
    /// </summary>
    public bool FactoryUpgradesUnlocked { get; set; }

    // ---------- Свойства для других скриптов ----------

    public Team Owner => owner;
    public string BaseName => baseName;
    public int Level => level;
    public int MaxLevel => slotsPerLevel.Length;
    public bool IsMaxLevel => level >= MaxLevel;

    /// <summary>Сколько ячеек сейчас открыто.</summary>
    public int OpenSlots => slotsPerLevel[level - 1];

    /// <summary>Цена улучшения базы до следующего уровня (0 — если уже максимум).</summary>
    public int UpgradeCost => IsMaxLevel ? 0 : upgradeCosts[level - 1];

    // Доход самой базы (для ResourceManager)
    public int GoldIncome => goldIncomePerLevel[level - 1];
    public int PlutoniumIncome => 0;
    public string SourceName => baseName;

    /// <summary>Найти базу нужной стороны (например, MainBase.Get(Team.Player)).</summary>
    public static MainBase Get(Team team)
    {
        foreach (MainBase b in all)
            if (b.owner == team) return b;
        return null;
    }

    // ---------- Жизненный цикл ----------

    /// <summary>Регистрируем базу и её доход.</summary>
    private void OnEnable()
    {
        all.Add(this);
        ResourceManager.Instance?.RegisterSource(this);
    }

    /// <summary>Убираем базу и доход всех её построек.</summary>
    private void OnDisable()
    {
        all.Remove(this);
        if (ResourceManager.Instance == null) return;
        ResourceManager.Instance.UnregisterSource(this);
        foreach (BuildingInstance b in slots)
            if (b != null) ResourceManager.Instance.UnregisterSource(b);
    }

    /// <summary>Ставим стартовые постройки бесплатно.</summary>
    private void Start()
    {
        if (startBuildings != null)
            for (int i = 0; i < startBuildings.Length && i < OpenSlots; i++)
                if (startBuildings[i] != null) PlaceBuilding(i, startBuildings[i]);
        UpdateLabel();
    }

    /// <summary>Клик мышкой по базе на карте — открываем окно базы.</summary>
    private void OnMouseUpAsButton()
    {
        // Если курсор над интерфейсом (кнопкой, окном) — клик по карте не считаем
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (BaseWindowUI.Instance != null) BaseWindowUI.Instance.Open(this);
    }

    // ---------- Ячейки ----------

    /// <summary>Открыта ли ячейка с номером index (0..4).</summary>
    public bool IsSlotOpen(int index) => index < OpenSlots;

    /// <summary>С какого уровня базы открывается ячейка.</summary>
    public int SlotUnlockLevel(int index)
    {
        for (int i = 0; i < slotsPerLevel.Length; i++)
            if (slotsPerLevel[i] > index) return i + 1;
        return slotsPerLevel.Length + 1;
    }

    /// <summary>Постройка в ячейке (null — пусто).</summary>
    public BuildingInstance GetBuilding(int index) => slots[index];

    /// <summary>Есть ли на базе такая постройка.</summary>
    public bool HasBuilding(BuildingData data)
    {
        foreach (BuildingInstance b in slots)
            if (b != null && b.Data == data) return true;
        return false;
    }

    /// <summary>Есть ли на базе постройка такого вида (например, Радар).</summary>
    public bool HasBuildingOfType(BuildingType type)
    {
        foreach (BuildingInstance b in slots)
            if (b != null && b.Data.type == type) return true;
        return false;
    }

    /// <summary>Открыть "особую" постройку (за героя или миссию) — Шаги 3 и 6.</summary>
    public void UnlockBuilding(BuildingData data)
    {
        if (data != null && unlocked.Add(data)) Changed?.Invoke();
    }

    /// <summary>Список построек, которые можно построить прямо сейчас (ещё не построены и открыты).</summary>
    public List<BuildingData> GetBuildableList()
    {
        var list = new List<BuildingData>();
        if (availableBuildings == null) return list;
        foreach (BuildingData d in availableBuildings)
            if (d != null && !HasBuilding(d) && (d.availableFromStart || unlocked.Contains(d)))
                list.Add(d);
        return list;
    }

    // ---------- Бонусы построек (для героев на Шаге 3) ----------

    /// <summary>Сколько дополнительных мест для героев дают постройки (Резервные комнаты).</summary>
    public int HeroCapacityBonus
    {
        get
        {
            int sum = 0;
            foreach (BuildingInstance b in slots)
                if (b != null) sum += b.Current.heroCapacityBonus;
            return sum;
        }
    }

    /// <summary>Героев скольких звёзд можно нанимать (без Бараков — только 1 звезда).</summary>
    public int MaxHeroStars
    {
        get
        {
            int max = 1;
            foreach (BuildingInstance b in slots)
                if (b != null) max = Mathf.Max(max, b.Current.maxHeroStars);
            return max;
        }
    }

    /// <summary>Есть ли Радар.</summary>
    public bool HasRadar => HasBuildingOfType(BuildingType.Radar);

    // ---------- Действия игрока ----------

    /// <summary>Улучшить базу. Если нельзя — вернёт false и причину в error.</summary>
    public bool TryUpgradeBase(out string error)
    {
        error = null;
        if (IsMaxLevel) { error = "База уже максимального уровня"; return false; }
        if (!ResourceManager.Instance.TrySpend(owner, UpgradeCost))
        {
            error = "Не хватает золота";
            return false;
        }
        level++;
        UpdateLabel();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Построить здание в пустой открытой ячейке.</summary>
    public bool TryBuild(int index, BuildingData data, out string error)
    {
        error = null;
        if (!IsSlotOpen(index)) { error = "Ячейка ещё закрыта"; return false; }
        if (slots[index] != null) { error = "Ячейка занята"; return false; }
        if (HasBuilding(data)) { error = "Такая постройка уже есть"; return false; }

        BuildingLevel l = data.GetLevel(1);
        if (!ResourceManager.Instance.TrySpend(owner, l.goldCost, l.plutoniumCost))
        {
            error = "Не хватает ресурсов";
            return false;
        }
        PlaceBuilding(index, data);
        return true;
    }

    /// <summary>
    /// Можно ли улучшить постройку в ячейке (без учёта денег).
    /// Если нельзя — reason объясняет почему (для кнопки в окне).
    /// </summary>
    public bool CanUpgradeBuilding(int index, out string reason)
    {
        reason = null;
        BuildingInstance b = slots[index];
        if (b == null) { reason = "Пусто"; return false; }
        if (b.IsMaxLevel) { reason = "Макс. уровень"; return false; }
        if (b.Data.GetLevel(b.Level + 1).requiresFactory && !FactoryUpgradesUnlocked)
        {
            reason = "Нужен Завод";
            return false;
        }
        return true;
    }

    /// <summary>Улучшить постройку в ячейке.</summary>
    public bool TryUpgradeBuilding(int index, out string error)
    {
        if (!CanUpgradeBuilding(index, out error)) return false;
        BuildingInstance b = slots[index];
        BuildingLevel next = b.Data.GetLevel(b.Level + 1);
        if (!ResourceManager.Instance.TrySpend(owner, next.goldCost, next.plutoniumCost))
        {
            error = "Не хватает ресурсов";
            return false;
        }
        b.LevelUp();
        Changed?.Invoke();
        return true;
    }

    // ---------- Внутреннее ----------

    /// <summary>Поставить постройку в ячейку без оплаты и подключить её доход.</summary>
    private void PlaceBuilding(int index, BuildingData data)
    {
        var b = new BuildingInstance(data, owner);
        slots[index] = b;
        ResourceManager.Instance?.RegisterSource(b);
        Changed?.Invoke();
    }

    /// <summary>Обновить надпись над базой на карте.</summary>
    private void UpdateLabel()
    {
        if (label != null) label.text = $"{baseName}\n<size=65%>Уровень {level}</size>";
    }
}
