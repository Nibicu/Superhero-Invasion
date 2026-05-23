using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    private ISelectable currentSelected;

    private void Awake()
    {
        Instance = this;
    }

    public void Select(ISelectable newSelection)
    {
        // Если уже что-то выбрано
        if (currentSelected != null)
        {
            currentSelected.Deselect();
        }

        // Сохраняем новый объект
        currentSelected = newSelection;

        // Вызываем Select у нового объекта
        currentSelected.Select();
    }

    public void DeselectCurrent()
    {
        if (currentSelected != null)
        {
            currentSelected.Deselect();
            currentSelected = null;
        }
    }
}