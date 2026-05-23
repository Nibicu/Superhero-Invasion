using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text slotText;

    private Button button;

    public Image backgroundImage;

    [Header("Gameplay Slot")]
    public BuildingSlot targetSlot;

    private void Start()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(OnSlotClicked);
    }
    private void OnSlotClicked()
    {
        // Locked
        if (!targetSlot.isUnlocked)
        {
            Debug.Log("Slot locked");

            return;
        }

        // Occupied
        if (targetSlot.isOccupied)
        {
            Debug.Log(
                "Building: " +
                targetSlot.currentBuilding.data.buildingName
            );

            return;
        }

        // Empty
        UIManager.Instance.OpenBuildMenu(targetSlot);
    }
    public void RefreshUI()
    {
        if (!targetSlot.isUnlocked)
        {
            slotText.text = "LOCK";

            return;
        }

        if (!targetSlot.isOccupied)
        {
            slotText.text = "EMPTY";

            return;
        }

        slotText.text =
            targetSlot.currentBuilding.data.buildingName;
    }
}