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


    private bool isDead;
    private Renderer spriteRenderer;


    private void Start()
    {
        currentHP = maxHP;

        spriteRenderer =
            GetComponent<Renderer>();
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
        if (isDead)
        {
            return;
        }


        currentHP -= amount;


        if (currentHP <= 0)
        {
            StartCoroutine(DeathDelay());
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private System.Collections.IEnumerator DeathDelay()
    {
        isDead = true;


        if (spriteRenderer != null)
        {
            spriteRenderer.material.color =
                new Color(0.8f, 0.3f, 0.05f);
        }


        isStunned = true;


        yield return new WaitForSeconds(0.5f);


        Destroy(gameObject);
    }
}