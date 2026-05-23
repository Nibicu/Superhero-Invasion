using UnityEngine;

public class BuildingSlot : MonoBehaviour
{
    [Header("Slot State")]
    public bool isOccupied = false; // Проверяет занят слот или нет

    [Header("Current Building")]
    public Building currentBuilding; // Хранит - какое здание построено

    public bool CanBuild()
    {
        return !isOccupied;
    }

    public void AssignBuilding(Building newBuilding) // Назначает здание
    {
        currentBuilding = newBuilding;

        isOccupied = true;

        Debug.Log("Building assigned to slot");
    }
}