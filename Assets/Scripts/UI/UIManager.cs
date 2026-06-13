using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    public Base playerBase;
    public static UIManager Instance;
    public TMP_Text levelText;
    public BuildingSlotUI[] slotUIElements;
    private BuildingSlot selectedSlot;
    private HeroData selectedHeroData;
    private HeroInstance selectedHero;
    public GameObject setDefenderButton;
    public GameObject squadHeroButtonTemplate;
    public TMP_Text commanderSlotText;
    public TMP_Text memberSlot1Text;
    public TMP_Text memberSlot2Text;
    public TMP_Text memberSlot3Text;
    public TMP_Text memberSlot4Text;
    private Squad selectedSquad;
    public GameObject sendSquadButton;
    public GameObject garrisonButton;
    private MapObject selectedMapObject;
    private Squad squadToAssign;
    public GameObject upgradeBaseButton;
    public TMP_Text garrisonText;

    private Squad[] squadSlots =
    new Squad[5];

    private Squad currentSquad =
    new Squad();

    [Header("Squad Slots")]

    public Button squadSlot1;
    public Button squadSlot2;
    public Button squadSlot3;
    public Button squadSlot4;
    public Button squadSlot5;

    public TMP_Text squadSlot1Text;
    public TMP_Text squadSlot2Text;
    public TMP_Text squadSlot3Text;
    public TMP_Text squadSlot4Text;
    public TMP_Text squadSlot5Text;

    public GameObject commanderPanel;
    public TMP_Text commanderDialogText;
    public Button returnButton;
    public Button guardButton;

    [Header("Select Squad Panel")]

    public GameObject selectSquadPanel;

    public Transform squadSelectContent;

    public GameObject squadSelectButtonTemplate;

    [Header("Squad Details")]

    public GameObject squadDetailsPanel;

    public TMP_Text squadCommanderText;

    public TMP_Text squadMembersText;

    public TMP_Text squadStatusText;

    [Header("Hero Recruit")]

    public Transform heroRosterContent;

    public GameObject heroRosterButtonTemplate;

    public Transform heroRecruitContent;

    public GameObject heroButtonTemplate;

    public HeroData[] availableHeroes;

    [Header("Recruit Hero Details")]

    public GameObject recruitHeroDetailsPanel;

    public Image recruitHeroPortrait;

    public TMP_Text squadTargetText;

    public TMP_Text recruitHeroName;

    public TMP_Text recruitHeroTier;

    public TMP_Text recruitHeroCost;

    public TMP_Text recruitHeroDescription;

    [Header("Squad Formation")]

    public GameObject squadFormationPanel;

    public Transform availableHeroesContent;

    [Header("Hero Details")]

    public GameObject heroDetailsPanel;

    public Image heroDetailsPortrait;

    public TMP_Text heroDetailsName;

    public TMP_Text heroDetailsLevel;

    public TMP_Text heroDetailsStatus;

    public TMP_Text heroDetailsTier;

    public TMP_Text heroDetailsDescription;

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
    public MovementType movementType =
    MovementType.Ground;

    private void Awake()
    {
        Instance = this;
    }

    public void OpenSquadDetails(Squad squad)
    {
        selectedSquad = squad;

        squadDetailsPanel.SetActive(true);

        squadCommanderText.text =
            "Commander: " +
            squad.commander.heroData.heroName;

        string members = "";

        foreach (HeroInstance hero
            in squad.members)
        {
            members +=
                hero.heroData.heroName +
                "\n";
        }

        squadMembersText.text =
            members;

        squadStatusText.text =
            "Status: " +
            squad.status.ToString();

        if (squad.targetObject != null)
        {
            squadTargetText.text =
                "Target: " +
                squad.targetObject.objectName;
        }
        else
        {
            squadTargetText.text =
                "Target: None";
        }
    }

    public void RefreshSquadDetails(Squad squad)
    {
        if (
            squadDetailsPanel.activeSelf &&
            selectedSquad == squad
        )
        {
            squadStatusText.text =
                "Status: " +
                squad.status.ToString();

            if (squad.targetObject != null)
            {
                squadTargetText.text =
                    "Target: " +
                    squad.targetObject.objectName;
            }
            else
            {
                squadTargetText.text =
                    "Target: None";
            }
        }
    }

    public void OpenMapObjectPanel(MapObject selectedObject)
    {
        selectedMapObject = selectedObject;

        heroRecruitPanel.SetActive(false);

        squadFormationPanel.SetActive(false);

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
            ShowBuildingSlots();

            incomeText.gameObject.SetActive(true);
            levelText.gameObject.SetActive(true);

            upgradeBaseButton.SetActive(true);

            incomeText.text =
                "Income: " +
                currentBase.income;

            levelText.text =
                "Level: " +
                currentBase.currentLevel;

            RefreshSlotUI();
        }
        else
        {
            incomeText.gameObject.SetActive(false);
            levelText.gameObject.SetActive(false);

            upgradeBaseButton.SetActive(false);

            HideBuildingSlots();
        }

        garrisonButton.SetActive(false);
        sendSquadButton.SetActive(false);

        if (selectedObject == playerBase)
        {
            garrisonButton.SetActive(true);
        }
        else
        {
            if (selectedObject.ownerFaction ==
                FactionType.Good)
            {
                garrisonButton.SetActive(true);
            }
            else
            {
                sendSquadButton.SetActive(true);
            }
        }

        if (selectedObject.garrisonSquad != null)
        {
            Debug.Log(
                "Garrison squad found on: " +
                selectedObject.objectName
            );
        }
        Debug.Log(
    "Selected object: " +
    selectedObject.objectName
);

        if (selectedObject.garrisonSquad != null)
        {
            Debug.Log(
                "Garrison squad: " +
                selectedObject.garrisonSquad.commander.heroData.heroName
            );
        }
        else
        {
            Debug.Log("No garrison");
        }
        if (selectedObject.garrisonSquad != null &&
    selectedObject.garrisonSquad.commander != null)
        {
            garrisonText.text =
                "Garrison: " +
                selectedObject.garrisonSquad.commander.heroData.heroName;
        }
        else
        {
            Debug.Log(
    "Object: " +
    selectedObject.objectName
);
            garrisonText.text =
                "Garrison: Empty";
        }
    }

    private void ShowBuildingSlots()
    {
        foreach (BuildingSlotUI slotUI
            in slotUIElements)
        {
            slotUI.gameObject.SetActive(true);
        }
    }

    private void HideBuildingSlots()
    {
        foreach (BuildingSlotUI slotUI
            in slotUIElements)
        {
            slotUI.gameObject.SetActive(false);
        }
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
    public void CloseSquadDetails()
    {
        squadDetailsPanel.SetActive(false);
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

    public void AddHeroToSquad(
    HeroInstance hero)
    {
        if (currentSquad.commander == null)
        {
            SelectCommander(hero);

            return;
        }

        if (hero == currentSquad.commander)
        {
            return;
        }

        if (currentSquad.members.Contains(hero))
        {
            return;
        }

        if (currentSquad.members.Count >= 4)
        {
            return;
        }

        currentSquad.members.Add(hero);

        RefreshSquadSlots();
    }

    private void RefreshSquadButtons()
    {
        Button[] buttons =
        {
        squadSlot1,
        squadSlot2,
        squadSlot3,
        squadSlot4,
        squadSlot5
    };

        TMP_Text[] texts =
        {
        squadSlot1Text,
        squadSlot2Text,
        squadSlot3Text,
        squadSlot4Text,
        squadSlot5Text
    };

        // Очищаем массив отрядов
        for (int i = 0; i < squadSlots.Length; i++)
        {
            squadSlots[i] = null;
        }

        // Скрываем все кнопки
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].gameObject.SetActive(false);
        }

        // Показываем существующие отряды
        for (
            int i = 0;
            i < SquadManager.Instance.activeSquads.Count &&
            i < buttons.Length;
            i++
        )
        {
            Squad squad =
                SquadManager.Instance.activeSquads[i];

            squadSlots[i] = squad;

            buttons[i].gameObject.SetActive(true);

            texts[i].text =
                squad.commander.heroData.heroName;
        }
    }
    private void RefreshSquadSlots()
    {
        memberSlot1Text.text = "Empty";
        memberSlot2Text.text = "Empty";
        memberSlot3Text.text = "Empty";
        memberSlot4Text.text = "Empty";

        if (currentSquad.members.Count > 0)
        {
            memberSlot1Text.text =
                currentSquad.members[0]
                .heroData.heroName;
        }

        if (currentSquad.members.Count > 1)
        {
            memberSlot2Text.text =
                currentSquad.members[1]
                .heroData.heroName;
        }

        if (currentSquad.members.Count > 2)
        {
            memberSlot3Text.text =
                currentSquad.members[2]
                .heroData.heroName;
        }

        if (currentSquad.members.Count > 3)
        {
            memberSlot4Text.text =
                currentSquad.members[3]
                .heroData.heroName;
        }
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

    public void OpenSquadFormationPanel()
    {
        basePanel.SetActive(false);

        heroRecruitPanel.SetActive(false);

        heroRosterPanel.SetActive(false);

        squadFormationPanel.SetActive(true);

        GenerateAvailableHeroes();
    }
    public void GenerateAvailableHeroes()
    {
        foreach (Transform child
            in availableHeroesContent)
        {
            Destroy(child.gameObject);
        }

        foreach (HeroInstance hero
     in HeroManager.Instance.ownedHeroes)
        {
            if (hero.status != HeroStatus.Idle)
            {
                continue;
            }

            GameObject buttonObject =
                Instantiate(
                    squadHeroButtonTemplate,
                    availableHeroesContent
                );

            buttonObject.SetActive(true);

            SquadHeroButton heroButton =
    buttonObject.GetComponent<SquadHeroButton>();

            heroButton.Setup(hero);
        }
    }

    public void GenerateHeroRoster()
    {
        foreach (Transform child in heroRosterContent)
        {
            Destroy(child.gameObject);
        }

        foreach (HeroInstance hero
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
            heroButton.Setup(hero);
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

        GenerateHeroRecruitList();

        Debug.Log(
            "Money left: " +
            PlayerData.Instance.currentMoney
        );
    }

    public void OpenSelectSquadPanel()
    {
        selectSquadPanel.SetActive(true);

        GenerateSquadSelection();
    }

    public void OpenSquadSlot1()
    {
        if (squadSlots[0] != null)
        {
            OpenSquadDetails(squadSlots[0]);
        }
    }

    public void OpenSquadSlot2()
    {
        if (squadSlots[1] != null)
        {
            OpenSquadDetails(squadSlots[1]);
        }
    }

    public void OpenSquadSlot3()
    {
        if (squadSlots[2] != null)
        {
            OpenSquadDetails(squadSlots[2]);
        }
    }

    public void OpenSquadSlot4()
    {
        if (squadSlots[3] != null)
        {
            OpenSquadDetails(squadSlots[3]);
        }
    }

    public void OpenSquadSlot5()
    {
        if (squadSlots[4] != null)
        {
            OpenSquadDetails(squadSlots[4]);
        }
    }

    public void OpenRecruitPanel()
    {
        basePanel.SetActive(false);

        heroRosterPanel.SetActive(false);

        squadFormationPanel.SetActive(false);

        heroRecruitPanel.SetActive(true);

        GenerateHeroRecruitList();
    }

    public void OpenHeroRosterPanel()
    {
        basePanel.SetActive(false);

        squadFormationPanel.SetActive(false);

        heroRecruitPanel.SetActive(false);

        heroRosterPanel.SetActive(true);

        GenerateHeroRoster();
    }

    public void OpenHeroRecruitDetails(
    HeroData heroData)
    {
        selectedHeroData = heroData;

        recruitHeroDetailsPanel.SetActive(true);

        recruitHeroPortrait.sprite =
            heroData.portrait;

        recruitHeroName.text =
            heroData.heroName;

        recruitHeroTier.text =
            "Tier: " + heroData.tier;

        recruitHeroCost.text =
            "Cost: " + heroData.cost;

        recruitHeroDescription.text =
            heroData.description;
    }

    public void OpenHeroDetails(
    HeroInstance hero)
    {
        selectedHero = hero;

        heroDetailsPanel.SetActive(true);

        heroDetailsPortrait.sprite =
            hero.heroData.portrait;

        heroDetailsName.text =
            hero.heroData.heroName;

        heroDetailsLevel.text =
            "Level: " + hero.level;

        heroDetailsStatus.text =
            "Status: " + hero.status;

        heroDetailsTier.text =
            "Tier: " + hero.heroData.tier;

        heroDetailsDescription.text =
            hero.heroData.description;
    }

    public void RemoveMember(
    int index)
    {
        if (index < 0)
        {
            return;
        }

        if (index >=
            currentSquad.members.Count)
        {
            return;
        }

        currentSquad.members.RemoveAt(
            index
        );

        RefreshSquadSlots();
    }

    public void RemoveMember1()
    {
        RemoveMember(0);
    }

    public void RemoveMember2()
    {
        RemoveMember(1);
    }

    public void RemoveMember3()
    {
        RemoveMember(2);
    }

    public void RemoveMember4()
    {
        RemoveMember(3);
    }

    public void SelectCommander(
    HeroInstance hero)
    {
        currentSquad.commander = hero;

        commanderSlotText.text =
            hero.heroData.heroName;
    }

    public void DisbandSelectedSquad()
    {
        if (selectedSquad == null)
        {
            return;
        }

        // Командир
        selectedSquad.commander.status =
            HeroStatus.Idle;

        // Участники
        foreach (HeroInstance hero
            in selectedSquad.members)
        {
            hero.status =
                HeroStatus.Idle;
        }

        // Удаляем отряд
        SquadManager.Instance.activeSquads
            .Remove(selectedSquad);

        // Обновляем кнопки
        RefreshSquadButtons();

        // Закрываем окно
        CloseSquadDetails();

        Debug.Log("Squad disbanded!");
    }
    public void CloseSquadFormationPanel()
    {
        squadFormationPanel.SetActive(false);
    }
    public void CloseHeroDetails()
    {
        heroDetailsPanel.SetActive(false);
    }
    public void CloseHeroRecruitDetails()
    {
        recruitHeroDetailsPanel.SetActive(false);
    }
    public void CloseHeroRosterPanel()
    {
        heroRosterPanel.SetActive(false);
    }

    public void CloseSelectSquadPanel()
    {
        selectSquadPanel.SetActive(false);

        selectedSquad = null;
    }

    public void SendCurrentSquad()
    {
        if (currentSquad.commander == null)
        {
            return;
        }

        // 1. меняем статус командира
        currentSquad.commander.status = HeroStatus.InSquad;

        // 2. меняем статус членов
        foreach (HeroInstance hero in currentSquad.members)
        {
            hero.status = HeroStatus.InSquad;
        }

        // 3. сохраняем отряд
        SquadManager.Instance.CreateSquad(currentSquad);
        RefreshSquadButtons();

        // 4. очищаем текущий отряд (ВАЖНО)
        currentSquad = new Squad();

        // 5. очищаем UI
        commanderSlotText.text = "Commander";

        memberSlot1Text.text = "Empty";
        memberSlot2Text.text = "Empty";
        memberSlot3Text.text = "Empty";
        memberSlot4Text.text = "Empty";

        // 6. закрываем панель
        CloseSquadFormationPanel();

        Debug.Log("Squad sent and reset!");
    }

    private void GenerateHeroRecruitList()
    {
        foreach (Transform child in heroRecruitContent)
        {
            Destroy(child.gameObject);
        }

        foreach (HeroData heroData in availableHeroes)
        {
            bool alreadyOwned = false;

            foreach (HeroInstance hero
                in HeroManager.Instance.ownedHeroes)
            {
                if (hero.heroData == heroData)
                {
                    alreadyOwned = true;

                    break;
                }
            }

            if (alreadyOwned)
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

    private void GenerateSquadSelection()
    {
        foreach (Transform child
            in squadSelectContent)
        {
            if (child.gameObject ==
                squadSelectButtonTemplate)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        foreach (Squad squad
            in SquadManager.Instance.activeSquads)
        {
            GameObject buttonObject =
                Instantiate(
                    squadSelectButtonTemplate,
                    squadSelectContent
                );

            buttonObject.SetActive(true);

            SquadSelectButton squadButton =
    buttonObject.GetComponent<SquadSelectButton>();

            squadButton.Setup(squad);
        }
    }

    public void AssignSquadToSelectedObject(
    Squad squad)
    {
       /* if (selectedMapObject.ownerFaction ==
    FactionType.Player)
        {
            Debug.Log("Object already belongs to player");

            return;
        } */
        squad.targetObject =
            selectedMapObject;

        squad.status =
            SquadStatus.Traveling;

        if (squad.worldObject == null)
        {
            SquadManager.Instance
                .SpawnSquadWorldObject(squad);
        }

        CloseSelectSquadPanel();
        CloseMapObjectPanel();

        Debug.Log(
            squad.commander.heroData.heroName +
            " sent to " +
            selectedMapObject.objectName
        );
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

    public void BuySelectedHero()
    {
        if (selectedHeroData == null)
        {
            return;
        }

        RecruitHero(selectedHeroData);

        recruitHeroDetailsPanel.SetActive(false);
    }

    public void SetHeroDefender()
    {
        if (selectedHero == null)
        {
            return;
        }

        selectedHero.status =
            HeroStatus.Defending;

        heroDetailsStatus.text =
            "Status: " +
            selectedHero.status;

        GenerateHeroRoster();

        Debug.Log(
            selectedHero.heroData.heroName +
            " is now defending."
        );
    }

    public void OpenCommanderPanel(Squad squad)
    {
        commanderPanel.SetActive(true);
        commanderDialogText.text = squad.commander.heroData.heroName + " захватил объект!";

        // Убираем старые слушатели, чтобы не дублировались
        returnButton.onClick.RemoveAllListeners();
        guardButton.onClick.RemoveAllListeners();

        returnButton.onClick.AddListener(() => ReturnToBase(squad));
        guardButton.onClick.AddListener(() => GuardObject(squad));
    }

    public void CloseCommanderPanel()
    {
        commanderPanel.SetActive(false);
    }

    private void ReturnToBase(Squad squad)
    {
        squad.targetObject = playerBase; // ссылка на базу игрока
        squad.status = SquadStatus.Traveling;
        CloseCommanderPanel();
        RefreshSquadDetails(squad);
    }

    private void GuardObject(Squad squad)
    {
        squad.status = SquadStatus.Occupying;

        if (squad.targetObject != null)
        {
            squad.targetObject.garrisonSquad = squad;

            Debug.Log(
                squad.commander.heroData.heroName +
                " is now guarding " +
                squad.targetObject.objectName
            );
        }

        CloseCommanderPanel();

        RefreshSquadDetails(squad);
    }

}