using UnityEngine;

public class Mine : MapObject
{
    [Header("Mine")]
    public int incomeBonus = 50;

    public override void Select()
    {
        base.Select();

        Debug.Log("Mine selected");
    }
}