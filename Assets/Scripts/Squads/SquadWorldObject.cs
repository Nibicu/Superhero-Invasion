using UnityEngine;
using static UnityEngine.UI.ScrollRect;

public class SquadWorldObject : MonoBehaviour
{
    public Squad squad;
    public float moveSpeed = 5f;

    private bool isMoving = false;

    private void Update()
    {
        if (squad.targetObject != null && squad.status == SquadStatus.Traveling)
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
        squad.status = SquadStatus.Arrived;
        UIManager.Instance.RefreshSquadDetails(squad);

        // Открываем панель командира только если это не база
        if (!(squad.targetObject is Base))
        {
            UIManager.Instance.OpenCommanderPanel(squad);
        }
    }
}