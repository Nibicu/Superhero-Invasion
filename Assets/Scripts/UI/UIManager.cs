using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public GameObject basePanel;

    [Header("Base UI")] 
    public TMP_Text titleText;
    public TMP_Text healthText;
    public TMP_Text incomeText;

    private void Awake()
    {
        Instance = this;
    }

    public void OpenBasePanel(Base selectedBase)// selectedBase - ссылка на конкретную базу, UI читает данные и отображает их.
    {
        basePanel.SetActive(true);

        // Обновляем UI данными базы
        titleText.text = selectedBase.baseName;
        healthText.text = "Health: " + selectedBase.health;
        incomeText.text = "Income: " + selectedBase.income;

        Debug.Log("Opening UI for: " + selectedBase.baseName);
    }

    public void CloseBasePanel()
    {
        basePanel.SetActive(false);
    }
}