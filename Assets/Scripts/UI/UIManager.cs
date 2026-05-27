using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public TMP_Text levelText;
    public BuildingSlotUI[] slotUIElements;
    private BuildingSlot selectedSlot;

    [Header("Hero Recruit")]

    public Transform heroRosterContent;

    public GameObject heroRosterButtonTemplate;

    public Transform heroRecruitContent;

    public GameObject heroButtonTemplate;

    public HeroData[] availableHeroes;

    [Header("Hero Roster UI")]

    public GameObject heroRosterPanel;

    [Header("Hero Recruit UI")]
    public GameObject heroRecruitPanel;

    [Header("Dynamic Build Menu")]
    public Transform buttonsContainer;

    [Header("Error UI")]
    public GameObject errorText;

    private BuildingData selectedBuildingData;

    [Header("Building Preview")]
    public Image previewIcon;
    private UniversalBuildButton selectedButton;

    public TMPro.TMP_Text previewName;

    public TMPro.TMP_Text previewCost;

    public TMPro.TMP_Text previewDescription;

    public GameObject buildConfirmButton;

    public GameObject buildButtonTemplate;

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
        heroRecruitPanel.SetActive(false);

        heroRosterPanel.SetActive(false);

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

        GenerateBuildMenu();

        buildMenuPanel.SetActive(true);
    }

    public void CloseBuildMenu()
    {
        buildMenuPanel.SetActive(false);

        buildConfirmButton.SetActive(false);

        errorText.SetActive(false);

        selectedBuildingData = null;

        if (selectedButton != null)
        {
            selectedButton.SetSelected(false);

            selectedButton = null;
        }
    }

    public void SelectBuilding(
    BuildingData buildingData,
    UniversalBuildButton button
)
    {
        selectedBuildingData = buildingData;

        // RESET OLD
        if (selectedButton != null)
        {
            selectedButton.SetSelected(false);
        }

        // NEW
        selectedButton = button;

        selectedButton.SetSelected(true);

        previewIcon.sprite = buildingData.icon;

        previewName.text =
            buildingData.buildingName;

        previewCost.text =
            "Cost: " +
            buildingData.cost;

        previewDescription.text =
            buildingData.description;

        buildConfirmButton.SetActive(true);
    }

    public void BuildSelectedBuilding()
    {
        if (currentBase == null)
        {
            return;
        }

        if (selectedSlot == null)
        {
            return;
        }

        if (selectedBuildingData == null)
        {
            return;
        }

        // NOT ENOUGH MONEY
        if (PlayerData.Instance.currentMoney <
            selectedBuildingData.cost)
        {
            errorText.SetActive(true);

            return;
        }

        // HIDE ERROR
        errorText.SetActive(false);

        currentBase.BuildInSlot(
            selectedBuildingData,
            selectedSlot
        );

        incomeText.text =
            "Income: " +
            currentBase.income;

        RefreshSlotUI();

        CloseBuildMenu();

        buildConfirmButton.SetActive(false);
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

    private void GenerateBuildMenu()
    {
        // Удаляем старые кнопки
        foreach (Transform child in buttonsContainer)
        {
            Destroy(child.gameObject);
        }

        // Создаем новые
        foreach (BuildingData buildingData
            in currentBase.availableBuildings)
        {
            GameObject buttonObject =
                Instantiate(
                    buildButtonTemplate,
                    buttonsContainer
                );

            buttonObject.SetActive(true);

            UniversalBuildButton buildButton =
                buttonObject.GetComponent<UniversalBuildButton>();

            buildButton.buildingData = buildingData;

            TMPro.TMP_Text buttonText =
                buttonObject.GetComponentInChildren<TMPro.TMP_Text>();

            buttonText.text =
                buildingData.buildingName;
        }
    }

    private void GenerateHeroRoster()
    {
        foreach (Transform child in heroRosterContent)
        {
            Destroy(child.gameObject);
        }

        foreach (HeroData heroData
            in HeroManager.Instance.ownedHeroes)
        {
            GameObject newButton =
                Instantiate(
                    heroRosterButtonTemplate,
                    heroRosterContent
                );

            newButton.SetActive(true);

            HeroRosterButton heroButton =
                newButton.GetComponent<HeroRosterButton>();

            heroButton.Setup(heroData);
        }
    }

    public void RecruitHero(HeroData heroData)
    {
        // CHECK MONEY
        if (PlayerData.Instance.currentMoney <
            heroData.cost)
        {
            Debug.Log("Not enough money");

            return;
        }

        // PAY
        PlayerData.Instance.currentMoney -=
            heroData.cost;

        // ADD HERO
        HeroManager.Instance.RecruitHero(
            heroData
        );

        Debug.Log(
            "Money left: " +
            PlayerData.Instance.currentMoney
        );
    }

    public void OpenRecruitPanel()
    {
        basePanel.SetActive(false);

        heroRosterPanel.SetActive(false);

        heroRecruitPanel.SetActive(true);

        GenerateHeroRecruitList();
    }

    public void OpenHeroRosterPanel()
    {
        basePanel.SetActive(false);

        heroRecruitPanel.SetActive(false);

        heroRosterPanel.SetActive(true);

        GenerateHeroRoster();
    }
    public void CloseHeroRosterPanel()
    {
        heroRosterPanel.SetActive(false);
    }

    private void GenerateHeroRecruitList()
    {
        foreach (Transform child in heroRecruitContent)
        {
            Destroy(child.gameObject);
        }

        foreach (HeroData heroData in availableHeroes)
        {
            if (HeroManager.Instance.ownedHeroes
                .Contains(heroData))
            {
                continue;
            }

            GameObject newButton =
                Instantiate(
                    heroButtonTemplate,
                    heroRecruitContent
                );

            newButton.SetActive(true);

            HeroRecruitButton heroButton =
                newButton.GetComponent<HeroRecruitButton>();

            heroButton.Setup(heroData);
        }
    }

    private void RefreshSlotUI()
    {
        foreach (BuildingSlotUI slotUI in slotUIElements)
        {
            slotUI.RefreshUI();
        }
    }

    public void CloseRecruitPanel()
    {
        heroRecruitPanel.SetActive(false);
    }

    public void CloseMapObjectPanel()
    {
        basePanel.SetActive(false);
    }
}