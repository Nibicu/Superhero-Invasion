using UnityEngine;

/// <summary>
/// Перемещение бойца по арене (как в Little Fighter 2):
/// X — влево/вправо, Y — вглубь/к зрителю. Без физики и гравитации.
/// - Move(dir) задаёт желаемое направление на ЭТОТ кадр (ИИ вызывает каждый кадр).
/// - Диагональ нормализуется (по диагонали не быстрее).
/// - Отбрасывание — отдельная скорость, которая плавно затухает.
/// - Позиция ограничивается границами арены (BattleManager.ClampToArena).
/// Rigidbody не используется: так движение предсказуемое и не "застревает".
/// </summary>
[RequireComponent(typeof(Fighter))]
public class FighterMovement : MonoBehaviour
{
    [Tooltip("Базовая скорость (единиц арены в секунду)")]
    [SerializeField] private float baseSpeed = 1.6f;
    [Tooltip("Прибавка скорости за 1 единицу характеристики 'Скорость'")]
    [SerializeField] private float speedPerStat = 0.02f;
    [Tooltip("Движение вглубь медленнее, чем по горизонтали (как в LF2)")]
    [SerializeField] private float depthSpeedFactor = 0.7f;
    [Tooltip("Как быстро гаснет отбрасывание")]
    [SerializeField] private float knockbackDrag = 10f;

    private Fighter fighter;
    private Vector2 input;        // Желаемое направление на этот кадр
    private Vector2 knockVelocity; // Скорость отбрасывания
    private float speed;          // Итоговая скорость ходьбы

    /// <summary>Куда смотрит боец: 1 — вправо, -1 — влево.</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>Скорость ходьбы.</summary>
    public float Speed => speed;

    private void Awake() => fighter = GetComponent<Fighter>();

    /// <summary>Рассчитать скорость из характеристики "Скорость".</summary>
    public void Setup(int speedStat)
    {
        speed = baseSpeed + speedStat * speedPerStat;
        Facing = fighter.Team == Team.Player ? 1 : -1;
    }

    /// <summary>Идти в направлении dir в этом кадре (длина больше 1 обрезается).</summary>
    public void Move(Vector2 dir) => input = dir.sqrMagnitude > 1f ? dir.normalized : dir;

    /// <summary>Повернуться лицом к точке по X.</summary>
    public void FaceTowards(float x)
    {
        float dx = x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) Facing = dx > 0 ? 1 : -1;
    }

    /// <summary>Отбросить бойца (скорость, затухает сама).</summary>
    public void ApplyKnockback(Vector2 velocity) => knockVelocity = velocity;

    /// <summary>Ходьба + отбрасывание + границы арены.</summary>
    private void Update()
    {
        if (BattleManager.Instance == null || !BattleManager.Instance.IsRunning) return;
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;

        if (fighter.CanAct)
        {
            if (input.sqrMagnitude > 0.0004f)
            {
                pos.x += input.x * speed * dt;
                pos.y += input.y * speed * depthSpeedFactor * dt;
                if (Mathf.Abs(input.x) > 0.1f) Facing = input.x > 0 ? 1 : -1;
                if (fighter.State != FighterState.Moving) fighter.SetState(FighterState.Moving);
            }
            else if (fighter.State != FighterState.Idle)
            {
                fighter.SetState(FighterState.Idle);
            }
        }

        if (knockVelocity.sqrMagnitude > 0.0001f)
        {
            pos += (Vector3)(knockVelocity * dt);
            knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, knockbackDrag * dt);
        }

        transform.position = BattleManager.Instance.ClampToArena(fighter.Team, pos);
        input = Vector2.zero; // намерение действует только один кадр
    }
}
