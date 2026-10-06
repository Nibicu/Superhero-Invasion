using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно главной базы: уровень, кнопка улучшения базы и 5 ячеек построек.
/// Открывается кликом по базе на карте.
/// Свою базу можно менять; вражескую — только смотреть, и только если построен Радар.
/// </summary>
public class BaseWindowUI : MonoBehaviour
{
    /// <summary>Единственное окно базы в сцене.</summary>
    public static BaseWindowUI Instance { get; private set; }

    [Header("Окно")]
    [SerializeField] private GameObject root;        // Всё окно (фон + панель)
    [SerializeField] private Image header;           // Полоска заголовка (красится в цвет стороны)
    [SerializeField] private TMP_Text titleText;     // "НАША БАЗА — уровень 1"
    [SerializeField] private TMP_Text subtitleText;  // Доход, ячейки, лимит героев
    [SerializeField] private Button closeButton;     // Крестик
    [SerializeField] private Button backdropButton;  // Клик по затемнению — закрыть

    [Header("Улучшение базы")]
    [SerializeField] private Button upgradeButton;   // Кнопка "Улучшить базу"
    [SerializeField] private TMP_Text upgradeText;   // Текст на ней

    [Header("Ячейки и меню")]
    [SerializeField] private BuildingSlotUI[] slotViews; // 5 карточек ячеек
    [SerializeField] private BuildMenuUI buildMenu;      // Меню выбора постройки
    [Tooltip("Окно выбора команды (для атаки на базу врага)")]
    [SerializeField] private SquadPickerUI picker;

    [Header("Цвета сторон")]
    [SerializeField] private Color playerColor = new Color(0.18f, 0.48f, 0.88f);
    [SerializeField] private Color enemyColor = new Color(0.85f, 0.22f, 0.23f);

    private MainBase current; // Какая база сейчас открыта
    private bool readOnly;    // Смотрим чужую базу (без кнопок)

    /// <summary>Запоминаем себя, подписываем кнопки, прячем окно.</summary>
    private void Awake()
    {
        Instance = this;
        closeButton.onClick.AddListener(Close);
        backdropButton.onClick.AddListener(Close);
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
        root.SetActive(false);
    }

    /// <summary>Перерисовываем окно, когда меняются деньги (цены краснеют/желтеют).</summary>
    private void Start()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.ResourcesChanged += OnResourcesChanged;
    }

    /// <summary>Отписываемся от событий.</summary>
    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.ResourcesChanged -= OnResourcesChanged;
        if (current != null) current.Changed -= Refresh;
        if (Instance == this) Instance = null;
    }

    /// <summary>Esc закрывает сначала меню строительства, потом окно.</summary>
    private void Update()
    {
        if (!root.activeSelf || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (buildMenu.IsOpen) buildMenu.Close();
        else Close();
    }

    /// <summary>
    /// Открыть окно базы. Чужая база — только с Радаром и только просмотр (+ кнопка атаки).
    /// Без Радара клик по базе врага сразу предлагает выбрать команду для атаки.
    /// </summary>
    public void Open(MainBase b)
    {
        if (b.Owner != Team.Player)
        {
            MainBase mine = MainBase.Get(Team.Player);
            if (mine == null || !mine.HasRadar)
            {
                ToastUI.Show("Без Радара база врага скрыта. Выберите команду для атаки");
                if (picker != null) picker.OpenFor(b, null);
                return;
            }
        }

        if (current != null) current.Changed -= Refresh;
        current = b;
        current.Changed += Refresh; // и при уроне базе (TakeDamage вызывает Changed)
        readOnly = b.Owner != Team.Player;

        buildMenu.Close();
        root.SetActive(true);
        Refresh();
    }

    /// <summary>Закрыть окно.</summary>
    public void Close()
    {
        if (current != null) current.Changed -= Refresh;
        current = null;
        buildMenu.Close();
        root.SetActive(false);
    }

    /// <summary>Открыть меню строительства для ячейки (вызывает карточка ячейки).</summary>
    public void OpenBuildMenu(int slotIndex)
    {
        if (current != null && !readOnly) buildMenu.Open(current, slotIndex);
    }

    /// <summary>Перерисовать всё окно по данным текущей базы.</summary>
    public void Refresh()
    {
        if (current == null) return;
        MainBase b = current;

        header.color = b.Owner == Team.Player ? playerColor : enemyColor;
        titleText.text = $"{b.BaseName}  <size=70%>уровень {b.Level} / {b.MaxLevel}</size>";

        int incomeGold = b.GoldIncome;
        for (int i = 0; i < MainBase.MaxSlots; i++)
        {
            BuildingInstance bi = b.GetBuilding(i);
            if (bi != null) incomeGold += bi.GoldIncome;
        }
        string stars = b.MaxHeroStars == 1 ? "1 звезды" : $"{b.MaxHeroStars} звёзд";
        Squad garrison = b.FindDefender();
        string garrisonText = garrison != null
            ? $"Гарнизон: команда {garrison.Number} (сила {BattleCalculator.SquadPower(garrison)})"
            : "<color=#FF7A7A>Гарнизона нет</color>";
        subtitleText.text =
            $"HP: <color=#6EE07A>{b.Hp}/{b.MaxHp}</color>  •  {garrisonText}  •  " +
            $"Доход: <color=#FFD84A>+{incomeGold}</color>  •  " +
            $"Ячейки: {b.OpenSlots}/{MainBase.MaxSlots}  •  Найм до {stars}";

        // Своя база — кнопка улучшения; база врага — кнопка атаки
        upgradeButton.gameObject.SetActive(true);
        if (readOnly)
        {
            upgradeButton.interactable = !b.IsUnderAttack && b.Hp > 0;
            upgradeText.text = b.IsUnderAttack
                ? "БАЗУ УЖЕ АТАКУЮТ"
                : $"АТАКОВАТЬ БАЗУ\n<size=80%>урон {b.DamagePerAttack} • охрана: сила {(garrison != null ? BattleCalculator.SquadPower(garrison) : 0)}</size>";
        }
        else
        {
            if (b.IsMaxLevel)
            {
                upgradeButton.interactable = false;
                upgradeText.text = "БАЗА МАКС. УРОВНЯ";
            }
            else
            {
                bool afford = ResourceManager.Instance.CanAfford(b.Owner, b.UpgradeCost);
                string col = afford ? "#FFD84A" : "#FF6B6B";
                upgradeButton.interactable = true;
                upgradeText.text = $"УЛУЧШИТЬ ДО УР. {b.Level + 1}\n<size=80%><color={col}>{b.UpgradeCost} золота</color> • +1 ячейка</size>";
            }
        }

        for (int i = 0; i < slotViews.Length; i++)
            slotViews[i].Setup(b, i, readOnly, this);

        buildMenu.Refresh();
    }

    /// <summary>Нажата кнопка: "Улучшить базу" (своя) или "Атаковать базу" (враг).</summary>
    private void OnUpgradeClicked()
    {
        if (current == null) return;
        if (readOnly)
        {
            if (picker != null) picker.OpenFor(current, Close);
            return;
        }
        if (current.TryUpgradeBase(out string error))
            ToastUI.Show($"База улучшена до уровня {current.Level}!");
        else
            ToastUI.Show(error);
    }

    /// <summary>Изменились деньги — обновляем цены на кнопках.</summary>
    private void OnResourcesChanged(Team team)
    {
        if (current != null && team == Team.Player) Refresh();
    }
}
