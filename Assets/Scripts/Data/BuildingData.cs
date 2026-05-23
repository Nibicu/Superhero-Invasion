using UnityEngine;

[CreateAssetMenu(
    fileName = "New Building",
    menuName = "RTS/Building Data"
)]
public class BuildingData : ScriptableObject
{
    [Header("Info")]
    public string buildingName;

    [TextArea]
    public string description;

    [Header("Economy")]
    public int cost = 100;

    public int incomeBonus = 50;

    [Header("Visual")]
    public Sprite icon;
}