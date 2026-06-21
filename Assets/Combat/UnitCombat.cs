using UnityEngine;

public class UnitCombat : MonoBehaviour
{
    [Header("Stats")]

    public int maxHP = 100;

    public int currentHP;

    public int damage = 10;

    public float attackRange = 1.5f;

    public float attackCooldown = 1f;

    public FactionType faction;

    private void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int amount)
    {
        currentHP -= amount;

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}