using UnityEngine;
using System.Collections;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    [Header("References")]
    public PlayerData playerData;

    [Header("Economy Settings")]
    public float incomeInterval = 10f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(IncomeTickRoutine());
    }

    private IEnumerator IncomeTickRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(incomeInterval);

            CalculateIncome();

            GiveIncome();
        }
    }

    private void CalculateIncome()
    {
        Base[] allBases = FindObjectsOfType<Base>();

        int income = 0;

        foreach (Base currentBase in allBases)
        {
            // Только базы игрока Good
            if (currentBase.ownerFaction == FactionType.Good)
            {
                income += currentBase.income;
            }
        }

        playerData.totalIncome = income;
    }

    private void GiveIncome()
    {
        playerData.currentMoney += playerData.totalIncome;

        Debug.Log(
            "Income received: +" +
            playerData.totalIncome +
            " | Current Money: " +
            playerData.currentMoney
        );
    }
}