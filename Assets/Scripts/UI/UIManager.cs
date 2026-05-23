using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

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
        }
        else
        {
            incomeText.gameObject.SetActive(false);
        }
    }

    public void BuildGenerator()
    {
        if (currentBase != null)
        {
            currentBase.Build(currentBase.generatorData);

            incomeText.text =
                "Income: " +
                currentBase.income;
        }
    }

    public void CloseMapObjectPanel()
    {
        basePanel.SetActive(false);
    }
}