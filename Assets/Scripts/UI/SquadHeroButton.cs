using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SquadHeroButton : MonoBehaviour
{
    public TMP_Text heroNameText;

    private HeroInstance hero;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(SelectHero);
    }

    public void Setup(
        HeroInstance heroInstance)
    {
        hero = heroInstance;

        heroNameText.text =
            hero.heroData.heroName;
    }

    private void SelectHero()
    {
        UIManager.Instance
            .SelectCommander(hero);
    }
}