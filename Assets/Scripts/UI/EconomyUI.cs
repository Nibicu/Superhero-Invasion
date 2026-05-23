using UnityEngine;
using TMPro;

public class EconomyUI : MonoBehaviour
{
    [Header("References")]
    public PlayerData playerData;

    [Header("UI")]
    public TMP_Text moneyText;

    private void Update()
    {
        moneyText.text =
            "Money: " +
            playerData.currentMoney;
    }
}