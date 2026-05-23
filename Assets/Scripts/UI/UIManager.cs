using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public TMP_Text levelText;
    public BuildingSlotUI[] slotUIElements;
    private BuildingSlot selectedSlot;

    [Header("Panels")]
    public GameObject basePanel;

    [Header("Build Menu")]
    public GameObject buildMenuPanel;

    [Header("UI Text")]
    public TMP_Text titleText;
    public TMP_Text healthText;
    public TMP_Text factionText;
    public TMP_Text incomeText;

    private Base currentBase;

    private void Awake()
    {
        Instance = this;
    }

    public void OpenMapObjectPanel(MapObject selectedObject)
    {
        basePanel.SetActive(true);

        titleText.text = selectedObject.objectName;

        healthText.text =
            "Health: " +
            selectedObject.GetCurrentHealth();

        factionText.text =
            "Faction: " +
            selectedObject.ownerFaction.ToString();

        currentBase = selectedObject as Base;

        if (currentBase != null)
        {
            incomeText.gameObject.SetActive(true);

            incomeText.text =
                "Income: " +
                currentBase.income;

            levelText.text =
                "Level: " +
                currentBase.currentLevel;
        }
        else
        {
            incomeText.gameObject.SetActive(false);
        }

        RefreshSlotUI();
    }
    public void OpenBuildMenu(BuildingSlot slot)
    {
        selectedSlot = slot;

        buildMenuPanel.SetActive(true);
    }

    public void CloseBuildMenu()
    {
        buildMenuPanel.SetActive(false);
    }

    public void BuildGenerator()
    {
        if (currentBase == null)
        {
            return;
        }

        if (selectedSlot == null)
        {
            return;
        }

        currentBase.BuildInSlot(
            currentBase.generatorData,
            selectedSlot
        );

        incomeText.text =
            "Income: " +
            currentBase.income;

        RefreshSlotUI();

        CloseBuildMenu();
    }
    public void UpgradeBase()
    {
        if (currentBase != null)
        {
            currentBase.UpgradeBase();

            // Обновляем UI
            levelText.text =
                "Level: " +
                currentBase.currentLevel;

            incomeText.text =
                "Income: " +
                currentBase.income;

            RefreshSlotUI();
        }
    }

    private void RefreshSlotUI()
    {
        foreach (BuildingSlotUI slotUI in slotUIElements)
        {
            slotUI.RefreshUI();
        }
    }

    public void CloseMapObjectPanel()
    {
        basePanel.SetActive(false);
    }
}