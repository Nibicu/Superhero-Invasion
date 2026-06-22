using UnityEngine;

public class UnitAI : MonoBehaviour
{
    private UnitCombat combat;

    private UnitCombat target;

    private float attackTimer;

    private void Start()
    {
        combat = GetComponent<UnitCombat>();
    }

    private void Update()
    {
        if (combat.isStunned)
        {
            return;
        }

        if (target == null)
        {
            FindTarget();
        }

        if (target == null)
        {
            FindTarget();
        }

        if (target == null)
        {
            return;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                target.transform.position
            );

        if (distance > combat.attackRange)
        {
            MoveToTarget();
        }
        else
        {
            AttackTarget();
        }
    }

    private void FindTarget()
    {
        if (target != null)
        {
            return;
        }

        UnitCombat[] allUnits =
            FindObjectsOfType<UnitCombat>();

        float closestDistance =
            Mathf.Infinity;

        UnitCombat closestTarget =
            null;

        foreach (UnitCombat unit in allUnits)
        {
            if (unit == combat)
            {
                continue;
            }

            if (unit.faction ==
                combat.faction)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    unit.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance =
                    distance;

                closestTarget =
                    unit;
            }
        }

        target = closestTarget;
    }

    private void MoveToTarget()
    {
        transform.position =
            Vector3.MoveTowards(
                transform.position,
                target.transform.position,
                3f * Time.deltaTime
            );
    }

    private void AttackTarget()
    {
        attackTimer += Time.deltaTime;

        if (attackTimer <
            combat.attackCooldown)
        {
            return;
        }

        attackTimer = 0f;

        combat.comboStep++;

        if (combat.comboStep < 3)
        {
            target.TakeDamage(
                combat.damage
            );

            target.Stun(0.4f);

            Vector3 direction =
                (target.transform.position -
                 transform.position).normalized;

            transform.position +=
                direction * 0.1f;

            target.transform.position +=
                direction * 0.1f;
        }
        else
        {
            target.TakeDamage(
                combat.damage * 2
            );

            Vector3 direction =
                (target.transform.position -
                 transform.position).normalized;

            transform.position +=
                direction * 0.2f;

            target.transform.position +=
                direction * 0.5f;

            combat.comboStep = 0;

            attackTimer = -1f;
        }

        if (target.currentHP <= 0)
        {
            target = null;
        }
    }
}