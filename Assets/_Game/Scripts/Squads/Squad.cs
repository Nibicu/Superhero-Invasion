using System.Collections.Generic;
using UnityEngine;

/// <summary>Чем сейчас занята команда.</summary>
public enum SquadStatus
{
    AtBase,    // Стоит на базе, готова к заданию
    Moving,    // Едет к объекту или миссии (Шаг 5)
    Capturing, // Захватывает объект (идёт таймер)
    OnMission, // Выполняет миссию (идёт таймер)
    Defending, // Защищает свой объект от нападения
    Returning  // Возвращается на базу
}

/// <summary>
/// Команда героев: капитан + до 3 героев.
/// Собирается, когда игрок отправляет героев к цели (объект, миссия, событие),
/// и распадается, когда возвращается на базу. Создаётся и меняется только через SquadManager.
/// </summary>
public class Squad
{
    /// <summary>Номер команды (1..N) — показывается на фишке.</summary>
    public int Number { get; set; }

    /// <summary>Чья команда.</summary>
    public Team Owner { get; }

    /// <summary>Капитан (обязателен). Его портрет — аватарка команды.</summary>
    public HeroInstance Captain { get; private set; }

    /// <summary>Остальные герои (0–3, без капитана).</summary>
    public IReadOnlyList<HeroInstance> Members => members;

    /// <summary>Чем занята команда.</summary>
    public SquadStatus Status { get; set; } = SquadStatus.AtBase;

    /// <summary>
    /// Доля здоровья команды 0..1 — среднее здоровье героев (по их макс. HP).
    /// Если задать — у всех героев команды станет такая доля здоровья.
    /// </summary>
    public float HpFraction
    {
        get
        {
            float cur = 0f, max = 0f;
            foreach (HeroInstance h in AllHeroes) { max += h.Stats.hp; cur += h.Stats.hp * h.HpFraction; }
            return max > 0f ? cur / max : 1f;
        }
        set
        {
            foreach (HeroInstance h in AllHeroes) h.HpFraction = Mathf.Clamp01(value);
        }
    }

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
                case SquadStatus.Defending: return "<color=#6EC8FF>Защита</color>";
                case SquadStatus.Returning: return "<color=#7FB8FF>Возврат</color>";
                default: return "<color=#6EE07A>На базе</color>";
            }
        }
    }
}
