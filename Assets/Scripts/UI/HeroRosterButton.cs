using TMPro;
using UnityEngine;

public class HeroRosterButton : MonoBehaviour
{
    public TMP_Text heroNameText;

    public TMP_Text statusText;

    public TMP_Text tierText;

    public void Setup(HeroInstance hero)
    {
        heroNameText.text =
            hero.heroData.heroName;

        statusText.text =
            "Status: " +
            hero.status.ToString();

        tierText.text =
            "Tier: " +
            hero.heroData.tier;
    }
}