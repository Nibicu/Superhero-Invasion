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
        if (combat.currentState == UnitState.Attack)
        {
            return;
        }

        if (combat.isKnockedDown)
        {
            target = null;
            return;
        }

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
            return;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                target.transform.position
            );

        if (distance > combat.Stats.attackRange)
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
        if (combat.lockedTarget != null)
        {
            target = combat.lockedTarget;
            return;
        }

        UnitCombat[] allUnits =
            FindObjectsOfType<UnitCombat>();

        float closestDistance = Mathf.Infinity;
        UnitCombat closestTarget = null;

        foreach (UnitCombat unit in allUnits)
        {
            if (unit == combat) continue;
            if (unit.Stats.faction == combat.Stats.faction) continue;
            if (unit.isKnockedDown) continue;

            float distance = Vector3.Distance(transform.position, unit.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = unit;
            }
        }

        target = closestTarget;
    }

    private void MoveToTarget()
    {
        if (
        combat.isKnockedDown ||
        combat.isStunned ||
        combat.currentState == UnitState.Attack ||
        combat.isRecoiling
       )
            return;
        if (combat.currentState != UnitState.Moving)
        {
            combat.currentState = UnitState.Moving;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                target.transform.position,
                1.5f * Time.deltaTime
            );
    }

    private void AttackTarget()
    {
        if (combat.isKnockedDown || combat.isStunned)
            return;

        attackTimer += Time.deltaTime;

        if (attackTimer < combat.Stats.attackCooldown)
            return;

        attackTimer = 0f;

        combat.attackTimer = 0f;
        combat.damageDealt = false;

        // Новая система состояний
        combat.currentState = UnitState.Attack;

        if (combat.lockedTarget == null)
        {
            combat.lockedTarget = target;
        }

        combat.comboStep++;
    }
}