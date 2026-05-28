using System.Collections.Generic;
using UnityEngine;

public class HeroManager : MonoBehaviour
{
    public static HeroManager Instance;

    public List<HeroInstance> ownedHeroes =
    new List<HeroInstance>();

    private void Awake()
    {
        Instance = this;
    }

    public void RecruitHero(HeroData heroData)
    {
        HeroInstance newHero =
            new HeroInstance(heroData);

        ownedHeroes.Add(newHero);

        Debug.Log(
            heroData.heroName +
            " recruited!"
        );
    }
}