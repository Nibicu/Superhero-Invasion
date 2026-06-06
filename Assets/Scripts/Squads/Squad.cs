using System.Collections.Generic;

[System.Serializable]
public class Squad
{
    public HeroInstance commander;
    public SquadStatus status =
    SquadStatus.Ready;

    public MapObject targetObject;

    public SquadWorldObject worldObject;

    public List<HeroInstance> members =
        new List<HeroInstance>();
}