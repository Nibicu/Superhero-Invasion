using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка одной ячейки в окне базы. Бывает в трёх состояниях:
/// 1) Закрыта — "Откроется на ур. X";
/// 2) Пустая — кнопка "Построить";
/// 3) С постройкой — иконка, название, уровень, эффект и кнопка "Улучшить".
/// </summary>
public class BuildingSlotUI : MonoBehaviour
{
    [SerializeField] private Image frame;            // Рамка карточки (подсвечивается цветом постройки)
    [SerializeField] private BuildingIconUI icon;    // Иконка постройки
    [SerializeField] private TMP_Text nameText;      // Название постройки
    [SerializeField] private TMP_Text levelText;     // "Ур. 1 / 2"
    [SerializeField] private TMP_Text infoText;      // Эффект (доход, бонусы)
    [SerializeField] private Button actionButton;    // Кнопка "Построить" / "Улучшить"
    [SerializeField] private TMP_Text actionText;    // Текст на кнопке
    [SerializeField] private GameObject lockOverlay; // Затемнение закрытой ячейки
    [SerializeField] private TMP_Text lockText;      // "Откроется на ур. 2"

    private static readonly Color FrameEmpty = new Color(1f, 1f, 1f, 0.12f); // Цвет рамки пустой ячейки

    private MainBase mainBase;   // База, которой принадлежит ячейка
    private int index;           // Номер ячейки (0..4)
    private BaseWindowUI window; // Окно, в котором карточка (чтобы открыть меню строительства)

    /// <summary>Подписываем кнопку один раз.</summary>
    private void Awake()
    {
        actionButton.onClick.AddListener(OnActionClicked);
    }

    /// <summary>
    /// Нарисовать карточку по данным базы.
    /// readOnly = true — смотрим чужую базу (кнопки скрыты).
    /// </summary>
    public void Setup(MainBase b, int slotIndex, bool readOnly, BaseWindowUI owner)
    {
        mainBase = b;
        index = slotIndex;
        window = owner;

        bool open = b.IsSlotOpen(index);
        BuildingInstance building = b.GetBuilding(index);

        lockOverlay.SetActive(!open);
        if (!open)
        {
            lockText.text = $"ЗАКРЫТО\n<size=75%>Откроется на\nуровне базы {b.SlotUnlockLevel(index)}</size>";
            icon.Hide();
            nameText.text = "";
            levelText.text = "";
            infoText.text = "";
            frame.color = FrameEmpty;
            actionButton.gameObject.SetActive(false);
            return;
        }

        if (building == null)
        {
            // Пустая ячейка
            icon.Hide();
            nameText.text = "Пустая ячейка";
            levelText.text = "";
            infoText.text = readOnly ? "" : "Здесь можно\nпостроить здание";
            frame.color = FrameEmpty;
            actionButton.gameObject.SetActive(!readOnly);
            actionButton.interactable = true;
            actionText.text = "ПОСТРОИТЬ";
            return;
        }

        // Ячейка с постройкой
        BuildingData d = building.Data;
        icon.Show(d);
        nameText.text = d.displayName;
        levelText.text = $"Ур. {building.Level} / {d.MaxLevel}";
        infoText.text = d.GetEffectText(building.Level);
        frame.color = d.color;

        actionButton.gameObject.SetActive(!readOnly);
        if (readOnly) return;

        if (b.CanUpgradeBuilding(index, out string reason))
        {
            BuildingLevel next = d.GetLevel(building.Level + 1);
            bool afford = ResourceManager.Instance.CanAfford(b.Owner, next.goldCost, next.plutoniumCost);
            actionButton.interactable = true;
            actionText.text = $"УЛУЧШИТЬ\n<size=80%>{CostText(next, afford)}</size>";
        }
        else
        {
            actionButton.interactable = false;
            actionText.text = reason;
        }
    }

    /// <summary>Нажата кнопка карточки: построить или улучшить.</summary>
    private void OnActionClicked()
    {
        if (mainBase == null) return;
        if (mainBase.GetBuilding(index) == null)
        {
            window.OpenBuildMenu(index);
        }
        else if (!mainBase.TryUpgradeBuilding(index, out string error))
        {
            ToastUI.Show(error);
        }
    }

    /// <summary>Текст цены: жёлтый, если хватает денег, красный — если нет.</summary>
    public static string CostText(BuildingLevel level, bool afford)
    {
        string col = afford ? "#FFD84A" : "#FF6B6B";
        string text = $"<color={col}>{level.goldCost} золота</color>";
        if (level.plutoniumCost > 0) text += $" <color=#C58BFF>{level.plutoniumCost} плут.</color>";
        return text;
    }
}
