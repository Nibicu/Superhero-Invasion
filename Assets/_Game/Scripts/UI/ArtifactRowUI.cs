using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна строка в окне артефактов: иконка (или цветной квадрат с буквой), название,
/// эффект и кнопка ("ИСПОЛЬЗОВАТЬ", "НАДЕТЬ", "СНЯТЬ", "ВЫБРАТЬ").
/// В сцене лежит выключенный шаблон, окно копирует его для каждой строки.
/// </summary>
public class ArtifactRowUI : MonoBehaviour
{
    [SerializeField] private Image iconBackground; // Цветной квадрат
    [SerializeField] private Image icon;           // Спрайт иконки (если есть)
    [SerializeField] private TMP_Text iconLetter;  // Буква, если спрайта нет
    [SerializeField] private TMP_Text nameText;    // Название
    [SerializeField] private TMP_Text infoText;    // Эффект / описание
    [SerializeField] private Button actionButton;  // Кнопка действия
    [SerializeField] private TMP_Text actionText;  // Текст на кнопке

    /// <summary>Строка артефакта.</summary>
    public void Setup(ArtifactData a, string action, bool enabled, Action onAction)
    {
        Fill(a.icon, a.iconLetter, a.color, a.displayName, a.EffectText(), action, enabled, onAction);
    }

    /// <summary>Строка героя (для выбора, кому надеть вещь или дать уровень).</summary>
    public void SetupHero(HeroInstance h, string info, string action, bool enabled, Action onAction)
    {
        Fill(h.Data.portrait, h.Data.Initials, h.Data.color, h.Data.displayName, info, action, enabled, onAction);
    }

    private void Fill(Sprite sprite, string letter, Color color, string title, string info, string action, bool enabled, Action onAction)
    {
        iconBackground.color = sprite != null ? Color.white : color;
        icon.gameObject.SetActive(sprite != null);
        icon.sprite = sprite;
        iconLetter.gameObject.SetActive(sprite == null);
        iconLetter.text = letter;
        nameText.text = title;
        infoText.text = info;
        actionText.text = action;
        actionButton.gameObject.SetActive(!string.IsNullOrEmpty(action));
        actionButton.interactable = enabled;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onAction?.Invoke());
    }
}
