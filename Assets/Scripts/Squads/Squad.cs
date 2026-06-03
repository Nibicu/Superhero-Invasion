using System.Collections.Generic;

[System.Serializable]
public class Squad
{
    public HeroInstance commander;
    public SquadStatus status =
    SquadStatus.Ready;

    public List<HeroInstance> members =
        new List<HeroInstance>();
}