using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Бафф, выпавший из коробки. Подбирает первый боец, который к нему подойдёт.
/// Бывает двух видов:
///  - Лечение: +30% от максимального HP;
///  - Усиление: случайный параметр (авто атака, атака, спец. атака, защита, спец. защита, скорость)
///    +30% на 15 секунд.
/// Внешний вид — в префабе Assets/_Game/Prefabs/BattleBuff.
/// </summary>
public class BattleBuffItem : MonoBehaviour
{
    [Tooltip("Сколько HP (доля от максимума) даёт лечение")]
    [SerializeField] private float healAmount = 0.3f;
    [Tooltip("Во сколько раз бафф усиливает параметр (1.3 = +30%)")]
    [SerializeField] private float boostMultiplier = 1.3f;
    [Tooltip("Сколько секунд действует усиление")]
    [SerializeField] private float boostDuration = 15f;
    [Tooltip("На каком расстоянии боец подбирает бафф")]
    [SerializeField] private float pickupRadius = 0.6f;

    [Header("Внешний вид")]
    [SerializeField] private SpriteRenderer icon;     // Значок (красится: лечение — зелёный, усиление — жёлтый)
    [SerializeField] private Transform floatPart;     // Что покачивается вверх-вниз
    [SerializeField] private TMP_Text label;          // Надпись "+HP" или "+Атака"
    [SerializeField] private SortingGroup sortingGroup;

    private float baseY;      // Исходная высота качающейся части

    /// <summary>Это лечение (иначе — усиление параметра).</summary>
    public bool IsHeal { get; private set; }

    /// <summary>Какой параметр усиливает (если не лечение).</summary>
    public BoostStat Stat { get; private set; }

    /// <summary>Подобран ли уже.</summary>
    public bool Taken { get; private set; }

    /// <summary>Положение на арене.</summary>
    public Vector2 Position => transform.position;

    /// <summary>Выбрать случайный вид баффа (вызывает BattleManager при появлении).</summary>
    public void Setup()
    {
        IsHeal = Random.value < 0.5f;
        Stat = (BoostStat)Random.Range(0, 6);
        if (icon != null) icon.color = IsHeal ? new Color(0.35f, 0.9f, 0.45f) : new Color(1f, 0.82f, 0.25f);
        if (label != null) label.text = IsHeal ? "+HP" : $"+{BoostNames.Get(Stat)}";
        if (floatPart != null) baseY = floatPart.localPosition.y;
        if (sortingGroup != null && BattleManager.Instance != null)
            sortingGroup.sortingOrder = BattleManager.Instance.DepthSortingOrder(transform.position.y);
    }

    /// <summary>Покачивание и проверка: не подошёл ли боец.</summary>
    private void Update()
    {
        if (floatPart != null)
            floatPart.localPosition = new Vector3(0f, baseY + Mathf.Sin(Time.time * 4f) * 0.08f, 0f);

        BattleManager bm = BattleManager.Instance;
        if (Taken || bm == null || !bm.IsRunning) return;
        foreach (Fighter f in bm.AllFighters)
        {
            if (!f.IsAlive || f.State == FighterState.KnockedDown) continue;
            Vector2 d = f.Position - Position;
            if (Mathf.Abs(d.x) > pickupRadius || Mathf.Abs(d.y) > pickupRadius * 0.7f) continue;
            PickUp(f);
            return;
        }
    }

    /// <summary>Боец подобрал бафф.</summary>
    private void PickUp(Fighter f)
    {
        Taken = true;
        if (IsHeal)
        {
            f.Health.Heal(f.Health.Max * healAmount);
            if (f.View != null) f.View.FlashHeal();
        }
        else f.ApplyBuff(Stat, boostMultiplier, boostDuration);
        Destroy(gameObject);
    }
}
