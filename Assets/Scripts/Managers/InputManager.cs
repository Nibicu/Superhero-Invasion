using UnityEngine;

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
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);

        // Если никуда не попали
        if (hit.collider == null)
        {
            SelectionManager.Instance.DeselectCurrent();
        }
    }
}