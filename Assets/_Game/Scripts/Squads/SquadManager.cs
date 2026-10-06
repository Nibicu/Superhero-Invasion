using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет командами обеих сторон.
/// Правила (GDD): максимум 5 команд, в команде капитан + до 4 героев,
/// можно и одного капитана. Один герой — только в одной команде.
/// Менять состав и распускать можно только команду, стоящую на базе.
/// </summary>
[DefaultExecutionOrder(-80)]
public class SquadManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр — доступен из любого скрипта.</summary>
    public static SquadManager Instance { get; private set; }

    [Tooltip("Сколько команд можно создать")]
    [SerializeField] private int maxSquads = 5;
    [Tooltip("Сколько героев в команде кроме капитана")]
    [SerializeField] private int maxMembers = 4;

    [Header("Лечение")]
    [Tooltip("Какую долю здоровья команда восстанавливает за секунду, стоя на базе (0.02 = 2%)")]
    [SerializeField] private float healPerSecond = 0.02f;

    [Header("Фишки команд на карте")]
    [Tooltip("Префаб фишки команды (Assets/_Game/Prefabs/SquadToken)")]
    [SerializeField] private SquadUnit unitPrefab;
    [Tooltip("Цвет фишек игрока")]
    [SerializeField] private Color playerColor = new Color(0.18f, 0.48f, 0.88f);
    [Tooltip("Цвет фишек врага")]
    [SerializeField] private Color enemyColor = new Color(0.85f, 0.22f, 0.23f);

    private readonly List<Squad> playerSquads = new List<Squad>(); // Команды игрока
    private readonly List<Squad> enemySquads = new List<Squad>();  // Команды врага

    /// <summary>Изменился список команд стороны или состав/статус команды.</summary>
    public event Action<Team> SquadsChanged;

    public int MaxSquads => maxSquads;
    public int MaxMembers => maxMembers;

    /// <summary>Запоминаем себя.</summary>
    private void Awake() => Instance = this;

    /// <summary>Убираем ссылку при выходе.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Команды на базе понемногу лечатся (после проваленной миссии).</summary>
    private void Update()
    {
        Heal(playerSquads);
        Heal(enemySquads);
    }

    private void Heal(List<Squad> squads)
    {
        foreach (Squad s in squads)
            if (s.Status == SquadStatus.AtBase && s.HpFraction < 1f)
                s.HpFraction = Mathf.Min(1f, s.HpFraction + healPerSecond * WorldTime.DeltaTime);
    }

    /// <summary>Команды стороны (по порядку номеров).</summary>
    public IReadOnlyList<Squad> GetSquads(Team team) => List(team);

    private List<Squad> List(Team team) => team == Team.Player ? playerSquads : enemySquads;

    /// <summary>Можно ли создать ещё одну команду.</summary>
    public bool CanCreateSquad(Team team) => List(team).Count < maxSquads;

    /// <summary>Свободные герои стороны (не в команде) — из них собирают команды.</summary>
    public List<HeroInstance> GetFreeHeroes(Team team)
    {
        var list = new List<HeroInstance>();
        foreach (HeroInstance h in HeroManager.Instance.GetHeroes(team))
            if (h.Status == HeroStatus.Free) list.Add(h);
        return list;
    }

    /// <summary>Создать команду. При успехе squad — новая команда.</summary>
    public bool TryCreateSquad(Team team, HeroInstance captain, IList<HeroInstance> heroes, out Squad squad, out string error)
    {
        squad = null;
        if (!CanCreateSquad(team)) { error = $"Максимум {maxSquads} команд"; return false; }
        if (!Validate(team, captain, heroes, null, out error)) return false;

        squad = new Squad(team, captain, heroes);
        List(team).Add(squad);
        SetHeroesStatus(squad, HeroStatus.InTeam);
        Renumber(team);
        NotifyChanged(team);
        return true;
    }

    /// <summary>Изменить состав существующей команды (только на базе).</summary>
    public bool TryEditSquad(Squad squad, HeroInstance captain, IList<HeroInstance> heroes, out string error)
    {
        if (squad.Status != SquadStatus.AtBase) { error = "Команда сейчас на задании"; return false; }
        if (!Validate(squad.Owner, captain, heroes, squad, out error)) return false;

        SetHeroesStatus(squad, HeroStatus.Free);   // старый состав освобождаем
        squad.SetHeroes(captain, heroes);
        SetHeroesStatus(squad, HeroStatus.InTeam); // новый — занят
        NotifyChanged(squad.Owner);
        return true;
    }

    /// <summary>Распустить команду (только на базе). Герои становятся свободными.</summary>
    public bool TryDisband(Squad squad, out string error)
    {
        error = null;
        if (squad.Status != SquadStatus.AtBase) { error = "Команда сейчас на задании"; return false; }
        SetHeroesStatus(squad, HeroStatus.Free);
        List(squad.Owner).Remove(squad);
        Renumber(squad.Owner);
        NotifyChanged(squad.Owner);
        return true;
    }

    /// <summary>
    /// Поменять статус команды (едет, захватывает...) — вызывают объекты карты на Шаге 5.
    /// Статус героев тоже обновляется (В команде / На задании).
    /// </summary>
    public void SetStatus(Squad squad, SquadStatus status)
    {
        squad.Status = status;
        SetHeroesStatus(squad, status == SquadStatus.AtBase ? HeroStatus.InTeam : HeroStatus.OnMission);
        NotifyChanged(squad.Owner);
    }

    /// <summary>
    /// Отправить команду к цели (объект карты или миссия).
    /// Команда должна стоять на базе, а цель — принимать её.
    /// На карте появляется фишка, которая едет от базы к цели.
    /// </summary>
    public bool SendSquad(Squad squad, ISquadTarget target, out string error)
    {
        if (squad.Status != SquadStatus.AtBase) { error = "Команда сейчас не на базе"; return false; }
        if (!target.CanAccept(squad, out error)) return false;
        MainBase home = MainBase.Get(squad.Owner);
        if (home == null || unitPrefab == null) { error = "Нет базы или префаба фишки"; return false; }

        SquadUnit unit = Instantiate(unitPrefab);
        unit.name = $"Squad_{squad.Owner}_{squad.Number}";
        unit.Init(squad, home.transform.position, target, squad.Owner == Team.Player ? playerColor : enemyColor);
        SetStatus(squad, SquadStatus.Moving);
        target.OnSquadDispatched(squad);
        return true;
    }

    /// <summary>Фишка вернулась на базу — команда снова свободна (вызывает SquadUnit).</summary>
    public void OnUnitReturned(SquadUnit unit)
    {
        SetStatus(unit.Squad, SquadStatus.AtBase);
        if (unit.Squad.Owner == Team.Player)
            ToastUI.Show($"Команда {unit.Squad.Number} вернулась на базу");
    }

    /// <summary>Сообщить всем, что команды стороны изменились (обновить интерфейс).</summary>
    public void NotifyChanged(Team team)
    {
        SquadsChanged?.Invoke(team);
        HeroManager.Instance.NotifyChanged(team); // статусы героев тоже поменялись
    }

    // ---------- Внутреннее ----------

    /// <summary>
    /// Проверить состав: капитан есть, героев не больше лимита, все свои,
    /// без повторов и свободны (или уже в этой же команде при редактировании).
    /// </summary>
    private bool Validate(Team team, HeroInstance captain, IList<HeroInstance> heroes, Squad editing, out string error)
    {
        error = null;
        if (captain == null) { error = "Выберите капитана"; return false; }

        var all = new List<HeroInstance> { captain };
        int count = 0;
        if (heroes != null)
            foreach (HeroInstance h in heroes)
            {
                if (h == null) continue;
                if (all.Contains(h)) { error = "Герой выбран дважды"; return false; }
                all.Add(h);
                count++;
            }
        if (count > maxMembers) { error = $"В команде максимум {maxMembers + 1} героев"; return false; }

        foreach (HeroInstance h in all)
        {
            if (h.Owner != team) { error = "Чужой герой"; return false; }
            bool inThisSquad = editing != null && IsInSquad(editing, h);
            if (h.Status != HeroStatus.Free && !inThisSquad)
            {
                error = $"{h.Data.displayName} уже в другой команде";
                return false;
            }
        }
        return true;
    }

    /// <summary>Состоит ли герой в команде.</summary>
    private static bool IsInSquad(Squad squad, HeroInstance hero)
    {
        foreach (HeroInstance h in squad.AllHeroes)
            if (h == hero) return true;
        return false;
    }

    /// <summary>Выставить статус всем героям команды.</summary>
    private static void SetHeroesStatus(Squad squad, HeroStatus status)
    {
        foreach (HeroInstance h in squad.AllHeroes) h.Status = status;
    }

    /// <summary>Перенумеровать команды 1..N после создания/роспуска.</summary>
    private void Renumber(Team team)
    {
        List<Squad> list = List(team);
        for (int i = 0; i < list.Count; i++) list[i].Number = i + 1;
    }
}
