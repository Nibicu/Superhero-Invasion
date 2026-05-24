using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance;

    [Header("Money")]
    public int currentMoney = 1000;

    public int totalIncome = 0;

    private void Awake()
    {
        Instance = this;
    }
}