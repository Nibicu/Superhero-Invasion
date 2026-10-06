using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна строка в меню строительства: иконка, название, описание, цена и кнопка "Построить".
/// В сцене лежит выключенный шаблон, BuildMenuUI копирует его для каждой постройки.
/// </summary>
public class BuildOptionUI : MonoBehaviour
{
    [SerializeField] private BuildingIconUI icon;     // Иконка постройки
    [SerializeField] private TMP_Text nameText;       // Название
    [SerializeField] private TMP_Text descText;       // Описание и эффект
    [SerializeField] private Button buildButton;      // Кнопка "Построить"
    [SerializeField] private TMP_Text buildText;      // Текст на кнопке (с ценой)

    /// <summary>Заполнить строку. onBuild вызывается при нажатии кнопки.</summary>
    public void Setup(BuildingData data, bool canAfford, Action onBuild)
    {
        icon.Show(data);
        nameText.text = data.displayName;
        string effect = data.GetEffectText(1);
        descText.text = string.IsNullOrEmpty(effect) ? data.description : $"{data.description}\n{effect}";
        buildText.text = $"ПОСТРОИТЬ\n<size=80%>{BuildingSlotUI.CostText(data.GetLevel(1), canAfford)}</size>";
        buildButton.onClick.RemoveAllListeners();
        buildButton.onClick.AddListener(() => onBuild());
    }
}
