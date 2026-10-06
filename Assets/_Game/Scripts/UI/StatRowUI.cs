using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна строка характеристики в карточке героя: название, число и полоска.
/// </summary>
public class StatRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label; // "Атака"
    [SerializeField] private TMP_Text value; // "120"
    [SerializeField] private Image fill;     // Полоска (Image Type = Filled)

    /// <summary>Заполнить строку. max — значение, при котором полоска полная.</summary>
    public void Set(string statName, int statValue, int max)
    {
        label.text = statName;
        value.text = statValue.ToString();
        fill.fillAmount = Mathf.Clamp01((float)statValue / max);
    }
}
