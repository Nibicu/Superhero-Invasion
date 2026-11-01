using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка таблицы итогов полигона: команда, боец, уровень, урон, лечение, получено урона, добил, выжил.
/// Фон строки — цвет команды (A — синий, B — красный).
/// </summary>
public class PolygonStatRowUI : MonoBehaviour
{
    [SerializeField] private Image background; // Фон строки
    [SerializeField] private TMP_Text[] cells; // Ячейки слева направо

    /// <summary>Заполнить строку. teamA — строка команды A (синий фон), иначе B (красный).</summary>
    public void Set(bool teamA, params string[] values)
    {
        background.color = teamA ? new Color(0.17f, 0.3f, 0.52f, 0.85f) : new Color(0.5f, 0.18f, 0.18f, 0.85f);
        for (int i = 0; i < cells.Length; i++) cells[i].text = i < values.Length ? values[i] : "";
    }
}
