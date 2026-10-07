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
/// - У базы есть здоровье (1000). Базу противника можно атаковать командой:
///   после подготовки к бою (20 с) её защищает гарнизон — ОДНА самая сильная команда,
///   стоящая на базе. Победа над гарнизоном (или пустая база) — 200 урона, команда едет домой.
///   Порядок нападения — в AttackableSite. Здоровье 0 — конец игры (GameOverUI).
/// - Пока базу атакуют: покупки на ней недоступны, а обе стороны не могут отправлять команды.
/// На объекте должен быть Collider2D, иначе клик не сработает.
/// </summary>
public class MainBase : AttackableSite, IIncomeSource
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

    [Header("Здоровье и атаки")]
    [Tooltip("Здоровье базы. 0 — конец игры")]
    [SerializeField] private int maxHp = 1000;
    [Tooltip("Сколько урона наносит одна успешная атака")]
    [SerializeField] private int damagePerAttack = 200;
    [Tooltip("Цвет базы (арена боя за базу красится в него)")]
    [SerializeField] private Color baseColor = new Color(0.18f, 0.48f, 0.88f);

    [Header("Ссылки")]
    [Tooltip("Надпись над базой на карте")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Заполнение полоски здоровья над базой (растягивается по X)")]
    [SerializeField] private Transform hpFill;
    [Tooltip("Полоска подготовки к бою (видна, пока базу атакуют)")]
    [SerializeField] private Transform siegeFill;
    [SerializeField] private GameObject siegeRoot;
    [Tooltip("Ширина полосок в единицах карты")]
    [SerializeField] private float barWidth = 3.4f;

    private int hp;           // Текущее здоровье
    private Squad defender;   // Гарнизон, выбранный к началу боя (null — до боя или база пустая)
    private bool battleStarted; // Подготовка кончилась, гарнизон выбран

    private int level = 1;                                                 // Текущий уровень базы
    private readonly BuildingInstance[] slots = new BuildingInstance[MaxSlots]; // Постройки в ячейках (null = пусто)
    private readonly HashSet<BuildingData> unlocked = new HashSet<BuildingData>(); // Открытые "особые" постройки

    private static readonly List<MainBase> all = new List<MainBase>(); // Все базы в сцене

    /// <summary>Что-то изменилось (уровень, постройки) — UI должен перерисоваться.</summary>
    public event Action Changed;

    /// <summary>
    /// Захвачен ли Завод: разрешает улучшать доходные постройки до ур.2.
    /// Будет включаться объектом "Завод" на Шаге 5.
    /// </summary>
    public bool FactoryUpgradesUnlocked
    {
        get => factoryUpgradesUnlocked;
        set
        {
            if (factoryUpgradesUnlocked == value) return;
            factoryUpgradesUnlocked = value;
            Changed?.Invoke(); // окно базы обновит кнопки "Улучшить"
        }
    }
    private bool factoryUpgradesUnlocked;

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

    /// <summary>Здоровье на старте.</summary>
    private void Awake() => hp = maxHp;

    /// <summary>Ставим стартовые постройки бесплатно.</summary>
    private void Start()
    {
        if (startBuildings != null)
            for (int i = 0; i < startBuildings.Length && i < OpenSlots; i++)
                if (startBuildings[i] != null) PlaceBuilding(i, startBuildings[i]);
        UpdateLabel();
        UpdateBars();
    }

    /// <summary>Клик мышкой по базе на карте — открываем окно базы (во время нападения — только сообщение).</summary>
    private void OnMouseUpAsButton()
    {
        // Если курсор над интерфейсом (кнопкой, окном) — клик по карте не считаем
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (IsUnderAttack) { ToastUI.Show(AttackStatusText()); return; }
        if (BaseWindowUI.Instance != null) BaseWindowUI.Instance.Open(this);
    }

    // ---------- Здоровье, гарнизон, атаки ----------

    /// <summary>Текущее здоровье.</summary>
    public int Hp => hp;

    /// <summary>Максимум здоровья.</summary>
    public int MaxHp => maxHp;

    /// <summary>Урон от одной успешной атаки.</summary>
    public int DamagePerAttack => damagePerAttack;

    /// <summary>Атакуют ли сейчас хоть одну главную базу (тогда команды отправлять нельзя).</summary>
    public static bool AnyUnderAttack(out MainBase attacked)
    {
        foreach (MainBase b in all)
            if (b.IsUnderAttack) { attacked = b; return true; }
        attacked = null;
        return false;
    }

    /// <summary>Здоровье базы изменилось (для окна базы).</summary>
    public event Action HpChanged;

    /// <summary>
    /// Гарнизон — самая сильная команда стороны, которая сейчас стоит на базе.
    /// Команды в пути, на захвате, на миссии и возвращающиеся базу не защищают.
    /// </summary>
    public Squad FindDefender()
    {
        if (SquadManager.Instance == null) return null;
        Squad best = null;
        int bestPower = -1;
        foreach (Squad s in SquadManager.Instance.GetSquads(owner))
        {
            if (s.Status != SquadStatus.AtBase) continue;
            int p = BattleCalculator.SquadPower(s);
            if (p > bestPower) { bestPower = p; best = s; }
        }
        return best;
    }

    /// <summary>Кто защищает базу: до начала боя — текущий сильнейший, после — выбранный гарнизон.</summary>
    private Squad CurrentDefender => battleStarted ? defender : FindDefender();

    // ---------- Место нападения (AttackableSite) ----------

    public override string TargetName => baseName;
    public override string SiteName => baseName;
    public override Color SiteColor => baseColor;

    /// <summary>Атакующие встают перед базой со стороны центра карты.</summary>
    public override Vector3 ApproachPoint => transform.position + new Vector3(transform.position.x > 0 ? -2.6f : 2.6f, 0f, 0f);

    /// <summary>Сила гарнизона (0 — база пустая).</summary>
    public override int DefenderPower
    {
        get
        {
            Squad d = CurrentDefender;
            return d != null ? BattleCalculator.SquadPower(d) : 0;
        }
    }

    /// <summary>Базу всегда защищает её владелец.</summary>
    public override bool TryGetDefendingTeam(out Team team)
    {
        team = owner;
        return true;
    }

    /// <summary>Гарнизон одной волной (одна территория арены).</summary>
    public override List<List<BattleUnit>> GetDefenderWaves()
    {
        var waves = new List<List<BattleUnit>>();
        Squad d = CurrentDefender;
        if (d == null) return waves;
        var wave = new List<BattleUnit>();
        foreach (HeroInstance h in d.AllHeroes)
            wave.Add(new BattleUnit
            {
                data = h.Data,
                stats = h.Stats,
                hpFraction = d.HpFraction,
                info = $"Гарнизон (команда {d.Number})  •  Ур. {h.Level}  •  сила {BattleCalculator.StatsPower(h.Stats)}"
            });
        waves.Add(wave);
        return waves;
    }

    /// <summary>Свою или разрушенную базу атаковать нельзя.</summary>
    protected override bool CanBeAttackedBy(Team team, out string reason)
    {
        reason = null;
        if (team == owner) { reason = "Это своя база"; return false; }
        if (hp <= 0) { reason = "База уже разрушена"; return false; }
        return true;
    }

    /// <summary>Подготовка кончилась — запоминаем гарнизон на этот бой.</summary>
    protected override void OnBattlePhaseStarting()
    {
        defender = FindDefender();
        battleStarted = true;
        if (defender == null)
            ToastUI.Show(owner == Team.Player ? "Нашу базу некому защищать!" : $"{baseName} без охраны!");
    }

    /// <summary>Гарнизон ранен в автобое: проиграл — −50%, отбился — −20%.</summary>
    protected override void ApplyDefenderAutoLoss(float loss)
    {
        if (defender != null)
            defender.HpFraction = Mathf.Max(BattleCalculator.MinHpAfterBattle, defender.HpFraction - loss);
    }

    /// <summary>После ручного боя у гарнизона столько HP, сколько осталось на арене.</summary>
    protected override void SetDefenderHp(float hpFraction)
    {
        if (defender != null) defender.HpFraction = hpFraction;
    }

    /// <summary>Атакующие победили — урон базе.</summary>
    protected override void OnAttackerWon(Squad squad) => TakeDamage(damagePerAttack);

    /// <summary>Нападение закончилось — гарнизон снова выбирается заново.</summary>
    protected override void OnAttackEnded()
    {
        defender = null;
        battleStarted = false;
    }

    /// <summary>Полоска подготовки и подпись над базой.</summary>
    protected override void UpdateAttackVisuals()
    {
        if (siegeRoot != null) siegeRoot.SetActive(IsUnderAttack);
        UpdateLabel();
        UpdateBars();
    }

    /// <summary>Нанести урон базе. 0 — конец игры.</summary>
    public void TakeDamage(int damage)
    {
        if (hp <= 0) return;
        hp = Mathf.Max(0, hp - damage);
        UpdateLabel();
        UpdateBars();
        HpChanged?.Invoke();
        Changed?.Invoke();

        if (owner == Team.Player) ToastUI.Show($"Враг нанёс нашей базе {damage} урона! HP {hp}/{maxHp}");
        else ToastUI.Show($"{baseName} получила {damage} урона! HP {hp}/{maxHp}");

        if (hp <= 0) GameOverUI.Show(owner != Team.Player);
    }

    /// <summary>Обновить полоски здоровья и подготовки к бою над базой.</summary>
    private void UpdateBars()
    {
        SetBar(hpFill, (float)hp / maxHp);
        SetBar(siegeFill, IsUnderAttack ? PrepProgress : 0f);
    }

    private void SetBar(Transform fill, float fraction)
    {
        if (fill == null) return;
        float w = barWidth * Mathf.Clamp01(fraction);
        fill.localScale = new Vector3(w, fill.localScale.y, 1f);
        fill.localPosition = new Vector3(-barWidth / 2f + w / 2f, fill.localPosition.y, 0f);
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

    /// <summary>
    /// Можно ли сейчас построить эту постройку: она открыта и либо её ещё нет,
    /// либо разрешено строить несколько штук (allowMultiple).
    /// </summary>
    public bool CanBuildType(BuildingData d)
    {
        if (d == null) return false;
        if (!d.availableFromStart && !unlocked.Contains(d)) return false;
        return d.allowMultiple || !HasBuilding(d);
    }

    /// <summary>Список построек, которые можно построить прямо сейчас.</summary>
    public List<BuildingData> GetBuildableList()
    {
        var list = new List<BuildingData>();
        if (availableBuildings == null) return list;
        foreach (BuildingData d in availableBuildings)
            if (CanBuildType(d)) list.Add(d);
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
        if (IsUnderAttack) { error = UnderAttackError; return false; }
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
        if (IsUnderAttack) { error = UnderAttackError; return false; }
        if (!IsSlotOpen(index)) { error = "Ячейка ещё закрыта"; return false; }
        if (slots[index] != null) { error = "Ячейка занята"; return false; }
        if (!CanBuildType(data)) { error = "Такая постройка уже есть"; return false; }

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
        if (IsUnderAttack) { error = UnderAttackError; return false; }
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
        if (label == null) return;
        string attack = "";
        if (IsUnderAttack)
            attack = Phase == AttackPhase.Preparing
                ? $"\n<size=60%><color=#FFB84A>Нападение! Бой через {Mathf.CeilToInt(PrepLeft)} с</color></size>"
                : "\n<size=60%><color=#FFB84A>Идёт бой!</color></size>";
        label.text = $"{baseName}\n<size=65%>Уровень {level}  •  HP {hp}</size>{attack}";
    }

    /// <summary>Текст ошибки при покупке во время нападения.</summary>
    private const string UnderAttackError = "База под атакой — покупки недоступны";
}
