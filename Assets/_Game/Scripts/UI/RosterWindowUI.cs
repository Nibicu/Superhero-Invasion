using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Окно РОСТЕР — все герои нашей стороны, которых можно нанять.
/// Сверху — сколько героев нанято из лимита и до скольких звёзд можно нанимать.
/// У каждого героя кнопка "НАНЯТЬ" (или причина, почему нельзя).
/// </summary>
public class RosterWindowUI : WindowUI
{
    [Header("РОСТЕР")]
    [SerializeField] private TMP_Text infoText;          // "Героев: 2 / 4 • Найм до 1 звезды"
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

    /// <summary>Подписываемся на изменения денег, героев и базы — чтобы окно было актуальным.</summary>
    private void Start()
    {
        ResourceManager.Instance.ResourcesChanged += OnTeamChanged;
        HeroManager.Instance.HeroesChanged += OnTeamChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed += RefreshIfOpen;
    }

    /// <summary>Отписываемся.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourcesChanged -= OnTeamChanged;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnTeamChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed -= RefreshIfOpen;
    }

    private void OnTeamChanged(Team team)
    {
        if (team == Team.Player) RefreshIfOpen();
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    /// <summary>Пересоздать карточки всех героев.</summary>
    public override void Refresh()
    {
        HeroManager hm = HeroManager.Instance;
        int maxStars = hm.GetMaxStars(Team.Player);
        infoText.text =
            $"Героев: {hm.GetHeroes(Team.Player).Count} / {hm.GetHeroLimit(Team.Player)}   •   " +
            $"Найм до {(maxStars == 1 ? "1 звезды" : maxStars + " звёзд")}";

        foreach (HeroCardUI c in cards) Destroy(c.gameObject);
        cards.Clear();

        foreach (HeroData data in hm.GetRoster(Team.Player))
        {
            HeroCardUI card = Instantiate(cardTemplate, grid);
            card.gameObject.SetActive(true);

            bool canHire = hm.CanHire(Team.Player, data, out string reason);
            bool afford = ResourceManager.Instance.CanAfford(Team.Player, data.hireCost);
            string costColor = afford ? "#FFD84A" : "#FF6B6B";
            string info = hm.IsHired(Team.Player, data)
                ? "<color=#6EE07A>В вашей команде</color>"
                : $"<color={costColor}>{data.hireCost} золота</color>";
            info = $"<color=#C9D3E6>{UnitClasses.Name(data.unitClass)}</color>  •  {info}"; // класс героя
            string action = canHire ? "НАНЯТЬ" : reason.ToUpper();

            HeroData captured = data; // копия для лямбды
            card.Setup(data, info, action, canHire, () => Hire(captured), () => heroInfo.ShowHero(captured));
            cards.Add(card);
        }
    }

    /// <summary>Нажата кнопка "НАНЯТЬ".</summary>
    private void Hire(HeroData data)
    {
        if (HeroManager.Instance.TryHire(Team.Player, data, out string error))
            ToastUI.Show($"{data.displayName} теперь в бюро!");
        else
            ToastUI.Show(error);
    }
}
