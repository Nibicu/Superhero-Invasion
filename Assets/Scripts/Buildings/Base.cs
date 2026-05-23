using UnityEngine;

public class Base : MonoBehaviour, ISelectable
{
    [Header("Base Info")]
    public string baseName = "Player Base";

    [Header("Base Stats")]
    public int health = 1000;
    public int income = 100;

    private void OnMouseDown()
    {
        SelectionManager.Instance.Select(this);
    }

    public void Select()
    {
        Debug.Log(baseName + " selected");

        UIManager.Instance.OpenBasePanel(this);
    }

    public void Deselect()
    {
        Debug.Log(baseName + " deselected");

        UIManager.Instance.CloseBasePanel();
    }
}