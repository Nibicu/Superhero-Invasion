using UnityEngine;
using System.Collections.Generic;

public class Base : MapObject
{
    [Header("Available Buildings")]
    public BuildingData generatorData;

    [Header("Base Economy")]
    public int income = 100;

    [Header("Building Slots")]
    public List<BuildingSlot> buildingSlots =
        new List<BuildingSlot>();

    public override void Select()
    {
        base.Select();

        Debug.Log("Base specific UI");
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