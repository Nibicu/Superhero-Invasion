using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно создания (и изменения) команды.
/// Сверху: большая ячейка капитана и 4 маленькие ячейки героев.
/// Снизу: список свободных героев. Клик по герою из списка —
/// первый станет капитаном, следующие займут свободные ячейки.
/// Клик по занятой ячейке — убрать героя.
/// </summary>
public class SquadWindowUI : WindowUI
{
    [Header("Заголовок и тексты")]
    [SerializeField] private TMP_Text titleText;    // "НОВАЯ КОМАНДА" / "КОМАНДА 2"
    [SerializeField] private TMP_Text summaryText;  // Героев, здоровье, сила
    [SerializeField] private TMP_Text captainBonusText; // Бонус капитана
    [SerializeField] private TMP_Text hintText;     // Подсказка

    [Header("Ячейки")]
    [SerializeField] private SquadMemberSlotUI captainSlot;   // Большая ячейка капитана
    [SerializeField] private SquadMemberSlotUI[] memberSlots; // 4 маленькие ячейки

    [Header("Список героев")]
    [SerializeField] private Transform listParent;         // Куда складывать карточки героев
    [SerializeField] private HeroListItemUI itemTemplate;  // Шаблон карточки (выключен)
    [SerializeField] private TMP_Text emptyListText;       // "Нет свободных героев"

    [Header("Кнопки")]
    [SerializeField] private Button confirmButton;  // "СОЗДАТЬ КОМАНДУ" / "СОХРАНИТЬ"
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button disbandButton;  // "РАСПУСТИТЬ" (только для существующей команды)

    private HeroInstance captain;                           // Выбранный капитан
    private readonly HeroInstance[] members = new HeroInstance[4]; // Выбранные герои (null = пусто)
    private Squad editing;                                  // Какую команду меняем (null — создаём новую)
    private readonly List<HeroListItemUI> items = new List<HeroListItemUI>(); // Созданные карточки

    /// <summary>Подписываем кнопки, прячем шаблон.</summary>
    protected override void Awake()
    {
        base.Awake();
        itemTemplate.gameObject.SetActive(false);
        confirmButton.onClick.AddListener(OnConfirm);
        disbandButton.onClick.AddListener(OnDisband);
    }

    /// <summary>Подписываемся на изменения героев (уровень, найм).</summary>
    private void Start() => HeroManager.Instance.HeroesChanged += OnHeroesChanged;

    /// <summary>Отписываемся.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnHeroesChanged;
    }

    private void OnHeroesChanged(Team team)
    {
        if (team == Team.Player && IsOpen) Refresh();
    }

    // ---------- Открытие ----------

    /// <summary>Открыть окно для создания новой команды.</summary>
    public void OpenNew()
    {
        if (!SquadManager.Instance.CanCreateSquad(Team.Player))
        {
            ToastUI.Show($"Максимум {SquadManager.Instance.MaxSquads} команд");
            return;
        }
        editing = null;
        captain = null;
        for (int i = 0; i < members.Length; i++) members[i] = null;
        Open();
    }

    /// <summary>Открыть окно существующей команды (посмотреть / изменить / распустить).</summary>
    public void OpenSquad(Squad squad)
    {
        editing = squad;
        captain = squad.Captain;
        for (int i = 0; i < members.Length; i++)
            members[i] = i < squad.Members.Count ? squad.Members[i] : null;
        Open();
    }

    // ---------- Отрисовка ----------

    /// <summary>Перерисовать окно.</summary>
    public override void Refresh()
    {
        bool canEdit = editing == null || editing.Status == SquadStatus.AtBase;

        titleText.text = editing == null ? "НОВАЯ КОМАНДА" : $"КОМАНДА {editing.Number}";

        // Ячейки
        captainSlot.Show(captain, canEdit, () => { captain = null; Refresh(); });
        for (int i = 0; i < memberSlots.Length; i++)
        {
            int index = i; // копия для лямбды
            memberSlots[i].Show(members[i], canEdit, () => { members[index] = null; Refresh(); });
        }

        // Итоги
        int count = captain != null ? 1 : 0;
        int hp = captain != null ? captain.Stats.hp : 0;
        foreach (HeroInstance m in members)
            if (m != null) { count++; hp += m.Stats.hp; }
        summaryText.text = $"Героев: {count} / {members.Length + 1}   •   Здоровье команды: {hp}";
        captainBonusText.text = captain != null && !string.IsNullOrEmpty(captain.Data.captainBonus)
            ? $"Бонус капитана: <color=#FFD84A>{captain.Data.captainBonus}</color>"
            : "Бонус капитана: —";

        // Подсказка
        if (!canEdit) hintText.text = "Команда на задании — менять состав нельзя";
        else if (captain == null) hintText.text = "Нажмите на героя внизу — он станет капитаном";
        else hintText.text = "Добавьте ещё героев или создайте команду. Клик по ячейке — убрать героя";

        // Кнопки
        confirmButton.interactable = canEdit && captain != null;
        confirmText.text = editing == null ? "СОЗДАТЬ\nКОМАНДУ" : "СОХРАНИТЬ";
        disbandButton.gameObject.SetActive(editing != null);
        disbandButton.interactable = canEdit;

        RebuildList(canEdit);
    }

    /// <summary>Пересоздать список доступных героев (свободные + свои из редактируемой команды, ещё не в ячейках).</summary>
    private void RebuildList(bool canEdit)
    {
        foreach (HeroListItemUI it in items) Destroy(it.gameObject);
        items.Clear();

        var available = SquadManager.Instance.GetFreeHeroes(Team.Player);
        if (editing != null)
            foreach (HeroInstance h in editing.AllHeroes)
                if (!available.Contains(h)) available.Add(h);
        available.RemoveAll(IsPlaced);

        emptyListText.gameObject.SetActive(available.Count == 0);
        if (!canEdit) return;

        foreach (HeroInstance h in available)
        {
            HeroListItemUI item = Instantiate(itemTemplate, listParent);
            item.gameObject.SetActive(true);
            HeroInstance captured = h; // копия для лямбды
            item.Setup(h, () => AddHero(captured));
            items.Add(item);
        }
    }

    /// <summary>Стоит ли герой уже в какой-то ячейке окна.</summary>
    private bool IsPlaced(HeroInstance h)
    {
        if (h == captain) return true;
        foreach (HeroInstance m in members)
            if (m == h) return true;
        return false;
    }

    // ---------- Действия ----------

    /// <summary>Клик по герою в списке: капитан, если его нет, иначе первая свободная ячейка.</summary>
    private void AddHero(HeroInstance hero)
    {
        if (captain == null)
        {
            captain = hero;
        }
        else
        {
            int free = System.Array.IndexOf(members, null);
            if (free < 0)
            {
                ToastUI.Show("Команда заполнена — уберите кого-нибудь");
                return;
            }
            members[free] = hero;
        }
        Refresh();
    }

    /// <summary>Нажата кнопка "Создать" / "Сохранить".</summary>
    private void OnConfirm()
    {
        SquadManager sm = SquadManager.Instance;
        string error;
        if (editing == null)
        {
            if (sm.TryCreateSquad(Team.Player, captain, members, out Squad squad, out error))
            {
                ToastUI.Show($"Команда {squad.Number} создана! Капитан — {captain.Data.displayName}");
                Close();
                return;
            }
        }
        else if (sm.TryEditSquad(editing, captain, members, out error))
        {
            ToastUI.Show($"Состав команды {editing.Number} сохранён");
            Close();
            return;
        }
        ToastUI.Show(error);
    }

    /// <summary>Нажата кнопка "Распустить".</summary>
    private void OnDisband()
    {
        if (editing == null) return;
        int number = editing.Number;
        if (SquadManager.Instance.TryDisband(editing, out string error))
        {
            ToastUI.Show($"Команда {number} распущена");
            Close();
        }
        else
        {
            ToastUI.Show(error);
        }
    }
}
