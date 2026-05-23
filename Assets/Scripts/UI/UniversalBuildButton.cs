using UnityEngine;
using UnityEngine.UI;

public class UniversalBuildButton : MonoBehaviour
{
    [Header("Building")]
    public BuildingData buildingData;

    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(Build);
    }

    private void Build()
    {
        UIManager.Instance.BuildSelectedBuilding(
            buildingData
        );
    }
}