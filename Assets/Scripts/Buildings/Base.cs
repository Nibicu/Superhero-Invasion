using UnityEngine;
using System.Collections.Generic;

public class Base : MapObject
{
    [Header("Base Level")]
    public int currentLevel = 1;

    public int maxLevel = 4;

    public int upgradeCost = 1000;

    [Header("Available Buildings")]
    public BuildingData[] availableBuildings;

    [Header("Base Economy")]
    public int income = 100;

    [Header("Building Slots")]
    public List<BuildingSlot> buildingSlots =
        new List<BuildingSlot>();

    private void Start()
    {
        UnlockSlotsByLevel();
    }
    public override void Select()
    {
        base.Select();

        Debug.Log("Base specific UI");
    }

    public void BuildInSlot(
    BuildingData buildingData,
    BuildingSlot targetSlot
)
    {
        // Проверка слота
        if (!targetSlot.CanBuild())
        {
            Debug.Log("Cannot build here");

            return;
        }

        // Проверка денег
        if (EconomyManager.Instance.playerData.currentMoney
            < buildingData.cost)
        {
            Debug.Log("Not enough money");

            return;
        }

        // Списываем деньги
        EconomyManager.Instance.playerData.currentMoney
            -= buildingData.cost;

        // Создаем объект
        GameObject newBuildingObject =
            new GameObject(buildingData.buildingName);

        // Добавляем Building
        Building newBuilding =
            newBuildingObject.AddComponent<Building>();

        // Назначаем data
        newBuilding.data = buildingData;

        // Назначаем в слот
        targetSlot.AssignBuilding(newBuilding);

        // Добавляем income
        income += buildingData.incomeBonus;

        Debug.Log(
            buildingData.buildingName +
            " built!"
        );
    }

    // BUILD GENERATOR
    public void Build(BuildingData buildingData)
    {
        BuildingSlot freeSlot = GetFreeSlot();

        if (freeSlot == null)
        {
            Debug.Log("No free slots");

            return;
        }

        // Проверка денег
        if (EconomyManager.Instance.playerData.currentMoney
            < buildingData.cost)
        {
            Debug.Log("Not enough money");

            return;
        }

        // Списываем деньги
        EconomyManager.Instance.playerData.currentMoney
            -= buildingData.cost;

        // Создаем объект
        GameObject newBuildingObject =
            new GameObject(buildingData.buildingName);

        // Добавляем Building
        Building newBuilding =
            newBuildingObject.AddComponent<Building>();

        // Назначаем data
        newBuilding.data = buildingData;

        // Назначаем в слот
        freeSlot.AssignBuilding(newBuilding);

        // Добавляем income
        income += buildingData.incomeBonus;

        Debug.Log(
            buildingData.buildingName +
            " built!"
        );
    }

    public void UpgradeBase()
    {
        // Проверка max level
        if (currentLevel >= maxLevel)
        {
            Debug.Log("Base already max level");

            return;
        }

        // Проверка денег
        if (EconomyManager.Instance.playerData.currentMoney
            < upgradeCost)
        {
            Debug.Log("Not enough money");

            return;
        }

        // Списываем деньги
        EconomyManager.Instance.playerData.currentMoney
            -= upgradeCost;

        // Повышаем уровень
        currentLevel++;

        Debug.Log(
            "Base upgraded to level: " +
            currentLevel
        );

        UnlockSlotsByLevel();
    }

    private void UnlockSlotsByLevel()
    {
        int unlockedSlots = 2 + currentLevel;

        for (int i = 0; i < buildingSlots.Count; i++)
        {
            if (i < unlockedSlots)
            {
                buildingSlots[i].UnlockSlot();
            }
        }
    }

    private BuildingSlot GetFreeSlot()
    {
        foreach (BuildingSlot slot in buildingSlots)
        {
            if (slot.CanBuild())
            {
                return slot;
            }
        }

        return null;
    }
}