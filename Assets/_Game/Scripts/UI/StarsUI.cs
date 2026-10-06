using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Звёзды героя (1–3). Показывает нужное количество звёздочек,
/// лишние прячет (родитель с HorizontalLayoutGroup сам их центрирует).
/// </summary>
public class StarsUI : MonoBehaviour
{
    [SerializeField] private Image[] stars; // Три картинки-звезды

    /// <summary>Показать count звёзд.</summary>
    public void Show(int count)
    {
        for (int i = 0; i < stars.Length; i++)
            stars[i].gameObject.SetActive(i < count);
    }
}
