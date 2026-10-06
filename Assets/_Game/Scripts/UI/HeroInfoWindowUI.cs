using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подробная карточка героя: портрет, имя, звёзды, уровень, описание,
/// 9 характеристик с полосками, навыки (открытые и закрытые), бонус командира.
/// Кнопка внизу: "НАНЯТЬ" (если герой ещё не нанят) или "ПРОКАЧАТЬ".
/// </summary>
public class HeroInfoWindowUI : WindowUI
{
    [Header("Карточка героя")]
    [SerializeField] private Image header;            // Полоса заголовка (цвет по звёздам)
    [SerializeField] private HeroPortraitUI portrait; // Большой портрет
    [SerializeField] private StarsUI stars;           // Звёзды
    [SerializeField] private TMP_Text nameText;       // Имя
    [SerializeField] private TMP_Text levelText;      // "Уровень 2 / 5 • На базе"
    [SerializeField] private TMP_Text descText;       // Описание
    [SerializeField] private StatRowUI[] statRows;    // 9 строк характеристик
    [SerializeField] private TMP_Text skillsText;     // Навыки
    [SerializeField] private TMP_Text captainText;    // Бонус командира
    [SerializeField] private Button actionButton;     // "НАНЯТЬ" / "ПРОКАЧАТЬ"
    [SerializeField] private TMP_Text actionText;     // Текст на кнопке
    [SerializeField] private Button boostButton;      // "УСИЛИТЬ" за плутоний (нужен Институт)
    [SerializeField] private TMP_Text boostText;      // Текст на кнопке усиления

    private HeroData current; // Какой герой показан

    /// <summary>Подписываем кнопки.</summary>
    protected override void Awake()
    {
        base.Awake();
        actionButton.onClick.AddListener(OnActionClicked);
        if (boostButton != null) boostButton.onClick.AddListener(OnBoostClicked);
    }

    /// <summary>Подписываемся на изменения денег и героев.</summary>
    private void Start()
    {
        ResourceManager.Instance.ResourcesChanged += OnTeamChanged;
        HeroManager.Instance.HeroesChanged += OnTeamChanged;
    }

    /// <summary>Отписываемся.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnTeamChanged;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnTeamChanged;
    }

    private void OnTeamChanged(Team team)
    {
        if (team == Team.Player && IsOpen) Refresh();
    }

    /// <summary>Открыть карточку героя (нанятого или нет — окно само разберётся).</summary>
    public void ShowHero(HeroData data)
    {
        current = data;
        Open();
    }

    /// <summary>Перерисовать карточку.</summary>
    public override void Refresh()
    {
        if (current == null) return;
        HeroManager hm = HeroManager.Instance;
        HeroInstance hero = hm.FindHero(Team.Player, current); // null — если не нанят

        header.color = HeroData.StarColor(current.stars);
        portrait.Show(current);
        stars.Show(current.stars);
        nameText.text = current.displayName;
        descText.text = current.description;

        // Уровень и статус
        levelText.text = hero != null
            ? $"Уровень {hero.Level} / {hm.MaxHeroLevel}  •  {hero.StatusText}" +
              (hero.Boosts > 0 ? $"\n<size=80%><color=#C58BFF>Усилен: {hero.Boosts} / {hm.MaxBoosts}</color></size>" : "")
            : "<color=#9AA4B5>Не нанят</color>";

        // Кнопка усиления — только для нанятых героев
        if (boostButton != null)
        {
            boostButton.gameObject.SetActive(hero != null);
            if (hero != null)
            {
                bool canBoost = hm.CanBoost(hero, out string boostReason);
                int cost = hm.GetBoostCost(hero);
                bool afford = ResourceManager.Instance.CanAfford(Team.Player, 0, cost);
                boostButton.interactable = canBoost;
                boostText.text = canBoost
                    ? $"УСИЛИТЬ +10%  <color={(afford ? "#E2C6FF" : "#FF6B6B")}>{cost} плутония</color>"
                    : $"УСИЛИТЬ: {boostReason.ToLower()}";
            }
        }

        // Характеристики (с учётом уровня, если герой нанят)
        int[] values = (hero != null ? hero.Stats : current.baseStats).ToArray();
        for (int i = 0; i < statRows.Length && i < values.Length; i++)
            statRows[i].Set(HeroStats.Names[i], values[i], HeroStats.BarMax[i]);

        // Навыки: открытые — белым, закрытые — серым с подписью уровня
        int level = hero != null ? hero.Level : 1;
        var sb = new StringBuilder();
        foreach (HeroSkill s in current.skills)
        {
            if (level >= s.unlockLevel)
                sb.AppendLine($"<color=#FFFFFF><b>{s.name}</b></color> — {s.description}");
            else
                sb.AppendLine($"<color=#6B7385><b>{s.name}</b> (откроется на ур. {s.unlockLevel}) — {s.description}</color>");
        }
        skillsText.text = sb.ToString().TrimEnd();
        captainText.text = string.IsNullOrEmpty(current.captainBonus) ? "—" : current.captainBonus;

        // Кнопка
        if (hero == null)
        {
            bool canHire = hm.CanHire(Team.Player, current, out string reason);
            bool afford = ResourceManager.Instance.CanAfford(Team.Player, current.hireCost);
            actionButton.interactable = canHire;
            actionText.text = canHire
                ? $"НАНЯТЬ\n<size=75%><color={(afford ? "#FFD84A" : "#FF6B6B")}>{current.hireCost} золота</color></size>"
                : reason.ToUpper();
        }
        else if (hero.Level >= hm.MaxHeroLevel)
        {
            actionButton.interactable = false;
            actionText.text = "МАКС. УРОВЕНЬ";
        }
        else
        {
            int cost = hm.GetLevelUpCost(hero);
            bool afford = ResourceManager.Instance.CanAfford(Team.Player, cost);
            actionButton.interactable = true;
            actionText.text = $"ПРОКАЧАТЬ ДО УР. {hero.Level + 1}\n<size=75%><color={(afford ? "#FFD84A" : "#FF6B6B")}>{cost} золота</color> • +10% к статам</size>";
        }
    }

    /// <summary>Нажата кнопка "УСИЛИТЬ" — усиление за плутоний (нужен Институт).</summary>
    private void OnBoostClicked()
    {
        HeroInstance hero = HeroManager.Instance.FindHero(Team.Player, current);
        if (hero == null) return;
        if (HeroManager.Instance.TryBoost(hero, out string error))
            ToastUI.Show($"{current.displayName} усилен! (+10% ко всем характеристикам)");
        else
            ToastUI.Show(error);
    }

    /// <summary>Нажата кнопка: нанять или прокачать.</summary>
    private void OnActionClicked()
    {
        HeroManager hm = HeroManager.Instance;
        HeroInstance hero = hm.FindHero(Team.Player, current);
        string error;
        if (hero == null)
        {
            if (hm.TryHire(Team.Player, current, out error)) ToastUI.Show($"{current.displayName} теперь в бюро!");
            else ToastUI.Show(error);
        }
        else
        {
            if (hm.TryLevelUp(hero, out error)) ToastUI.Show($"{current.displayName} достиг уровня {hero.Level}!");
            else ToastUI.Show(error);
        }
    }
}
