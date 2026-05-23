using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    private void Update()
    {
        // ЛКМ
        if (Input.GetMouseButtonDown(0))
        {
            CheckForDeselection();
        }
    }

    private void CheckForDeselection()
    {
        // Если кликнули по UI
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector2 mousePosition =
            Camera.main.ScreenToWorldPoint(Input.mousePosition);

        RaycastHit2D hit =
            Physics2D.Raycast(mousePosition, Vector2.zero);

        // Если не попали в объект
        if (hit.collider == null)
        {
            SelectionManager.Instance.DeselectCurrent();
        }
    }
}