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

    public override string TargetName => Data.title;
    public override string SiteName => Data.title;
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
        rewardText = string.Join(",  ", got);

        if (team == Team.Player) ToastUI.Show($"Общая миссия «{Data.title}» выполнена!");
        else ToastUI.Show($"Враг выполнил общую миссию «{Data.title}»");

        if (manager != null) manager.OnMissionCompleted(this);
    }

    /// <summary>Текст для окна итога.</summary>
    protected override string AttackerWonText(Team winner) =>
        winner == Team.Player
            ? $"Общая миссия «{Data.title}» выполнена!\n<size=85%>Награда: {rewardText}</size>"
            : $"Общую миссию «{Data.title}» выполнил враг.";

    // ---------- Жизненный цикл ----------

    /// <summary>Пульсация и таймер подготовки.</summary>
    protected override void Update()
    {
        base.Update();
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
            ToastUI.Show($"Враг выполняет «{Data.title}»! Успейте за {Mathf.CeilToInt(PrepLeft)} с — будет битва за флаг");
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
        string status = "<color=#FFD84A>Общая миссия</color>";
        if (IsUnderAttack)
        {
            string col = IsContested ? "#FFD84A" : AttackingTeam == Team.Player ? "#7FB8FF" : "#FF7A7A";
            string what = IsContested ? "Битва за флаг" : "Бой";
            status = Phase == AttackPhase.Preparing
                ? $"<color={col}>{what} через {Mathf.CeilToInt(PrepLeft)} с</color>"
                : $"<color={col}>Идёт бой!</color>";
        }
        if (label != null) label.text = $"{Data.title}\n<size=70%>{status}</size>";

        if (barRoot != null) barRoot.SetActive(IsUnderAttack);
        if (barFill == null || !IsUnderAttack) return;
        float fill = PrepProgress;
        Transform t = barFill.transform;
        t.localScale = new Vector3(barWidth * fill, t.localScale.y, 1f);
        t.localPosition = new Vector3(-barWidth / 2f + barWidth * fill / 2f, t.localPosition.y, 0f);
        barFill.color = IsContested ? new Color(1f, 0.85f, 0.25f) : AttackingTeam == Team.Player ? playerColor : enemyColor;
    }
}
