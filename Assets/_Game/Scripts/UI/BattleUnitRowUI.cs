using TMPro;
using UnityEngine;

/// <summary>
/// Строка бойца в окне перед боем: портрет, имя и подпись (уровень, HP или территория).
/// </summary>
public class BattleUnitRowUI : MonoBehaviour
{
    [SerializeField] private HeroPortraitUI portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text infoText;

    /// <summary>Заполнить строку.</summary>
    public void Setup(HeroData data, string info)
    {
        portrait.Show(data);
        nameText.text = data.displayName;
        infoText.text = info;
    }
}
