using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка в окне выбора команды: портрет капитана, номер, состав, сила, статус
/// и кнопка "ОТПРАВИТЬ".
/// </summary>
public class SquadPickRowUI : MonoBehaviour
{
    [SerializeField] private HeroPortraitUI portrait; // Портрет капитана
    [SerializeField] private TMP_Text nameText;       // "Команда 1 — Титан"
    [SerializeField] private TMP_Text infoText;       // Героев, сила, статус
    [SerializeField] private Button sendButton;       // "ОТПРАВИТЬ"
    [SerializeField] private TMP_Text sendText;       // Текст на кнопке

    /// <summary>Заполнить строку.</summary>
    public void Setup(Squad squad, bool canSend, string buttonText, Action onSend)
    {
        portrait.Show(squad.Captain.Data);
        nameText.text = $"Команда {squad.Number}  <size=80%><color=#9AA4B5>капитан</color> {squad.Captain.Data.displayName}</size>";
        infoText.text = $"Героев: {squad.Size}   •   Сила: {squad.Power}   •   Здоровье: {squad.CurrentHp}   •   {squad.StatusText}";
        sendButton.interactable = canSend;
        sendText.text = buttonText;
        sendButton.onClick.RemoveAllListeners();
        sendButton.onClick.AddListener(() => onSend?.Invoke());
    }
}
