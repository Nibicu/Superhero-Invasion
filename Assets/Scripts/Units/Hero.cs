using UnityEngine;

public class Hero : MonoBehaviour
{
    [Header("Hero Data")]
    public HeroData heroData;

    [Header("Runtime Stats")]
    public int currentHealth;

    private void Start()
    {
        if (heroData != null)
        {
            currentHealth =
                heroData.maxHealth;
        }
    }
}