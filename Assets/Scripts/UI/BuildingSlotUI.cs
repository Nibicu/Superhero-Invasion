using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text slotText;

    public Image backgroundImage;

    [Header("Gameplay Slot")]
    public BuildingSlot targetSlot;

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