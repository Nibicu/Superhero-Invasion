using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна ячейка нижней панели команд.
/// С командой: аватарка капитана, номер, число героев, полоска HP и статус.
/// Без команды: "+" и подпись "Создать команду".
/// </summary>
public class SquadSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;           // Клик по ячейке
    [SerializeField] private Image frame;             // Рамка (цвет по звёздам капитана)
    [SerializeField] private GameObject emptyState;   // "+" и подпись
    [SerializeField] private GameObject filledState;  // Аватарка и данные команды
    [SerializeField] private HeroPortraitUI portrait; // Портрет капитана
    [SerializeField] private TMP_Text numberText;     // Номер команды
    [SerializeField] private TMP_Text sizeText;       // Число героев в команде
    [SerializeField] private Image hpFill;            // Полоска HP (Image Type = Filled)
    [SerializeField] private TMP_Text statusText;     // "На базе" / "В пути"...

    private static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.25f); // Рамка пустой ячейки

    private Action onClick; // Что делать при клике
    private Squad squad;    // Какая команда показана (null — пустая ячейка)

    /// <summary>Подписываем кнопку.</summary>
    private void Awake() => button.onClick.AddListener(() => onClick?.Invoke());

    /// <summary>Полоска HP обновляется каждый кадр (команда лечится на базе).</summary>
    private void Update()
    {
        if (squad == null) return;
        hpFill.fillAmount = squad.HpFraction;
        hpFill.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.4f), squad.HpFraction);
    }

    /// <summary>Показать команду (или пустую ячейку, если squad == null).</summary>
    public void Show(Squad squad, Action click)
    {
        onClick = click;
        this.squad = squad;
        bool has = squad != null;
        emptyState.SetActive(!has);
        filledState.SetActive(has);
        frame.color = has ? HeroData.StarColor(squad.Captain.Data.stars) : EmptyColor;
        if (!has) return;

        portrait.Show(squad.Captain.Data);
        numberText.text = squad.Number.ToString();
        sizeText.text = squad.Size.ToString();
        hpFill.fillAmount = squad.HpFraction;
        hpFill.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.4f), squad.HpFraction);
        statusText.text = squad.StatusText;
    }
}
