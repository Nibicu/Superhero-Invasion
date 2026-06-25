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
    public bool isAttacking;
    public UnitCombat lockedTarget;


    [Header("Combat")]

    public bool isStunned;
    public float stunTimer;
    public int comboStep;

    public bool isKnockedDown;
    public float knockdownTimer;

    public bool isRecoiling;
    public float recoilTimer;


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
        if (isKnockedDown)
        {
            knockdownTimer -= Time.deltaTime;

            if (knockdownTimer <= 0)
            {
                isKnockedDown = false;

                transform.rotation =
                    Quaternion.identity;
            }

            return;
        }

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;

            if (stunTimer <= 0)
            {
                isStunned = false;
            }
        }

        if (isRecoiling)
        {
            recoilTimer -= Time.deltaTime;

            if (recoilTimer <= 0)
            {
                isRecoiling = false;
            }
        }
    }
    public void Stun(float duration)
    {
        isStunned = true;

        stunTimer = duration;
    }
    public void KnockDown(float duration)
    {
        isKnockedDown = true;
        knockdownTimer = duration;

        lockedTarget = null; // ❗ важно

        transform.rotation =
            Quaternion.Euler(
                70,
                0,
                0
            );
    }
    public void TakeDamage(int amount)
    {
        if (isDead)
        {
            return;
        }

        currentHP -= amount;

        comboStep = 0;

        if (currentHP <= 0)
        {
            StartCoroutine(DeathDelay());
        }
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
    private void Die()
    {
        Destroy(gameObject);
    }
}