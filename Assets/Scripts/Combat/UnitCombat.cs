using UnityEngine;

public class UnitCombat : MonoBehaviour
{
    [Header("Stats")] // Это постоянные характеристики бойца.

    public int maxHP = 100; // Максимальное здоровье.
    public int currentHP; // Текущее здоровье.
    public int damage = 10; // Базовый урон одной обычной атаки.
    public float attackRange = 1.5f; // На каком расстоянии можно начать атаку.
    public float attackCooldown = 1f; // Минимальное время между двумя атаками. Не между комбо. Именно между ударами.
    public FactionType faction; // К какой стороне принадлежит юнит.
    public UnitState currentState = UnitState.Idle; // Что сейчас делает персонаж?
    public bool isAttacking; // временно
    public UnitCombat lockedTarget; // цель, на которой герой сфокусировался.


    [Header("Combat")] // Временные состояния

    public bool isStunned; // Стан во время удара.
    public float stunTimer; // Время стана.
    public int comboStep; // Какой сейчас удар серии.
    public bool isKnockedDown; // Лежит на земле.
    public float knockdownTimer; // Через сколько секунд встанет.
    public bool isRecoiling; // вроде уже стоит, но ещё не готов идти.
    public float recoilTimer; // Сколько ещё длится восстановление.
    private bool isDead; // Чтобы нельзя было убить одного и того же персонажа десять раз подряд.
    private Renderer spriteRenderer; // временно



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
    }//Выдать нокдаун.
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
    }//Получить урон.

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
    }//Унечтожение персонажа.
    private void Die()
    {
        Destroy(gameObject);
    }//Ненужен.
}