using UnityEngine;

public class BuildingSlot : MonoBehaviour
{
    [Header("Slot State")]
    public bool isUnlocked = false;

    public bool isOccupied = false;

    [Header("Current Building")]
    public Building currentBuilding;

    public bool CanBuild()
    {
        return isUnlocked && !isOccupied;
    }

    public void UnlockSlot()
    {
        isUnlocked = true;

        Debug.Log(gameObject.name + " unlocked");
    }

    public void AssignBuilding(Building newBuilding)
    {
        currentBuilding = newBuilding;

        isOccupied = true;

        Debug.Log("Building assigned to slot");
    }
}