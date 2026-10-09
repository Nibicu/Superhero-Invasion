using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Окно артефактов. Работает в трёх режимах:
///  1) ИНВЕНТАРЬ (кнопка слева внизу): все артефакты игрока.
///     Активируемый — "ИСПОЛЬЗОВАТЬ" (для "+1 уровень" сначала выбрать героя);
///     вещь — "НАДЕТЬ" → выбрать героя → вещь в первую свободную ячейку.
///  2) ЯЧЕЙКА ГЕРОЯ (клик по ячейке в карточке героя): надетая вещь ("СНЯТЬ") и вещи из инвентаря ("НАДЕТЬ").
///  3) ВЫБОР ГЕРОЯ: кому надеть вещь или дать бесплатный уровень.
/// </summary>
public class ArtifactWindowUI : WindowUI
{
    /// <summary>Единственное окно в сцене.</summary>
    public static ArtifactWindowUI Instance { get; private set; }

    [Header("Артефакты")]
    [SerializeField] private TMP_Text titleText;        // Заголовок
    [SerializeField] private Transform listParent;      // Куда складывать строки
    [SerializeField] private ArtifactRowUI rowTemplate; // Шаблон строки (выключен)
    [SerializeField] private TMP_Text emptyText;        // "Инвентарь пуст..."

    /// <summary>Режим окна.</summary>
    private enum Mode { Inventory, Slot, PickHero }

    private readonly List<GameObject> rows = new List<GameObject>(); // Созданные строки
    private Mode mode;
    private HeroInstance slotHero;   // Чья ячейка (режим Slot)
    private int slotIndex;           // Какая ячейка (режим Slot)
    private ArtifactData picking;    // Для какого артефакта выбираем героя (режим PickHero)

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (ArtifactManager.Instance != null) ArtifactManager.Instance.Changed += OnChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (ArtifactManager.Instance != null) ArtifactManager.Instance.Changed -= OnChanged;
        if (Instance == this) Instance = null;
    }

    private void OnChanged(Team team)
    {
        if (team == Team.Player && IsOpen) Refresh();
    }

    // ---------- Открытие ----------

    /// <summary>Открыть инвентарь.</summary>
    public void OpenInventory()
    {
        mode = Mode.Inventory;
        Open();
    }

    /// <summary>Открыть выбор вещи для ячейки героя.</summary>
    public void OpenForSlot(HeroInstance hero, int slot)
    {
        mode = Mode.Slot;
        slotHero = hero;
        slotIndex = slot;
        Open();
    }

    /// <summary>Выбрать героя для артефакта (надеть вещь или дать уровень).</summary>
    private void PickHero(ArtifactData a)
    {
        mode = Mode.PickHero;
        picking = a;
        Refresh();
    }

    // ---------- Отрисовка ----------

    public override void Refresh()
    {
        foreach (GameObject g in rows) Destroy(g);
        rows.Clear();
        ArtifactManager am = ArtifactManager.Instance;
        if (am == null) return;
        IReadOnlyList<ArtifactData> stash = am.GetStash(Team.Player);

        switch (mode)
        {
            case Mode.Inventory:
                titleText.text = "ИНВЕНТАРЬ";
                foreach (ArtifactData a in stash)
                {
                    ArtifactData captured = a;
                    if (a.kind == ArtifactKind.Activatable)
                        AddRow(a, "ИСПОЛЬЗОВАТЬ", true, () => Use(captured));
                    else
                        AddRow(a, "НАДЕТЬ", true, () => PickHero(captured));
                }
                ShowEmpty(stash.Count == 0, "Инвентарь пуст.\nАртефакты — награда за задания и общие миссии.");
                break;

            case Mode.Slot:
                titleText.text = $"ЯЧЕЙКА {slotIndex + 1}: {slotHero.Data.displayName.ToUpper()}";
                ArtifactData worn = slotHero.Slots[slotIndex];
                if (worn != null) AddRow(worn, "СНЯТЬ", true, Unequip);
                int items = 0;
                foreach (ArtifactData a in stash)
                {
                    if (a.kind != ArtifactKind.Item) continue;
                    items++;
                    ArtifactData captured = a;
                    AddRow(a, worn != null ? "ЗАМЕНИТЬ" : "НАДЕТЬ", true, () => Equip(captured));
                }
                ShowEmpty(worn == null && items == 0, "В инвентаре нет вещей, которые можно надеть.");
                break;

            case Mode.PickHero:
                titleText.text = picking.kind == ArtifactKind.Item ? $"КОМУ НАДЕТЬ: {picking.displayName.ToUpper()}" : "КОМУ ДАТЬ УРОВЕНЬ?";
                var heroes = HeroManager.Instance.GetHeroes(Team.Player);
                foreach (HeroInstance h in heroes)
                {
                    HeroInstance captured = h;
                    int free = 0;
                    foreach (ArtifactData s in h.Slots) if (s == null) free++;
                    string info = picking.kind == ArtifactKind.Item
                        ? $"Ур. {h.Level}  •  свободных ячеек: {free}/{HeroInstance.SlotCount}"
                        : $"Ур. {h.Level} / {HeroManager.Instance.MaxHeroLevel}";
                    bool ok = picking.kind == ArtifactKind.Item ? free > 0 : h.Level < HeroManager.Instance.MaxHeroLevel;
                    AddHeroRow(h, info, "ВЫБРАТЬ", ok, () => ChooseHero(captured));
                }
                ShowEmpty(heroes.Count == 0, "У вас пока нет героев.");
                break;
        }
    }

    private void AddRow(ArtifactData a, string action, bool enabled, System.Action onAction)
    {
        ArtifactRowUI row = Instantiate(rowTemplate, listParent);
        row.gameObject.SetActive(true);
        row.Setup(a, action, enabled, onAction);
        rows.Add(row.gameObject);
    }

    private void AddHeroRow(HeroInstance h, string info, string action, bool enabled, System.Action onAction)
    {
        ArtifactRowUI row = Instantiate(rowTemplate, listParent);
        row.gameObject.SetActive(true);
        row.SetupHero(h, info, action, enabled, onAction);
        rows.Add(row.gameObject);
    }

    private void ShowEmpty(bool show, string text)
    {
        emptyText.gameObject.SetActive(show);
        emptyText.text = text;
    }

    // ---------- Действия ----------

    /// <summary>Использовать активируемый артефакт ("+1 уровень" — сначала выбрать героя).</summary>
    private void Use(ArtifactData a)
    {
        if (a.effect == ArtifactEffect.FreeHeroLevel) { PickHero(a); return; }
        if (ArtifactManager.Instance.TryActivate(Team.Player, a, null, out string error))
            ToastUI.Show($"{a.displayName}: {a.EffectText()}");
        else ToastUI.Show(error);
    }

    /// <summary>Герой выбран: надеть вещь или дать уровень, потом вернуться в инвентарь.</summary>
    private void ChooseHero(HeroInstance h)
    {
        ArtifactManager am = ArtifactManager.Instance;
        string error;
        bool ok = picking.kind == ArtifactKind.Item
            ? am.TryEquipFree(h, picking, out error)
            : am.TryActivate(Team.Player, picking, h, out error);
        if (ok) ToastUI.Show(picking.kind == ArtifactKind.Item
            ? $"{h.Data.displayName}: надет «{picking.displayName}»"
            : $"{h.Data.displayName} достиг уровня {h.Level}!");
        else ToastUI.Show(error);
        mode = Mode.Inventory;
        Refresh();
    }

    /// <summary>Надеть вещь в выбранную ячейку героя.</summary>
    private void Equip(ArtifactData a)
    {
        if (ArtifactManager.Instance.TryEquip(slotHero, slotIndex, a, out string error))
        {
            ToastUI.Show($"{slotHero.Data.displayName}: надет «{a.displayName}»");
            Close();
        }
        else ToastUI.Show(error);
    }

    /// <summary>Снять вещь из выбранной ячейки.</summary>
    private void Unequip()
    {
        if (ArtifactManager.Instance.TryUnequip(slotHero, slotIndex, out string error)) Refresh();
        else ToastUI.Show(error);
    }
}
