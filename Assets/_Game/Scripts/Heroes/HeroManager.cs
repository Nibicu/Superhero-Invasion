using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет героями обеих сторон: список доступных для найма (РОСТЕР),
/// нанятые герои (МОИ ГЕРОИ), лимит героев и прокачка.
///
/// Правила найма (GDD):
/// - игрок за супергероев нанимает только героев, враг — только злодеев;
/// - без Бараков — только 1 звезда, Бараки ур.1 — до 2 звёзд, ур.2 — до 3;
/// - лимит героев = стартовый (4) + Резервные комнаты + доп. бонусы (Военная база);
/// - каждого героя можно нанять только один раз.
/// </summary>
[DefaultExecutionOrder(-90)]
public class HeroManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр — доступен из любого скрипта.</summary>
    public static HeroManager Instance { get; private set; }

    [Header("Все герои и злодеи игры")]
    [Tooltip("Сюда добавлять новых героев (файлы HeroData)")]
    [SerializeField] private HeroData[] heroPool;

    [Header("Стороны")]
    [Tooltip("За кого играет игрок. Враг автоматически получает противоположную сторону")]
    [SerializeField] private Side playerSide = Side.Heroes;

    [Header("Лимиты и прокачка")]
    [Tooltip("Сколько героев можно держать в начале игры (без Резервных комнат)")]
    [SerializeField] private int baseHeroLimit = 4;
    [Tooltip("Максимальный уровень героя")]
    [SerializeField] private int maxHeroLevel = 5;
    [Tooltip("Цена прокачки = это число × звёзды × текущий уровень")]
    [SerializeField] private int levelUpCostPerStar = 150;

    [Header("Усиление в Институте (за плутоний)")]
    [Tooltip("Сколько раз можно усилить одного героя")]
    [SerializeField] private int maxBoosts = 3;
    [Tooltip("Цена усиления = это число × (сколько усилений уже было + 1)")]
    [SerializeField] private int boostPlutoniumCost = 20;

    private readonly List<HeroInstance> playerHeroes = new List<HeroInstance>(); // Нанятые герои игрока
    private readonly List<HeroInstance> enemyHeroes = new List<HeroInstance>();  // Нанятые злодеи врага
    private int playerExtraLimit; // Доп. места для героев игрока (Военная база и т.п.)
    private int enemyExtraLimit;  // То же для врага

    /// <summary>Изменился список героев стороны или их уровень/статус.</summary>
    public event Action<Team> HeroesChanged;

    /// <summary>Максимальный уровень героя.</summary>
    public int MaxHeroLevel => maxHeroLevel;

    /// <summary>Запоминаем себя.</summary>
    private void Awake() => Instance = this;

    /// <summary>Убираем ссылку при выходе.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Списки ----------

    /// <summary>За какую сторону конфликта играет команда.</summary>
    public Side GetSide(Team team)
    {
        if (team == Team.Player) return playerSide;
        return playerSide == Side.Heroes ? Side.Villains : Side.Heroes;
    }

    /// <summary>Нанятые герои стороны.</summary>
    public IReadOnlyList<HeroInstance> GetHeroes(Team team) => team == Team.Player ? playerHeroes : enemyHeroes;

    /// <summary>
    /// РОСТЕР — все герои нужной стороны (и нанятые, и нет),
    /// отсортированы по звёздам, потом по цене.
    /// </summary>
    public List<HeroData> GetRoster(Team team)
    {
        Side side = GetSide(team);
        var list = new List<HeroData>();
        if (heroPool != null)
            foreach (HeroData h in heroPool)
                if (h != null && h.side == side) list.Add(h);
        list.Sort((a, b) => a.stars != b.stars ? a.stars.CompareTo(b.stars) : a.hireCost.CompareTo(b.hireCost));
        return list;
    }

    /// <summary>Нанят ли уже этот герой.</summary>
    public bool IsHired(Team team, HeroData data)
    {
        foreach (HeroInstance h in GetHeroes(team))
            if (h.Data == data) return true;
        return false;
    }

    /// <summary>Найти нанятого героя по его данным (или null).</summary>
    public HeroInstance FindHero(Team team, HeroData data)
    {
        foreach (HeroInstance h in GetHeroes(team))
            if (h.Data == data) return h;
        return null;
    }

    // ---------- Лимит героев ----------

    /// <summary>Сколько героев сторона может держать одновременно.</summary>
    public int GetHeroLimit(Team team)
    {
        MainBase b = MainBase.Get(team);
        int bonus = b != null ? b.HeroCapacityBonus : 0;
        int extra = team == Team.Player ? playerExtraLimit : enemyExtraLimit;
        return baseHeroLimit + bonus + extra;
    }

    /// <summary>Добавить места для героев (Военная база на Шаге 5). Можно передать минус, чтобы забрать.</summary>
    public void AddExtraLimit(Team team, int amount)
    {
        if (team == Team.Player) playerExtraLimit += amount;
        else enemyExtraLimit += amount;
        HeroesChanged?.Invoke(team);
    }

    /// <summary>Героев скольких звёзд сторона может нанимать (зависит от Бараков).</summary>
    public int GetMaxStars(Team team)
    {
        MainBase b = MainBase.Get(team);
        return b != null ? b.MaxHeroStars : 1;
    }

    // ---------- Найм ----------

    /// <summary>
    /// Можно ли нанять героя (без учёта денег).
    /// Если нельзя — reason коротко объясняет почему (для кнопки).
    /// </summary>
    public bool CanHire(Team team, HeroData data, out string reason)
    {
        reason = null;
        if (data.side != GetSide(team)) { reason = "Чужая сторона"; return false; }
        if (IsHired(team, data)) { reason = "Нанят"; return false; }
        if (data.stars > GetMaxStars(team))
        {
            reason = GetMaxStars(team) <= 1 ? "Нужны Бараки" : "Нужны Бараки ур. 2";
            return false;
        }
        if (GetHeroes(team).Count >= GetHeroLimit(team)) { reason = "Нет мест"; return false; }
        return true;
    }

    /// <summary>Нанять героя: проверка правил, оплата, добавление в МОИ ГЕРОИ.</summary>
    public bool TryHire(Team team, HeroData data, out string error)
    {
        if (!CanHire(team, data, out error))
        {
            if (error == "Нет мест") error = "Достигнут лимит героев — постройте Резервные комнаты";
            return false;
        }
        if (!ResourceManager.Instance.TrySpend(team, data.hireCost))
        {
            error = "Не хватает золота";
            return false;
        }
        var list = team == Team.Player ? playerHeroes : enemyHeroes;
        list.Add(new HeroInstance(data, team));
        HeroesChanged?.Invoke(team);
        return true;
    }

    // ---------- Прокачка ----------

    /// <summary>Цена прокачки героя до следующего уровня (0 — если уже максимум).</summary>
    public int GetLevelUpCost(HeroInstance hero)
    {
        if (hero.Level >= maxHeroLevel) return 0;
        return levelUpCostPerStar * hero.Data.stars * hero.Level;
    }

    /// <summary>Прокачать героя на 1 уровень за золото.</summary>
    public bool TryLevelUp(HeroInstance hero, out string error)
    {
        error = null;
        if (hero.Level >= maxHeroLevel) { error = "Максимальный уровень"; return false; }
        if (!ResourceManager.Instance.TrySpend(hero.Owner, GetLevelUpCost(hero)))
        {
            error = "Не хватает золота";
            return false;
        }
        hero.LevelUp();
        HeroesChanged?.Invoke(hero.Owner);
        return true;
    }

    // ---------- Усиление (Институт ядерной физики) ----------

    /// <summary>Сколько раз можно усилить героя.</summary>
    public int MaxBoosts => maxBoosts;

    /// <summary>Цена следующего усиления в плутонии.</summary>
    public int GetBoostCost(HeroInstance hero) => boostPlutoniumCost * (hero.Boosts + 1);

    /// <summary>Можно ли усилить героя (без учёта плутония). reason — почему нельзя.</summary>
    public bool CanBoost(HeroInstance hero, out string reason)
    {
        reason = null;
        if (!MapObject.TeamCanBoostHeroes(hero.Owner)) { reason = "Нужен Институт"; return false; }
        if (hero.Boosts >= maxBoosts) { reason = "Макс. усиление"; return false; }
        return true;
    }

    /// <summary>Усилить героя за плутоний: +10% ко всем характеристикам.</summary>
    public bool TryBoost(HeroInstance hero, out string error)
    {
        if (!CanBoost(hero, out error))
        {
            if (error == "Нужен Институт") error = "Захватите Институт ядерной физики";
            return false;
        }
        if (!ResourceManager.Instance.TrySpend(hero.Owner, 0, GetBoostCost(hero)))
        {
            error = "Не хватает плутония";
            return false;
        }
        hero.AddBoost();
        HeroesChanged?.Invoke(hero.Owner);
        return true;
    }

    /// <summary>Сообщить, что у героя поменялся статус (вызывают команды на Шаге 4).</summary>
    public void NotifyChanged(Team team) => HeroesChanged?.Invoke(team);
}
