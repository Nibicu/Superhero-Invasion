using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна строка характеристики в карточке героя: значок, название, полоска и число.
/// </summary>
public class StatRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;     // Значок характеристики (сердце, меч, щит...)
    [SerializeField] private TMP_Text label; // "Сила атаки"
    [SerializeField] private TMP_Text value; // "120"
    [SerializeField] private Image fill;     // Полоска (Image Type = Filled)

    /// <summary>Заполнить строку. max — значение, при котором полоска полная.</summary>
    public void Set(string statName, int statValue, int max, Sprite iconSprite = null)
    {
        SetText(statName, statValue.ToString(), Mathf.Clamp01((float)statValue / max), iconSprite);
    }

    /// <summary>Заполнить строку произвольным текстом значения ("40 / 100") и долей полоски.</summary>
    public void SetText(string statName, string valueText, float fraction, Sprite iconSprite = null)
    {
        label.text = statName;
        value.text = valueText;
        fill.fillAmount = Mathf.Clamp01(fraction);
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
    }
}
