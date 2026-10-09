using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Предмет на арене, который подбирает первый подошедший боец. Бывает:
///  - Зелье (падает с неба во время боя): +15% HP;
///  - Бафф из ящика: лечение +30% HP или усиление случайного параметра
///    (авто атака, атака, спец. атака, защита, спец. защита, скорость) +30% на 15 секунд.
/// Внешний вид — в префабе Assets/_Game/Prefabs/BattleBuff.
/// </summary>
public class BattleBuffItem : MonoBehaviour
{
    [Tooltip("Сколько HP (доля от максимума) даёт лечение из ящика")]
    [SerializeField] private float healAmount = 0.3f;
    [Tooltip("Сколько HP (доля от максимума) даёт зелье")]
    [SerializeField] private float potionAmount = 0.15f;
    [Tooltip("Во сколько раз бафф усиливает параметр (1.3 = +30%)")]
    [SerializeField] private float boostMultiplier = 1.3f;
    [Tooltip("Сколько секунд действует усиление")]
    [SerializeField] private float boostDuration = 15f;
    [Tooltip("На каком расстоянии боец подбирает предмет")]
    [SerializeField] private float pickupRadius = 0.6f;

    [Header("Внешний вид")]
    [SerializeField] private SpriteRenderer icon;     // Значок (зелье — красный, лечение — зелёный, усиление — жёлтый)
    [SerializeField] private Transform floatPart;     // Что покачивается вверх-вниз (и падает с неба)
    [SerializeField] private TMP_Text label;          // Надпись "+15% HP", "+HP" или "+Атака"
    [SerializeField] private SortingGroup sortingGroup;

    private float baseY;       // Исходная высота качающейся части
    private float fallHeight;  // На какой высоте сейчас (пока падает с неба)
    private float heal;        // Сколько лечит (доля HP), если это лечение

    /// <summary>Лечит ли (зелье или лечение из ящика). Иначе — усиление параметра.</summary>
    public bool IsHeal { get; private set; }

    /// <summary>Это зелье (падает с неба).</summary>
    public bool IsPotion { get; private set; }

    /// <summary>Какой параметр усиливает (если не лечение).</summary>
    public BoostStat Stat { get; private set; }

    /// <summary>Подобран ли уже.</summary>
    public bool Taken { get; private set; }

    /// <summary>Приземлился ли (пока падает — подобрать нельзя).</summary>
    public bool Landed => fallHeight <= 0f;

    /// <summary>Можно ли подобрать прямо сейчас.</summary>
    public bool Available => !Taken && Landed;

    /// <summary>Положение на арене (на земле).</summary>
    public Vector2 Position => transform.position;

    /// <summary>Бафф из ящика: случайно лечение (+30%) или усиление параметра.</summary>
    public void Setup()
    {
        IsHeal = Random.value < 0.5f;
        Stat = (BoostStat)Random.Range(0, 6);
        heal = healAmount;
        Paint(IsHeal ? new Color(0.35f, 0.9f, 0.45f) : new Color(1f, 0.82f, 0.25f),
              IsHeal ? "+HP" : $"+{BoostNames.Get(Stat)}");
    }

    /// <summary>Зелье: лечит 15% HP.</summary>
    public void SetupPotion()
    {
        IsHeal = true;
        IsPotion = true;
        heal = potionAmount;
        Paint(new Color(0.95f, 0.3f, 0.4f), $"+{Mathf.RoundToInt(potionAmount * 100)}% HP");
    }

    /// <summary>Начать падение с неба с высоты height.</summary>
    public void DropIn(float height) => fallHeight = height;

    private void Paint(Color c, string text)
    {
        if (icon != null) icon.color = c;
        if (label != null) label.text = text;
        if (floatPart != null) baseY = floatPart.localPosition.y;
        if (sortingGroup != null && BattleManager.Instance != null)
            sortingGroup.sortingOrder = BattleManager.Instance.DepthSortingOrder(transform.position.y);
    }

    /// <summary>Падение, покачивание и проверка: не подошёл ли боец.</summary>
    private void Update()
    {
        if (fallHeight > 0f) fallHeight = Mathf.Max(0f, fallHeight - BattleBox.FallSpeed * Time.deltaTime);
        if (floatPart != null)
            floatPart.localPosition = new Vector3(0f, baseY + fallHeight + (Landed ? Mathf.Sin(Time.time * 4f) * 0.08f : 0f), 0f);

        BattleManager bm = BattleManager.Instance;
        if (!Available || bm == null || !bm.IsRunning) return;
        foreach (Fighter f in bm.AllFighters)
        {
            if (!f.IsAlive || f.State == FighterState.KnockedDown) continue;
            Vector2 d = f.Position - Position;
            if (Mathf.Abs(d.x) > pickupRadius || Mathf.Abs(d.y) > pickupRadius * 0.7f) continue;
            PickUp(f);
            return;
        }
    }

    /// <summary>Боец подобрал предмет.</summary>
    private void PickUp(Fighter f)
    {
        Taken = true;
        if (IsHeal)
        {
            f.Health.Heal(f.Health.Max * heal);
            if (f.View != null) f.View.FlashHeal();
        }
        else f.ApplyBuff(Stat, boostMultiplier, boostDuration);
        Destroy(gameObject);
    }
}
