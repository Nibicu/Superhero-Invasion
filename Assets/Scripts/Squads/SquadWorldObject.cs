using UnityEngine;
using static UnityEngine.UI.ScrollRect;

public class SquadWorldObject : MonoBehaviour
{
    public Squad squad;
    public float moveSpeed = 5f;

    private bool isMoving = false;

    private void Update()
    {
        if (squad != null)
        {
            Debug.Log(
                squad.commander.heroData.heroName +
                " status = " +
                squad.status
            );
        }

        if (squad.targetObject != null &&
            squad.status == SquadStatus.Traveling)
        {
            MoveTowardsTarget();
        }
    }

    private void MoveTowardsTarget()
    {
        Vector3 targetPos = squad.targetObject.transform.position;
        Vector3 direction = (targetPos - transform.position).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;

        // Проверка прибытия
        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            OnArrived();
        }
    }

    private void OnArrived()
    {
        if (squad.targetObject is Base)
        {
            squad.status = SquadStatus.Ready;
        }
        else
        {
            squad.status = SquadStatus.Arrived;

            squad.targetObject.ownerFaction =
                FactionType.Good;
        }

        gameObject.SetActive(false);

        UIManager.Instance.RefreshSquadDetails(squad);

        if (!(squad.targetObject is Base))
        {
            UIManager.Instance.OpenCommanderPanel(squad);
        }
    }
}