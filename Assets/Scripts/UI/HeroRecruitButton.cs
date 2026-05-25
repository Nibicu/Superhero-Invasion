using TMPro;
using UnityEngine;

public class HeroRecruitButton : MonoBehaviour
{
    public TMP_Text heroNameText;

    public TMP_Text costText;

    public TMP_Text tierText;

    private HeroData currentHeroData;

    public void Setup(HeroData heroData)
    {
        currentHeroData = heroData;

        heroNameText.text =
            heroData.heroName;

        costText.text =
            "Cost: " +
            heroData.cost;

        tierText.text =
            "Tier: " +
            heroData.tier;
    }
}