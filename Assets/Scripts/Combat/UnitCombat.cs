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


    [Header("Combat")]

    public bool isStunned;
    public float stunTimer;
    public int comboStep;

    private void Start()
    {
        currentHP = maxHP;
    }

    private void Update()
    {
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;

            if (stunTimer <= 0)
            {
                isStunned = false;
            }
        }
    }
    public void Stun(float duration)
    {
        isStunned = true;

        stunTimer = duration;
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