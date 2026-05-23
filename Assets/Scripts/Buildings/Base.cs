using UnityEngine;
using System.Collections.Generic;

public class Base : MapObject
{
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
    public void BuildGenerator()
    {
        BuildingSlot freeSlot = GetFreeSlot();

        if (freeSlot == null)
        {
            Debug.Log("No free slots");

            return;
        }

        // Создаем объект здания
        GameObject newBuildingObject =
            new GameObject("Generator");

        // Добавляем Building component
        Building newBuilding =
            newBuildingObject.AddComponent<Building>();

        // Настраиваем Generator
        newBuilding.buildingName = "Generator";
        newBuilding.incomeBonus = 50;

        // Назначаем в слот
        freeSlot.AssignBuilding(newBuilding);

        // Увеличиваем income базы
        income += newBuilding.incomeBonus;

        Debug.Log(
            "Generator built! Income increased to: " +
            income
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