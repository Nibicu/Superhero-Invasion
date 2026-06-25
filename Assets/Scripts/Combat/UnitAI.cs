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

        if (combat.isKnockedDown)
        {
            target = null;
            return;
        }

        if (target == null)
        {
            FindTarget();
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
            if (unit.faction == combat.faction) continue;
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
        combat.isAttacking ||
        combat.isRecoiling
       )
            return;

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

        if (attackTimer < combat.attackCooldown)
            return;

        attackTimer = 0f;

        combat.isAttacking = true;
        StartCoroutine(UnlockAttack());

        if (combat.lockedTarget == null)
        {
            combat.lockedTarget = target;
        }
        combat.comboStep++;

        Vector3 direction =
            (target.transform.position - transform.position).normalized;

        float push = 0f;

        // 🟡 УДАР 1–2
        if (combat.comboStep < 3)
        {
            target.TakeDamage(combat.damage);
            target.Stun(1.0f);

            push = 0.05f;
        }
        else
        {
            // 🔴 3-й УДАР (финиш)
            target.TakeDamage(combat.damage * 2);
            target.KnockDown(1.5f);

            push = 0.8f;

            combat.comboStep = 0;
        }

        // 💥 ИМПАКТ (ВАЖНО — ТОЛЬКО ОДИН РАЗ)
        target.transform.position += direction * push;
        target.isRecoiling = true;
        target.recoilTimer = 0.3f;

        // 🧠 лёгкий откат атакующего (ощущение удара)
        transform.position -= -direction * (push * 0.3f);

        if (target.currentHP <= 0)
        {
            target = null;
        }
    }
    private System.Collections.IEnumerator UnlockAttack()
    {
        yield return new WaitForSeconds(0.2f);

        combat.isAttacking = false;
    }
}