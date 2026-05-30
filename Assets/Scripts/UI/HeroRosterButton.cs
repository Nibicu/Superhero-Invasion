using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroRosterButton : MonoBehaviour
{
    private Button button;

    private HeroInstance currentHero;

    public TMP_Text heroNameText;

    public TMP_Text statusText;

    public TMP_Text tierText;

    public void Setup(HeroInstance hero)
    {
        currentHero = hero;

        heroNameText.text =
            hero.heroData.heroName;

        statusText.text =
            "Status: " +
            hero.status.ToString();

        tierText.text =
            "Tier: " +
            hero.heroData.tier;
    }

    private void Awake()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(OpenDetails);
    }

    private void OpenDetails()
    {
        UIManager.Instance
            .OpenHeroDetails(currentHero);
    }

}