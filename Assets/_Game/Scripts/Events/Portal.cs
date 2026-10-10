using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Портал в центре карты — появляется на время события "Портал" (EventManager).
/// Закрыть портал = пройти все его территории с охраной (охраны в 2 раза больше, чем у объектов).
/// Порядок нападения — как у объекта (AttackableSite): подготовка 20 с → бой;
/// если за подготовку приедет соперник — битва за флаг.
/// Прогресс сохраняется для каждой стороны: прошли 2 территории и проиграли —
/// в следующий раз начнёте с 3-й.
/// Итог события:
///  - закрыла одна сторона — она получает артефакт, база другой стороны получает урон;
///  - никто не закрыл — урон получают обе базы (это делает EventManager).
/// Пока портал закрыт (нет события), его не видно и на него нельзя нажать.
/// </summary>
public class Portal : AttackableSite
{
    [Header("Данные")]
    [SerializeField] private PortalData data;

    [Header("Внешний вид")]
    [SerializeField] private GameObject visualRoot;   // Всё, что видно (прячется, пока портала нет)
    [SerializeField] private SpriteRenderer glow;     // Свечение
    [SerializeField] private SpriteRenderer circle;   // Круг портала
    [SerializeField] private TMP_Text letter;         // Символ в центре
    [SerializeField] private TMP_Text label;          // Название, таймер и прогресс над порталом
    [SerializeField] private Transform spin;          // Что вращается
    [SerializeField] private GameObject barRoot;      // Полоска подготовки к бою
    [SerializeField] private SpriteRenderer barFill;  // Заполнение полоски
    [SerializeField] private float barWidth = 1.3f;   // Ширина полоски

    [Header("Цвета")]
    [SerializeField] private Color playerColor = new Color(0.18f, 0.48f, 0.88f);
    [SerializeField] private Color enemyColor = new Color(0.85f, 0.22f, 0.23f);

    private readonly Dictionary<Team, int> progress = new Dictionary<Team, int>(); // Пройдено территорий каждой стороной
    private bool isOpen;          // Идёт событие "Портал"
    private bool closed;          // Портал уже закрыт кем-то
    private Team closedBy;        // Кто закрыл
    private string rewardText = ""; // Что получил закрывший (для окна итога)
    private float labelTimer;     // Когда обновить подпись

    /// <summary>Единственный портал на карте.</summary>
    public static Portal Instance { get; private set; }

    /// <summary>Настройки портала.</summary>
    public PortalData Data => data;

    /// <summary>Открыт ли портал (идёт событие).</summary>
    public bool IsOpen => isOpen;

    /// <summary>Закрыт ли портал одной из сторон.</summary>
    public bool IsClosed => closed;

    /// <summary>Кто закрыл портал (если IsClosed).</summary>
    public Team ClosedBy => closedBy;

    /// <summary>Сколько секунд осталось до конца события (пишет EventManager).</summary>
    public float TimeLeft { get; set; }

    /// <summary>Сколько территорий у портала.</summary>
    public int TerritoryCount => data.TerritoryCount;

    /// <summary>Сколько территорий прошла сторона.</summary>
    public int GetProgress(Team team) => progress.TryGetValue(team, out int p) ? p : 0;

    // ---------- Жизненный цикл ----------

    private void Awake()
    {
        Instance = this;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Событие началось — портал открывается, прогресс обеих сторон с нуля.</summary>
    public void Open(float duration)
    {
        isOpen = true;
        closed = false;
        rewardText = "";
        TimeLeft = duration;
        progress[Team.Player] = 0;
        progress[Team.Enemy] = 0;
        if (circle != null) circle.color = data.color;
        if (glow != null) glow.color = new Color(data.color.r, data.color.g, data.color.b, 0.45f);
        if (letter != null) { letter.text = data.iconLetter; letter.color = Color.white; }
        SetVisible(true);
        UpdateAttackVisuals();
    }

    /// <summary>Событие кончилось — портал исчезает.</summary>
    public void Hide()
    {
        isOpen = false;
        SetVisible(false);
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OnPortalHidden(this);
    }

    /// <summary>Показать/спрятать картинку и клик.</summary>
    private void SetVisible(bool on)
    {
        if (visualRoot != null) visualRoot.SetActive(on);
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = on;
    }

    /// <summary>Вращение, таймер подготовки, подпись.</summary>
    protected override void Update()
    {
        base.Update();
        if (!isOpen) return;
        if (spin != null) spin.Rotate(0f, 0f, -90f * Time.deltaTime);
        labelTimer -= Time.deltaTime;
        if (labelTimer > 0f) return;
        labelTimer = 0.5f;
        UpdateAttackVisuals();
    }

    // ---------- Охрана ----------

    /// <summary>Чей прогресс сейчас важен: нападающей команды, а если нападения нет — игрока.</summary>
    private Team CurrentSide => Attacker != null ? Attacker.Squad.Owner : Team.Player;

    /// <summary>Отряды одной волны охраны.</summary>
    private List<BattleUnit> WaveUnits(int w)
    {
        var list = new List<BattleUnit>();
        foreach (GuardEntry g in data.waves[w].guards)
        {
            if (g == null || g.unit == null) continue;
            HeroStats s = BattleCalculator.GuardStats(g);
            list.Add(new BattleUnit
            {
                data = g.unit,
                stats = s,
                hpFraction = 1f,
                guard = true, // охрана портала не отступает
                info = $"{UnitClasses.Name(g.unit.unitClass)}  •  Территория {w + 1}  •  Ур. {g.level}  •  сила {BattleCalculator.UnitPower(g.unit, s)}"
            });
        }
        return list;
    }

    /// <summary>Сила охраны, которую стороне team ещё осталось пройти.</summary>
    public int RemainingPower(Team team)
    {
        int sum = 0;
        for (int w = GetProgress(team); w < data.TerritoryCount; w++)
            foreach (GuardEntry g in data.waves[w].guards)
                if (g != null && g.unit != null) sum += BattleCalculator.UnitPower(g.unit, BattleCalculator.GuardStats(g));
        return sum;
    }

    // ---------- Место нападения (AttackableSite) ----------

    public override string TargetName => data.title;
    public override string SiteName => data.title;
    public override Color SiteColor => data.color;

    /// <summary>Охрана, которую нападающей стороне ещё осталось пройти.</summary>
    public override int DefenderPower => closed ? 0 : RemainingPower(CurrentSide);

    /// <summary>Портал ничей.</summary>
    public override bool TryGetDefendingTeam(out Team team)
    {
        team = Team.Player;
        return false;
    }

    /// <summary>Обычный бой: только ещё не пройденные территории нападающей стороны.</summary>
    public override List<List<BattleUnit>> GetDefenderWaves()
    {
        var waves = new List<List<BattleUnit>>();
        for (int w = GetProgress(CurrentSide); w < data.TerritoryCount; w++) waves.Add(WaveUnits(w));
        return waves;
    }

    /// <summary>Битва за флаг: все территории, но пройденные этой стороной — пустые.</summary>
    public override List<List<BattleUnit>> GetContestWaves(Team side)
    {
        var waves = new List<List<BattleUnit>>();
        int done = GetProgress(side);
        for (int w = 0; w < data.TerritoryCount; w++)
            waves.Add(w < done ? new List<BattleUnit>() : WaveUnits(w));
        return waves;
    }

    /// <summary>Портал стоит на главной дороге — команды встают сбоку от него, чтобы не закрывать его.</summary>
    public override Vector3 ApproachPoint => transform.position + new Vector3(-1.4f, -0.5f, 0f);

    /// <summary>Нападать можно, пока портал открыт, не закрыт и время не вышло.</summary>
    protected override bool CanBeAttackedBy(Team team, out string reason)
    {
        reason = null;
        if (!isOpen) { reason = "Портала нет"; return false; }
        if (closed) { reason = "Портал уже закрыт"; return false; }
        if (TimeLeft <= 0f) { reason = "Портал вот-вот исчезнет"; return false; }
        return true;
    }

    /// <summary>Ручной бой или битва за флаг закончились — запоминаем пройденные территории.</summary>
    protected override void OnBattleResult(BattleResult result)
    {
        if (IsContested)
        {
            // В битве за флаг пройденные территории уже учтены (пустые) — берём наибольшее
            progress[Team.Player] = Mathf.Max(GetProgress(Team.Player), Mathf.Min(result.ourTerritories, TerritoryCount));
            progress[Team.Enemy] = Mathf.Max(GetProgress(Team.Enemy), Mathf.Min(result.rivalTerritories, TerritoryCount));
            return;
        }
        Team side = CurrentSide; // в обычном бою на портале нападает игрок
        progress[side] = Mathf.Min(TerritoryCount, GetProgress(side) + result.ourTerritories);
    }

    /// <summary>
    /// Автобой проигран — сколько территорий команда всё же прошла:
    /// волны, общая сила которых не больше 70% силы команды.
    /// </summary>
    protected override void OnAutoResolved(Squad squad, bool won)
    {
        if (won) return;
        Team side = squad.Owner;
        float budget = BattleCalculator.SquadPower(squad) * 0.7f;
        int p = GetProgress(side);
        while (p < TerritoryCount)
        {
            int wavePower = 0;
            foreach (GuardEntry g in data.waves[p].guards)
                if (g != null && g.unit != null) wavePower += BattleCalculator.UnitPower(g.unit, BattleCalculator.GuardStats(g));
            if (wavePower > budget) break;
            budget -= wavePower;
            p++;
        }
        p = Mathf.Min(p, TerritoryCount - 1); // последнюю территорию проигравший не проходит
        if (p > GetProgress(side) && side == Team.Player)
            ToastUI.Show($"Портал: пройдено территорий {p}/{TerritoryCount} — прогресс сохранён");
        progress[side] = Mathf.Max(GetProgress(side), p);
    }

    /// <summary>Портал закрыт: закрывший получает артефакты, база другой стороны — урон.</summary>
    protected override void OnAttackerWon(Squad squad)
    {
        if (closed) return;
        closed = true;
        closedBy = squad.Owner;
        progress[closedBy] = TerritoryCount;

        var got = new List<string>();
        if (ArtifactManager.Instance != null)
            for (int i = 0; i < data.rewardArtifacts; i++)
            {
                ArtifactData a = ArtifactManager.Instance.GiveRandom(closedBy);
                if (a != null) got.Add($"<color=#FF9EE6>артефакт «{a.displayName}»</color>");
            }
        rewardText = got.Count > 0 ? string.Join(",  ", got) : "";

        Team other = closedBy == Team.Player ? Team.Enemy : Team.Player;
        MainBase b = MainBase.Get(other);
        if (b != null) b.TakeDamage(data.baseDamage, "Портал");

        if (closedBy == Team.Player) ToastUI.Show($"Мы закрыли портал! Враг получает {data.baseDamage} урона");
        else ToastUI.Show($"Враг закрыл портал! Наша база получает {data.baseDamage} урона");
        UpdateAttackVisuals();
    }

    /// <summary>Текст для окна итога.</summary>
    protected override string AttackerWonText(Team winner) =>
        winner == Team.Player
            ? $"Портал закрыт!\n<size=85%>База врага получила {data.baseDamage} урона" +
              (rewardText != "" ? $"\nНаграда: {rewardText}" : "") + "</size>"
            : $"Враг закрыл портал.\nНаша база получила {data.baseDamage} урона.";

    /// <summary>Проиграли охране — сколько территорий уже пройдено.</summary>
    protected override string AttackerLostText(Team team) =>
        GetProgress(team) > 0 ? $"\n<size=85%><color=#C58BFF>Пройдено территорий: {GetProgress(team)}/{TerritoryCount} — прогресс сохранён</color></size>" : "";

    // ---------- Клик ----------

    /// <summary>Клик по порталу — окно портала (или сразу список команд, чтобы успеть к битве за флаг).</summary>
    private void OnMouseUpAsButton()
    {
        if (!isOpen) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (IsUnderAttack && CanJoin(Team.Player))
        {
            ToastUI.Show($"Враг закрывает портал! Успейте за {Mathf.CeilToInt(PrepLeft)} с — будет битва за флаг");
            if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OpenJoinPicker(this);
            return;
        }
        if (IsUnderAttack) { ToastUI.Show(AttackStatusText()); return; }
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OpenPortal(this);
    }

    // ---------- Внешний вид ----------

    /// <summary>Подпись над порталом (таймер, прогресс сторон, бой) и полоска подготовки.</summary>
    protected override void UpdateAttackVisuals()
    {
        if (data == null || !isOpen) return;
        string status;
        if (closed)
            status = closedBy == Team.Player ? "<color=#7FB8FF>Закрыт нами</color>" : "<color=#FF7A7A>Закрыт врагом</color>";
        else if (IsUnderAttack)
        {
            string col = IsContested ? "#FFD84A" : AttackingTeam == Team.Player ? "#7FB8FF" : "#FF7A7A";
            string what = IsContested ? "Битва за флаг" : "Бой";
            status = Phase == AttackPhase.Preparing
                ? $"<color={col}>{what} через {Mathf.CeilToInt(PrepLeft)} с</color>"
                : $"<color={col}>Идёт бой!</color>";
        }
        else
            status = $"<color=#FFD84A>{Mathf.FloorToInt(TimeLeft / 60f)}:{Mathf.FloorToInt(TimeLeft % 60f):00}</color>  " +
                     $"<color=#7FB8FF>Мы {GetProgress(Team.Player)}/{TerritoryCount}</color>  <color=#FF7A7A>Враг {GetProgress(Team.Enemy)}/{TerritoryCount}</color>";
        if (label != null) label.text = $"{data.title.ToUpper()}\n<size=70%>{status}</size>";

        if (barRoot != null) barRoot.SetActive(IsUnderAttack);
        if (barFill == null || !IsUnderAttack) return;
        float fill = PrepProgress;
        Transform t = barFill.transform;
        t.localScale = new Vector3(barWidth * fill, t.localScale.y, 1f);
        t.localPosition = new Vector3(-barWidth / 2f + barWidth * fill / 2f, t.localPosition.y, 0f);
        barFill.color = IsContested ? new Color(1f, 0.85f, 0.25f) : AttackingTeam == Team.Player ? playerColor : enemyColor;
    }
}
