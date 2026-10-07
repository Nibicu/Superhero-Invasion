using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Коробка на арене. Её разбивают авто атаками (каждая авто атака — один удар).
/// Разбитая коробка с шансом buffChance оставляет бафф (BattleBuffItem):
/// лечение +30% HP или временное усиление случайного параметра.
/// Расставляет коробки BattleManager перед началом боя (не больше 5).
/// Внешний вид — в префабе Assets/_Game/Prefabs/BattleBox (можно заменить спрайт).
/// </summary>
public class BattleBox : MonoBehaviour
{
    [Tooltip("Сколько авто атак нужно, чтобы разбить коробку")]
    [SerializeField] private int hitsToBreak = 3;
    [Tooltip("Шанс, что из коробки выпадет бафф (0.5 = 50%)")]
    [SerializeField, Range(0f, 1f)] private float buffChance = 0.5f;
    [Tooltip("Картинка коробки (трясётся от ударов)")]
    [SerializeField] private Transform visual;
    [Tooltip("Для правильного перекрытия по глубине")]
    [SerializeField] private SortingGroup sortingGroup;

    private int hitsLeft;     // Сколько ударов осталось
    private float shakeTimer; // Тряска после удара

    /// <summary>Разбита ли коробка.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>Положение на арене.</summary>
    public Vector2 Position => transform.position;

    private void Awake() => hitsLeft = hitsToBreak;

    private void Start()
    {
        if (sortingGroup != null && BattleManager.Instance != null)
            sortingGroup.sortingOrder = BattleManager.Instance.DepthSortingOrder(transform.position.y);
    }

    /// <summary>Удар авто атакой. Последний удар разбивает коробку.</summary>
    public void Hit()
    {
        if (IsBroken) return;
        hitsLeft--;
        shakeTimer = 0.2f;
        if (hitsLeft > 0) return;

        IsBroken = true;
        if (Random.value < buffChance && BattleManager.Instance != null)
            BattleManager.Instance.SpawnBuff(transform.position);
        Destroy(gameObject);
    }

    /// <summary>Тряска после удара.</summary>
    private void Update()
    {
        if (visual == null || shakeTimer <= 0f) return;
        shakeTimer -= Time.deltaTime;
        float s = shakeTimer > 0f ? Mathf.Sin(Time.time * 60f) * 0.06f : 0f;
        visual.localPosition = new Vector3(s, visual.localPosition.y, 0f);
    }
}
