using System.Collections.Generic;
using UnityEngine;

/// <summary>Чем сейчас занята команда.</summary>
public enum SquadStatus
{
    AtBase,    // Стоит на базе, готова к заданию
    Moving,    // Едет к объекту или миссии (Шаг 5)
    Capturing, // Захватывает объект (идёт таймер)
    OnMission, // Выполняет миссию (идёт таймер)
    Returning  // Возвращается на базу
}

/// <summary>
/// Команда героев: капитан + до 4 героев.
/// Именно команды отправляются на захват объектов и миссии.
/// Создаётся и меняется только через SquadManager.
/// </summary>
public class Squad
{
    /// <summary>Номер команды (1..5) — показывается на аватарке.</summary>
    public int Number { get; set; }

    /// <summary>Чья команда.</summary>
    public Team Owner { get; }

    /// <summary>Капитан (обязателен). Его портрет — аватарка команды.</summary>
    public HeroInstance Captain { get; private set; }

    /// <summary>Остальные герои (0–4, без капитана).</summary>
    public IReadOnlyList<HeroInstance> Members => members;

    /// <summary>Чем занята команда.</summary>
    public SquadStatus Status { get; set; } = SquadStatus.AtBase;

    /// <summary>Доля здоровья команды 0..1 (пока битв нет — всегда 1).</summary>
    public float HpFraction { get; set; } = 1f;

    private readonly List<HeroInstance> members = new List<HeroInstance>(); // Герои кроме капитана

    /// <summary>Создать команду.</summary>
    public Squad(Team owner, HeroInstance captain, IEnumerable<HeroInstance> heroes)
    {
        Owner = owner;
        SetHeroes(captain, heroes);
    }

    /// <summary>Заменить состав (проверки делает SquadManager).</summary>
    public void SetHeroes(HeroInstance captain, IEnumerable<HeroInstance> heroes)
    {
        Captain = captain;
        members.Clear();
        if (heroes != null)
            foreach (HeroInstance h in heroes)
                if (h != null && h != captain) members.Add(h);
    }

    /// <summary>Все герои команды: капитан первым, потом остальные.</summary>
    public IEnumerable<HeroInstance> AllHeroes
    {
        get
        {
            if (Captain != null) yield return Captain;
            foreach (HeroInstance h in members) yield return h;
        }
    }

    /// <summary>Сколько героев в команде (с капитаном).</summary>
    public int Size => (Captain != null ? 1 : 0) + members.Count;

    /// <summary>Максимальное здоровье команды — сумма HP всех героев.</summary>
    public int MaxHp
    {
        get
        {
            int sum = 0;
            foreach (HeroInstance h in AllHeroes) sum += h.Stats.hp;
            return sum;
        }
    }

    /// <summary>Текущее здоровье команды.</summary>
    public int CurrentHp => Mathf.RoundToInt(MaxHp * HpFraction);

    /// <summary>
    /// "Сила" команды — сумма сил всех героев (формула — BattleCalculator.UnitPower).
    /// </summary>
    public int Power
    {
        get
        {
            int sum = 0;
            foreach (HeroInstance h in AllHeroes) sum += BattleCalculator.HeroPower(h);
            return sum;
        }
    }

    /// <summary>Статус словами для интерфейса.</summary>
    public string StatusText
    {
        get
        {
            switch (Status)
            {
                case SquadStatus.Moving: return "<color=#FFB84A>В пути</color>";
                case SquadStatus.Capturing: return "<color=#C58BFF>Захват</color>";
                case SquadStatus.OnMission: return "<color=#FFD84A>Миссия</color>";
                case SquadStatus.Returning: return "<color=#7FB8FF>Возврат</color>";
                default: return "<color=#6EE07A>На базе</color>";
            }
        }
    }
}
