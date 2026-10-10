using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ведёт ручной бой на арене. Бывает два вида боя.
///
/// ОБЫЧНЫЙ БОЙ (захват объекта, атака или защита базы):
/// - Карта ставится на паузу, камера переезжает на арену (она в этой же сцене, далеко от карты).
/// - Арена разделена на территории (обычно 3). На каждой — своя волна охраны.
///   Наши не могут пройти дальше открытой территории (невидимая стена + барьер).
/// - Волна уничтожена → барьер открывается, наши идут дальше, появляется следующая волна.
/// - Все волны уничтожены — победа; все наши выбыли или нажато "Отступить" — поражение.
///
/// БИТВА ЗА ФЛАГ (наша и вражеская команды напали на объект одновременно):
/// - 7 территорий: 3 наших слева, центр с флагом, 3 вражеских справа.
/// - Охрана объекта дублируется: своя копия у каждой команды (наши — слева, враг — справа).
/// - Кто прошёл свои 3 территории — выходит в центр. Флаг нужно удержать flagHoldTime секунд:
///   в зоне только наши — отсчёт идёт к нам, только враги — к ним, обе стороны или никого — стоит.
///   Если враг уже набрал 7 с, а мы его выбили — сначала отсчёт уходит обратно до нуля, потом идёт к нам.
/// - Победа: удержали флаг или вся команда врага выбыла. Поражение — наоборот (или "Отступить").
///
/// После боя камера и интерфейс карты возвращаются, а место боя получает результат.
/// Хранит списки бойцов, чтобы удары и ИИ не искали объекты по сцене каждый кадр.
/// </summary>
public class BattleManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static BattleManager Instance { get; private set; }

    /// <summary>Множитель урона (чтобы бои не длились слишком долго).</summary>
    public static float DamageMultiplier => Instance != null ? Instance.damageMultiplier : 1f;

    /// <summary>Бой закончился (окно перед боем показывает следующий запрос, если он есть).</summary>
    public static event Action BattleEnded;

    /// <summary>Сколько территорий у каждой команды в битве за флаг.</summary>
    public const int LaneTerritories = 3;

    [Header("Арена")]
    [Tooltip("Левый край арены (x = 0 арены), на уровне 'горизонта' пола")]
    [SerializeField] private Transform arenaRoot;
    [Tooltip("Куда складываются бойцы и снаряды во время боя (очищается после боя)")]
    [SerializeField] private Transform runtimeRoot;
    [Tooltip("Ширина одной территории")]
    [SerializeField] private float territoryWidth = 20f;
    [Tooltip("Нижняя граница пола (локально от arenaRoot)")]
    [SerializeField] private float floorMinY = -3f;
    [Tooltip("Верхняя граница пола (дальняя от зрителя)")]
    [SerializeField] private float floorMaxY = 0.8f;
    [Tooltip("Барьеры между территориями (0 — между 1-й и 2-й и т.д.). Для битвы за флаг нужно 6")]
    [SerializeField] private GameObject[] barriers;

    [Header("Декорации")]
    [Tooltip("Видны только в обычном бою (финишный флажок, таблички 1–3)")]
    [SerializeField] private GameObject[] normalOnly;
    [Tooltip("Видны только в битве за флаг (таблички 7 территорий, столбы ворот, зона флага)")]
    [SerializeField] private GameObject[] contestedOnly;

    [Header("Битва за флаг")]
    [Tooltip("Зона флага в центре арены")]
    [SerializeField] private FlagZoneView flagZone;
    [Tooltip("Сколько секунд нужно удерживать флаг")]
    [SerializeField] private float flagHoldTime = 10f;
    [Tooltip("Размер зоны флага: полуширина по X и полувысота по глубине (Y)")]
    [SerializeField] private Vector2 flagZoneRadius = new Vector2(3f, 1.3f);

    [Header("Префабы")]
    [SerializeField] private Fighter fighterPrefab;
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Коробка, которую можно разбить авто атаками")]
    [SerializeField] private BattleBox boxPrefab;
    [Tooltip("Бафф, который выпадает из коробки")]
    [SerializeField] private BattleBuffItem buffPrefab;

    [Header("Ящики и зелья")]
    [Tooltip("Сколько ящиков стоит на арене с начала обычного боя: от и до")]
    [SerializeField] private Vector2Int boxCount = new Vector2Int(1, 2);
    [Tooltip("Раз во сколько секунд с неба падает предмет: от и до")]
    [SerializeField] private Vector2 dropInterval = new Vector2(7f, 12f);
    [Tooltip("Шанс, что упадёт зелье (иначе — ящик)")]
    [SerializeField, Range(0f, 1f)] private float potionChance = 0.55f;
    [Tooltip("Больше этого числа ящиков на арене одновременно не бывает")]
    [SerializeField] private int maxBoxes = 6;
    [Tooltip("Больше этого числа зелий на арене одновременно не бывает")]
    [SerializeField] private int maxPotions = 6;
    [Tooltip("С какой высоты падают предметы")]
    [SerializeField] private float dropHeight = 7f;

    private float dropTimer; // До следующего падения предмета

    [Header("Камера")]
    [Tooltip("Размер камеры на арене (меньше — крупнее бойцы)")]
    [SerializeField] private float cameraSize = 5f;
    [Tooltip("Сдвиг камеры по вертикали относительно arenaRoot")]
    [SerializeField] private float cameraYOffset = -0.3f;
    [Tooltip("Плавность слежения камеры")]
    [SerializeField] private float cameraFollowSpeed = 3f;

    [Header("Бой")]
    [Tooltip("Множитель урона всех ударов и снарядов")]
    [SerializeField] private float damageMultiplier = 1.5f;
    [Tooltip("Сколько секунд показывается результат перед возвратом на карту")]
    [SerializeField] private float resultDelay = 2.5f;
    [Tooltip("Где появляются враги на своей территории (отступ от её края со стороны команды)")]
    [SerializeField] private float enemySpawnOffset = 13f;

    [Header("Интерфейс")]
    [Tooltip("Интерфейс карты — прячется на время боя")]
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private BattleHUD hud;

    // Бойцы по сторонам
    private readonly List<Fighter> heroes = new List<Fighter>();      // Наши
    private readonly List<Fighter> guards = new List<Fighter>();      // Охрана против наших (все волны, включая павших)
    private readonly List<Fighter> rivals = new List<Fighter>();      // Команда врага (битва за флаг)
    private readonly List<Fighter> rivalGuards = new List<Fighter>(); // Копия охраны против врага
    // Противники каждой стороны (заполняются при появлении бойцов)
    private readonly List<Fighter> heroesFoes = new List<Fighter>();
    private readonly List<Fighter> guardsFoes = new List<Fighter>();
    private readonly List<Fighter> rivalsFoes = new List<Fighter>();
    private readonly List<Fighter> rivalGuardsFoes = new List<Fighter>();
    private readonly List<Fighter> allFighters = new List<Fighter>();   // Все бойцы
    private readonly List<BattleBox> boxes = new List<BattleBox>();      // Коробки на арене
    private readonly List<BattleBuffItem> buffs = new List<BattleBuffItem>(); // Выпавшие баффы

    private ArenaTint[] tints;            // Детали арены, которые красятся в цвет объекта
    private List<List<BattleUnit>> waves; // Охрана по территориям
    private List<List<BattleUnit>> rivalWaves; // Охрана на половине врага (битва за флаг; обычно та же, что waves)
    private string siteName;              // За что бьёмся (название)
    private Action<BattleResult> onFinished; // Кого известить об итоге
    private bool contested;               // Битва за флаг (две команды)
    private bool defenseMode;             // Защита объекта с подкреплением (наша команда защищает, враг прорывается)
    private int rivalLaneLength = LaneTerritories; // Сколько территорий с охраной проходит враг
    private int totalTerritories;         // Сколько территорий на арене
    private int heroWave;                 // Сколько территорий прошли наши (в обычном бою — номер текущей)
    private int rivalWave;                // Сколько территорий прошёл враг
    private float heroMaxX;               // До какого X (локально) могут дойти наши
    private float rivalMinX;              // До какого X (локально) может дойти враг
    private float heroRallyX;             // Куда идут наши, когда рядом нет противников (мир)
    private float rivalRallyX;            // Куда идёт враг (мир)
    private float flagValue;              // Захват флага: + к нам, − к врагу (от −flagHoldTime до +flagHoldTime)
    private bool finishing;               // Итог уже известен, ждём возврата
    private Camera cam;
    private Vector3 savedCamPos;
    private float savedCamSize;

    /// <summary>Идёт ли бой.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Куда складывать снаряды и бойцов.</summary>
    public Transform RuntimeRoot => runtimeRoot;

    /// <summary>
    /// Порядок отрисовки по глубине: кто ниже на экране — тот выше (рисуется поверх).
    /// Считается относительно арены, чтобы числа не выходили за допустимые пределы.
    /// </summary>
    public int DepthSortingOrder(float worldY) => -Mathf.RoundToInt((worldY - arenaRoot.position.y) * 100f);

    private void Awake()
    {
        Instance = this;
        WorldTime.Paused = false;
        tints = arenaRoot.GetComponentsInChildren<ArenaTint>(true);
        SetDecor(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Все бойцы на арене (для подбора баффов).</summary>
    public IReadOnlyList<Fighter> AllFighters => allFighters;

    /// <summary>Коробки на арене (разбитые удаляются сами).</summary>
    public IReadOnlyList<BattleBox> Boxes => boxes;

    /// <summary>Баффы на арене.</summary>
    public IReadOnlyList<BattleBuffItem> Buffs => buffs;

    // ---------- Кто с кем ----------

    /// <summary>Союзники (своя сторона).</summary>
    public IReadOnlyList<Fighter> GetAllies(BattleFaction f)
    {
        switch (f)
        {
            case BattleFaction.Heroes: return heroes;
            case BattleFaction.Guards: return guards;
            case BattleFaction.Rivals: return rivals;
            default: return rivalGuards;
        }
    }

    /// <summary>Противники стороны.</summary>
    public IReadOnlyList<Fighter> GetOpponents(BattleFaction f)
    {
        switch (f)
        {
            case BattleFaction.Heroes: return heroesFoes;
            case BattleFaction.Guards: return guardsFoes;
            case BattleFaction.Rivals: return rivalsFoes;
            default: return rivalGuardsFoes;
        }
    }

    // ---------- Начало боя ----------

    /// <summary>
    /// Обычный бой. title и color — название и цвет арены,
    /// ours — наши бойцы (своя команда, а при защите — защитники объекта или гарнизон),
    /// theirWaves — противники по территориям (охрана объекта или нападающая команда),
    /// finished — вызовется после возврата на карту.
    /// </summary>
    public void StartBattle(string title, Color color, List<BattleUnit> ours, List<List<BattleUnit>> theirWaves, Action<BattleResult> finished)
    {
        if (IsRunning) return;
        contested = false;
        defenseMode = false;
        totalTerritories = Mathf.Clamp(theirWaves.Count, 1, (barriers?.Length ?? 0) + 1);
        BeginBattle(title, color, theirWaves, finished);

        heroMaxX = territoryWidth - 0.5f;
        heroRallyX = WorldX(enemySpawnOffset - 1f);
        SpawnTeam(ours, BattleFaction.Heroes, false);
        SpawnGuardWave(0, BattleFaction.Guards);

        hud.Show(this, siteName, heroes);
        hud.ShowMessage($"{siteName.ToUpper()}\n<size=60%>Территория 1 / {totalTerritories}</size>");
    }

    /// <summary>
    /// Битва за флаг: наша команда ours (слева) и команда врага rivalUnits (справа)
    /// одновременно бьются с охраной guardWaves (у каждой — своя копия), потом — за флаг в центре.
    /// rivalGuardWaves — если у врага своя охрана (портал: пройденные территории пустые); null — та же копия.
    /// </summary>
    public void StartContestedBattle(string title, Color color, List<BattleUnit> ours, List<BattleUnit> rivalUnits,
                                     List<List<BattleUnit>> guardWaves, Action<BattleResult> finished,
                                     List<List<BattleUnit>> rivalGuardWaves = null)
    {
        if (IsRunning) return;
        if (barriers == null || barriers.Length < LaneTerritories * 2)
            Debug.LogError("BattleManager: для битвы за флаг нужно 6 барьеров на арене");
        contested = true;
        defenseMode = false;
        rivalLaneLength = LaneTerritories;
        totalTerritories = LaneTerritories * 2 + 1;
        BeginBattle(title, color, guardWaves, finished);
        if (rivalGuardWaves != null) rivalWaves = rivalGuardWaves;

        heroMaxX = territoryWidth - 0.5f;
        rivalMinX = (totalTerritories - 1) * territoryWidth + 0.5f;
        heroRallyX = WorldX(enemySpawnOffset - 1f);
        rivalRallyX = WorldX(ArenaLength - enemySpawnOffset + 1f);
        flagValue = 0f;
        if (flagZone != null)
        {
            flagZone.transform.position = arenaRoot.position + (Vector3)FlagPoint;
            flagZone.Setup(flagZoneRadius);
            flagZone.SetProgress(0f, false);
        }

        SpawnTeam(ours, BattleFaction.Heroes, false);
        SpawnTeam(rivalUnits, BattleFaction.Rivals, true);
        SpawnGuardWave(0, BattleFaction.Guards);
        SpawnGuardWave(0, BattleFaction.RivalGuards);

        hud.Show(this, siteName, heroes);
        hud.ShowMessage($"БИТВА ЗА ФЛАГ!\n<size=60%>Пройдите 3 территории быстрее врага и удержите флаг {flagHoldTime:0} с</size>", 3.5f);
    }

    /// <summary>
    /// Защита нашего объекта с подкреплением: враг напал, а наша команда успела прийти на защиту.
    /// Арена из 2 территорий: наша команда ours стоит на левой, охрана объекта guardUnits — на правой,
    /// нападающие attackers начинают справа: сначала бьются с охраной, потом прорываются к нашей команде.
    /// Охраны нет — всё на одной территории.
    /// </summary>
    public void StartDefenseBattle(string title, Color color, List<BattleUnit> ours, List<BattleUnit> attackers,
                                   List<BattleUnit> guardUnits, Action<BattleResult> finished)
    {
        if (IsRunning) return;
        contested = false;
        defenseMode = true;
        bool hasGuards = guardUnits != null && guardUnits.Count > 0;
        totalTerritories = hasGuards ? 2 : 1;
        rivalLaneLength = hasGuards ? 1 : 0;
        BeginBattle(title, color, new List<List<BattleUnit>> { guardUnits ?? new List<BattleUnit>() }, finished);

        heroMaxX = territoryWidth - 0.5f;
        rivalMinX = (totalTerritories - 1) * territoryWidth + 0.5f;
        heroRallyX = WorldX(territoryWidth - 5f);                 // наши держат позицию у своей границы
        rivalRallyX = WorldX(ArenaLength - enemySpawnOffset + 1f); // враг идёт на охрану

        // Наши — у правого края своей территории, лицом к врагу
        int rowsX = ours.Count > 5 ? 3 : 2;
        for (int i = 0; i < ours.Count; i++)
        {
            float x = territoryWidth - 5f - (i % rowsX) * 1.2f;
            float y = Mathf.Lerp(floorMinY + 0.4f, floorMaxY - 0.4f, (i + 0.5f) / ours.Count);
            Spawn(ours[i], BattleFaction.Heroes, false, new Vector2(x, y));
        }
        SpawnTeam(attackers, BattleFaction.Rivals, true);
        if (hasGuards) SpawnGuardWave(0, BattleFaction.RivalGuards);

        // Камера сразу на нападающих (они начинают справа)
        Vector3 cp = cam.transform.position;
        cam.transform.position = new Vector3(arenaRoot.position.x + Mathf.Max(HalfViewWidth(), ArenaLength - HalfViewWidth()), cp.y, cp.z);

        hud.Show(this, siteName, heroes);
        hud.ShowMessage(hasGuards
            ? "ЗАЩИТА!\n<size=60%>Враг сначала бьётся с охраной объекта, потом — с вашей командой</size>"
            : "ЗАЩИТА!\n<size=60%>Отбейте нападение</size>", 3.5f);
    }

    /// <summary>Общая подготовка арены, камеры и интерфейса.</summary>
    private void BeginBattle(string title, Color color, List<List<BattleUnit>> guardWaves, Action<BattleResult> finished)
    {
        waves = guardWaves;
        rivalWaves = guardWaves;
        siteName = title;
        onFinished = finished;
        IsRunning = true;
        finishing = false;
        WorldTime.Paused = true;
        heroWave = 0;
        rivalWave = 0;

        foreach (ArenaTint t in tints) t.Apply(color);
        // Нужные барьеры закрыты, лишние (за краем арены) спрятаны
        if (barriers != null)
            for (int i = 0; i < barriers.Length; i++)
                if (barriers[i] != null) barriers[i].SetActive(i < totalTerritories - 1);
        SetDecor(contested);
        SpawnBoxes();

        cam = Camera.main;
        savedCamPos = cam.transform.position;
        savedCamSize = cam.orthographicSize;
        cam.orthographicSize = cameraSize;
        cam.transform.position = new Vector3(arenaRoot.position.x + HalfViewWidth(), arenaRoot.position.y + cameraYOffset, savedCamPos.z);
        SetMapUI(false);
    }

    /// <summary>Выставить команду у своего края арены (наши — слева, враг — справа), вразброс по глубине.</summary>
    private void SpawnTeam(List<BattleUnit> units, BattleFaction faction, bool fromRight)
    {
        int rowsX = units.Count > 5 ? 3 : 2;
        for (int i = 0; i < units.Count; i++)
        {
            float x = 1.5f + (i % rowsX) * 1.2f;
            if (fromRight) x = ArenaLength - x;
            float y = Mathf.Lerp(floorMinY + 0.4f, floorMaxY - 0.4f, (i + 0.5f) / units.Count);
            Spawn(units[i], faction, false, new Vector2(x, y));
        }
    }

    /// <summary>
    /// Выпустить волну охраны index. Guards — на территории index слева,
    /// RivalGuards (копия для врага) — зеркально, на территории index справа.
    /// </summary>
    private void SpawnGuardWave(int index, BattleFaction faction)
    {
        bool mirror = faction == BattleFaction.RivalGuards;
        List<List<BattleUnit>> source = mirror ? rivalWaves : waves;
        if (source == null || index >= source.Count) return;
        List<BattleUnit> wave = source[index];
        for (int j = 0; j < wave.Count; j++)
        {
            float x = index * territoryWidth + enemySpawnOffset + (j % 2) * 1.5f + j * 0.6f;
            if (mirror) x = ArenaLength - x;
            float y = Mathf.Lerp(floorMinY + 0.5f, floorMaxY - 0.5f, (j + 0.5f) / wave.Count);
            Spawn(wave[j], faction, contested || defenseMode, new Vector2(x, y), mirror ? totalTerritories - 1 - index : index);
        }
    }

    /// <summary>Создать бойца в точке localPos (координаты арены) и внести его в списки сторон.</summary>
    private Fighter Spawn(BattleUnit u, BattleFaction faction, bool neutralLook, Vector2 localPos, int territory = -1)
    {
        Vector3 pos = arenaRoot.position + (Vector3)localPos;
        Fighter f = Instantiate(fighterPrefab, pos, Quaternion.identity, runtimeRoot);
        f.Combat.SetProjectilePrefab(projectilePrefab);
        f.Init(u.data, u.stats, faction, u.hpFraction, neutralLook);
        f.Territory = territory;
        f.IsReinforcement = u.reinforcement;
        f.IsGuard = u.guard;
        allFighters.Add(f);

        switch (faction)
        {
            case BattleFaction.Heroes: heroes.Add(f); guardsFoes.Add(f); rivalsFoes.Add(f); break;
            case BattleFaction.Guards: guards.Add(f); heroesFoes.Add(f); break;
            case BattleFaction.Rivals: rivals.Add(f); heroesFoes.Add(f); rivalGuardsFoes.Add(f); break;
            default: rivalGuards.Add(f); rivalsFoes.Add(f); break;
        }
        return f;
    }

    // ---------- Коробки и баффы ----------

    /// <summary>
    /// Поставить немного ящиков с начала боя (остальное падает с неба во время боя).
    /// Обычный бой — 1–2 по арене. Битва за флаг — честно: по одному на каждой половине (зеркально).
    /// </summary>
    private void SpawnBoxes()
    {
        boxes.Clear();
        buffs.Clear();
        dropTimer = UnityEngine.Random.Range(dropInterval.x, dropInterval.y);
        if (boxPrefab == null) return;
        if (contested)
        {
            Vector2 p = RandomFloorPoint(3f, LaneTerritories * territoryWidth - 3f);
            SpawnBox(p);
            SpawnBox(new Vector2(ArenaLength - p.x, p.y));
            return;
        }
        int count = Mathf.Clamp(UnityEngine.Random.Range(boxCount.x, boxCount.y + 1), 0, maxBoxes);
        for (int i = 0; i < count; i++) SpawnBox(RandomFloorPoint(4f, ArenaLength - 3f));
    }

    /// <summary>
    /// Падение предметов с неба: раз в dropInterval секунд — зелье или ящик
    /// рядом со случайным бойцом команд (там, куда он может дойти). Лимиты — maxBoxes и maxPotions.
    /// </summary>
    private void UpdateDrops()
    {
        dropTimer -= Time.deltaTime;
        if (dropTimer > 0f) return;
        dropTimer = UnityEngine.Random.Range(dropInterval.x, dropInterval.y);

        // Рядом с кем уронить: живые бойцы команд (наши и вражеская команда)
        var candidates = new List<Fighter>();
        foreach (Fighter f in heroes) if (f.IsAlive) candidates.Add(f);
        foreach (Fighter f in rivals) if (f.IsAlive) candidates.Add(f);
        if (candidates.Count == 0) return;
        Fighter near = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        int potions = 0, liveBoxes = 0;
        foreach (BattleBuffItem b in buffs) if (b != null && !b.Taken && b.IsPotion) potions++;
        foreach (BattleBox b in boxes) if (b != null && !b.IsBroken) liveBoxes++;
        bool potion = UnityEngine.Random.value < potionChance;
        if (potion && potions >= maxPotions) potion = false;
        if (!potion && liveBoxes >= maxBoxes) { if (potions >= maxPotions) return; potion = true; }

        Vector3 spot = near.Position + new Vector2(UnityEngine.Random.Range(-6f, 6f), 0f);
        spot.y = arenaRoot.position.y + UnityEngine.Random.Range(floorMinY + 0.4f, floorMaxY - 0.4f);
        spot = ClampToArena(near, spot); // туда, куда этот боец может дойти

        if (potion && buffPrefab != null)
        {
            BattleBuffItem item = Instantiate(buffPrefab, spot, Quaternion.identity, runtimeRoot);
            item.SetupPotion();
            item.DropIn(dropHeight);
            buffs.Add(item);
        }
        else if (!potion && boxPrefab != null)
        {
            BattleBox b = Instantiate(boxPrefab, spot, Quaternion.identity, runtimeRoot);
            b.DropIn(dropHeight);
            boxes.Add(b);
        }
    }

    /// <summary>Случайная точка пола между minX и maxX (не вплотную к барьерам).</summary>
    private Vector2 RandomFloorPoint(float minX, float maxX)
    {
        float x = UnityEngine.Random.Range(minX, maxX);
        float edge = Mathf.Repeat(x, territoryWidth);
        if (edge < 1.5f) x += 1.5f;
        else if (edge > territoryWidth - 1.5f) x -= 1.5f;
        float y = UnityEngine.Random.Range(floorMinY + 0.4f, floorMaxY - 0.4f);
        return new Vector2(x, y);
    }

    private void SpawnBox(Vector2 localPos)
    {
        BattleBox b = Instantiate(boxPrefab, arenaRoot.position + (Vector3)localPos, Quaternion.identity, runtimeRoot);
        boxes.Add(b);
    }

    /// <summary>Из разбитой коробки выпал бафф (вызывает BattleBox).</summary>
    public void SpawnBuff(Vector3 worldPos)
    {
        if (buffPrefab == null) return;
        BattleBuffItem item = Instantiate(buffPrefab, worldPos, Quaternion.identity, runtimeRoot);
        item.Setup();
        buffs.Add(item);
    }

    /// <summary>Может ли боец дойти до точки (она в пределах его открытых территорий).</summary>
    public bool IsReachable(Fighter f, Vector2 worldPoint)
    {
        Vector2 clamped = ClampToArena(f, worldPoint);
        return (clamped - worldPoint).sqrMagnitude < 0.04f;
    }

    /// <summary>
    /// Куда отступать раненому бойцу: наши — к началу своей текущей территории (влево),
    /// враг в битве за флаг — к началу своей (вправо), охрана — назад к месту, где появилась.
    /// </summary>
    public Vector2 GetRetreatPoint(Fighter f)
    {
        float localX;
        switch (f.Faction)
        {
            case BattleFaction.Heroes:
                localX = Mathf.Min(heroWave, totalTerritories - 1) * territoryWidth + 1f;
                break;
            case BattleFaction.Rivals:
                localX = ArenaLength - Mathf.Min(rivalWave, LaneTerritories) * territoryWidth - 1f;
                break;
            case BattleFaction.RivalGuards:
                localX = f.HomeX - arenaRoot.position.x - 4f;
                break;
            default: // Guards смотрят на наших слева — отступают вправо
                localX = f.HomeX - arenaRoot.position.x + 4f;
                break;
        }
        Vector3 p = ClampToArena(f, arenaRoot.position + new Vector3(localX, f.Position.y - arenaRoot.position.y, 0f));
        return p;
    }

    // ---------- Ход боя ----------

    private void Update()
    {
        if (!IsRunning) return;
        UpdateCamera();
        if (finishing) return;
        UpdateDrops();
        if (contested) UpdateContested();
        else if (defenseMode) UpdateDefense();
        else UpdateNormal();
    }

    /// <summary>Обычный бой: зачистили волну — открываем следующую территорию.</summary>
    private void UpdateNormal()
    {
        if (AliveCount(heroes) == 0)
        {
            Finish(false, "Все герои выбыли из боя");
            return;
        }
        if (AliveCount(guards) == 0)
        {
            heroWave++;
            if (heroWave >= totalTerritories) { Finish(true, $"{siteName}: победа!"); return; }
            OpenHeroTerritory();
            hud.ShowMessage($"ВПЕРЁД!\n<size=60%>Территория {heroWave + 1} / {totalTerritories}</size>");
        }
        hud.SetProgress(Mathf.Min(heroWave + 1, totalTerritories), totalTerritories, AliveCount(guards));
    }

    /// <summary>Защита с подкреплением: враг прорывается через охрану к нашей команде.</summary>
    private void UpdateDefense()
    {
        if (AliveCount(heroes) == 0) { Finish(false, "Защитники выбыли — объект достаётся врагу"); return; }
        if (AliveCount(rivals) == 0) { Finish(true, "Нападение отбито!"); return; }

        // Враг перебил охрану объекта — барьер открывается, дальше бьётся с нашей командой
        if (rivalWave < rivalLaneLength && AliveCount(rivalGuards) == 0)
        {
            rivalWave++;
            int barrier = totalTerritories - 1 - rivalWave;
            if (barriers != null && barrier >= 0 && barrier < barriers.Length && barriers[barrier] != null)
                barriers[barrier].SetActive(false);
            rivalMinX = 0.5f;
            heroMaxX = ArenaLength - 0.5f;
            hud.ShowMessage("<color=#FF7A7A>ВРАГ ПРОРВАЛСЯ!</color>\n<size=60%>Охрана объекта пала — ваш ход</size>");
        }
        hud.SetProgressText($"Охрана объекта: {AliveCount(rivalGuards)}   •   Нападающих: {AliveCount(rivals)}   •   Наших: {AliveCount(heroes)}");
    }

    /// <summary>Битва за флаг: продвижение обеих команд и захват флага.</summary>
    private void UpdateContested()
    {
        if (AliveCount(heroes) == 0) { Finish(false, "Все наши герои выбыли — объект достаётся врагу"); return; }
        if (AliveCount(rivals) == 0) { Finish(true, "Команда врага разбита!"); return; }

        // Наши зачистили свою территорию
        if (heroWave < LaneTerritories && AliveCount(guards) == 0)
        {
            heroWave++;
            OpenHeroTerritory();
            hud.ShowMessage(heroWave < LaneTerritories
                ? $"ВПЕРЁД!\n<size=60%>Территория {heroWave + 1} / {LaneTerritories}</size>"
                : "К ФЛАГУ!\n<size=60%>Удержите центр</size>");
        }

        // Враг зачистил свою территорию
        if (rivalWave < LaneTerritories && AliveCount(rivalGuards) == 0)
        {
            rivalWave++;
            if (barriers != null && barriers.Length > totalTerritories - 1 - rivalWave && barriers[totalTerritories - 1 - rivalWave] != null)
                barriers[totalTerritories - 1 - rivalWave].SetActive(false);
            rivalMinX = (totalTerritories - 1 - rivalWave) * territoryWidth + 0.5f;
            if (rivalWave < LaneTerritories)
            {
                rivalRallyX = WorldX(ArenaLength - (rivalWave * territoryWidth + enemySpawnOffset - 1f));
                SpawnGuardWave(rivalWave, BattleFaction.RivalGuards);
            }
            else hud.ShowMessage("<color=#FF7A7A>ВРАГ В ЦЕНТРЕ!</color>\n<size=60%>Не дайте ему удержать флаг</size>");
        }

        UpdateFlag();
        hud.SetProgressText(ContestedProgressText());
    }

    /// <summary>Открыть нашим следующую территорию: убрать барьер, выпустить новую волну охраны.</summary>
    private void OpenHeroTerritory()
    {
        int barrier = heroWave - 1;
        if (barriers != null && barrier < barriers.Length && barriers[barrier] != null)
            barriers[barrier].SetActive(false);
        heroMaxX = (heroWave + 1) * territoryWidth - 0.5f;
        if (!contested || heroWave < LaneTerritories)
        {
            heroRallyX = WorldX(heroWave * territoryWidth + enemySpawnOffset - 1f);
            SpawnGuardWave(heroWave, BattleFaction.Guards);
        }
    }

    /// <summary>
    /// Захват флага: в зоне только наши — отсчёт к нам, только враги — к ним,
    /// обе стороны или никого — отсчёт стоит.
    /// </summary>
    private void UpdateFlag()
    {
        int ours = CountInZone(heroes), theirs = CountInZone(rivals);
        if (ours > 0 && theirs == 0) flagValue += Time.deltaTime;
        else if (theirs > 0 && ours == 0) flagValue -= Time.deltaTime;
        flagValue = Mathf.Clamp(flagValue, -flagHoldTime, flagHoldTime);

        if (flagZone != null) flagZone.SetProgress(flagValue / flagHoldTime, ours > 0 && theirs > 0);

        if (flagValue >= flagHoldTime) Finish(true, "Флаг удержан — объект наш!");
        else if (flagValue <= -flagHoldTime) Finish(false, "Враг удержал флаг — объект достаётся ему");
    }

    /// <summary>Сколько живых бойцов из списка стоит в зоне флага.</summary>
    private int CountInZone(List<Fighter> list)
    {
        int n = 0;
        Vector2 c = (Vector2)arenaRoot.position + FlagPoint;
        foreach (Fighter f in list)
        {
            if (!f.IsAlive) continue;
            Vector2 d = f.Position - c;
            float ex = d.x / flagZoneRadius.x, ey = d.y / flagZoneRadius.y;
            if (ex * ex + ey * ey <= 1f) n++;
        }
        return n;
    }

    /// <summary>Строка прогресса сверху: территории обеих команд и флаг.</summary>
    private string ContestedProgressText()
    {
        string flag;
        if (flagValue > 0.05f) flag = $"<color=#7FB8FF>Флаг: наши {flagValue:0.0} / {flagHoldTime:0}</color>";
        else if (flagValue < -0.05f) flag = $"<color=#FF7A7A>Флаг: враг {-flagValue:0.0} / {flagHoldTime:0}</color>";
        else flag = "Флаг: ничей";
        return $"<color=#7FB8FF>Наши: {Mathf.Min(heroWave, LaneTerritories)}/{LaneTerritories}</color>   •   " +
               $"<color=#FF7A7A>Враг: {Mathf.Min(rivalWave, LaneTerritories)}/{LaneTerritories}</color>   •   {flag}";
    }

    /// <summary>
    /// Куда идти команде, когда рядом нет противников: к следующей волне охраны
    /// или (в битве за флаг, пройдя свои территории) — к флагу. Охрана стоит на месте (false).
    /// </summary>
    public bool TryGetRallyPoint(Fighter f, out Vector2 point)
    {
        Vector2 flag = (Vector2)arenaRoot.position + FlagPoint + f.RallyOffset;
        switch (f.Faction)
        {
            case BattleFaction.Heroes:
                point = contested && heroWave >= LaneTerritories ? flag : new Vector2(heroRallyX, f.Position.y);
                return true;
            case BattleFaction.Rivals:
                if (contested && rivalWave >= LaneTerritories) point = flag;
                else if (defenseMode && rivalWave >= rivalLaneLength) point = new Vector2(WorldX(territoryWidth * 0.5f), f.Position.y); // к нашей команде
                else point = new Vector2(rivalRallyX, f.Position.y);
                return true;
            default:
                point = f.Position;
                return false;
        }
    }

    /// <summary>
    /// Ограничить позицию бойца ареной: каждая сторона — только в своих открытых территориях,
    /// охрана в битве за флаг — только на своей половине.
    /// </summary>
    public Vector3 ClampToArena(Fighter f, Vector3 pos)
    {
        Vector3 local = pos - arenaRoot.position;
        float minX = 0.5f, maxX = ArenaLength - 0.5f;
        switch (f.Faction)
        {
            case BattleFaction.Heroes: maxX = heroMaxX; break;
            case BattleFaction.Rivals: minX = rivalMinX; break;
        }
        // Охрана не уходит со своей территории (иначе отступивший охранник прятался бы за барьером)
        if (f.Territory >= 0)
        {
            minX = f.Territory * territoryWidth + 0.5f;
            maxX = (f.Territory + 1) * territoryWidth - 0.5f;
        }
        local.x = Mathf.Clamp(local.x, minX, maxX);
        local.y = Mathf.Clamp(local.y, floorMinY, floorMaxY);
        local.z = 0f;
        return arenaRoot.position + local;
    }

    /// <summary>Находится ли X внутри арены (для снарядов).</summary>
    public bool IsInsideArenaX(float worldX)
    {
        float x = worldX - arenaRoot.position.x;
        return x > -1f && x < ArenaLength + 1f;
    }

    /// <summary>Отступить (кнопка в интерфейсе боя).</summary>
    public void Retreat()
    {
        if (IsRunning && !finishing) Finish(false, "Команда отступила");
    }

    // ---------- Конец боя ----------

    /// <summary>Итог известен: показать результат и через паузу вернуться на карту.</summary>
    private void Finish(bool win, string details)
    {
        finishing = true;
        hud.ShowResult(win, details);
        StartCoroutine(EndAfterDelay(win));
    }

    private IEnumerator EndAfterDelay(bool win)
    {
        yield return new WaitForSeconds(resultDelay);

        var result = new BattleResult
        {
            win = win,
            ourHp = RemainingHp(heroes),
            theirHp = defenseMode ? RemainingHp(rivals) : RemainingHp(guards),
            rivalHp = RemainingHp(rivals),
            reinforcementHp = ReinforcementHp(),
            ourTerritories = contested ? Mathf.Min(heroWave, LaneTerritories) : heroWave,
            rivalTerritories = Mathf.Min(rivalWave, LaneTerritories)
        };

        // Сначала место боя применяет итог (захват, урон базе) и показывает окно итога,
        // а на карту возвращаемся, когда игрок нажмёт "ПРОДОЛЖИТЬ"
        Action<BattleResult> callback = onFinished;
        onFinished = null;
        callback?.Invoke(result);
        yield return new WaitUntil(() => !BattleResultWindowUI.IsShowing);

        Cleanup();
        BattleEnded?.Invoke();
    }

    /// <summary>Сколько здоровья (доля) осталось у команды подкрепления (1 — если её не было).</summary>
    private float ReinforcementHp()
    {
        var list = new List<Fighter>();
        foreach (Fighter f in allFighters) if (f.IsReinforcement) list.Add(f);
        return list.Count > 0 ? RemainingHp(list) : 1f;
    }

    /// <summary>Сколько здоровья (доля) осталось у бойцов (выбывшие — 0), не меньше минимума.</summary>
    private static float RemainingHp(List<Fighter> list)
    {
        float cur = 0f, max = 0f;
        foreach (Fighter f in list) { max += f.Health.Max; cur += f.IsAlive ? f.Health.Current : 0f; }
        return Mathf.Max(BattleCalculator.MinHpAfterBattle, max > 0 ? cur / max : 0f);
    }

    /// <summary>Убрать бойцов и снаряды, вернуть камеру и интерфейс карты, снять паузу.</summary>
    private void Cleanup()
    {
        IsRunning = false;
        for (int i = runtimeRoot.childCount - 1; i >= 0; i--) Destroy(runtimeRoot.GetChild(i).gameObject);
        heroes.Clear(); guards.Clear(); rivals.Clear(); rivalGuards.Clear();
        heroesFoes.Clear(); guardsFoes.Clear(); rivalsFoes.Clear(); rivalGuardsFoes.Clear();
        allFighters.Clear(); boxes.Clear(); buffs.Clear();
        contested = false;
        defenseMode = false;
        SetDecor(false);
        cam.transform.position = savedCamPos;
        cam.orthographicSize = savedCamSize;
        hud.Hide();
        SetMapUI(true);
        WorldTime.Paused = false;
    }

    // ---------- Помощники ----------

    /// <summary>Длина арены (все территории).</summary>
    private float ArenaLength => totalTerritories * territoryWidth;

    /// <summary>Центр зоны флага (локально от arenaRoot): середина центральной территории.</summary>
    private Vector2 FlagPoint => new Vector2((LaneTerritories + 0.5f) * territoryWidth, (floorMinY + floorMaxY) / 2f);

    /// <summary>Показать декорации нужного вида боя.</summary>
    private void SetDecor(bool contestedMode)
    {
        if (normalOnly != null) foreach (GameObject g in normalOnly) if (g != null) g.SetActive(!contestedMode);
        if (contestedOnly != null) foreach (GameObject g in contestedOnly) if (g != null) g.SetActive(contestedMode);
        if (flagZone != null) flagZone.gameObject.SetActive(contestedMode);
    }

    /// <summary>
    /// Камера следит за нашими героями и не выходит за края арены.
    /// При защите с подкреплением — пока враг бьётся с охраной объекта, камера следит за нападающими,
    /// а когда враг прорвался — за всеми сразу.
    /// </summary>
    private void UpdateCamera()
    {
        float sum = 0f, lead = 2f; // lead — сдвиг камеры вперёд по ходу движения
        int n = 0;
        if (defenseMode)
        {
            foreach (Fighter f in rivals)
                if (f.IsAlive) { sum += f.transform.position.x; n++; }
            if (rivalWave >= rivalLaneLength)
                foreach (Fighter f in heroes)
                    if (f.IsAlive) { sum += f.transform.position.x; n++; }
            lead = rivalWave >= rivalLaneLength ? 0f : -2f; // враг идёт влево
        }
        if (n == 0)
            foreach (Fighter f in heroes)
                if (f.IsAlive) { sum += f.transform.position.x; n++; }
        if (n == 0) return;

        float half = HalfViewWidth();
        float minX = arenaRoot.position.x + half;
        float maxX = arenaRoot.position.x + ArenaLength - half;
        float targetX = Mathf.Clamp(sum / n + lead, minX, Mathf.Max(minX, maxX));
        Vector3 p = cam.transform.position;
        p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-cameraFollowSpeed * Time.deltaTime));
        cam.transform.position = p;
    }

    private float HalfViewWidth() => cameraSize * (cam != null ? cam.aspect : 16f / 9f);

    private float WorldX(float localX) => arenaRoot.position.x + localX;

    private static int AliveCount(List<Fighter> list)
    {
        int n = 0;
        foreach (Fighter f in list) if (f.IsAlive) n++;
        return n;
    }

    /// <summary>Показать/спрятать интерфейс карты.</summary>
    private void SetMapUI(bool visible)
    {
        if (mapCanvas == null) return;
        mapCanvas.enabled = visible;
        var raycaster = mapCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = visible;
    }
}
