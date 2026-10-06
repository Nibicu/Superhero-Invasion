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
///   её защищает гарнизон — ОДНА самая сильная команда, стоящая на базе.
///   Победа над гарнизоном (или штурм пустой базы) — 200 урона, команда едет домой.
///   Здоровье 0 — конец игры (GameOverUI).
/// На объекте должен быть Collider2D, иначе клик не сработает.
/// </summary>
public class MainBase : MonoBehaviour, IIncomeSource, ISquadTarget, IBattleSite
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
    [Tooltip("Сколько секунд длится штурм/автобой у базы")]
    [SerializeField] private float siegeTime = 10f;
    [Tooltip("Цвет базы (арена боя за базу красится в него)")]
    [SerializeField] private Color baseColor = new Color(0.18f, 0.48f, 0.88f);

    [Header("Ссылки")]
    [Tooltip("Надпись над базой на карте")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Заполнение полоски здоровья над базой (растягивается по X)")]
    [SerializeField] private Transform hpFill;
    [Tooltip("Полоска штурма (видна, пока базу атакуют)")]
    [SerializeField] private Transform siegeFill;
    [SerializeField] private GameObject siegeRoot;
    [Tooltip("Ширина полосок в единицах карты")]
    [SerializeField] private float barWidth = 3.4f;

    private int hp;                    // Текущее здоровье
    private SquadUnit attacker;        // Команда, которая сейчас атакует базу
    private Squad defender;            // Гарнизон в момент атаки (null — база пустая)
    private float siegeProgress;       // Прогресс штурма 0..1
    private bool awaitingDecision;     // Игрок выбирает в окне перед боем
    private bool inManualBattle;       // Идёт ручной бой
    private BattleForecast forecast;   // Прогноз автобоя
    private bool autoWin;              // Итог автобоя

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

    /// <summary>Идёт штурм (таймер автобоя или штурм пустой базы).</summary>
    private void Update()
    {
        if (attacker == null || awaitingDecision || inManualBattle) return;
        siegeProgress += WorldTime.DeltaTime / Mathf.Max(0.1f, siegeTime);
        UpdateBars();
        if (siegeProgress >= 1f) FinishSiege();
    }

    /// <summary>Клик мышкой по базе на карте — открываем окно базы.</summary>
    private void OnMouseUpAsButton()
    {
        // Если курсор над интерфейсом (кнопкой, окном) — клик по карте не считаем
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (BaseWindowUI.Instance != null) BaseWindowUI.Instance.Open(this);
    }

    // ---------- Здоровье, гарнизон, атаки ----------

    /// <summary>Текущее здоровье.</summary>
    public int Hp => hp;

    /// <summary>Максимум здоровья.</summary>
    public int MaxHp => maxHp;

    /// <summary>Урон от одной успешной атаки.</summary>
    public int DamagePerAttack => damagePerAttack;

    /// <summary>Атакует ли базу сейчас кто-то.</summary>
    public bool IsUnderAttack => attacker != null;

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

    // ISquadTarget — базу противника можно атаковать
    public string TargetName => baseName;

    /// <summary>Атакующие встают перед базой со стороны центра карты.</summary>
    public Vector3 ApproachPoint => transform.position + new Vector3(transform.position.x > 0 ? -2.6f : 2.6f, 0f, 0f);

    /// <summary>Можно ли атаковать базу этой командой.</summary>
    public bool CanAccept(Squad squad, out string reason)
    {
        reason = null;
        if (squad.Owner == owner) { reason = "Это своя база"; return false; }
        if (hp <= 0) { reason = "База уже разрушена"; return false; }
        if (attacker != null && attacker.Squad.Owner == squad.Owner) { reason = "Базу уже атакует ваша команда"; return false; }
        return true;
    }

    public void OnSquadDispatched(Squad squad) { }

    /// <summary>
    /// Команда приехала к базе. Нет гарнизона — штурм без боя (200 урона по таймеру).
    /// Есть гарнизон: игрок — окно перед боем, враг — автобой.
    /// </summary>
    public void OnSquadArrived(SquadUnit unit)
    {
        Team team = unit.Squad.Owner;
        if (attacker != null || hp <= 0)
        {
            if (team == Team.Player) ToastUI.Show($"{baseName}: атака невозможна, команда возвращается");
            unit.ReturnHome();
            return;
        }

        attacker = unit;
        siegeProgress = 0f;
        SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.Capturing);
        defender = FindDefender();

        if (defender == null)
        {
            // База пустая — штурм без боя
            forecast = BattleForecast.Win;
            autoWin = true;
            awaitingDecision = false;
            if (siegeRoot != null) siegeRoot.SetActive(true);
            if (team == Team.Player) ToastUI.Show($"{baseName} без охраны! Команда {unit.Squad.Number} идёт на штурм");
            else ToastUI.Show("Враг штурмует нашу базу! Защитников нет!");
        }
        else if (team == Team.Player && BattlePrepWindowUI.Instance != null)
        {
            awaitingDecision = true;
            BattlePrepWindowUI.Instance.RequestBattle(this, unit);
        }
        else
        {
            BeginAutoBattle(unit, BattleCalculator.Forecast(BattleCalculator.SquadPower(unit.Squad), DefenderPower));
            ToastUI.Show($"Враг атакует нашу базу! Защищает команда {defender.Number}");
        }
    }

    // IBattleSite — бой за базу: защитники — гарнизон (одна волна)
    public string SiteName => baseName;
    public Color SiteColor => baseColor;
    public int DefenderPower => defender != null ? BattleCalculator.SquadPower(defender) : 0;

    /// <summary>Гарнизон одной волной (одна территория арены).</summary>
    public List<List<BattleUnit>> GetDefenderWaves()
    {
        var waves = new List<List<BattleUnit>>();
        if (defender == null) return waves;
        var wave = new List<BattleUnit>();
        foreach (HeroInstance h in defender.AllHeroes)
            wave.Add(new BattleUnit
            {
                data = h.Data,
                stats = h.Stats,
                hpFraction = defender.HpFraction,
                info = $"Гарнизон (команда {defender.Number})  •  Ур. {h.Level}  •  сила {BattleCalculator.StatsPower(h.Stats)}"
            });
        waves.Add(wave);
        return waves;
    }

    /// <summary>Автобой у базы: идёт таймер штурма, итог применится в конце.</summary>
    public void BeginAutoBattle(SquadUnit unit, BattleForecast f)
    {
        if (attacker != unit) return;
        awaitingDecision = false;
        forecast = f;
        autoWin = BattleCalculator.RollAutoBattle(f);
        siegeProgress = 0f;
        if (siegeRoot != null) siegeRoot.SetActive(true);
    }

    public void OnManualBattleStarted()
    {
        awaitingDecision = false;
        inManualBattle = true;
    }

    /// <summary>Ручной бой у базы закончился: здоровье обеих команд и урон базе.</summary>
    public void OnManualBattleFinished(SquadUnit unit, BattleResult result)
    {
        inManualBattle = false;
        attacker = null;
        if (siegeRoot != null) siegeRoot.SetActive(false);
        if (unit == null) return;

        unit.Squad.HpFraction = result.attackerHp;
        if (defender != null) defender.HpFraction = result.defenderHp;
        if (result.win) TakeDamage(damagePerAttack);
        else if (unit.Squad.Owner == Team.Player) ToastUI.Show($"Гарнизон отбил атаку на «{baseName}»");
        unit.ReturnHome();
    }

    /// <summary>Таймер штурма закончился: потери HP и урон базе (если атакующие победили).</summary>
    private void FinishSiege()
    {
        SquadUnit unit = attacker;
        attacker = null;
        siegeProgress = 0f;
        if (siegeRoot != null) siegeRoot.SetActive(false);

        if (defender != null)
        {
            Squad atk = unit.Squad;
            atk.HpFraction = Mathf.Max(BattleCalculator.MinHpAfterBattle, atk.HpFraction - BattleCalculator.AutoBattleHpLoss(forecast, autoWin));
            defender.HpFraction = Mathf.Max(BattleCalculator.MinHpAfterBattle, defender.HpFraction - (autoWin ? 0.5f : 0.2f));
        }

        if (autoWin) TakeDamage(damagePerAttack);
        else if (owner == Team.Player) ToastUI.Show("Наш гарнизон отбил атаку врага!");
        else ToastUI.Show($"Атака на «{baseName}» отбита гарнизоном. Команда {unit.Squad.Number} ранена");
        unit.ReturnHome();
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

    /// <summary>Обновить полоски здоровья и штурма над базой.</summary>
    private void UpdateBars()
    {
        SetBar(hpFill, (float)hp / maxHp);
        SetBar(siegeFill, siegeProgress);
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
        if (label != null) label.text = $"{baseName}\n<size=65%>Уровень {level}  •  HP {hp}</size>";
    }
}
