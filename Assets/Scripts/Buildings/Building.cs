using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Data")]
    public BuildingData data;

    public string GetBuildingName()
    {
        return data.buildingName;
    }

    public int GetIncomeBonus()
    {
        return data.incomeBonus;
    }
}