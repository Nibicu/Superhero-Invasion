using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Внешний вид бойца. Сейчас — "человечек из квадратов" с процедурными позами:
/// ходьба (ноги), удар (рука вперёд), выстрел (обе руки), реакция на удар (вспышка, наклон),
/// падение и смерть (лежит, смерть — серый и полупрозрачный).
/// Перекрытие по глубине: кто ниже на экране — тот рисуется поверх (SortingGroup).
///
/// Как подключить настоящие спрайты и анимации позже:
/// добавьте Animator в поле animator и выключите proceduralAnimation.
/// В Animator приходят: int "State" (0 Idle, 1 Moving, 2 Attacking, 3 Hurt, 4 KnockedDown, 5 Dead),
/// триггеры "Attack", "Special", "Hurt". Боевая логика при этом не меняется.
/// </summary>
[RequireComponent(typeof(Fighter))]
public class FighterView : MonoBehaviour
{
    [Header("Части тела (временная графика)")]
    [SerializeField] private Transform visualRoot;   // Всё тело (переворачивается по направлению взгляда)
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer head;
    [SerializeField] private SpriteRenderer[] limbs; // Руки и ноги (темнее тела)
    [SerializeField] private SpriteRenderer belt;
    [SerializeField] private Transform armPivot;     // Передняя рука (поворачивается при ударе)
    [SerializeField] private Transform backArm;      // Задняя рука (для выстрела)
    [SerializeField] private Transform legLeft;
    [SerializeField] private Transform legRight;
    [SerializeField] private SpriteRenderer teamRing; // Кружок под ногами — цвет команды
    [SerializeField] private TMP_Text initials;      // Буква на груди
    [SerializeField] private TMP_Text nameTag;       // Имя над головой
    [SerializeField] private SortingGroup sortingGroup;

    [Header("Цвета команд")]
    [SerializeField] private Color playerColor = new Color(0.25f, 0.6f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("Цвет нейтральной охраны в битве за флаг")]
    [SerializeField] private Color neutralColor = new Color(0.75f, 0.75f, 0.75f);

    [Header("Анимации (на будущее)")]
    [Tooltip("Animator с настоящими анимациями (необязательно)")]
    [SerializeField] private Animator animator;
    [Tooltip("Процедурные позы из квадратов. Выключите, если используете Animator")]
    [SerializeField] private bool proceduralAnimation = true;

    private Fighter fighter;
    private SpriteRenderer[] allParts;   // Все части для вспышки и затухания
    private Color[] baseColors;          // Исходные цвета частей
    private float flashTimer;            // Вспышка при попадании
    private float legBaseY;              // Исходная высота ног
    private Vector3 visualBasePos;

    private static readonly int StateParam = Animator.StringToHash("State");

    private void Awake()
    {
        fighter = GetComponent<Fighter>();
        if (legLeft != null) legBaseY = legLeft.localPosition.y;
        if (visualRoot != null) visualBasePos = visualRoot.localPosition;
    }

    /// <summary>Покрасить бойца в цвета героя и команды.</summary>
    public void Setup(Fighter f)
    {
        Color c = f.Data.color;
        Color dark = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f);
        Color light = Color.Lerp(c, Color.white, 0.35f);
        if (body != null) body.color = c;
        if (head != null) head.color = light;
        if (belt != null) belt.color = dark * 0.8f + new Color(0, 0, 0, 0.2f);
        if (limbs != null) foreach (SpriteRenderer r in limbs) if (r != null) r.color = dark;
        if (teamRing != null)
        {
            Color t = f.NeutralLook ? neutralColor : f.Team == Team.Player ? playerColor : enemyColor;
            teamRing.color = new Color(t.r, t.g, t.b, 0.75f);
        }
        if (initials != null) initials.text = f.Data.Initials;
        if (nameTag != null)
        {
            nameTag.text = f.Data.displayName;
            nameTag.color = f.NeutralLook ? new Color(0.85f, 0.85f, 0.85f)
                : f.Team == Team.Player ? new Color(0.75f, 0.88f, 1f) : new Color(1f, 0.75f, 0.75f);
        }

        allParts = visualRoot.GetComponentsInChildren<SpriteRenderer>(true); // только тело, без полоски HP
        baseColors = new Color[allParts.Length];
        for (int i = 0; i < allParts.Length; i++) baseColors[i] = allParts[i].color;

        f.StateChanged += OnStateChanged;
        f.Combat.AttackStarted += OnAttackStarted;
    }

    /// <summary>Вспышка при попадании.</summary>
    public void FlashHit()
    {
        flashTimer = 0.12f;
        if (animator != null) animator.SetTrigger("Hurt");
    }

    private void OnStateChanged(Fighter f, FighterState state)
    {
        if (animator != null) animator.SetInteger(StateParam, (int)state);
        if (state == FighterState.Dead && nameTag != null) nameTag.gameObject.SetActive(false);
    }

    private void OnAttackStarted(bool special)
    {
        if (animator != null) animator.SetTrigger(special ? "Special" : "Attack");
    }

    private void LateUpdate()
    {
        // Глубина: чем ниже на экране (меньше Y), тем выше порядок отрисовки
        if (sortingGroup != null && BattleManager.Instance != null)
            sortingGroup.sortingOrder = BattleManager.Instance.DepthSortingOrder(transform.position.y);
        if (visualRoot == null) return;

        int facing = fighter.Movement.Facing;
        visualRoot.localScale = new Vector3(facing, 1f, 1f);
        if (proceduralAnimation) AnimatePose(facing);
        UpdateColors();
    }

    /// <summary>Процедурные позы по состоянию.</summary>
    private void AnimatePose(int facing)
    {
        float t = fighter.StateTime;
        float rotZ = 0f;           // Наклон всего тела
        Vector3 offset = Vector3.zero;
        float armAngle = -70f;     // Рука опущена
        float armStretch = 1f;
        float backArmAngle = 0f;
        float legSwing = 0f;

        switch (fighter.State)
        {
            case FighterState.Idle:
                offset.y = Mathf.Sin(Time.time * 3f) * 0.02f;
                break;
            case FighterState.Moving:
                legSwing = Mathf.Sin(Time.time * 14f) * 0.09f;
                offset.y = Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.05f;
                armAngle = -70f + Mathf.Sin(Time.time * 14f) * 20f;
                break;
            case FighterState.Attacking:
                float p = Mathf.Clamp01(t / fighter.attackDuration);
                float punch = Mathf.Sin(p * Mathf.PI); // 0 → 1 → 0
                armAngle = Mathf.Lerp(-70f, 0f, punch);
                armStretch = 1f + punch * 0.6f;
                rotZ = -8f * punch * facing;
                if (fighter.Combat.IsSpecialAttack) backArmAngle = 90f * punch;
                break;
            case FighterState.Hurt:
                rotZ = 14f * facing;
                offset.x = -0.05f * facing;
                armAngle = -20f;
                break;
            case FighterState.KnockedDown:
                rotZ = 90f * facing;
                offset.y = -0.2f;
                break;
            case FighterState.Dead:
                rotZ = 90f * facing;
                offset.y = -0.25f;
                break;
        }

        visualRoot.localPosition = visualBasePos + offset;
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        if (armPivot != null)
        {
            armPivot.localRotation = Quaternion.Euler(0f, 0f, armAngle);
            armPivot.localScale = new Vector3(armStretch, 1f, 1f);
        }
        if (backArm != null) backArm.localRotation = Quaternion.Euler(0f, 0f, backArmAngle);
        if (legLeft != null) legLeft.localPosition = new Vector3(legLeft.localPosition.x, legBaseY + legSwing, 0f);
        if (legRight != null) legRight.localPosition = new Vector3(legRight.localPosition.x, legBaseY - legSwing, 0f);
    }

    /// <summary>Цвета: вспышка при ударе, мигание при подъёме, серость после смерти.</summary>
    private void UpdateColors()
    {
        if (allParts == null) return;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        bool dead = fighter.State == FighterState.Dead;
        float deadFade = dead ? Mathf.Clamp01(fighter.StateTime / 1.5f) : 0f;
        bool blink = fighter.IsInvulnerable && fighter.State != FighterState.KnockedDown && !dead
                     && Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;

        for (int i = 0; i < allParts.Length; i++)
        {
            Color c = baseColors[i];
            if (flashTimer > 0f) c = Color.Lerp(c, new Color(1f, 0.35f, 0.3f, c.a), 0.7f);
            if (dead)
            {
                float grey = c.grayscale;
                c = Color.Lerp(c, new Color(grey, grey, grey, c.a), deadFade);
                c.a *= Mathf.Lerp(1f, 0.4f, deadFade);
            }
            if (blink) c.a *= 0.4f;
            allParts[i].color = c;
        }
    }

    private void OnDestroy()
    {
        if (fighter == null) return;
        fighter.StateChanged -= OnStateChanged;
        if (fighter.Combat != null) fighter.Combat.AttackStarted -= OnAttackStarted;
    }
}
