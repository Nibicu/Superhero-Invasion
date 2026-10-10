using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Объект на карте, который можно захватить (Банк, Завод и т.д.).
/// - Клик по объекту открывает окно объекта (MapObjectWindowUI).
/// - Команда приезжает → подготовка к бою 20 с → бой с охраной (окно перед боем)
///   → при победе объект переходит к её стороне → бонусы получает новый владелец
///   (и теряет старый) → команда едет домой. Порядок нападения — в AttackableSite.
/// Объект НЕАКТИВЕН, пока его не откроет событие (EventManager): неактивный объект ничей,
/// серый, и захватить его нельзя. Во время события объект можно захватывать и отбивать;
/// когда событие кончается, последний владелец получает артефакт, а объект снова ничей и неактивен.
/// Данные (название, бонусы, охрана) — в MapObjectData.
/// На объекте должен быть Collider2D для клика.
/// </summary>
public class MapObject : AttackableSite, IIncomeSource
{
    [Header("Данные")]
    [SerializeField] private MapObjectData data;

    [Header("Внешний вид (ссылки на детей)")]
    [SerializeField] private SpriteRenderer zone;      // Круг вокруг объекта — цвет владельца
    [SerializeField] private SpriteRenderer flag;      // Флажок — цвет владельца
    [SerializeField] private TMP_Text label;           // Название и владелец над объектом
    [SerializeField] private GameObject progressRoot;  // Полоска подготовки к бою (видна только во время нападения)
    [SerializeField] private Transform progressFill;   // Заполнение полоски (растягивается по X)
    [SerializeField] private float progressWidth = 2.4f; // Ширина полоски в единицах мира

    [Header("Цвета владельцев")]
    [SerializeField] private Color neutralColor = new Color(0.6f, 0.63f, 0.68f);
    [SerializeField] private Color playerColor = new Color(0.18f, 0.48f, 0.88f);
    [SerializeField] private Color enemyColor = new Color(0.85f, 0.22f, 0.23f);

    private static readonly List<MapObject> all = new List<MapObject>(); // Все объекты в сцене

    private bool hasOwner;         // Захвачен ли кем-нибудь
    private Team owner;            // Владелец (если hasOwner)
    private bool incomeRegistered; // Зарегистрирован ли доход в ResourceManager
    private bool isActive;         // Открыт ли объект событием (можно захватывать)
    private SpriteRenderer[] sprites; // Все картинки объекта (для серого вида, когда неактивен)
    private Color[] spriteColors;     // Их исходные цвета
    private float labelTimer;         // Когда обновить подпись с таймером события

    // ---------- Свойства ----------

    public MapObjectData Data => data;
    public bool HasOwner => hasOwner;

    /// <summary>Открыт ли объект событием (только тогда его можно захватить).</summary>
    public bool IsActive => isActive;

    /// <summary>Сколько секунд ещё идёт событие этого объекта (пишет EventManager).</summary>
    public float EventTimeLeft { get; set; }

    /// <summary>Владелец (имеет смысл, только если HasOwner).</summary>
    public Team Owner => owner;

    /// <summary>Все объекты на карте.</summary>
    public static IReadOnlyList<MapObject> All => all;

    /// <summary>Принадлежит ли объект этой стороне.</summary>
    public bool IsOwnedBy(Team team) => hasOwner && owner == team;

    /// <summary>Сколько объектов на карте у стороны.</summary>
    public static int CountOwnedBy(Team team)
    {
        int n = 0;
        foreach (MapObject o in all)
            if (o.IsOwnedBy(team)) n++;
        return n;
    }

    /// <summary>Владеет ли сторона хоть одним объектом, разрешающим усиление героев (Институт).</summary>
    public static bool TeamCanBoostHeroes(Team team)
    {
        foreach (MapObject o in all)
            if (o.IsOwnedBy(team) && o.data.allowsHeroBoost) return true;
        return false;
    }

    // IIncomeSource: доход идёт текущему владельцу
    Team IIncomeSource.Owner => owner;
    public int GoldIncome => hasOwner ? data.goldIncome : 0;
    public int PlutoniumIncome => hasOwner ? data.plutoniumIncome : 0;
    public string SourceName => data.displayName;

    // ---------- Место нападения (AttackableSite) ----------

    public override string TargetName => data.displayName;
    public override string SiteName => data.displayName;
    public override Color SiteColor => data.color;

    /// <summary>Защитники объекта — его охрана из MapObjectData + команда подкрепления (если пришла).</summary>
    public override int DefenderPower =>
        BattleCalculator.GarrisonPower(data) + (Reinforcement != null ? BattleCalculator.SquadPower(Reinforcement.Squad) : 0);

    /// <summary>На защиту объекта владелец может прислать команду.</summary>
    public override bool AllowsReinforcement => true;

    /// <summary>Объект защищает его владелец (у нейтрального — никто из игроков).</summary>
    public override bool TryGetDefendingTeam(out Team team)
    {
        team = owner;
        return hasOwner;
    }

    /// <summary>
    /// Защитники по территориям (для боя и окна перед боем):
    ///  - нейтральный объект — охрана по волнам, обычно 3 территории;
    ///  - захваченный объект — вся охрана на 1 территории (как гарнизон базы),
    ///    а если пришло подкрепление — оно на 2-й территории.
    /// </summary>
    public override List<List<BattleUnit>> GetDefenderWaves()
    {
        var waves = new List<List<BattleUnit>>();
        if (!hasOwner) return GuardWaves();

        var guardsAll = new List<BattleUnit>();
        foreach (List<BattleUnit> w in GuardWaves()) guardsAll.AddRange(w);
        waves.Add(guardsAll);
        if (Reinforcement != null)
        {
            List<BattleUnit> squad = GetSquadUnits(Reinforcement);
            for (int i = 0; i < squad.Count; i++)
            {
                BattleUnit u = squad[i];
                u.reinforcement = true;
                u.info = "Подкрепление  •  " + u.info;
                squad[i] = u;
            }
            waves.Add(squad);
        }
        return waves;
    }

    /// <summary>Охрана объекта по волнам из MapObjectData.</summary>
    private List<List<BattleUnit>> GuardWaves()
    {
        var waves = new List<List<BattleUnit>>();
        if (data.waves == null) return waves;
        bool owned = hasOwner;
        for (int w = 0; w < data.waves.Length; w++)
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
                    guard = true, // охрана объекта не отступает
                    info = owned
                        ? $"{UnitClasses.Name(g.unit.unitClass)}  •  Охрана объекта  •  Ур. {g.level}  •  сила {BattleCalculator.UnitPower(g.unit, s)}"
                        : $"{UnitClasses.Name(g.unit.unitClass)}  •  Территория {w + 1}  •  Ур. {g.level}  •  сила {BattleCalculator.UnitPower(g.unit, s)}"
                });
            }
            waves.Add(list);
        }
        return waves;
    }

    /// <summary>Точка подъезда — со стороны главной дороги (y = 0), чуть не доезжая до здания.</summary>
    public override Vector3 ApproachPoint
    {
        get
        {
            Vector3 p = transform.position;
            p.y -= Mathf.Sign(p.y) * 1.8f;
            return p;
        }
    }

    /// <summary>Свой объект захватывать не нужно.</summary>
    protected override bool CanBeAttackedBy(Team team, out string reason)
    {
        reason = null;
        if (!isActive) { reason = "Объект неактивен — его откроет событие"; return false; }
        if (EventTimeLeft <= 0f) { reason = "Событие заканчивается"; return false; }
        if (IsOwnedBy(team)) { reason = "Объект уже ваш"; return false; }
        return true;
    }

    /// <summary>Нападающие победили охрану — объект переходит к ним.</summary>
    protected override void OnAttackerWon(Squad squad) => TakeOver(squad.Owner);

    /// <summary>Текст для окна итога: кому теперь принадлежит объект и что он даёт.</summary>
    protected override string AttackerWonText(Team winner) =>
        winner == Team.Player
            ? $"Объект «{data.displayName}» теперь ваш!\n<size=85%><color=#FFD84A>{data.GetBonusText().Replace("\n", "  •  ")}</color></size>"
            : $"Объект «{data.displayName}» теперь принадлежит врагу.";

    /// <summary>Автобой: подкрепление теряет HP (проиграло — −50%, отбилось — −20%).</summary>
    protected override void ApplyDefenderAutoLoss(float loss)
    {
        if (Reinforcement == null) return;
        Squad s = Reinforcement.Squad;
        s.HpFraction = Mathf.Max(BattleCalculator.MinHpAfterBattle, s.HpFraction - loss);
    }

    /// <summary>Ручной бой: у подкрепления остаётся столько HP, сколько было в конце боя.</summary>
    protected override void SetDefenderHp(float defendersHp, BattleResult result)
    {
        if (Reinforcement != null) Reinforcement.Squad.HpFraction = result.reinforcementHp;
    }

    // ---------- Жизненный цикл ----------

    /// <summary>Регистрируем объект. Без данных объект работать не может — выключаем его с понятной ошибкой.</summary>
    private void OnEnable()
    {
        if (data == null)
        {
            Debug.LogError($"MapObject '{name}': не назначено поле Data (файл MapObjectData). Объект выключен.", this);
            enabled = false;
            return;
        }
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (incomeRegistered && ResourceManager.Instance != null) ResourceManager.Instance.UnregisterSource(this);
        incomeRegistered = false;
    }

    private void Start()
    {
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        spriteColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;
        UpdateVisuals();
    }

    /// <summary>Таймер подготовки (база) + раз в полсекунды подпись с таймером события.</summary>
    protected override void Update()
    {
        base.Update();
        if (!isActive) return;
        labelTimer -= Time.deltaTime;
        if (labelTimer > 0f) return;
        labelTimer = 0.5f;
        UpdateAttackVisuals();
    }

    // ---------- Событие ----------

    /// <summary>Событие открыло объект: теперь его можно захватывать.</summary>
    public void Activate()
    {
        isActive = true;
        UpdateVisuals();
    }

    /// <summary>
    /// Событие закончилось: объект снова ничей и неактивен (бонусы у владельца пропадают).
    /// Возвращает, был ли у объекта владелец, и кто (lastOwner).
    /// </summary>
    public bool Deactivate(out Team lastOwner)
    {
        lastOwner = owner;
        bool had = hasOwner;
        if (hasOwner) ApplyEffects(owner, -1);
        hasOwner = false;
        isActive = false;
        if (incomeRegistered && ResourceManager.Instance != null) ResourceManager.Instance.UnregisterSource(this);
        incomeRegistered = false;
        UpdateVisuals();
        if (data.allowsHeroBoost && had) HeroManager.Instance.NotifyChanged(lastOwner);
        return had;
    }

    /// <summary>Клик по объекту — открыть окно объекта (во время нападения — только сообщение).</summary>
    private void OnMouseUpAsButton()
    {
        if (!enabled) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!isActive)
        {
            if (MapObjectWindowUI.Instance != null) MapObjectWindowUI.Instance.Open(this);
            return;
        }
        if (IsUnderAttack && CanJoin(Team.Player))
        {
            // Враг готовится к захвату — можно успеть своей командой (будет битва за флаг)
            ToastUI.Show($"Враг захватывает «{data.displayName}»! Успейте за {Mathf.CeilToInt(PrepLeft)} с — будет битва за флаг");
            if (MapObjectWindowUI.Instance != null) MapObjectWindowUI.Instance.OpenJoinPicker(this);
            return;
        }
        if (IsUnderAttack && CanReinforce(Team.Player))
        {
            // Враг напал на наш объект — можно прислать команду на защиту
            ToastUI.Show($"Враг напал на «{data.displayName}»! Отправьте команду на защиту — успейте за {Mathf.CeilToInt(PrepLeft)} с");
            if (MapObjectWindowUI.Instance != null) MapObjectWindowUI.Instance.OpenJoinPicker(this);
            return;
        }
        if (IsUnderAttack) { ToastUI.Show(AttackStatusText()); return; }
        if (MapObjectWindowUI.Instance != null) MapObjectWindowUI.Instance.Open(this);
    }

    // ---------- Владелец ----------

    /// <summary>Объект переходит к стороне newOwner + сообщение.</summary>
    private void TakeOver(Team newOwner)
    {
        bool wasOurs = IsOwnedBy(Team.Player);
        SetOwner(newOwner);
        if (newOwner == Team.Player)
            ToastUI.Show($"{data.displayName} захвачен! {data.GetBonusText().Split('\n')[0]}");
        else if (wasOurs)
            ToastUI.Show($"Враг захватил наш объект: {data.displayName}!");
        else
            ToastUI.Show($"Враг захватил объект: {data.displayName}");
    }

    /// <summary>Сменить владельца: снять бонусы со старого, выдать новому.</summary>
    public void SetOwner(Team newOwner)
    {
        if (hasOwner && owner == newOwner) return;
        if (hasOwner) ApplyEffects(owner, -1);

        hasOwner = true;
        owner = newOwner;
        ApplyEffects(owner, +1);

        if (!incomeRegistered)
        {
            ResourceManager.Instance.RegisterSource(this);
            incomeRegistered = true;
        }
        UpdateVisuals();
    }

    /// <summary>
    /// Включить (sign = +1) или выключить (sign = -1) особые бонусы для стороны.
    /// Доход отдельно не трогаем — он сам идёт текущему владельцу.
    /// </summary>
    private void ApplyEffects(Team team, int sign)
    {
        if (data.heroLimitBonus != 0)
            HeroManager.Instance.AddExtraLimit(team, data.heroLimitBonus * sign);

        if (data.unlocksFactoryUpgrades)
        {
            MainBase b = MainBase.Get(team);
            if (b != null) b.FactoryUpgradesUnlocked = sign > 0 || TeamOwnsOtherFactory(team);
        }

        if (data.allowsHeroBoost) HeroManager.Instance.NotifyChanged(team);
    }

    /// <summary>Есть ли у стороны другой Завод, кроме этого (на случай, если Заводов несколько).</summary>
    private bool TeamOwnsOtherFactory(Team team)
    {
        foreach (MapObject o in all)
            if (o != this && o.data.unlocksFactoryUpgrades && o.IsOwnedBy(team)) return true;
        return false;
    }

    // ---------- Внешний вид ----------

    /// <summary>Цвет владельца.</summary>
    public Color OwnerColor => !hasOwner ? neutralColor : owner == Team.Player ? playerColor : enemyColor;

    /// <summary>Владелец словами.</summary>
    public string OwnerText => !isActive ? "<color=#D5DAE3>Неактивен</color>"
        : !hasOwner ? "Нейтральный" : owner == Team.Player ? "<color=#7FB8FF>Наш</color>" : "<color=#FF7A7A>Враг</color>";

    /// <summary>Перекрасить круг и флажок, обновить подпись. Неактивный объект — серый и бледный.</summary>
    private void UpdateVisuals()
    {
        if (sprites != null)
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color o = spriteColors[i];
                float g = o.grayscale * 0.7f;
                sprites[i].color = isActive ? o : new Color(g, g, g, o.a * 0.75f);
            }
        Color c = isActive ? OwnerColor : new Color(0.35f, 0.37f, 0.4f);
        if (zone != null) zone.color = new Color(c.r, c.g, c.b, 0.35f);
        if (flag != null) flag.color = c;
        UpdateAttackVisuals();
    }

    /// <summary>Подпись над объектом и полоска подготовки к бою (цвет нападающих).</summary>
    protected override void UpdateAttackVisuals()
    {
        if (data == null) return;
        string attack = "";
        if (IsUnderAttack)
        {
            string col = AttackingTeam == Team.Player ? "#7FB8FF" : "#FF7A7A";
            if (IsContested) col = "#FFD84A";
            string what = IsContested ? "Битва за флаг" : HasReinforcement ? "Бой (с подкреплением)" : "Бой";
            attack = Phase == AttackPhase.Preparing
                ? $"\n<size=60%><color={col}>{what} через {Mathf.CeilToInt(PrepLeft)} с</color></size>"
                : $"\n<size=60%><color={col}>Идёт бой!</color></size>";
        }
        string evt = isActive && !IsUnderAttack
            ? $"\n<size=60%><color=#FFD84A>Событие: {Mathf.FloorToInt(EventTimeLeft / 60f)}:{Mathf.FloorToInt(EventTimeLeft % 60f):00}</color></size>"
            : "";
        if (label != null) label.text = $"{data.displayName}\n<size=70%>{OwnerText}</size>{attack}{evt}";

        if (progressRoot != null) progressRoot.SetActive(IsUnderAttack);
        if (progressFill == null || !IsUnderAttack) return;
        float w = progressWidth * PrepProgress;
        progressFill.localScale = new Vector3(w, progressFill.localScale.y, 1f);
        progressFill.localPosition = new Vector3(-progressWidth / 2f + w / 2f, progressFill.localPosition.y, 0f);
        var sr = progressFill.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = IsContested ? new Color(1f, 0.85f, 0.25f) : AttackingTeam == Team.Player ? playerColor : enemyColor;
    }
}
