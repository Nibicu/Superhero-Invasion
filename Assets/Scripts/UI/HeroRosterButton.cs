using TMPro;
using UnityEngine;

public class HeroRosterButton : MonoBehaviour
{
    public TMP_Text heroNameText;

    public TMP_Text statusText;

    public TMP_Text tierText;

    public void Setup(HeroData heroData)
    {
        heroNameText.text =
            heroData.heroName;

        statusText.text =
            "Status: Idle";

        tierText.text =
            "Tier: " +
            heroData.tier;
    }
}