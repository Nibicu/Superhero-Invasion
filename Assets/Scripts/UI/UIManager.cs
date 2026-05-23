using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public TMP_Text levelText;
    public BuildingSlotUI[] slotUIElements;

    [Header("Panels")]
    public GameObject basePanel;

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

    public void BuildGenerator()
    {
        if (currentBase != null)
        {
            currentBase.Build(currentBase.generatorData);

            incomeText.text =
                "Income: " +
                currentBase.income;

            RefreshSlotUI();
        }
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