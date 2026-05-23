using UnityEngine;

public class Base : MonoBehaviour
{
    [Header("Base Info")]
    public string baseName = "Player Base";

    [Header("Base Stats")]
    public int health = 1000; // HP базы
    public int income = 20; // Gold пас.добыча

    private void OnMouseDown()
    {
        Debug.Log(baseName + " selected");

        UIManager.Instance.OpenBasePanel(this);
    }
}