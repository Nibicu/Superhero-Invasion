using System.Collections.Generic;
using UnityEngine;

public class HeroManager : MonoBehaviour
{
    public static HeroManager Instance;

    public List<HeroData> ownedHeroes =
        new List<HeroData>();

    private void Awake()
    {
        Instance = this;
    }

    public void RecruitHero(HeroData heroData)
    {
        ownedHeroes.Add(heroData);

        Debug.Log(
            heroData.heroName +
            " recruited!"
        );
    }
}