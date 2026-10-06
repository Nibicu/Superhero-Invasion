using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Портрет героя в интерфейсе. Если у героя есть спрайт-портрет — показывает его,
/// иначе — цветной круг с инициалами (временная графика).
/// </summary>
public class HeroPortraitUI : MonoBehaviour
{
    [SerializeField] private Image ring;      // Кольцо-рамка (цвет по звёздам)
    [SerializeField] private Image background; // Круг цвета героя
    [SerializeField] private Image portrait;   // Спрайт портрета
    [SerializeField] private TMP_Text initials; // Инициалы, если спрайта нет

    /// <summary>Показать портрет героя.</summary>
    public void Show(HeroData data)
    {
        bool hasSprite = data.portrait != null;
        if (ring != null) ring.color = HeroData.StarColor(data.stars);
        background.color = hasSprite ? Color.white : data.color;
        portrait.gameObject.SetActive(hasSprite);
        portrait.sprite = data.portrait;
        initials.gameObject.SetActive(!hasSprite);
        initials.text = data.Initials;
    }
}
