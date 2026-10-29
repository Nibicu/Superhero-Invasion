using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка задания фракции в тактической паузе: название (как его видит игрок), награда, охрана,
/// состояние и кнопка "НАЧАТЬ".
/// </summary>
public class FactionMissionCardUI : MonoBehaviour
{
    [SerializeField] private Image background;   // Фон карточки (цвет задания)
    [SerializeField] private TMP_Text titleText; // Название
    [SerializeField] private TMP_Text infoText;  // Награда и охрана
    [SerializeField] private Button button;      // "НАЧАТЬ"
    [SerializeField] private TMP_Text buttonText; // Текст на кнопке

    /// <summary>Заполнить карточку. canLaunch — можно ли начать, state — подпись на кнопке, если нельзя.</summary>
    public void Setup(GlobalMissionData data, bool canLaunch, string state, Action launch)
    {
        titleText.text = data.TitleFor(Team.Player);
        background.color = new Color(data.color.r * 0.35f, data.color.g * 0.35f, data.color.b * 0.35f, 0.95f);
        infoText.text = $"Награда: {data.GetRewardText()}\n<color=#9AA4B5>Охрана: {data.GetGuardText()}</color>";
        button.interactable = canLaunch;
        buttonText.text = canLaunch ? "НАЧАТЬ" : state.ToUpper();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => launch?.Invoke());
    }
}
