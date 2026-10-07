using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Окно МОИ ГЕРОИ — нанятые герои игрока.
/// На карточке: уровень и статус (на базе / в команде / на задании).
/// Клик открывает подробную карточку героя, где его можно прокачать.
/// </summary>
public class MyHeroesWindowUI : WindowUI
{
    [Header("МОИ ГЕРОИ")]
    [SerializeField] private TMP_Text infoText;          // "Героев: 2 / 4"
    [SerializeField] private TMP_Text emptyText;         // "Пока никого нет..."
    [SerializeField] private Transform grid;             // Куда складывать карточки
    [SerializeField] private HeroCardUI cardTemplate;    // Шаблон карточки (выключен)
    [SerializeField] private HeroInfoWindowUI heroInfo;  // Подробная карточка героя

    private readonly List<HeroCardUI> cards = new List<HeroCardUI>(); // Созданные карточки

    /// <summary>Прячем шаблон.</summary>
    protected override void Awake()
    {
        base.Awake();
        cardTemplate.gameObject.SetActive(false);
    }

    /// <summary>Подписываемся на изменения героев и базы.</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnHeroesChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed += RefreshIfOpen;
    }

    /// <summary>Отписываемся.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnHeroesChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed -= RefreshIfOpen;
    }

    private void OnHeroesChanged(Team team)
    {
        if (team == Team.Player) RefreshIfOpen();
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    /// <summary>Пересоздать карточки нанятых героев.</summary>
    public override void Refresh()
    {
        HeroManager hm = HeroManager.Instance;
        IReadOnlyList<HeroInstance> heroes = hm.GetHeroes(Team.Player);
        infoText.text = $"Героев: {heroes.Count} / {hm.GetHeroLimit(Team.Player)}";
        emptyText.gameObject.SetActive(heroes.Count == 0);

        foreach (HeroCardUI c in cards) Destroy(c.gameObject);
        cards.Clear();

        foreach (HeroInstance hero in heroes)
        {
            HeroCardUI card = Instantiate(cardTemplate, grid);
            card.gameObject.SetActive(true);
            HeroInstance captured = hero; // копия для лямбды
            card.Setup(hero.Data, $"{UnitClasses.Name(hero.Data.unitClass)}  •  Ур. {hero.Level}  •  {hero.StatusText}", "ПОДРОБНЕЕ", true,
                () => heroInfo.ShowHero(captured.Data), () => heroInfo.ShowHero(captured.Data));
            cards.Add(card);
        }
    }
}
