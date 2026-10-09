using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ящик на арене. Его разбивают авто атаками (каждая авто атака — один удар).
/// Разбитый ящик с шансом buffChance оставляет бафф (BattleBuffItem):
/// лечение +30% HP или временное усиление случайного параметра.
/// Несколько ящиков стоят с начала боя, остальные падают с неба во время боя (DropIn).
/// Внешний вид — в префабе Assets/_Game/Prefabs/BattleBox (можно заменить спрайт).
/// </summary>
public class BattleBox : MonoBehaviour
{
    [Tooltip("Сколько авто атак нужно, чтобы разбить ящик")]
    [SerializeField] private int hitsToBreak = 3;
    [Tooltip("Шанс, что из ящика выпадет бафф (0.5 = 50%)")]
    [SerializeField, Range(0f, 1f)] private float buffChance = 0.5f;
    [Tooltip("Картинка ящика (трясётся от ударов, падает с неба)")]
    [SerializeField] private Transform visual;
    [Tooltip("Для правильного перекрытия по глубине")]
    [SerializeField] private SortingGroup sortingGroup;

    private int hitsLeft;       // Сколько ударов осталось
    private float shakeTimer;   // Тряска после удара
    private float baseY;        // Обычная высота картинки над землёй
    private float fallHeight;   // На какой высоте над землёй сейчас картинка (пока падает)

    /// <summary>Скорость падения с неба (единиц в секунду).</summary>
    public const float FallSpeed = 12f;

    /// <summary>Разбит ли ящик.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>Приземлился ли (пока падает — разбить нельзя).</summary>
    public bool Landed => fallHeight <= 0f;

    /// <summary>Можно ли сейчас ударить ящик.</summary>
    public bool CanBeHit => !IsBroken && Landed;

    /// <summary>Положение на арене (на земле).</summary>
    public Vector2 Position => transform.position;

    private void Awake()
    {
        hitsLeft = hitsToBreak;
        if (visual != null) baseY = visual.localPosition.y;
    }

    private void Start()
    {
        if (sortingGroup != null && BattleManager.Instance != null)
            sortingGroup.sortingOrder = BattleManager.Instance.DepthSortingOrder(transform.position.y);
    }

    /// <summary>Начать падение с неба с высоты height.</summary>
    public void DropIn(float height) => fallHeight = height;

    /// <summary>Удар авто атакой. Последний удар разбивает ящик.</summary>
    public void Hit()
    {
        if (!CanBeHit) return;
        hitsLeft--;
        shakeTimer = 0.2f;
        if (hitsLeft > 0) return;

        IsBroken = true;
        if (Random.value < buffChance && BattleManager.Instance != null)
            BattleManager.Instance.SpawnBuff(transform.position);
        Destroy(gameObject);
    }

    /// <summary>Падение с неба и тряска после удара.</summary>
    private void Update()
    {
        if (visual == null) return;
        if (fallHeight > 0f) fallHeight = Mathf.Max(0f, fallHeight - FallSpeed * Time.deltaTime);
        float shake = 0f;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            shake = Mathf.Sin(Time.time * 60f) * 0.06f;
        }
        visual.localPosition = new Vector3(shake, baseY + fallHeight, 0f);
    }
}
