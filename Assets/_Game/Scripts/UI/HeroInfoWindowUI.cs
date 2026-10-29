using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подробная карточка героя: портрет, имя, звёзды, уровень, жизни, описание,
/// опыт и 10 характеристик со значками и полосками, навыки (открытые и закрытые), бонус командира.
/// Кнопка внизу: "НАНЯТЬ" (если герой ещё не нанят). У героя 5 уровня в паузе — "ПОВЫСИТЬ ЗВЕЗДУ" (за плутоний).
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
    [SerializeField] private StatRowUI[] statRows;    // 10 строк характеристик
    [SerializeField] private StatRowUI xpRow;         // Строка опыта ("Опыт 40 / 100")
    [Tooltip("Значки характеристик по порядку HeroStats.Names (Assets/_Game/Art/Icons/Stats)")]
    [SerializeField] private Sprite[] statIcons = new Sprite[0];
    [SerializeField] private Sprite xpIcon;           // Значок опыта (мозг)
    [SerializeField] private TMP_Text skillsText;     // Навыки
    [SerializeField] private ScrollRect skillsScroll; // Прокрутка навыков (текст длинный — крутится колесом мыши)
    [SerializeField] private TMP_Text captainText;    // Бонус командира
    [SerializeField] private Button actionButton;     // "НАНЯТЬ"
    [SerializeField] private TMP_Text actionText;     // Текст на кнопке
    [SerializeField] private Button boostButton;      // "ПОВЫСИТЬ ЗВЕЗДУ" за плутоний (5 уровень, в паузе)
    [SerializeField] private TMP_Text boostText;      // Текст на этой кнопке

    [Header("Инвентарь героя (3 ячейки для вещей-артефактов)")]
    [SerializeField] private GameObject slotsRoot;    // Ряд ячеек (виден только у нанятого героя)
    [SerializeField] private Button[] slotButtons;    // Кнопки-ячейки
    [SerializeField] private TMP_Text[] slotTexts;    // Буква надетой вещи или "+"

    private HeroData current; // Какой герой показан

    /// <summary>Подписываем кнопки.</summary>
    protected override void Awake()
    {
        base.Awake();
        actionButton.onClick.AddListener(OnActionClicked);
        if (boostButton != null) boostButton.onClick.AddListener(OnBoostClicked);
        if (slotButtons != null)
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int index = i; // копия для лямбды
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(index));
            }
    }

    /// <summary>Подписываемся на изменения денег, героев и артефактов.</summary>
    private void Start()
    {
        ResourceManager.Instance.ResourcesChanged += OnTeamChanged;
        HeroManager.Instance.HeroesChanged += OnTeamChanged;
        if (ArtifactManager.Instance != null) ArtifactManager.Instance.Changed += OnTeamChanged;
    }

    /// <summary>Отписываемся.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnTeamChanged;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnTeamChanged;
        if (ArtifactManager.Instance != null) ArtifactManager.Instance.Changed -= OnTeamChanged;
    }

    /// <summary>Клик по ячейке — выбрать вещь для неё (или снять надетую).</summary>
    private void OnSlotClicked(int slot)
    {
        HeroInstance hero = HeroManager.Instance.FindHero(Team.Player, current);
        if (hero != null && ArtifactWindowUI.Instance != null) ArtifactWindowUI.Instance.OpenForSlot(hero, slot);
    }

    /// <summary>Нарисовать 3 ячейки: надетая вещь — её цвет и буква, пустая — "+".</summary>
    private void RefreshSlots(HeroInstance hero)
    {
        if (slotsRoot != null) slotsRoot.SetActive(hero != null);
        if (hero == null || slotButtons == null) return;
        for (int i = 0; i < slotButtons.Length && i < HeroInstance.SlotCount; i++)
        {
            ArtifactData a = hero.Slots[i];
            slotButtons[i].image.color = a != null ? a.color : new Color(0.2f, 0.25f, 0.34f);
            if (slotTexts != null && i < slotTexts.Length)
                slotTexts[i].text = a != null ? a.iconLetter : "+";
        }
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
        RefreshSlots(hero);
        portrait.Show(current);
        stars.Show(current.stars);
        nameText.text = current.displayName;
        descText.text = current.description;

        // Уровень, звёзды, жизни и статус
        if (hero != null) stars.Show(Mathf.Min(3, Mathf.FloorToInt(hero.Stars)));
        levelText.text = hero != null
            ? $"Уровень {hero.Level} / {hm.MaxHeroLevel}  •  {hero.StarsText}  •  {hero.StatusText}" +
              $"\n<size=85%>Жизни: <color=#FF6B7A>{hero.Lives} / {HeroInstance.MaxLives}</color>{(hm.IsReserve(hero) ? "  •  <color=#FF9A6B>в запасе</color>" : "")}</size>"
            : "<color=#9AA4B5>Не нанят</color>";
        // Класс, тип урона скилов и сила
        HeroStats shown = hero != null ? hero.Stats : current.baseStats;
        levelText.text += $"\n<size=85%><color=#FFD84A>{UnitClasses.Name(current.unitClass)}</color>  •  сила {BattleCalculator.UnitPower(current, shown)}" +
                          $"\nскилы от: {UnitClasses.DamageName(current.skillDamage)}</size>";

        // Повышение звезды — у героя 5 уровня, в тактической паузе, за плутоний
        if (boostButton != null)
        {
            bool show = hero != null && hm.StarReady(hero);
            boostButton.gameObject.SetActive(show);
            if (show)
            {
                bool can = hm.CanStarUp(hero, out string reason);
                bool afford = ResourceManager.Instance.CanAfford(Team.Player, 0, hm.StarUpCost);
                string next = current.stars >= 3 ? "3.5 зв. (+10%)" : $"{current.stars + 1} зв.";
                boostButton.interactable = can;
                boostText.text = can
                    ? $"ПОВЫСИТЬ ЗВЕЗДУ → {next}  <color={(afford ? "#E2C6FF" : "#FF6B6B")}>{hm.StarUpCost} плутония</color>"
                    : $"ЗВЕЗДА: {reason.ToLower()}";
            }
        }

        // Опыт (до следующего уровня)
        if (xpRow != null)
        {
            if (hero == null) xpRow.SetText("Опыт", "—", 0f, xpIcon);
            else if (hero.Level >= hm.MaxHeroLevel) xpRow.SetText("Опыт", "макс.", 1f, xpIcon);
            else xpRow.SetText("Опыт", $"{hero.Xp} / {HeroInstance.XpPerLevel}", (float)hero.Xp / HeroInstance.XpPerLevel, xpIcon);
        }

        // Характеристики (с учётом уровня и звезды, если герой нанят)
        int[] values = (hero != null ? hero.Stats : current.baseStats).ToArray();
        for (int i = 0; i < statRows.Length && i < values.Length; i++)
            statRows[i].Set(HeroStats.Names[i], values[i], HeroStats.BarMax[i], i < statIcons.Length ? statIcons[i] : null);

        // Навыки: открытые — белым, закрытые — серым с подписью уровня
        int level = hero != null ? hero.Level : 1;
        var sb = new StringBuilder();
        // Пассивный навык — работает всегда
        HeroSkill passive = current.passive ?? new HeroSkill();
        sb.AppendLine(passive.IsEmpty
            ? "<color=#6B7385><b>Пассивный навык:</b> пока нет</color>"
            : $"<color=#C58BFF><b>{passive.name}</b></color> (пассивный) — {passive.description}");
        // 4 скила: открываются на 1, 2, 4 и 5 уровне
        for (int i = 0; i < HeroData.SkillUnlockLevels.Length; i++)
        {
            HeroSkill s = current.GetSkill(i);
            int unlock = HeroData.SkillUnlockLevels[i];
            string cd = s.cooldown > 0f ? $", перезарядка {s.cooldown:0} с" : "";
            if (s.IsEmpty)
                sb.AppendLine($"<color=#6B7385>Скил {i + 1} (ур. {unlock}): пока нет</color>");
            else if (level >= unlock)
                sb.AppendLine($"<color=#FFFFFF><b>{s.name}</b></color> <size=85%>(скил {i + 1}{cd})</size> — {s.description}");
            else
                sb.AppendLine($"<color=#6B7385><b>{s.name}</b> (откроется на ур. {unlock}{cd}) — {s.description}</color>");
        }
        // Пока своих скилов нет — в бою работает скил класса
        if (!current.HasOwnSkills)
        {
            ClassProfile cp = current.Profile;
            sb.AppendLine($"<color=#FFD84A><b>{cp.skillName}</b></color> <size=85%>(скил класса, пока нет своих)</size> — {cp.skillDescription}");
        }
        skillsText.text = sb.ToString().TrimEnd();
        if (skillsScroll != null)
        {
            // Высота текста меняется — пересчитать и показать начало списка
            LayoutRebuilder.ForceRebuildLayoutImmediate(skillsText.rectTransform);
            skillsScroll.verticalNormalizedPosition = 1f;
        }
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
        actionButton.gameObject.SetActive(hero == null); // нанятого героя качает опыт — кнопки прокачки нет
    }

    /// <summary>Нажата кнопка "ПОВЫСИТЬ ЗВЕЗДУ" (5 уровень, в паузе, за плутоний).</summary>
    private void OnBoostClicked()
    {
        HeroInstance hero = HeroManager.Instance.FindHero(Team.Player, current);
        if (hero == null) return;
        if (HeroManager.Instance.TryStarUp(hero, out string error))
            ToastUI.Show($"{current.displayName} теперь {hero.StarsText}! Характеристики выросли");
        else
            ToastUI.Show(error);
    }

    /// <summary>Нажата кнопка: нанять или прокачать.</summary>
    private void OnActionClicked()
    {
        HeroManager hm = HeroManager.Instance;
        HeroInstance hero = hm.FindHero(Team.Player, current);
        string error;
        if (hero != null) return;
        if (hm.TryHire(Team.Player, current, out error)) ToastUI.Show($"{current.displayName} теперь в бюро!");
        else ToastUI.Show(error);
    }
}
