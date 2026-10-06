using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Маленькая карточка героя в списке окна команды:
/// портрет, звёзды, имя, уровень. Вся карточка — кнопка.
/// </summary>
public class HeroListItemUI : MonoBehaviour
{
    [SerializeField] private Image frame;             // Рамка (цвет по звёздам)
    [SerializeField] private HeroPortraitUI portrait; // Портрет
    [SerializeField] private StarsUI stars;           // Звёзды
    [SerializeField] private TMP_Text nameText;       // Имя
    [SerializeField] private TMP_Text levelText;      // "Ур. 2"
    [SerializeField] private Button button;           // Клик по карточке

    /// <summary>Заполнить карточку. onClick — что сделать при нажатии.</summary>
    public void Setup(HeroInstance hero, Action onClick)
    {
        frame.color = HeroData.StarColor(hero.Data.stars);
        portrait.Show(hero.Data);
        stars.Show(hero.Data.stars);
        nameText.text = hero.Data.displayName;
        levelText.text = $"Ур. {hero.Level}";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}
