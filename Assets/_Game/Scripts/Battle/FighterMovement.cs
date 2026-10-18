using UnityEngine;

/// <summary>
/// Перемещение бойца по арене (как в Little Fighter 2):
/// X — влево/вправо, Y — вглубь/к зрителю. Без физики и гравитации.
/// - Move(dir) задаёт желаемое направление на ЭТОТ кадр (ИИ вызывает каждый кадр).
/// - Диагональ нормализуется (по диагонали не быстрее).
/// - Отбрасывание — отдельная скорость, которая плавно затухает.
/// - Позиция ограничивается границами арены (BattleManager.ClampToArena).
/// - БЕГ: Move(dir, true) — быстрее ходьбы, но тратит шкалу бега (усталость).
///   Шкала пуста — бежать нельзя, пока она не наполнится целиком; потом снова можно.
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

    [Header("Бег")]
    [Tooltip("Во сколько раз бег быстрее ходьбы")]
    [SerializeField] private float runMultiplier = 1.8f;
    [Tooltip("Сколько секунд можно бежать с полной шкалой")]
    [SerializeField] private float runDuration = 1.6f;
    [Tooltip("За сколько секунд пустая шкала бега наполняется целиком (бежать снова можно только с полной)")]
    [SerializeField] private float runRecovery = 3.5f;

    private Fighter fighter;
    private Vector2 input;        // Желаемое направление на этот кадр
    private Vector2 knockVelocity; // Скорость отбрасывания
    private float speed;          // Итоговая скорость ходьбы
    private bool wantRun;         // Хочет бежать в этом кадре
    private bool exhausted;       // Шкала бега кончилась — ждём, пока наполнится целиком

    /// <summary>Шкала бега 0..1 (1 — полная).</summary>
    public float Stamina { get; private set; } = 1f;

    /// <summary>Бежит ли боец прямо сейчас (для анимации).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Можно ли сейчас побежать (шкала не на восстановлении).</summary>
    public bool CanRun => !exhausted && Stamina > 0f;

    /// <summary>Куда смотрит боец: 1 — вправо, -1 — влево.</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>Скорость ходьбы (с учётом баффа скорости).</summary>
    public float Speed => fighter.HasBuff && fighter.BuffStat == BoostStat.Speed
        ? baseSpeed + fighter.Stats.speed * speedPerStat
        : speed;

    private void Awake() => fighter = GetComponent<Fighter>();

    /// <summary>Рассчитать скорость из характеристики "Скорость".</summary>
    public void Setup(int speedStat)
    {
        speed = baseSpeed + speedStat * speedPerStat;
        // Наши и копия охраны для врага смотрят вправо, остальные — влево
        BattleFaction f = fighter.Faction;
        Facing = f == BattleFaction.Heroes || f == BattleFaction.RivalGuards ? 1 : -1;
    }

    /// <summary>
    /// Идти в направлении dir в этом кадре (длина больше 1 обрезается).
    /// run — бежать (если шкала бега позволяет).
    /// </summary>
    public void Move(Vector2 dir, bool run = false)
    {
        input = dir.sqrMagnitude > 1f ? dir.normalized : dir;
        wantRun = run;
    }

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

        bool running = false;
        if (fighter.CanAct)
        {
            if (input.sqrMagnitude > 0.0004f)
            {
                float v = Speed;
                if (wantRun && CanRun)
                {
                    running = true;
                    v *= runMultiplier;
                    Stamina = Mathf.Max(0f, Stamina - dt / Mathf.Max(0.1f, runDuration));
                    if (Stamina <= 0f) exhausted = true; // выдохся — ждать полную шкалу
                }
                pos.x += input.x * v * dt;
                pos.y += input.y * v * depthSpeedFactor * dt;
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

        // Шкала бега восстанавливается, когда боец не бежит
        IsRunning = running;
        if (!running && Stamina < 1f)
        {
            Stamina = Mathf.Min(1f, Stamina + dt / Mathf.Max(0.1f, runRecovery));
            if (Stamina >= 1f) exhausted = false;
        }

        transform.position = BattleManager.Instance.ClampToArena(fighter, pos);
        input = Vector2.zero; // намерение действует только один кадр
        wantRun = false;
    }
}
