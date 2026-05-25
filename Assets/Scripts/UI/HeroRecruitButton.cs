using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroRecruitButton : MonoBehaviour
{
    public TMP_Text heroNameText;

    public TMP_Text costText;

    public TMP_Text tierText;

    private Button button;

    private HeroData currentHeroData;

    private void Awake()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(Recruit);
    }
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
    private void Recruit()
    {
        UIManager.Instance.RecruitHero(
            currentHeroData
        );
    }
}