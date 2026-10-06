using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Иконка постройки в интерфейсе. Если у постройки есть спрайт — показывает его,
/// если нет — рисует цветную плашку с первой буквой названия.
/// Используется в карточках ячеек и в меню строительства.
/// </summary>
public class BuildingIconUI : MonoBehaviour
{
    [SerializeField] private Image background; // Цветная плашка
    [SerializeField] private Image icon;       // Спрайт постройки
    [SerializeField] private TMP_Text letter;  // Буква, если спрайта нет

    /// <summary>Показать иконку постройки.</summary>
    public void Show(BuildingData data)
    {
        gameObject.SetActive(true);
        bool hasSprite = data.icon != null;
        background.color = hasSprite ? new Color(1f, 1f, 1f, 0.92f) : data.color;
        icon.gameObject.SetActive(hasSprite);
        icon.sprite = data.icon;
        letter.gameObject.SetActive(!hasSprite);
        letter.text = string.IsNullOrEmpty(data.displayName) ? "?" : data.displayName.Substring(0, 1);
    }

    /// <summary>Спрятать иконку (пустая ячейка).</summary>
    public void Hide() => gameObject.SetActive(false);
}
