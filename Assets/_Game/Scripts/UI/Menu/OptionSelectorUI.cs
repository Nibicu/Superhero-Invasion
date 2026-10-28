using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка настройки с выбором из списка: "Название   ‹ значение ›".
/// Стрелки листают варианты по кругу, при смене вызывается onChanged(номер варианта).
/// </summary>
public class OptionSelectorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText; // Текущее значение
    [SerializeField] private Button prevButton;  // Стрелка "‹"
    [SerializeField] private Button nextButton;  // Стрелка "›"

    private string[] options = new string[0]; // Варианты
    private int index;                        // Выбранный вариант
    private Action<int> onChanged;            // Что делать при смене

    /// <summary>Подписать стрелки.</summary>
    private void Awake()
    {
        prevButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));
    }

    /// <summary>Задать варианты, выбранный номер и действие при смене.</summary>
    public void Setup(string[] values, int selected, Action<int> changed)
    {
        options = values;
        index = Mathf.Clamp(selected, 0, Mathf.Max(0, values.Length - 1));
        onChanged = changed;
        Refresh();
    }

    /// <summary>Перейти к соседнему варианту (по кругу).</summary>
    private void Step(int dir)
    {
        if (options.Length == 0) return;
        index = (index + dir + options.Length) % options.Length;
        Refresh();
        onChanged?.Invoke(index);
    }

    /// <summary>Показать текущее значение.</summary>
    private void Refresh() => valueText.text = options.Length > 0 ? options[index] : "—";
}
