using UnityEngine;

public class Base : MapObject
{
    [Header("Base Economy")]
    public int income = 100;

    public override void Select()
    {
        base.Select();

        Debug.Log("Base specific UI");
    }
}