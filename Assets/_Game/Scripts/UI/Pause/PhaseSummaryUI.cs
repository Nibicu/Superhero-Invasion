using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Итоги фазы: в конце каждой фазы (миссий или события), когда начинается тактическая пауза,
/// по центру экрана появляются очки фракции и плутоний — сколько стало и сколько заработано
/// за фазу (+N зелёным), а ниже — портреты героев, которые за фазу повысили уровень.
/// Само исчезает через несколько секунд; клик закрывает сразу.
/// Собирает окно PauseBuilder (Tools → Superhero → Собрать фракции и паузу).
/// </summary>
public class PhaseSummaryUI : MonoBehaviour
{
    [Header("Окно")]
    [SerializeField] private GameObject root;          // Всё окно (фон на весь экран + панель)
    [SerializeField] private CanvasGroup group;        // Для плавного исчезновения
    [SerializeField] private Button closeButton;       // Клик в любом месте — закрыть
    [SerializeField] private TMP_Text titleText;       // "ИТОГИ ФАЗЫ"

    [Header("Ресурсы")]
    [SerializeField] private TMP_Text goldText;        // Больше не используется (золото стало очками фракции)
    [SerializeField] private TMP_Text plutoniumText;   // "120  +40"
    [SerializeField] private TMP_Text factionText;     // "900  +450"

    [Header("Герои, повысившие уровень")]
    [SerializeField] private TMP_Text levelHeader;     // "Повысили уровень:"
    [SerializeField] private RectTransform avatarList; // Ряд портретов
    [SerializeField] private GameObject avatarTemplate; // Шаблон: портрет (HeroPortraitUI) + подпись "Ур. 3"

    [Header("Время")]
    [Tooltip("Сколько секунд итоги видны (потом плавно исчезают)")]
    [SerializeField] private float showTime = 4f;
    [Tooltip("Сколько секунд длится исчезновение")]
    [SerializeField] private float fadeTime = 0.5f;
    [Tooltip("За сколько секунд числа \"набегают\" до итоговых")]
    [SerializeField] private float countTime = 1f;

    private bool tracking;          // Идёт фаза миссий/события — считаем заработок
    private int earnedGold;         // Не используется (золото стало очками фракции)
    private int earnedPlutonium;    // Заработано плутония за фазу
    private int earnedPoints;       // Заработано очков фракции за фазу
    private readonly Dictionary<HeroInstance, int> startLevels = new Dictionary<HeroInstance, int>(); // Уровни героев в начале фазы
    private readonly List<GameObject> avatars = new List<GameObject>(); // Созданные портреты
    private float shownFor;         // Сколько секунд окно уже видно
    private int endGold, endPlutonium, endPoints; // Итоговые числа (к ним "набегают" счётчики)

    /// <summary>Прячем окно и шаблон, подписываем клик.</summary>
    private void Awake()
    {
        if (avatarTemplate != null) avatarTemplate.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        root.SetActive(false);
    }

    /// <summary>Подписываемся на смену фаз и начисления.</summary>
    private void Start()
    {
        if (PhaseManager.Instance != null) PhaseManager.Instance.Changed += OnPhaseChanged;
        if (ResourceManager.Instance != null) ResourceManager.Instance.Added += OnResourcesAdded;
        OnPhaseChanged();
    }

    /// <summary>Отписываемся.</summary>
    private void OnDestroy()
    {
        if (PhaseManager.Instance != null) PhaseManager.Instance.Changed -= OnPhaseChanged;
        if (ResourceManager.Instance != null) ResourceManager.Instance.Added -= OnResourcesAdded;
    }

    /// <summary>Фаза сменилась: началась фаза миссий/события — начинаем считать; началась пауза — показываем итоги.</summary>
    private void OnPhaseChanged()
    {
        PhaseManager pm = PhaseManager.Instance;
        if (pm == null) return;
        bool active = pm.Phase == GamePhase.Missions || pm.Phase == GamePhase.Event;
        if (active && !tracking) BeginTracking();
        else if (pm.Phase == GamePhase.Pause && tracking)
        {
            tracking = false;
            Show();
        }
    }

    /// <summary>Начало фазы: обнуляем заработок и запоминаем уровни героев.</summary>
    private void BeginTracking()
    {
        tracking = true;
        earnedGold = earnedPlutonium = earnedPoints = 0;
        startLevels.Clear();
        if (HeroManager.Instance != null)
            foreach (HeroInstance h in HeroManager.Instance.GetHeroes(Team.Player)) startLevels[h] = h.Level;
    }

    /// <summary>Игроку что-то выдали (доход, награда) — прибавляем к заработку фазы.</summary>
    private void OnResourcesAdded(Team team, int gold, int plutonium)
    {
        if (!tracking || team != Team.Player) return;
        if (gold > 0) earnedPoints += gold; // очки фракции лежат в кошельке на месте золота
        if (plutonium > 0) earnedPlutonium += plutonium;
    }

    /// <summary>Показать итоги фазы.</summary>
    private void Show()
    {
        ResourceManager rm = ResourceManager.Instance;
        endGold = rm != null ? rm.GetGold(Team.Player) : 0;
        endPlutonium = rm != null ? rm.GetPlutonium(Team.Player) : 0;
        endPoints = endGold; // очки фракции = кошелёк (бывшее золото)
        if (titleText != null) titleText.text = "ИТОГИ ФАЗЫ";

        // Герои, которые за фазу выросли в уровне
        foreach (GameObject a in avatars) Destroy(a);
        avatars.Clear();
        if (HeroManager.Instance != null && avatarTemplate != null)
            foreach (HeroInstance h in HeroManager.Instance.GetHeroes(Team.Player))
            {
                if (!startLevels.TryGetValue(h, out int was) || h.Level <= was) continue;
                GameObject a = Instantiate(avatarTemplate, avatarList);
                a.SetActive(true);
                HeroPortraitUI p = a.GetComponentInChildren<HeroPortraitUI>(true);
                if (p != null) p.Show(h.Data);
                Transform label = a.transform.Find("Fill/Label"); // подпись (не текст внутри портрета)
                TMP_Text t = label != null ? label.GetComponent<TMP_Text>() : null;
                if (t != null) t.text = $"{h.Data.displayName}\n<color=#6EE07A>Ур. {was} → {h.Level}</color>";
                avatars.Add(a);
            }
        if (levelHeader != null)
            levelHeader.text = avatars.Count > 0 ? "Повысили уровень:" : "<color=#8792A6>Никто не повысил уровень</color>";

        shownFor = 0f;
        group.alpha = 1f;
        root.SetActive(true);
        transform.SetAsLastSibling(); // поверх окна паузы
        UpdateCounters(0f);
    }

    /// <summary>Спрятать итоги.</summary>
    public void Hide() => root.SetActive(false);

    /// <summary>Числа набегают, через showTime секунд окно плавно исчезает (время реальное — карта на паузе).</summary>
    private void Update()
    {
        if (!root.activeSelf) return;
        shownFor += Time.unscaledDeltaTime;
        UpdateCounters(Mathf.Clamp01(shownFor / Mathf.Max(0.01f, countTime)));
        if (shownFor > showTime)
        {
            group.alpha = 1f - Mathf.Clamp01((shownFor - showTime) / Mathf.Max(0.01f, fadeTime));
            if (group.alpha <= 0f) Hide();
        }
    }

    /// <summary>Нарисовать числа: от "было" (итог минус заработок) к итогу, t = 0..1.</summary>
    private void UpdateCounters(float t)
    {
        if (goldText != null) goldText.gameObject.SetActive(false); // отдельного золота больше нет
        plutoniumText.text = Counter(endPlutonium, earnedPlutonium, t, "#C58BFF");
        factionText.text = Counter(endPoints, earnedPoints, t, "#C77DFF");
    }

    /// <summary>Одно число: "1500" и под ним зелёным "+600 за фазу".</summary>
    private static string Counter(int end, int earned, float t, string color)
    {
        int shown = Mathf.RoundToInt(Mathf.Lerp(end - earned, end, t));
        return $"<color={color}>{shown}</color>\n<size=60%><color=#6EE07A>+{earned}</color> <color=#9AA4B5>за фазу</color></size>";
    }
}
