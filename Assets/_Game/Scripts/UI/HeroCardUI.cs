using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Маленькая карточка героя в списках РОСТЕР и МОИ ГЕРОИ:
/// портрет, звёзды, имя, строка информации и кнопка.
/// Клик по самой карточке открывает подробную карточку героя.
/// В сцене лежит выключенный шаблон, окна копируют его для каждого героя.
/// </summary>
public class HeroCardUI : MonoBehaviour
{
    [SerializeField] private Image frame;            // Рамка (цвет по звёздам)
    [SerializeField] private HeroPortraitUI portrait; // Портрет
    [SerializeField] private StarsUI stars;          // Звёзды
    [SerializeField] private TMP_Text nameText;      // Имя
    [SerializeField] private TMP_Text infoText;      // Цена или уровень/статус
    [SerializeField] private Button cardButton;      // Клик по карточке
    [SerializeField] private Button actionButton;    // Кнопка снизу ("НАНЯТЬ", "ПОДРОБНЕЕ"...)
    [SerializeField] private TMP_Text actionText;    // Текст на кнопке

    /// <summary>
    /// Заполнить карточку.
    /// action — текст кнопки, actionEnabled — можно ли нажать,
    /// onAction — что сделать по кнопке, onClick — по клику на карточку.
    /// </summary>
    public void Setup(HeroData data, string info, string action, bool actionEnabled, Action onAction, Action onClick)
    {
        frame.color = HeroData.StarColor(data.stars);
        portrait.Show(data);
        stars.Show(data.stars);
        nameText.text = data.displayName;
        infoText.text = info;

        actionText.text = action;
        actionButton.interactable = actionEnabled;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onAction?.Invoke());

        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(() => onClick?.Invoke());
    }
}
