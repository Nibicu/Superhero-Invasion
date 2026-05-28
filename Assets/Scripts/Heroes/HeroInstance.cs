using UnityEngine;

[System.Serializable]
public class HeroInstance
{
    public HeroData heroData;

    public HeroStatus status =
        HeroStatus.Idle;

    public int level = 1;

    public int experience = 0;

    public HeroInstance(HeroData data)
    {
        heroData = data;
    }
}