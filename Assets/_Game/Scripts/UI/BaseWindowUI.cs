using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно главной базы: уровень, кнопка улучшения базы и 5 ячеек построек.
/// Открывается кликом по базе на карте.
/// Свою базу можно менять; вражескую — только смотреть, и только если построен Радар.
/// Второй экземпляр (embedded = true) встроен во вкладку "База" окна тактической паузы: без крестика и затемнения.
/// </summary>
public class BaseWindowUI : MonoBehaviour
{
    /// <summary>Единственное окно базы в сцене.</summary>
    public static BaseWindowUI Instance { get; private set; }

    [Header("Окно")]
    [Tooltip("Встроено во вкладку паузы (не отдельное окно): не закрывается само, не поднимается наверх")]
    [SerializeField] private bool embedded;
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
        if (!embedded) Instance = this;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (!embedded) AttackableSite.AttackChanged += OnAttackChanged;
        if (!embedded) root.SetActive(false);
    }

    /// <summary>
    /// На базу напали — окно закрывается, пока не закончится нападение
    /// (покупки во время нападения недоступны).
    /// </summary>
    private void OnAttackChanged(AttackableSite site)
    {
        if (current == null || !ReferenceEquals(site, current) || !site.IsUnderAttack) return;
        ToastUI.Show($"{current.BaseName}: нападение! Окно базы закрыто до конца боя");
        if (picker != null && picker.IsOpen) picker.Close();
        Close();
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
        AttackableSite.AttackChanged -= OnAttackChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>Esc закрывает сначала меню строительства, потом окно.</summary>
    private void Update()
    {
        if (embedded || !root.activeSelf || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (buildMenu.IsOpen) buildMenu.Close();
        else Close();
    }

    /// <summary>
    /// Открыть окно базы. Чужая база — только с Радаром и только просмотр (+ кнопка атаки).
    /// Без Радара клик по базе врага сразу предлагает выбрать команду для атаки.
    /// Во время нападения на базу окно не открывается.
    /// </summary>
    public void Open(MainBase b)
    {
        if (b.IsUnderAttack && !embedded) { ToastUI.Show(b.AttackStatusText()); return; }
        if (b.Owner != Team.Player)
        {
            MainBase mine = MainBase.Get(Team.Player);
            if (mine == null || !mine.HasRadar)
            {
                ToastUI.Show("Без иконки «Радар» база врага скрыта. Выберите команду для атаки");
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
        if (!embedded) transform.SetAsLastSibling(); // поверх окна тактической паузы
        Refresh();
    }

    /// <summary>Можно ли сейчас строить (только в тактической паузе).</summary>
    private static bool CanBuildNow => PhaseManager.Instance == null || PhaseManager.Instance.CanBuild;

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
        titleText.text = b.BaseName;

        // Строительства больше нет: база — только цель (HP, защитники); Радар, резерв найма и 2★ дают иконки фракции
        int income = b.GoldIncome; // с артефактами "+к доходу"
        string stars = b.MaxHeroStars == 1 ? "1 звезды" : $"{b.MaxHeroStars} звёзд";
        Squad garrison = b.FindDefender();
        string garrisonText = garrison != null
            ? $"Защитники: {garrison.Size}"
            : "<color=#FF7A7A>Защитников нет</color>";
        subtitleText.text =
            $"HP: <color=#6EE07A>{b.Hp}/{b.MaxHp}</color>  •  {garrisonText}  •  " +
            $"Доход: <color=#C77DFF>+{income} очков фракции</color> раз в 10 с  •  Найм до {stars}";

        // Своя база — без кнопки; база врага — кнопка атаки
        upgradeButton.gameObject.SetActive(readOnly);
        if (readOnly)
        {
            upgradeButton.interactable = b.Hp > 0;
            upgradeText.text = $"АТАКОВАТЬ БАЗУ\n<size=80%>урон {b.DamagePerAttack} • рекомендуется {Recommendation.Text(b.RecommendedHeroes, Team.Player)}</size>";
        }

        // Ячейки построек спрятаны
        for (int i = 0; i < slotViews.Length; i++)
            if (slotViews[i] != null) slotViews[i].gameObject.SetActive(false);
        if (buildMenu != null) buildMenu.Close();
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
