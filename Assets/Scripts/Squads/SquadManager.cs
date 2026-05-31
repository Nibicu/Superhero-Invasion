using System.Collections.Generic;
using UnityEngine;

public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance;

    public List<Squad> activeSquads =
        new List<Squad>();

    private void Awake()
    {
        Instance = this;
    }

    public void CreateSquad(
        Squad squad)
    {
        activeSquads.Add(squad);

        Debug.Log(
            "Squad created. Total squads: " +
            activeSquads.Count
        );
    }
}