using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно "НАПАДЕНИЕ НА БАЗУ!": враг приехал к нашей базе, а дома больше 4 героев —
/// игрок выбирает, кто будет защищать (до 4). Пока окно открыто, карта стоит на паузе.
/// Сразу выбраны первые 4 по иерархии; клик по аватарке — выбрать / убрать.
/// </summary>
public class DefenderPickerUI : WindowUI
{
    /// <summary>Единственное окно в сцене.</summary>
    public static DefenderPickerUI Instance { get; private set; }

    [Header("Выбор защитников")]
    [SerializeField] private TMP_Text countText;      // "Выбрано: 3 / 4"
    [SerializeField] private Transform listParent;    // Сетка аватарок
    [SerializeField] private HeroAvatarUI template;   // Шаблон аватарки (выключен)
    [SerializeField] private Button confirmButton;    // "К БОЮ!"

    private MainBase home;                                                  // Какую базу защищаем
    private readonly List<HeroInstance> selected = new List<HeroInstance>(); // Выбранные защитники
    private readonly List<HeroAvatarUI> avatars = new List<HeroAvatarUI>();  // Созданные аватарки
    private bool pausedByMe;                                                // Окно поставило карту на паузу

    /// <summary>Запомнить себя, подписать кнопку.</summary>
    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        template.gameObject.SetActive(false);
        confirmButton.onClick.AddListener(Close);
    }

    /// <summary>Убрать ссылку.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    /// <summary>Открыть выбор защитников базы (сразу выбраны первые 4 по иерархии). Карта — на паузе.</summary>
    public void OpenFor(MainBase b)
    {
        home = b;
        selected.Clear();
        foreach (HeroInstance h in SquadManager.Instance.GetHeroesAtHome(Team.Player))
            if (selected.Count < MainBase.MaxDefenders) selected.Add(h);
        home.SetDefenders(selected);
        if (!WorldTime.Paused) { WorldTime.Paused = true; pausedByMe = true; }
        Open();
    }

    /// <summary>Закрыть: выбор остаётся, карта снова идёт.</summary>
    public override void Close()
    {
        base.Close();
        if (home != null) home.SetDefenders(selected);
        if (pausedByMe) { WorldTime.Paused = false; pausedByMe = false; }
    }

    /// <summary>Пересоздать аватарки героев, которые дома.</summary>
    public override void Refresh()
    {
        foreach (HeroAvatarUI a in avatars) Destroy(a.gameObject);
        avatars.Clear();
        foreach (HeroInstance h in SquadManager.Instance.GetHeroesAtHome(Team.Player))
        {
            HeroAvatarUI a = Instantiate(template, listParent);
            a.gameObject.SetActive(true);
            HeroInstance captured = h;
            a.Setup(h, () => Toggle(captured));
            a.SetSelected(selected.Contains(h));
            avatars.Add(a);
        }
        countText.text = $"Выбрано: {selected.Count} / {MainBase.MaxDefenders}";
        confirmButton.interactable = selected.Count > 0;
    }

    /// <summary>Выбрать или убрать защитника (не больше 4).</summary>
    private void Toggle(HeroInstance h)
    {
        if (selected.Contains(h)) selected.Remove(h);
        else if (selected.Count < MainBase.MaxDefenders) selected.Add(h);
        else { ToastUI.Show($"Защитников не больше {MainBase.MaxDefenders} — сначала уберите кого-нибудь"); return; }
        if (home != null) home.SetDefenders(selected);
        Refresh();
    }
}
