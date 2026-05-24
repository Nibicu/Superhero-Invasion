using UnityEngine;
using UnityEngine.UI;

public class UniversalBuildButton : MonoBehaviour
{
    [Header("Building")]
    public BuildingData buildingData;

    private Button button;

    private Image image;

    private void Start()
    {
        image = GetComponent<Image>();

        button = GetComponent<Button>();

        button.onClick.AddListener(Build);
    }
    public void SetSelected(bool selected)
    {
        if (selected)
        {
            image.color = Color.green;
        }
        else
        {
            image.color = Color.white;
        }
    }
    private void Build()
    {
        UIManager.Instance.SelectBuilding(
            buildingData,
            this
        );
    }
}