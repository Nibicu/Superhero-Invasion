using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text slotText;

    private Button button;

    public Image backgroundImage;

    [Header("Visuals")]
    public GameObject lockOverlay;

    public Image buildingIcon;

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
        // LOCKED
        if (!targetSlot.isUnlocked)
        {
            lockOverlay.SetActive(true);

            buildingIcon.gameObject.SetActive(false);

            slotText.text = "";

            return;
        }

        // SLOT OPEN
        lockOverlay.SetActive(false);

        // EMPTY
        if (!targetSlot.isOccupied)
        {
            buildingIcon.gameObject.SetActive(false);

            slotText.text = "EMPTY";

            return;
        }

        // BUILDING EXISTS
        slotText.text = "";

        buildingIcon.gameObject.SetActive(true);

        buildingIcon.sprite =
            targetSlot.currentBuilding.data.icon;
    }
}