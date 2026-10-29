using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Общая миссия на карте — круг со знаком "!". Её видят и могут выполнить обе стороны.
/// Работает как захват нейтрального объекта (порядок нападения — в AttackableSite):
///  команда приезжает → подготовка 20 с → бой с охраной по территориям (волнам);
///  если за подготовку приедет команда соперника — битва за флаг.
/// Победитель получает награду (золото, плутоний, артефакты), миссия исчезает,
/// и GlobalMissionManager начинает отсчёт до следующей. Проиграли — миссия остаётся
/// (охрана снова полная).
/// Создаётся GlobalMissionManager'ом из префаба GlobalMissionMarker.
/// </summary>
public class GlobalMission : AttackableSite
{
    [Header("Внешний вид")]
    [SerializeField] private SpriteRenderer glow;      // Пульсирующее свечение
    [SerializeField] private SpriteRenderer circle;    // Цветной круг
    [SerializeField] private TMP_Text letter;          // Знак "!"
    [SerializeField] private TMP_Text label;           // Название и статус над кругом
    [SerializeField] private Transform pulse;          // Что пульсирует (масштаб)
    [SerializeField] private GameObject barRoot;       // Полоска подготовки к бою
    [SerializeField] private SpriteRenderer barFill;   // Заполнение полоски
    [SerializeField] private float barWidth = 1.3f;    // Ширина полоски

    [Header("Цвета")]
    [SerializeField] private Color playerColor = new Color(0.18f, 0.48f, 0.88f);
    [SerializeField] private Color enemyColor = new Color(0.85f, 0.22f, 0.23f);

    private GlobalMissionManager manager; // Кто создал миссию
    private bool completed;               // Миссия уже выполнена (вот-вот исчезнет)
    private string rewardText = "";       // Что получил победитель (для окна итога)

    /// <summary>Данные миссии.</summary>
    public GlobalMissionData Data { get; private set; }

    /// <summary>Выполнена ли миссия.</summary>
    public bool IsCompleted => completed;

    /// <summary>Запустить миссию (вызывает GlobalMissionManager).</summary>
    public void Init(GlobalMissionData data, GlobalMissionManager owner)
    {
        Data = data;
        manager = owner;
        if (circle != null) circle.color = data.color;
        if (glow != null) glow.color = new Color(data.color.r, data.color.g, data.color.b, 0.4f);
        if (letter != null) { letter.text = data.iconLetter; letter.color = data.color; }
        UpdateAttackVisuals();
    }

    // ---------- Место нападения (AttackableSite) ----------

    public override string TargetName => Title;
    public override string SiteName => Title;

    /// <summary>Название задания так, как его видит игрок (у героев и злодеев — по-своему).</summary>
    public string Title => Data.TitleFor(Team.Player);

    /// <summary>Кто начал это задание фракции.</summary>
    public Team LaunchedBy { get; set; }
    public override Color SiteColor => Data.color;

    /// <summary>Сила охраны миссии.</summary>
    public override int DefenderPower => completed ? 0 : BattleCalculator.GarrisonPower(Data.waves);

    /// <summary>Миссия ничья — её никто не защищает.</summary>
    public override bool TryGetDefendingTeam(out Team team)
    {
        team = Team.Player;
        return false;
    }

    /// <summary>Охрана по территориям (одна волна = одна территория).</summary>
    public override List<List<BattleUnit>> GetDefenderWaves()
    {
        var waves = new List<List<BattleUnit>>();
        if (Data.waves == null) return waves;
        for (int w = 0; w < Data.waves.Length; w++)
        {
            var list = new List<BattleUnit>();
            foreach (GuardEntry g in Data.waves[w].guards)
            {
                if (g == null || g.unit == null) continue;
                HeroStats s = BattleCalculator.GuardStats(g);
                list.Add(new BattleUnit
                {
                    data = g.unit,
                    stats = s,
                    hpFraction = 1f,
                    level = g.level,
                    guard = true, // охрана не отступает
                    info = $"{UnitClasses.Name(g.unit.unitClass)}  •  Территория {w + 1}  •  Ур. {g.level}  •  сила {BattleCalculator.UnitPower(g.unit, s)}"
                });
            }
            waves.Add(list);
        }
        return waves;
    }

    /// <summary>Команда встаёт чуть ближе к главной дороге, чтобы не закрывать знак.</summary>
    public override Vector3 ApproachPoint
    {
        get
        {
            if (RoadNetwork.Instance != null && RoadNetwork.Instance.HasGrid)
                return RoadNetwork.Instance.NearestRoad(transform.position); // карта-картинка: на дороге
            Vector3 p = transform.position;
            p.y -= Mathf.Sign(p.y) * 1.3f;
            return p;
        }
    }

    /// <summary>Выполнять миссию может любая сторона, пока она не выполнена.</summary>
    protected override bool CanBeAttackedBy(Team team, out string reason)
    {
        reason = null;
        if (completed) { reason = "Миссия уже выполнена"; return false; }
        return true;
    }

    /// <summary>Охрана побеждена — награда победителю, миссия исчезает.</summary>
    protected override void OnAttackerWon(Squad squad)
    {
        if (completed) return;
        completed = true;
        Team team = squad.Owner;

        ResourceManager.Instance.Add(team, Data.rewardGold, Data.rewardPlutonium);
        var got = new List<string>();
        if (Data.rewardGold > 0) got.Add($"<color=#FFD84A>+{Data.rewardGold} золота</color>");
        if (Data.rewardPlutonium > 0) got.Add($"<color=#C58BFF>+{Data.rewardPlutonium} плутония</color>");
        if (ArtifactManager.Instance != null)
            for (int i = 0; i < Data.rewardArtifacts; i++)
            {
                ArtifactData a = ArtifactManager.Instance.GiveRandom(team);
                if (a != null) got.Add($"<color=#FF9EE6>артефакт «{a.displayName}»</color>");
            }
        if (Data.rewardHero)
        {
            HeroInstance hero = HeroManager.Instance.GrantRandomHero(team, Data.rewardHeroMaxStars);
            if (hero != null)
                got.Add($"<color=#7FE0FF>герой {hero.Data.displayName} ({hero.Data.stars} зв.){(HeroManager.Instance.IsReserve(hero) ? " — в запасе" : "")}</color>");
        }
        rewardText = string.Join(",  ", got);
        if (team != LaunchedBy && manager != null)
        {
            manager.OnMissionFailed(this); // выполнил соперник — задание начавшего провалено
            return;
        }

        if (team == Team.Player) ToastUI.Show($"Задание «{Title}» выполнено!");
        else ToastUI.Show($"Враг выполнил задание «{Title}»");

        if (manager != null) manager.OnMissionCompleted(this);
    }

    /// <summary>Текст для окна итога.</summary>
    protected override string AttackerWonText(Team winner) =>
        winner == Team.Player
            ? $"Задание «{Title}» выполнено!\n<size=85%>Награда: {rewardText}</size>"
            : $"Задание «{Title}» выполнил враг.";

    // ---------- Жизненный цикл ----------

    /// <summary>Задание фракции — больше опыта за победу.</summary>
    protected override int WinXp => 50;

    /// <summary>Своя команда проиграла бой за своё задание — задание пропадает (следующая пауза — без задания).</summary>
    protected override void OnAttackerLost(Squad squad)
    {
        if (completed || squad.Owner != LaunchedBy || manager == null) return;
        completed = true;
        manager.OnMissionFailed(this);
    }

    private bool shown = true; // Видно ли задание игроку сейчас

    /// <summary>Задание врага игрок видит, только когда у него идёт подготовка к бою.</summary>
    private void UpdateVisibility()
    {
        bool visible = GlobalMissionManager.IsVisibleTo(this, Team.Player);
        if (visible == shown) return;
        shown = visible;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true)) c.enabled = visible;
        if (visible && LaunchedBy != Team.Player) ToastUI.Show($"Враг начал подготовку к бою за «{Title}»! Успейте помешать — бой через {Mathf.CeilToInt(PrepLeft)} с");
    }

    /// <summary>Пульсация и таймер подготовки.</summary>
    protected override void Update()
    {
        base.Update();
        UpdateVisibility();
        if (pulse != null)
            pulse.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.time * 3f));
    }

    /// <summary>
    /// Клик по "!" — окно общей миссии. Если соперник уже готовится к бою —
    /// сразу список команд, чтобы успеть вступить (битва за флаг).
    /// </summary>
    private void OnMouseUpAsButton()
    {
        if (Data == null || completed) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (IsUnderAttack && CanJoin(Team.Player))
        {
            ToastUI.Show($"Враг выполняет «{Title}»! Успейте за {Mathf.CeilToInt(PrepLeft)} с — будет битва за флаг");
            if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OpenJoinPicker(this);
            return;
        }
        if (IsUnderAttack) { ToastUI.Show(AttackStatusText()); return; }
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.OpenGlobal(this);
    }

    // ---------- Внешний вид ----------

    /// <summary>Подпись над кругом и полоска подготовки к бою (цвет нападающих).</summary>
    protected override void UpdateAttackVisuals()
    {
        if (Data == null) return;
        string status = LaunchedBy == Team.Player ? "<color=#7FB8FF>Наше задание фракции</color>" : "<color=#FF9A9A>Задание врага</color>";
        if (IsUnderAttack)
        {
            string col = IsContested ? "#FFD84A" : AttackingTeam == Team.Player ? "#7FB8FF" : "#FF7A7A";
            string what = IsContested ? "Битва за флаг" : "Бой";
            status = Phase == AttackPhase.Preparing
                ? $"<color={col}>{what} через {Mathf.CeilToInt(PrepLeft)} с</color>"
                : $"<color={col}>Идёт бой!</color>";
        }
        if (label != null) label.text = $"{Title}\n<size=70%>{status}</size>";

        if (barRoot != null) barRoot.SetActive(IsUnderAttack);
        if (barFill == null || !IsUnderAttack) return;
        float fill = PrepProgress;
        Transform t = barFill.transform;
        t.localScale = new Vector3(barWidth * fill, t.localScale.y, 1f);
        t.localPosition = new Vector3(-barWidth / 2f + barWidth * fill / 2f, t.localPosition.y, 0f);
        barFill.color = IsContested ? new Color(1f, 0.85f, 0.25f) : AttackingTeam == Team.Player ? playerColor : enemyColor;
    }
}
