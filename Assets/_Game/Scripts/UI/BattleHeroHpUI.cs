using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка героя в панели "ВАША КОМАНДА" во время боя: портрет, имя, полоска HP и энергии.
/// </summary>
public class BattleHeroHpUI : MonoBehaviour
{
    [SerializeField] private HeroPortraitUI portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image hpFill;      // Image Type = Filled
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image energyFill;  // Image Type = Filled
    [SerializeField] private CanvasGroup group; // Затемнение выбывшего

    private Fighter fighter;

    /// <summary>Привязать строку к бойцу.</summary>
    public void Bind(Fighter f)
    {
        fighter = f;
        portrait.Show(f.Data);
        nameText.text = f.Data.displayName;
        Update();
    }

    private void Update()
    {
        if (fighter == null) return;
        float frac = fighter.IsAlive ? fighter.Health.Fraction : 0f;
        hpFill.fillAmount = frac;
        hpFill.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.4f), frac);
        hpText.text = fighter.IsAlive ? $"{Mathf.CeilToInt(fighter.Health.Current)}" : "выбыл";
        if (energyFill != null && fighter.Combat.MaxEnergy > 0) energyFill.fillAmount = fighter.Combat.Energy / fighter.Combat.MaxEnergy;
        if (group != null) group.alpha = fighter.IsAlive ? 1f : 0.45f;
    }
}
