using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Этап нападения на объект или базу.</summary>
public enum AttackPhase
{
    None,      // Никто не нападает
    Preparing, // Команда приехала, идёт подготовка к бою (карта не стоит, можно отступить)
    Deciding,  // Игрок выбирает в окне перед боем: автобой или ручной бой
    Fighting   // Идёт ручной бой на арене
}

/// <summary>
/// "Место нападения" — общая основа для объекта карты (MapObject) и главной базы (MainBase).
/// Порядок нападения одинаковый для всех:
///  1) команда приезжает → идёт подготовка к бою (prepTime, 20 с). Можно отступить;
///  2) подготовка закончилась:
///     - защитников нет → нападающие сразу побеждают без боя;
///     - в бою участвует игрок (нападает или защищается) → окно перед боем (10 с на выбор);
///     - бьются враг и нейтральная охрана → автобой сразу;
///  3) автобой даёт итог мгновенно, ручной бой — после арены;
///  4) итог: победа (захват объекта / урон базе) или поражение, команда едет домой.
/// Пока идёт нападение, окна этого места закрыты и покупки в нём недоступны.
/// </summary>
public abstract class AttackableSite : MonoBehaviour, ISquadTarget
{
    [Header("Нападение")]
    [Tooltip("Сколько секунд идёт подготовка к бою после прибытия команды")]
    [SerializeField] private float prepTime = 20f;

    /// <summary>Нападение на какое-то место началось или закончилось (окна закрываются/обновляются).</summary>
    public static event Action<AttackableSite> AttackChanged;

    private SquadUnit attacker;  // Фишка нападающей команды (null — никто не нападает)
    private AttackPhase phase;   // Этап нападения
    private float prepLeft;      // Сколько секунд подготовки осталось

    // ---------- Свойства ----------

    /// <summary>Нападающая команда на месте (null — нападения нет).</summary>
    public SquadUnit Attacker => attacker;

    /// <summary>Идёт ли нападение (от прибытия команды до итога).</summary>
    public bool IsUnderAttack => attacker != null;

    /// <summary>Этап нападения.</summary>
    public AttackPhase Phase => phase;

    /// <summary>Чья команда нападает (имеет смысл, только если IsUnderAttack).</summary>
    public Team AttackingTeam => attacker != null ? attacker.Squad.Owner : Team.Player;

    /// <summary>Длительность подготовки к бою.</summary>
    public float PrepTime => prepTime;

    /// <summary>Сколько секунд подготовки осталось.</summary>
    public float PrepLeft => prepLeft;

    /// <summary>Прогресс подготовки 0..1 (для полоски на карте).</summary>
    public float PrepProgress => prepTime > 0f ? 1f - Mathf.Clamp01(prepLeft / prepTime) : 1f;

    /// <summary>Игрок защищается (нападает враг на место игрока).</summary>
    public bool PlayerDefends => attacker != null && attacker.Squad.Owner != Team.Player;

    // ---------- Что каждое место определяет само ----------

    /// <summary>Название для сообщений и списка команд.</summary>
    public abstract string TargetName { get; }

    /// <summary>Куда подъезжает команда.</summary>
    public abstract Vector3 ApproachPoint { get; }

    /// <summary>Название для окна боя и арены.</summary>
    public abstract string SiteName { get; }

    /// <summary>Цвет арены.</summary>
    public abstract Color SiteColor { get; }

    /// <summary>Общая сила защитников (0 — защищать некому).</summary>
    public abstract int DefenderPower { get; }

    /// <summary>Защитники по волнам (одна волна = одна территория арены).</summary>
    public abstract List<List<BattleUnit>> GetDefenderWaves();

    /// <summary>Кто владеет местом и защищает его. false — место ничьё (нейтральное).</summary>
    public abstract bool TryGetDefendingTeam(out Team team);

    /// <summary>Можно ли этой команде нападать сюда (своё нельзя и т.п.).</summary>
    protected abstract bool CanBeAttackedBy(Squad squad, out string reason);

    /// <summary>Нападающие победили: захват объекта или урон базе.</summary>
    protected abstract void OnAttackerWon(Squad squad);

    /// <summary>Перерисовать полоску/подпись нападения на карте.</summary>
    protected abstract void UpdateAttackVisuals();

    /// <summary>Подготовка закончилась, сейчас начнётся бой (база выбирает гарнизон).</summary>
    protected virtual void OnBattlePhaseStarting() { }

    /// <summary>Защитники потеряли долю здоровья в автобое (гарнизон базы).</summary>
    protected virtual void ApplyDefenderAutoLoss(float loss) { }

    /// <summary>Защитникам после ручного боя осталось столько здоровья (гарнизон базы).</summary>
    protected virtual void SetDefenderHp(float hpFraction) { }

    /// <summary>Нападение закончилось (база забывает гарнизон).</summary>
    protected virtual void OnAttackEnded() { }

    // ---------- Подготовка к бою ----------

    /// <summary>Тикает таймер подготовки (по времени карты — во время боя на арене стоит).</summary>
    protected virtual void Update()
    {
        if (phase != AttackPhase.Preparing) return;
        prepLeft -= WorldTime.DeltaTime;
        UpdateAttackVisuals();
        if (prepLeft <= 0f) StartBattlePhase();
    }

    // ---------- ISquadTarget ----------

    /// <summary>Можно ли отправить сюда команду.</summary>
    public bool CanAccept(Squad squad, out string reason)
    {
        if (!CanBeAttackedBy(squad, out reason)) return false;
        if (attacker != null && attacker.Squad.Owner == squad.Owner)
        {
            reason = "Здесь уже ваша команда";
            return false;
        }
        return true;
    }

    /// <summary>Команду только что отправили (в пути) — месту ничего делать не нужно.</summary>
    public virtual void OnSquadDispatched(Squad squad) { }

    /// <summary>Команда приехала — начинается подготовка к бою.</summary>
    public void OnSquadArrived(SquadUnit unit)
    {
        Team team = unit.Squad.Owner;
        if (attacker != null || !CanBeAttackedBy(unit.Squad, out _))
        {
            // Пока ехали — место уже наше или здесь уже кто-то нападает
            if (team == Team.Player) ToastUI.Show($"{SiteName}: нападение невозможно, команда возвращается");
            unit.ReturnHome();
            return;
        }

        attacker = unit;
        phase = AttackPhase.Preparing;
        prepLeft = prepTime;
        SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.Capturing);

        if (team == Team.Player)
            ToastUI.Show($"Команда {unit.Squad.Number} у цели «{SiteName}». Бой через {prepTime:0} с (можно отступить)");
        else if (TryGetDefendingTeam(out Team def) && def == Team.Player)
            ToastUI.Show($"Враг напал на «{SiteName}»! Бой через {prepTime:0} с");

        UpdateAttackVisuals();
        AttackChanged?.Invoke(this);
    }

    /// <summary>Можно ли команде отступить отсюда (только пока идёт подготовка).</summary>
    public bool CanRetreat(SquadUnit unit) => unit != attacker || phase == AttackPhase.Preparing;

    /// <summary>Команда отступила (в пути или во время подготовки).</summary>
    public virtual void OnSquadRecalled(SquadUnit unit)
    {
        if (unit != attacker) return;
        bool enemyRetreated = PlayerDefends;
        EndAttack();
        if (enemyRetreated) ToastUI.Show($"Враг отступил от «{SiteName}»");
    }

    // ---------- Бой ----------

    /// <summary>Подготовка закончилась — решаем, как пройдёт бой.</summary>
    private void StartBattlePhase()
    {
        prepLeft = 0f;
        OnBattlePhaseStarting();

        // Защищать некому — победа без боя и без потерь
        if (DefenderPower <= 0)
        {
            Finish(true, attacker.Squad.HpFraction);
            return;
        }

        bool playerAttacks = attacker.Squad.Owner == Team.Player;
        if ((playerAttacks || PlayerDefends) && BattlePrepWindowUI.Instance != null)
        {
            // Игрок выбирает: автобой или ручной бой
            phase = AttackPhase.Deciding;
            UpdateAttackVisuals();
            BattlePrepWindowUI.Instance.RequestBattle(this);
            return;
        }
        ResolveAuto(); // игрок не участвует — сразу автобой
    }

    /// <summary>Отряд нападающих — для окна перед боем и арены.</summary>
    public List<BattleUnit> GetAttackerUnits()
    {
        var list = new List<BattleUnit>();
        if (attacker == null) return list;
        Squad s = attacker.Squad;
        foreach (HeroInstance h in s.AllHeroes)
            list.Add(new BattleUnit
            {
                data = h.Data,
                stats = h.Stats,
                hpFraction = s.HpFraction,
                info = $"Команда {s.Number}  •  Ур. {h.Level}  •  сила {BattleCalculator.StatsPower(h.Stats)}"
            });
        return list;
    }

    /// <summary>Автобой: итог сразу (уверенная победа / 50 на 50 / поражение) и потери HP.</summary>
    public void ResolveAuto()
    {
        if (attacker == null) return;
        Squad squad = attacker.Squad;
        BattleForecast f = BattleCalculator.Forecast(BattleCalculator.SquadPower(squad), DefenderPower);
        bool won = BattleCalculator.RollAutoBattle(f);
        ApplyDefenderAutoLoss(won ? 0.5f : 0.2f);
        float hp = Mathf.Max(BattleCalculator.MinHpAfterBattle, squad.HpFraction - BattleCalculator.AutoBattleHpLoss(f, won));
        Finish(won, hp);
    }

    /// <summary>Игрок выбрал ручной бой — ждём итога арены.</summary>
    public void OnManualBattleStarted()
    {
        phase = AttackPhase.Fighting;
        UpdateAttackVisuals();
    }

    /// <summary>
    /// Ручной бой закончился. result — с точки зрения игрока (win = победили наши).
    /// Если игрок защищался, "наши" — это защитники.
    /// </summary>
    public void OnManualBattleFinished(BattleResult result)
    {
        if (attacker == null) return;
        bool defended = PlayerDefends;
        bool attackerWon = defended ? !result.win : result.win;
        SetDefenderHp(defended ? result.ourHp : result.theirHp);
        Finish(attackerWon, defended ? result.theirHp : result.ourHp);
    }

    /// <summary>Применить итог: здоровье команды, победа/поражение, команда едет домой.</summary>
    private void Finish(bool attackerWon, float attackerHp)
    {
        SquadUnit unit = attacker;
        Squad squad = unit.Squad;
        bool defended = PlayerDefends;
        int lost = Mathf.Max(0, Mathf.RoundToInt((squad.HpFraction - attackerHp) * 100f));
        squad.HpFraction = attackerHp;

        EndAttack();

        if (attackerWon)
        {
            OnAttackerWon(squad);
        }
        else if (defended)
        {
            ToastUI.Show($"Нападение на «{SiteName}» отбито!");
        }
        else if (squad.Owner == Team.Player)
        {
            ToastUI.Show($"Бой за «{SiteName}» проигран. Команда {squad.Number} потеряла {lost}% HP");
        }
        unit.ReturnHome();
    }

    /// <summary>Сбросить нападение и сообщить окнам.</summary>
    private void EndAttack()
    {
        attacker = null;
        phase = AttackPhase.None;
        prepLeft = 0f;
        OnAttackEnded();
        UpdateAttackVisuals();
        AttackChanged?.Invoke(this);
    }

    /// <summary>Текст для сообщения "сюда нельзя, идёт нападение".</summary>
    public string AttackStatusText()
    {
        switch (phase)
        {
            case AttackPhase.Preparing: return $"{SiteName}: идёт нападение, бой через {Mathf.CeilToInt(prepLeft)} с";
            case AttackPhase.Deciding: return $"{SiteName}: начинается бой";
            case AttackPhase.Fighting: return $"{SiteName}: идёт бой";
            default: return SiteName;
        }
    }
}
