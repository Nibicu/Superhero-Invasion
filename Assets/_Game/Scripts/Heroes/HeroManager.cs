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
    [SerializeField] private int baseHeroLimit = 6;
    [Tooltip("Больше этого героев не бывает никогда (со всеми постройками и бонусами)")]
    [SerializeField] private int maxHeroLimit = 20;
    [Tooltip("Максимальный уровень героя")]
    [SerializeField] private int maxHeroLevel = 5;
    [Tooltip("Сколько плутония стоит повышение звезды (на 5 уровне, один раз)")]
    [SerializeField] private int starUpPlutoniumCost = 150;
    [Tooltip("Сколько фаз побеждённый герой ждёт, прежде чем его можно восстановить")]
    [SerializeField] private int defeatCooldownPhases = 2;

    private readonly List<HeroInstance> playerHeroes = new List<HeroInstance>(); // Нанятые герои игрока
    private readonly List<HeroInstance> enemyHeroes = new List<HeroInstance>();  // Нанятые злодеи врага
    private int playerExtraLimit; // Доп. места для героев игрока (Военная база и т.п.)
    private int enemyExtraLimit;  // То же для врага

    /// <summary>Изменился список героев стороны или их уровень/статус.</summary>
    public event Action<Team> HeroesChanged;

    /// <summary>Максимальный уровень героя.</summary>
    public int MaxHeroLevel => maxHeroLevel;

    /// <summary>Запоминаем себя. Если игру начали из меню (Песочница) — сторона игрока из выбора в меню.</summary>
    private void Awake()
    {
        Instance = this;
        if (GameSetup.IsSet) playerSide = GameSetup.PlayerSide;
    }

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
                if (h != null && h.side == side && h.hireable) list.Add(h);
        list.Sort((a, b) => a.stars != b.stars ? a.stars.CompareTo(b.stars) : a.hireCost.CompareTo(b.hireCost));
        return list;
    }

    /// <summary>Нанят ли уже этот герой.</summary>
    public bool IsHired(Team team, HeroData data)
    {
        foreach (HeroInstance h in GetHeroes(team))
            if (h.Data == data) return true;
        foreach (DefeatedHero d in GetDefeated(team))
            if (d.hero.Data == data) return true; // побеждённого героя заново не нанять — только восстановить
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
        return Mathf.Min(maxHeroLimit, baseHeroLimit + bonus + extra);
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
        if (PhaseManager.Instance != null && !PhaseManager.Instance.CanHire) { reason = "Только в паузе"; return false; }
        foreach (DefeatedHero d in GetDefeated(team))
            if (d.hero.Data == data) { reason = "Побеждён"; return false; }
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
            else if (error == "Только в паузе") error = "Нанимать героев можно только на драфте и в тактической паузе";
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

    /// <summary>Лидер стороны — первый герой в иерархии (пока ничего не даёт, понадобится позже).</summary>
    public HeroInstance GetLeader(Team team)
    {
        var list = team == Team.Player ? playerHeroes : enemyHeroes;
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// Герой в запасе ("не в команде"): его место в иерархии дальше лимита героев.
    /// Запасного героя нельзя отправить на задание и он не защищает базу; поставьте его выше
    /// (поменяйте местами с другим героем) или дождитесь, пока лимит вырастет.
    /// </summary>
    public bool IsReserve(HeroInstance hero)
    {
        var list = hero.Owner == Team.Player ? playerHeroes : enemyHeroes;
        int index = list.IndexOf(hero);
        return index >= GetHeroLimit(hero.Owner);
    }

    /// <summary>
    /// Подарить стороне случайного героя её стороны (не больше maxStars звёзд, которого ещё нет).
    /// Даётся даже сверх лимита — тогда герой в запасе. Вернёт героя (или null, если дарить некого).
    /// </summary>
    public HeroInstance GrantRandomHero(Team team, int maxStars)
    {
        var options = new List<HeroData>();
        foreach (HeroData d in GetRoster(team))
            if (d.stars <= maxStars && !IsHired(team, d)) options.Add(d);
        if (options.Count == 0) return null;
        HeroData pick = options[UnityEngine.Random.Range(0, options.Count)];
        var hero = new HeroInstance(pick, team);
        (team == Team.Player ? playerHeroes : enemyHeroes).Add(hero);
        HeroesChanged?.Invoke(team);
        return hero;
    }

    /// <summary>
    /// Иерархия: переставить героя на место newIndex в списке стороны (порядок в ряду героев).
    /// </summary>
    public void MoveHero(Team team, HeroInstance hero, int newIndex)
    {
        var list = team == Team.Player ? playerHeroes : enemyHeroes;
        if (!list.Remove(hero)) return;
        list.Insert(Mathf.Clamp(newIndex, 0, list.Count), hero);
        HeroesChanged?.Invoke(team);
    }

    // ---------- Опыт, уровень и звезда ----------

    /// <summary>Сколько плутония стоит повышение звезды.</summary>
    public int StarUpCost => starUpPlutoniumCost;

    /// <summary>
    /// Дать герою опыт (за задания и бои). Каждые 100 опыта — новый уровень (до максимума).
    /// </summary>
    public void GiveXp(HeroInstance hero, int amount)
    {
        if (hero == null) return;
        int gained = hero.AddXp(amount, maxHeroLevel);
        if (gained > 0 && hero.Owner == Team.Player)
            ToastUI.Show(hero.Level >= maxHeroLevel
                ? $"{hero.Data.displayName} достиг {hero.Level} уровня! В паузе можно повысить звезду"
                : $"{hero.Data.displayName} достиг {hero.Level} уровня!");
        HeroesChanged?.Invoke(hero.Owner);
    }

    /// <summary>Готов ли герой повысить звезду (5 уровень, звезда ещё не повышена) — без учёта паузы и плутония.</summary>
    public bool StarReady(HeroInstance hero) => hero.Level >= maxHeroLevel && !hero.StarUpgraded;

    /// <summary>Можно ли повысить звезду сейчас. Если нельзя — reason объясняет почему.</summary>
    public bool CanStarUp(HeroInstance hero, out string reason)
    {
        reason = null;
        if (hero.StarUpgraded) { reason = "Звезда уже повышена"; return false; }
        if (hero.Level < maxHeroLevel) { reason = $"Нужен {maxHeroLevel} уровень"; return false; }
        if (PhaseManager.Instance != null && !PhaseManager.Instance.CanBuild) { reason = "Только в паузе"; return false; }
        return true;
    }

    /// <summary>
    /// Повысить звезду за плутоний: характеристики как у героя с этим числом звёзд
    /// (3★ становится 3.5★ и получает +10%). Только один раз.
    /// </summary>
    public bool TryStarUp(HeroInstance hero, out string error)
    {
        if (!CanStarUp(hero, out error)) return false;
        if (!ResourceManager.Instance.TrySpend(hero.Owner, 0, starUpPlutoniumCost)) { error = "Не хватает плутония"; return false; }
        hero.UpgradeStar();
        HeroesChanged?.Invoke(hero.Owner);
        return true;
    }

    // ---------- Жизни и побеждённые герои ----------

    /// <summary>Побеждённый герой: ждёт восстановления.</summary>
    public class DefeatedHero
    {
        public HeroInstance hero;  // Сам герой (уровень, опыт, звезда сохраняются)
        public int phasesLeft;     // Сколько фаз ещё ждать
    }

    private readonly List<DefeatedHero> playerDefeated = new List<DefeatedHero>(); // Побеждённые герои игрока
    private readonly List<DefeatedHero> enemyDefeated = new List<DefeatedHero>();  // Побеждённые герои врага

    /// <summary>Побеждённые герои стороны.</summary>
    public IReadOnlyList<DefeatedHero> GetDefeated(Team team) => team == Team.Player ? playerDefeated : enemyDefeated;

    /// <summary>
    /// Герой выбыл в бою: минус жизнь. Жизней не осталось — герой побеждён: пропадает из списка героев,
    /// его вещи уходят в инвентарь, место в лимите освобождается; восстановить его можно через 2 фазы за двойную цену.
    /// </summary>
    public void LoseLife(HeroInstance hero)
    {
        if (hero == null || hero.Lives <= 0) return;
        hero.Lives--;
        if (hero.Lives > 0)
        {
            if (hero.Owner == Team.Player) ToastUI.Show($"{hero.Data.displayName} выбыл в бою — осталось жизней: {hero.Lives}");
            HeroesChanged?.Invoke(hero.Owner);
            return;
        }
        var list = hero.Owner == Team.Player ? playerHeroes : enemyHeroes;
        list.Remove(hero);
        if (ArtifactManager.Instance != null) ArtifactManager.Instance.ReturnAll(hero);
        (hero.Owner == Team.Player ? playerDefeated : enemyDefeated).Add(new DefeatedHero { hero = hero, phasesLeft = defeatCooldownPhases });
        if (hero.Owner == Team.Player) ToastUI.Show($"{hero.Data.displayName} побеждён! Восстановить можно через {defeatCooldownPhases} фазы");
        HeroesChanged?.Invoke(hero.Owner);
    }

    /// <summary>Началась новая фаза — побеждённые герои на шаг ближе к восстановлению.</summary>
    public void OnPhaseStarted()
    {
        foreach (List<DefeatedHero> list in new[] { playerDefeated, enemyDefeated })
            foreach (DefeatedHero d in list)
                if (d.phasesLeft > 0) d.phasesLeft--;
        HeroesChanged?.Invoke(Team.Player);
    }

    /// <summary>Цена восстановления побеждённого героя (двойная цена найма).</summary>
    public int RestoreCost(DefeatedHero d) => d.hero.Data.hireCost * 2;

    /// <summary>Можно ли восстановить героя. Если нельзя — reason объясняет почему.</summary>
    public bool CanRestore(DefeatedHero d, out string reason)
    {
        reason = null;
        if (d.phasesLeft > 0) { reason = $"Через {d.phasesLeft} ф."; return false; }
        if (PhaseManager.Instance != null && !PhaseManager.Instance.CanHire) { reason = "Только в паузе"; return false; }
        if (GetHeroes(d.hero.Owner).Count >= GetHeroLimit(d.hero.Owner)) { reason = "Нет мест"; return false; }
        return true;
    }

    /// <summary>Восстановить побеждённого героя: 3 жизни, полное здоровье, уровень/опыт/звезда — как были.</summary>
    public bool TryRestore(DefeatedHero d, out string error)
    {
        if (!CanRestore(d, out error)) return false;
        if (!ResourceManager.Instance.TrySpend(d.hero.Owner, RestoreCost(d))) { error = "Не хватает золота"; return false; }
        Team team = d.hero.Owner;
        (team == Team.Player ? playerDefeated : enemyDefeated).Remove(d);
        d.hero.Lives = HeroInstance.MaxLives;
        d.hero.HpFraction = 1f;
        d.hero.Status = HeroStatus.Free;
        (team == Team.Player ? playerHeroes : enemyHeroes).Add(d.hero);
        HeroesChanged?.Invoke(team);
        return true;
    }

    /// <summary>Сообщить, что у героя поменялся статус (вызывают команды на Шаге 4).</summary>
    public void NotifyChanged(Team team) => HeroesChanged?.Invoke(team);
}
