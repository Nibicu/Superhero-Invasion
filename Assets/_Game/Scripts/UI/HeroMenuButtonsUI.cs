using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопки слева внизу экрана: "РОСТЕР" и "МОИ ГЕРОИ".
/// На кнопке "МОИ ГЕРОИ" показывается счётчик героев (2/4).
/// </summary>
public class HeroMenuButtonsUI : MonoBehaviour
{
    [SerializeField] private Button rosterButton;          // Кнопка РОСТЕР
    [SerializeField] private Button myHeroesButton;        // Кнопка МОИ ГЕРОИ
    [SerializeField] private TMP_Text myHeroesCounter;     // Счётчик "2/4" на кнопке
    [SerializeField] private RosterWindowUI rosterWindow;  // Окно РОСТЕР
    [SerializeField] private MyHeroesWindowUI myHeroesWindow; // Окно МОИ ГЕРОИ

    /// <summary>Подписываем кнопки.</summary>
    private void Awake()
    {
        rosterButton.onClick.AddListener(rosterWindow.Open);
        myHeroesButton.onClick.AddListener(myHeroesWindow.Open);
    }

    /// <summary>Подписываемся на изменения героев и базы (лимит меняется от Резервных комнат).</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnHeroesChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed += UpdateCounter;
        UpdateCounter();
    }

    /// <summary>Отписываемся.</summary>
    private void OnDestroy()
    {
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnHeroesChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed -= UpdateCounter;
    }

    private void OnHeroesChanged(Team team)
    {
        if (team == Team.Player) UpdateCounter();
    }

    /// <summary>Обновить счётчик на кнопке МОИ ГЕРОИ.</summary>
    private void UpdateCounter()
    {
        HeroManager hm = HeroManager.Instance;
        myHeroesCounter.text = $"{hm.GetHeroes(Team.Player).Count}/{hm.GetHeroLimit(Team.Player)}";
    }
}
