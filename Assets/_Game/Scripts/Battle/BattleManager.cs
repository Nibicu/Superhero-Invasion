using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ведёт ручной бой за объект.
/// - Карта ставится на паузу, камера переезжает на арену (она в этой же сцене, далеко от карты).
/// - Арена разделена на территории (обычно 3). На каждой — своя волна охраны объекта.
///   Наши герои не могут пройти дальше открытой территории (невидимая стена + барьер).
/// - Волна уничтожена → барьер открывается, герои идут дальше, появляется следующая волна.
/// - Все волны уничтожены — победа; все наши выбыли или нажато "Отступить" — поражение.
/// - После боя камера и интерфейс карты возвращаются, а захватываемый объект получает результат.
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
    [Tooltip("Барьеры между территориями (0 — между 1-й и 2-й и т.д.)")]
    [SerializeField] private GameObject[] barriers;

    [Header("Префабы")]
    [SerializeField] private Fighter fighterPrefab;
    [SerializeField] private Projectile projectilePrefab;

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
    [Tooltip("Где появляются враги на своей территории (отступ от её левого края)")]
    [SerializeField] private float enemySpawnOffset = 13f;

    [Header("Интерфейс")]
    [Tooltip("Интерфейс карты — прячется на время боя")]
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private BattleHUD hud;

    private readonly List<Fighter> players = new List<Fighter>(); // Наши бойцы
    private readonly List<Fighter> enemies = new List<Fighter>(); // Охрана (все волны, включая павших)
    private ArenaTint[] tints;          // Детали арены, которые красятся в цвет объекта
    private MapObjectData objectData;   // За что бьёмся
    private Action<bool, float> onFinished; // Кого известить об итоге: (победа, доля HP команды)
    private int territory;              // Текущая территория (с 0)
    private int totalTerritories;
    private float unlockedMaxX;         // До какого X (локально) могут дойти наши
    private bool finishing;             // Итог уже известен, ждём возврата
    private Camera cam;
    private Vector3 savedCamPos;
    private float savedCamSize;

    /// <summary>Идёт ли бой.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Куда складывать снаряды и бойцов.</summary>
    public Transform RuntimeRoot => runtimeRoot;

    /// <summary>Точка сбора наших (X в мире): к ней идут, когда врагов рядом нет.</summary>
    public float RallyX { get; private set; }

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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Бойцы команды.</summary>
    public IReadOnlyList<Fighter> GetTeam(Team team) => team == Team.Player ? players : enemies;

    /// <summary>Противники команды.</summary>
    public IReadOnlyList<Fighter> GetOpponents(Team team) => team == Team.Player ? enemies : players;

    // ---------- Начало боя ----------

    /// <summary>
    /// Начать бой: data — объект (его охрана и цвет), squad — наша команда,
    /// finished(победа, доля HP) — вызовется после возврата на карту.
    /// </summary>
    public void StartBattle(MapObjectData data, Squad squad, Action<bool, float> finished)
    {
        if (IsRunning) return;
        objectData = data;
        onFinished = finished;
        IsRunning = true;
        finishing = false;
        WorldTime.Paused = true;

        totalTerritories = Mathf.Clamp(data.waves != null ? data.waves.Length : 1, 1, (barriers?.Length ?? 0) + 1);
        foreach (ArenaTint t in tints) t.Apply(data.color);
        if (barriers != null) foreach (GameObject b in barriers) if (b != null) b.SetActive(true);

        territory = 0;
        unlockedMaxX = territoryWidth - 0.5f;
        RallyX = WorldX(enemySpawnOffset - 1f);

        // Наши герои — слева, вразброс по глубине
        var heroes = new List<HeroInstance>(squad.AllHeroes);
        for (int i = 0; i < heroes.Count; i++)
        {
            float x = 1.5f + (i % 2) * 1.2f;
            float y = Mathf.Lerp(floorMinY + 0.4f, floorMaxY - 0.4f, (i + 0.5f) / heroes.Count);
            Spawn(heroes[i].Data, heroes[i].Stats, Team.Player, squad.HpFraction, new Vector2(x, y));
        }
        SpawnWave(0);

        // Камера и интерфейс
        cam = Camera.main;
        savedCamPos = cam.transform.position;
        savedCamSize = cam.orthographicSize;
        cam.orthographicSize = cameraSize;
        cam.transform.position = new Vector3(arenaRoot.position.x + HalfViewWidth(), arenaRoot.position.y + cameraYOffset, savedCamPos.z);
        SetMapUI(false);
        hud.Show(this, data.displayName, players);
        hud.ShowMessage($"{data.displayName.ToUpper()}\n<size=60%>Территория 1 / {totalTerritories}</size>");
    }

    /// <summary>Создать бойца в точке localPos (координаты арены).</summary>
    private Fighter Spawn(HeroData data, HeroStats stats, Team team, float hpFraction, Vector2 localPos)
    {
        Vector3 pos = arenaRoot.position + (Vector3)localPos;
        Fighter f = Instantiate(fighterPrefab, pos, Quaternion.identity, runtimeRoot);
        f.Combat.SetProjectilePrefab(projectilePrefab);
        f.Init(data, stats, team, hpFraction);
        (team == Team.Player ? players : enemies).Add(f);
        return f;
    }

    /// <summary>Выпустить охрану территории index.</summary>
    private void SpawnWave(int index)
    {
        if (objectData.waves == null || index >= objectData.waves.Length) return;
        GuardEntry[] guards = objectData.waves[index].guards;
        int count = 0;
        foreach (GuardEntry g in guards) if (g != null && g.unit != null) count++;
        int j = 0;
        foreach (GuardEntry g in guards)
        {
            if (g == null || g.unit == null) continue;
            float x = index * territoryWidth + enemySpawnOffset + (j % 2) * 1.5f + j * 0.6f;
            float y = Mathf.Lerp(floorMinY + 0.5f, floorMaxY - 0.5f, (j + 0.5f) / count);
            Spawn(g.unit, BattleCalculator.GuardStats(g), Team.Enemy, 1f, new Vector2(x, y));
            j++;
        }
    }

    // ---------- Ход боя ----------

    private void Update()
    {
        if (!IsRunning) return;
        UpdateCamera();
        if (finishing) return;

        int enemiesAlive = AliveCount(enemies);
        if (AliveCount(players) == 0)
        {
            Finish(false, "Все герои выбыли из боя");
        }
        else if (enemiesAlive == 0)
        {
            territory++;
            if (territory >= totalTerritories) Finish(true, $"{objectData.displayName} захвачен!");
            else OpenTerritory(territory);
        }
        hud.SetProgress(Mathf.Min(territory + 1, totalTerritories), totalTerritories, AliveCount(enemies));
    }

    /// <summary>Открыть следующую территорию: убрать барьер, выпустить новую волну.</summary>
    private void OpenTerritory(int index)
    {
        if (barriers != null && index - 1 < barriers.Length && barriers[index - 1] != null)
            barriers[index - 1].SetActive(false);
        unlockedMaxX = (index + 1) * territoryWidth - 0.5f;
        RallyX = WorldX(index * territoryWidth + enemySpawnOffset - 1f);
        SpawnWave(index);
        hud.ShowMessage($"ВПЕРЁД!\n<size=60%>Территория {index + 1} / {totalTerritories}</size>");
    }

    /// <summary>Ограничить позицию бойца ареной (наши — только до открытой территории).</summary>
    public Vector3 ClampToArena(Team team, Vector3 pos)
    {
        Vector3 local = pos - arenaRoot.position;
        float maxX = team == Team.Player ? unlockedMaxX : totalTerritories * territoryWidth - 0.5f;
        local.x = Mathf.Clamp(local.x, 0.5f, maxX);
        local.y = Mathf.Clamp(local.y, floorMinY, floorMaxY);
        local.z = 0f;
        return arenaRoot.position + local;
    }

    /// <summary>Находится ли X внутри арены (для снарядов).</summary>
    public bool IsInsideArenaX(float worldX)
    {
        float x = worldX - arenaRoot.position.x;
        return x > -1f && x < totalTerritories * territoryWidth + 1f;
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

        // Сколько здоровья осталось у команды (выбывшие — 0)
        float cur = 0f, max = 0f;
        foreach (Fighter f in players) { max += f.Health.Max; cur += f.IsAlive ? f.Health.Current : 0f; }
        float hpFraction = Mathf.Max(BattleCalculator.MinHpAfterBattle, max > 0 ? cur / max : 0f);

        Cleanup();
        Action<bool, float> callback = onFinished;
        onFinished = null;
        callback?.Invoke(win, hpFraction);
        BattleEnded?.Invoke();
    }

    /// <summary>Убрать бойцов и снаряды, вернуть камеру и интерфейс карты, снять паузу.</summary>
    private void Cleanup()
    {
        IsRunning = false;
        for (int i = runtimeRoot.childCount - 1; i >= 0; i--) Destroy(runtimeRoot.GetChild(i).gameObject);
        players.Clear();
        enemies.Clear();
        cam.transform.position = savedCamPos;
        cam.orthographicSize = savedCamSize;
        hud.Hide();
        SetMapUI(true);
        WorldTime.Paused = false;
    }

    // ---------- Помощники ----------

    /// <summary>Камера следит за нашими героями и не выходит за края арены.</summary>
    private void UpdateCamera()
    {
        float sum = 0f;
        int n = 0;
        foreach (Fighter f in players)
            if (f.IsAlive) { sum += f.transform.position.x; n++; }
        if (n == 0) return;

        float half = HalfViewWidth();
        float minX = arenaRoot.position.x + half;
        float maxX = arenaRoot.position.x + totalTerritories * territoryWidth - half;
        float targetX = Mathf.Clamp(sum / n + 2f, minX, Mathf.Max(minX, maxX));
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
