using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Объект на карте, который можно захватить (Банк, Завод и т.д.).
/// - Клик по объекту открывает окно объекта (MapObjectWindowUI).
/// - Команда приезжает → идёт таймер захвата → объект переходит к её стороне
///   → бонусы получает новый владелец (и теряет старый) → команда едет домой.
/// Данные (название, бонусы, время захвата) — в MapObjectData.
/// На объекте должен быть Collider2D для клика.
/// </summary>
public class MapObject : MonoBehaviour, ISquadTarget, IIncomeSource
{
    [Header("Данные")]
    [SerializeField] private MapObjectData data;

    [Header("Внешний вид (ссылки на детей)")]
    [SerializeField] private SpriteRenderer zone;      // Круг вокруг объекта — цвет владельца
    [SerializeField] private SpriteRenderer flag;      // Флажок — цвет владельца
    [SerializeField] private TMP_Text label;           // Название и владелец над объектом
    [SerializeField] private GameObject progressRoot;  // Полоска захвата (видна только во время захвата)
    [SerializeField] private Transform progressFill;   // Заполнение полоски (растягивается по X)
    [SerializeField] private float progressWidth = 2.4f; // Ширина полоски в единицах мира

    [Header("Цвета владельцев")]
    [SerializeField] private Color neutralColor = new Color(0.6f, 0.63f, 0.68f);
    [SerializeField] private Color playerColor = new Color(0.18f, 0.48f, 0.88f);
    [SerializeField] private Color enemyColor = new Color(0.85f, 0.22f, 0.23f);

    private static readonly List<MapObject> all = new List<MapObject>(); // Все объекты в сцене

    private bool hasOwner;       // Захвачен ли кем-нибудь
    private Team owner;          // Владелец (если hasOwner)
    private SquadUnit capturer;  // Команда, которая сейчас захватывает
    private float progress;      // Прогресс захвата 0..1
    private bool incomeRegistered; // Зарегистрирован ли доход в ResourceManager

    // ---------- Свойства ----------

    public MapObjectData Data => data;
    public bool HasOwner => hasOwner;
    public bool IsCapturing => capturer != null;
    public Team CapturingTeam => capturer != null ? capturer.Squad.Owner : Team.Player;
    public float CaptureProgress => progress;

    /// <summary>Все объекты на карте.</summary>
    public static IReadOnlyList<MapObject> All => all;

    /// <summary>Принадлежит ли объект этой стороне.</summary>
    public bool IsOwnedBy(Team team) => hasOwner && owner == team;

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

    // ISquadTarget
    public string TargetName => data.displayName;

    /// <summary>Точка подъезда — со стороны главной дороги (y = 0), чуть не доезжая до здания.</summary>
    public Vector3 ApproachPoint
    {
        get
        {
            Vector3 p = transform.position;
            p.y -= Mathf.Sign(p.y) * 1.8f;
            return p;
        }
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

    private void Start() => UpdateVisuals();

    /// <summary>Идёт таймер захвата.</summary>
    private void Update()
    {
        if (capturer == null) return;
        progress += Time.deltaTime / Mathf.Max(0.1f, data.captureTime);
        UpdateProgressBar();
        if (progress >= 1f) FinishCapture();
    }

    /// <summary>Клик по объекту — открыть окно объекта.</summary>
    private void OnMouseUpAsButton()
    {
        if (!enabled) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (MapObjectWindowUI.Instance != null) MapObjectWindowUI.Instance.Open(this);
    }

    // ---------- Захват ----------

    /// <summary>Можно ли отправить команду на захват.</summary>
    public bool CanAccept(Squad squad, out string reason)
    {
        reason = null;
        if (IsOwnedBy(squad.Owner)) { reason = "Объект уже ваш"; return false; }
        if (capturer != null && capturer.Squad.Owner == squad.Owner) { reason = "Объект уже захватывает ваша команда"; return false; }
        return true;
    }

    /// <summary>Команду отправили к объекту — объекту ничего делать не нужно.</summary>
    public void OnSquadDispatched(Squad squad) { }

    /// <summary>Команда приехала: начинаем захват (если можно), иначе отправляем её домой.</summary>
    public void OnSquadArrived(SquadUnit unit)
    {
        Team team = unit.Squad.Owner;
        if (IsOwnedBy(team) || capturer != null)
        {
            // Пока ехали — объект уже стал нашим или его кто-то захватывает (битв пока нет)
            if (team == Team.Player) ToastUI.Show($"{data.displayName}: захват невозможен, команда возвращается");
            unit.ReturnHome();
            return;
        }

        capturer = unit;
        progress = 0f;
        SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.Capturing);
        if (progressRoot != null) progressRoot.SetActive(true);
        UpdateProgressBar();
        if (team == Team.Player) ToastUI.Show($"Команда {unit.Squad.Number} начала захват: {data.displayName}");
        else if (IsOwnedBy(Team.Player)) ToastUI.Show($"Враг пытается захватить наш объект: {data.displayName}!");
    }

    /// <summary>Таймер закончился — объект переходит к захватчику, команда едет домой.</summary>
    private void FinishCapture()
    {
        SquadUnit unit = capturer;
        capturer = null;
        progress = 0f;
        if (progressRoot != null) progressRoot.SetActive(false);

        Team newOwner = unit.Squad.Owner;
        bool wasOurs = IsOwnedBy(Team.Player);
        SetOwner(newOwner);
        unit.ReturnHome();

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
    public string OwnerText => !hasOwner ? "Нейтральный" : owner == Team.Player ? "<color=#7FB8FF>Наш</color>" : "<color=#FF7A7A>Враг</color>";

    /// <summary>Перекрасить круг и флажок, обновить подпись.</summary>
    private void UpdateVisuals()
    {
        Color c = OwnerColor;
        if (zone != null) zone.color = new Color(c.r, c.g, c.b, 0.35f);
        if (flag != null) flag.color = c;
        if (label != null) label.text = $"{data.displayName}\n<size=70%>{OwnerText}</size>";
    }

    /// <summary>Растянуть полоску захвата по прогрессу (и покрасить в цвет захватчика).</summary>
    private void UpdateProgressBar()
    {
        if (progressFill == null) return;
        float w = progressWidth * Mathf.Clamp01(progress);
        progressFill.localScale = new Vector3(w, progressFill.localScale.y, 1f);
        progressFill.localPosition = new Vector3(-progressWidth / 2f + w / 2f, progressFill.localPosition.y, 0f);
        var sr = progressFill.GetComponent<SpriteRenderer>();
        if (sr != null && capturer != null) sr.color = capturer.Squad.Owner == Team.Player ? playerColor : enemyColor;
    }
}
