using System.Collections.Generic;
using UnityEngine;

public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance;

    public GameObject squadWorldPrefab;

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

    public void SpawnSquadWorldObject(
    Squad squad)
    {
        Debug.Log("SpawnSquadWorldObject called");

        GameObject squadObject =
            Instantiate(
                squadWorldPrefab,
                UIManager.Instance.playerBase.transform.position,
                Quaternion.identity
            );

        SquadWorldObject worldObject =
            squadObject.GetComponent<SquadWorldObject>();

        worldObject.squad = squad;

        squad.worldObject = worldObject;

        Debug.Log(
            squad.commander.heroData.heroName +
            " spawned on map"
        );
    }
}