using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ячейка в окне команды: большая — для капитана, маленькие — для героев.
/// Пустая — показывает "+" и подпись; занятая — героя.
/// Клик по занятой ячейке убирает героя из команды.
/// </summary>
public class SquadMemberSlotUI : MonoBehaviour
{
    [SerializeField] private Image frame;             // Рамка
    [SerializeField] private Button button;           // Клик по ячейке
    [SerializeField] private GameObject emptyState;   // Что видно, когда пусто ("+" и подпись)
    [SerializeField] private GameObject filledState;  // Что видно, когда есть герой
    [SerializeField] private HeroPortraitUI portrait; // Портрет героя
    [SerializeField] private StarsUI stars;           // Звёзды
    [SerializeField] private TMP_Text nameText;       // Имя и уровень

    private static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.15f); // Рамка пустой ячейки

    private Action onClick; // Что делать при клике

    /// <summary>Подписываем кнопку.</summary>
    private void Awake() => button.onClick.AddListener(() => onClick?.Invoke());

    /// <summary>Показать героя (или пустую ячейку, если hero == null).</summary>
    public void Show(HeroInstance hero, bool interactable, Action click)
    {
        onClick = click;
        button.interactable = interactable;
        bool has = hero != null;
        emptyState.SetActive(!has);
        filledState.SetActive(has);
        frame.color = has ? HeroData.StarColor(hero.Data.stars) : EmptyColor;
        if (!has) return;
        portrait.Show(hero.Data);
        stars.Show(hero.Data.stars);
        nameText.text = $"{hero.Data.displayName}\n<size=75%><color=#9AA4B5>Ур. {hero.Level}</color></size>";
    }
}
