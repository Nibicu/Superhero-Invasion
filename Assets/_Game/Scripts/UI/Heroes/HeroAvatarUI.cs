using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Аватарка купленного героя: портрет, полоска здоровья и подпись, где он сейчас.
/// Герой на задании — аватарка затемнена. Можно подсветить как выбранную (выбор защитников базы).
/// </summary>
public class HeroAvatarUI : MonoBehaviour
{
    [SerializeField] private HeroPortraitUI portrait; // Портрет
    [SerializeField] private Image hpFill;            // Заполнение полоски здоровья (Filled)
    [SerializeField] private Image dim;               // Затемнение (герой на задании)
    [SerializeField] private Image selection;         // Рамка выбора (выбор защитников)
    [SerializeField] private TMP_Text statusText;     // "В пути" / номер команды
    [SerializeField] private Button button;           // Клик по аватарке
    [SerializeField] private TMP_Text livesText;      // Сколько жизней (рядом со значком сердца)

    private bool reserve; // Герой в запасе ("не в команде")

    /// <summary>Герой этой аватарки.</summary>
    public HeroInstance Hero { get; private set; }

    /// <summary>Отметить героя как запасного ("не в команде").</summary>
    public void SetReserve(bool on)
    {
        reserve = on;
        UpdateState();
    }

    /// <summary>Заполнить аватарку. clicked — что сделать по клику.</summary>
    public void Setup(HeroInstance hero, Action clicked)
    {
        Hero = hero;
        portrait.Show(hero.Data);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clicked?.Invoke());
        SetSelected(false);
        UpdateState();
    }

    /// <summary>Обновить здоровье, затемнение и подпись.</summary>
    public void UpdateState()
    {
        if (Hero == null) return;
        hpFill.fillAmount = Hero.HpFraction;
        hpFill.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.4f), Hero.HpFraction);
        if (livesText != null) livesText.text = Hero.Lives.ToString();
        bool away = !Hero.IsAtHome;
        dim.gameObject.SetActive(away || reserve);
        if (statusText != null)
        {
            statusText.gameObject.SetActive(away || reserve);
            statusText.text = reserve ? "ЗАПАС" : "В ПУТИ";
        }
    }

    /// <summary>Подсветить аватарку как выбранную.</summary>
    public void SetSelected(bool on)
    {
        if (selection != null) selection.gameObject.SetActive(on);
    }
}
