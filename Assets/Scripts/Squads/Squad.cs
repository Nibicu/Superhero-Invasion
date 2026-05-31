using System.Collections.Generic;

[System.Serializable]
public class Squad
{
    public HeroInstance commander;

    public List<HeroInstance> members =
        new List<HeroInstance>();
}