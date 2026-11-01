using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка бойца в списке полигона «Битва»: портрет, имя, класс и звёзды,
/// кнопки «+A» и «+B» — добавить бойца в команду A или B.
/// </summary>
public class PolygonUnitCardUI : MonoBehaviour
{
    [SerializeField] private HeroPortraitUI portrait; // Портрет (или кружок с инициалами)
    [SerializeField] private TMP_Text nameText;       // Имя
    [SerializeField] private TMP_Text infoText;       // Класс, звёзды, сторона
    [SerializeField] private Button addAButton;       // "+A"
    [SerializeField] private Button addBButton;       // "+B"

    /// <summary>Заполнить карточку. onAdd(true) — в команду A, onAdd(false) — в команду B.</summary>
    public void Setup(HeroData data, Action<bool> onAdd)
    {
        portrait.Show(data);
        nameText.text = data.displayName;
        string side = !data.hireable ? "<color=#C9A06B>моб</color>"
            : data.side == Side.Heroes ? "<color=#7FB8FF>герой</color>" : "<color=#FF8A7A>злодей</color>";
        infoText.text = $"{UnitClasses.Name(data.unitClass)} • {data.stars} зв. • {side}";
        addAButton.onClick.AddListener(() => onAdd(true));
        addBButton.onClick.AddListener(() => onAdd(false));
    }
}
