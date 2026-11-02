using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Внешний вид бойца.
/// СПРАЙТЫ: если у героя (HeroData.sprites) или по умолчанию (defaultSprites) есть спрайты персонажа —
/// боец рисуется покадровой анимацией: покой, ходьба, бег, атака (CharacterSprites).
/// Иначе — запасной "человечек из квадратов" с процедурными позами:
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

    [Header("Спрайты персонажа")]
    [Tooltip("Спрайты по умолчанию — у всех героев, кому не нарисовали свои (Data/Characters/Sprites_Default)")]
    [SerializeField] private CharacterSprites defaultSprites;
    [Tooltip("Цвет шара щита вокруг бойца (Барьер Солдата), прозрачность 15%")]
    [SerializeField] private Color shieldBubbleColor = new Color(0.05f, 0.35f, 0.12f, 0.15f);
    [Tooltip("Насколько затемнён клон (Фантом): цвет, на который умножается картинка")]
    [SerializeField] private Color cloneTint = new Color(0.22f, 0.22f, 0.28f, 1f);
    [Tooltip("Цвет шкалы бега под полоской HP")]
    [SerializeField] private Color staminaColor = new Color(1f, 0.85f, 0.3f);

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
    private SpriteRenderer aura;         // Аура баффа (копия кружка под ногами)
    private float healTimer;             // Зелёная вспышка лечения
    private SpriteRenderer bloodDrop;    // Капля крови рядом с полоской HP (кровотечение)
    private static Sprite dropSprite;    // Картинка капли (рисуется один раз)
    private Color buffColor = new Color(1f, 0.85f, 0.25f); // Цвет ауры баффа
    private CharacterSprites sheet;      // Спрайты персонажа (null — человечек из квадратов)
    private SpriteRenderer spriteBody;   // Картинка персонажа (кадры анимации)
    private Sprite[] currentFrames;      // Какая анимация сейчас играет
    private int animCycle;               // Последний повтор анимации скила (HeroKit.AnimCycle)
    private int kneeCycle;               // Последний удар коленом (FighterCombat.KneeCycle)
    private SpriteRenderer shieldBubble; // Шар щита вокруг бойца
    private float animClock;             // Время текущей анимации
    private Transform staminaFill;       // Заполнение шкалы бега
    private GameObject staminaRoot;      // Шкала бега (видна, только когда не полная)
    private float staminaWidth = 1.04f;  // Полная ширина шкалы бега
    private Transform healthBar, nameTagRoot; // Полоска HP и имя — поднимаются вместе с бойцом в воздухе
    private float barBaseY, tagBaseY, bubbleBaseY; // Их высота, когда боец на земле
    /// <summary>Спрайты персонажа (null — человечек из квадратов). Скилы берут отсюда кадры снарядов.</summary>
    public CharacterSprites Sheet => sheet;

    /// <summary>Круглый спрайт (как кружок команды) — для эффектов скилов (дым и т.п.).</summary>
    public Sprite CircleSprite => teamRing != null ? teamRing.sprite : null;

    private string popText;              // Короткая надпись над головой ("МЕТКО!", "БАРЬЕР")
    private Color popColor;              // Её цвет
    private float popTimer;              // Сколько ещё видна

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

        // Спрайты персонажа: свои у героя или по умолчанию
        sheet = f.Data.sprites != null ? f.Data.sprites : defaultSprites;
        if (sheet != null) SetupSprite();
        SetupStaminaBar();

        allParts = visualRoot.GetComponentsInChildren<SpriteRenderer>(true); // только тело, без полоски HP
        baseColors = new Color[allParts.Length];
        for (int i = 0; i < allParts.Length; i++) baseColors[i] = allParts[i].color;

        // Аура баффа — копия кружка команды (позже можно заменить своим спрайтом)
        if (teamRing != null)
        {
            aura = Instantiate(teamRing, teamRing.transform.parent);
            aura.name = "BuffAura";
            aura.transform.localScale = teamRing.transform.localScale * 1.6f;
            aura.sortingOrder = teamRing.sortingOrder - 1;
            aura.gameObject.SetActive(false);
        }

        SetupShieldBubble();
        SetupBloodDrop();

        // Высота полоски HP, имени и шара щита на земле (в прыжке они поднимаются вместе с бойцом)
        healthBar = transform.Find("HealthBar");
        nameTagRoot = transform.Find("NameTag");
        if (healthBar != null) barBaseY = healthBar.localPosition.y;
        if (nameTagRoot != null) tagBaseY = nameTagRoot.localPosition.y;
        if (shieldBubble != null) bubbleBaseY = shieldBubble.transform.localPosition.y;

        f.StateChanged += OnStateChanged;
        f.Combat.AttackStarted += OnAttackStarted;
        f.BuffChanged += OnBuffChanged;
        f.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Включить спрайты персонажа: части "человечка" прячутся, появляется картинка с кадрами.
    /// Ноги — в точке бойца; полоска HP и имя опускаются к голове персонажа.
    /// </summary>
    private void SetupSprite()
    {
        foreach (Transform c in visualRoot) c.gameObject.SetActive(false);
        var go = new GameObject("SpriteBody");
        go.transform.SetParent(visualRoot, false);
        spriteBody = go.AddComponent<SpriteRenderer>();
        Sprite first = CharacterSprites.Frame(sheet.idle, 0f, 1f);
        spriteBody.sprite = first;
        spriteBody.sortingOrder = body != null ? body.sortingOrder : 4;
        float ppu = first != null ? first.pixelsPerUnit : 32f;
        float unit = sheet.scale / ppu; // единиц мира на 1 пиксель
        go.transform.localScale = new Vector3(sheet.scale, sheet.scale, 1f);
        go.transform.localPosition = new Vector3(sheet.offsetXPx * unit, sheet.feetBelowCenterPx * unit, 0f);

        // Голова персонажа ниже, чем у "человечка" — опускаем полоску HP и имя
        float top = first != null ? (first.rect.height / 2f + sheet.feetBelowCenterPx) * unit * 0.5f + 0.35f : 2.2f;
        Transform bar = transform.Find("HealthBar"), tag = transform.Find("NameTag");
        if (bar != null) bar.localPosition = new Vector3(bar.localPosition.x, Mathf.Min(bar.localPosition.y, top), 0f);
        if (tag != null && bar != null) tag.localPosition = new Vector3(tag.localPosition.x, bar.localPosition.y + 0.28f, 0f);
    }

    /// <summary>Шар щита: круг вокруг бойца (тот же круглый спрайт, что у кружка команды), почти прозрачный.</summary>
    private void SetupShieldBubble()
    {
        if (teamRing == null) return;
        var go = new GameObject("ShieldBubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.85f, 0f); // центр — на уровне груди
        shieldBubble = go.AddComponent<SpriteRenderer>();
        shieldBubble.sprite = teamRing.sprite;
        shieldBubble.color = shieldBubbleColor;
        shieldBubble.sortingOrder = (body != null ? body.sortingOrder : 4) + 20; // поверх персонажа
        float w = teamRing.sprite != null ? teamRing.sprite.bounds.size.x : 1f;
        float size = 2f / Mathf.Max(0.01f, w); // шар диаметром ~2 единицы
        go.transform.localScale = new Vector3(size, size, 1f);
        go.SetActive(false);
    }

    /// <summary>Капля крови слева от полоски HP — видна, пока идёт кровотечение (Сойка).</summary>
    private void SetupBloodDrop()
    {
        Transform bar = transform.Find("HealthBar");
        var go = new GameObject("BloodDrop");
        go.transform.SetParent(bar != null ? bar : transform, false);
        go.transform.localPosition = new Vector3(-0.72f, 0.02f, 0f);
        bloodDrop = go.AddComponent<SpriteRenderer>();
        bloodDrop.sprite = DropSprite();
        bloodDrop.sortingOrder = (body != null ? body.sortingOrder : 4) + 30;
        go.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        go.SetActive(false);
    }

    /// <summary>Картинка капли крови 9×12 пикселей (рисуется кодом один раз; потом можно заменить своей).</summary>
    private static Sprite DropSprite()
    {
        if (dropSprite != null) return dropSprite;
        string[] map =
        {
            "....#....",
            "....#....",
            "...###...",
            "...###...",
            "..#####..",
            "..#####..",
            ".###o###.",
            ".##oo###.",
            ".##o####.",
            ".#######.",
            "..#####..",
            "...###...",
        };
        int w = map[0].Length, h = map.Length;
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = map[h - 1 - y][x];
                tex.SetPixel(x, y, c == '#' ? new Color(0.85f, 0.05f, 0.1f) : c == 'o' ? new Color(1f, 0.55f, 0.55f) : Color.clear);
            }
        tex.Apply();
        dropSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        return dropSprite;
    }

    /// <summary>Это клон (Фантом): затемнить картинку и подписать имя.</summary>
    public void MarkClone()
    {
        if (baseColors != null)
            for (int i = 0; i < baseColors.Length; i++)
                baseColors[i] = new Color(baseColors[i].r * cloneTint.r, baseColors[i].g * cloneTint.g, baseColors[i].b * cloneTint.b, baseColors[i].a);
        RefreshTag();
    }

    /// <summary>Шкала бега под полоской HP (копия полоски, жёлтая и тоньше).</summary>
    private void SetupStaminaBar()
    {
        Transform bar = transform.Find("HealthBar");
        if (bar == null) return;
        Transform bg = bar.Find("Bg"), fill = bar.Find("Fill");
        if (bg == null || fill == null) return;
        staminaRoot = new GameObject("StaminaBar");
        staminaRoot.transform.SetParent(bar, false);
        staminaRoot.transform.localPosition = new Vector3(0f, -0.13f, 0f);
        Transform b = Instantiate(bg, staminaRoot.transform);
        b.localPosition = Vector3.zero;
        b.localScale = new Vector3(bg.localScale.x, bg.localScale.y * 0.55f, 1f);
        staminaFill = Instantiate(fill, staminaRoot.transform);
        staminaFill.localPosition = Vector3.zero;
        staminaFill.localScale = new Vector3(fill.localScale.x, fill.localScale.y * 0.55f, 1f);
        staminaWidth = fill.localScale.x;
        var sr = staminaFill.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = staminaColor;
        staminaRoot.SetActive(false);
    }

    /// <summary>Шкала бега: видна, только пока не полная.</summary>
    private void UpdateStaminaBar()
    {
        if (staminaRoot == null) return;
        float s = fighter.Movement.Stamina;
        bool show = fighter.IsAlive && s < 0.999f;
        if (staminaRoot.activeSelf != show) staminaRoot.SetActive(show);
        if (!show) return;
        float w = staminaWidth * s;
        staminaFill.localScale = new Vector3(w, staminaFill.localScale.y, 1f);
        staminaFill.localPosition = new Vector3(-staminaWidth / 2f + w / 2f, 0f, 0f);
    }

    /// <summary>Кадр анимации по состоянию: покой, ходьба, бег, атака, скил.</summary>
    private void UpdateSpriteFrame()
    {
        Sprite[] frames;
        float fps;
        bool once = false; // кадры скила играют один раз
        SkillSprites skill = fighter.Kit != null && fighter.IsAlive ? sheet.GetSkill(fighter.Kit.AnimSkill) : null;
        if (skill != null && skill.HasFrames)
        {
            frames = skill.frames;
            fps = skill.fps;
            once = true;
            if (fighter.Kit.AnimCycle != animCycle) { animCycle = fighter.Kit.AnimCycle; currentFrames = null; } // повтор (каждая пуля)
        }
        else switch (fighter.State)
        {
            case FighterState.Moving:
                bool run = fighter.Movement.IsRunning && sheet.run != null && sheet.run.Length > 0;
                frames = run ? sheet.run : sheet.walk;
                fps = run ? sheet.runFps : sheet.walkFps;
                break;
            case FighterState.Attacking:
                if (fighter.Kit != null && fighter.Kit.Pose == KitPose.Roll) { frames = sheet.run; fps = sheet.runFps * 1.5f; }
                else if (fighter.Combat.IsDashAttack) { frames = Has(sheet.dashAttack) ? sheet.dashAttack : sheet.GetAttack(3, true); fps = 10f; }
                else { frames = sheet.GetAttack(fighter.Combat.ComboStep, fighter.Combat.IsFinisher); fps = 8f; }
                break;
            case FighterState.Idle:
                frames = sheet.idle;
                fps = sheet.idleFps;
                break;
            case FighterState.Dizzy:
                frames = Has(sheet.dizzy) ? sheet.dizzy : sheet.idle;
                fps = sheet.idleFps;
                break;
            case FighterState.Held:
                frames = Has(sheet.held) ? sheet.held : sheet.idle;
                fps = Has(sheet.held) ? sheet.idleFps : 0f;
                break;
            case FighterState.Jumping:
                if (fighter.Combat.IsJumpAttacking) { frames = Has(sheet.jumpAttack) ? sheet.jumpAttack : sheet.GetAttack(3, true); fps = 10f; }
                else if (Has(sheet.jump)) { frames = sheet.jump; fps = 10f; }
                else { frames = Has(sheet.run) ? sheet.run : sheet.idle; fps = 0f; } // первый кадр бега
                break;
            case FighterState.Airborne:
                frames = Has(sheet.launched) ? sheet.launched : sheet.idle;
                fps = Has(sheet.launched) ? 10f : 0f;
                break;
            case FighterState.Holding:
                bool kneeNow = fighter.Combat.SinceKnee >= 0f && fighter.Combat.SinceKnee < 0.25f;
                frames = kneeNow ? (Has(sheet.knee) ? sheet.knee : sheet.attack) : (Has(sheet.grab) ? sheet.grab : sheet.attack);
                fps = 10f;
                once = kneeNow;
                if (fighter.Combat.KneeCycle != kneeCycle) { kneeCycle = fighter.Combat.KneeCycle; currentFrames = null; } // новый удар
                break;
            default: // реакция на удар, лежит, выбыл — первый кадр покоя (наклон и падение делает поза)
                frames = sheet.idle;
                fps = 0f;
                break;
        }
        if (frames == null || frames.Length == 0) frames = sheet.idle;
        if (frames != currentFrames) { currentFrames = frames; animClock = 0f; }
        animClock += Time.deltaTime;
        Sprite s = once ? CharacterSprites.FrameOnce(frames, animClock, fps) : CharacterSprites.Frame(frames, animClock, fps);
        if (s != null) spriteBody.sprite = s;
    }

    /// <summary>Есть ли кадры.</summary>
    private static bool Has(Sprite[] frames) => frames != null && frames.Length > 0;

    /// <summary>Короткая надпись над головой на полсекунды ("МЕТКО!", "БАРЬЕР").</summary>
    public void Pop(string text, Color color)
    {
        popText = text;
        popColor = color;
        popTimer = 0.7f;
        RefreshTag();
    }

    private void OnStatusChanged(Fighter f) => RefreshTag();

    /// <summary>
    /// Подпись над головой: имя, бафф, эффекты (стан, немота, щит) и короткая надпись.
    /// </summary>
    private void RefreshTag()
    {
        if (nameTag == null || fighter == null) return;
        string t = fighter.Data.displayName + (fighter.IsClone ? " (клон)" : "");
        if (fighter.HasBuff) t += $"\n<size=75%><color=#FFD84A>+{BoostNames.Get(fighter.BuffStat)}</color></size>";
        string status = "";
        if (fighter.State == FighterState.Dizzy) status += "<color=#FFE066>ШАТАЕТСЯ</color> ";
        if (fighter.State == FighterState.Held) status += "<color=#FFB060>В ЗАХВАТЕ</color> ";
        if (fighter.IsStunned) status += "<color=#9FD0FF>СТАН</color> ";
        if (fighter.IsSilenced) status += "<color=#C58BFF>НЕМОТА</color> ";
        if (fighter.IsCharmed) status += "<color=#FF8AD8>ШАРМ</color> ";
        if (fighter.IsCursed) status += "<color=#B070FF>ПРОКЛЯТ</color> ";
        if (fighter.Shield > 0f) status += $"<color=#7FD8FF>ЩИТ {Mathf.CeilToInt(fighter.Shield)}</color>";
        if (status != "") t += $"\n<size=70%>{status.Trim()}</size>";
        if (popTimer > 0f && !string.IsNullOrEmpty(popText))
            t += $"\n<size=80%><b><color=#{ColorUtility.ToHtmlStringRGB(popColor)}>{popText}</color></b></size>";
        nameTag.text = t;
    }

    /// <summary>Зелёная вспышка — бойца вылечили.</summary>
    public void FlashHeal() => healTimer = 0.45f;

    /// <summary>Бафф начался/закончился: аура и подпись над головой.</summary>
    private void OnBuffChanged(Fighter f)
    {
        if (aura != null) aura.gameObject.SetActive(f.HasBuff);
        RefreshTag();
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
        else RefreshTag(); // "ШАТАЕТСЯ", "В ЗАХВАТЕ" 
    }

    private void OnAttackStarted()
    {
        if (animator != null) animator.SetTrigger("Attack");
    }

    private void LateUpdate()
    {
        // Глубина: чем ниже на экране (меньше Y), тем выше порядок отрисовки
        if (sortingGroup != null && BattleManager.Instance != null)
            sortingGroup.sortingOrder = BattleManager.Instance.DepthSortingOrder(transform.position.y);
        if (visualRoot == null) return;

        UpdateEffects();
        if (popTimer > 0f)
        {
            popTimer -= Time.deltaTime;
            if (popTimer <= 0f) RefreshTag();
        }
        int facing = fighter.Movement.Facing;
        visualRoot.localScale = new Vector3(facing, 1f, 1f);
        if (proceduralAnimation) AnimatePose(facing);
        if (spriteBody != null) UpdateSpriteFrame();
        if (shieldBubble != null)
        {
            bool on = fighter.Shield > 0f && fighter.IsAlive;
            if (shieldBubble.gameObject.activeSelf != on) shieldBubble.gameObject.SetActive(on);
        }
        if (bloodDrop != null)
        {
            bool bleeding = fighter.IsBleeding && fighter.IsAlive;
            if (bloodDrop.gameObject.activeSelf != bleeding) bloodDrop.gameObject.SetActive(bleeding);
            if (bleeding) bloodDrop.transform.localPosition = new Vector3(-0.72f, 0.02f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.06f, 0f); // капает
        }
        UpdateStaminaBar();
        UpdateColors();
        LiftWithHeight();
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
            case FighterState.Attacking when fighter.Kit != null && fighter.Kit.Pose != KitPose.None:
                // Позы личных скилов
                float kt = fighter.Kit.ActionTime;
                switch (fighter.Kit.Pose)
                {
                    case KitPose.Aim: // винтовка: обе руки вперёд, отдача
                        armAngle = 0f;
                        backArmAngle = 90f;
                        armStretch = 1.4f;
                        offset.x = -0.04f * facing * Mathf.Abs(Mathf.Sin(kt * Mathf.PI * 2f));
                        break;
                    case KitPose.Roll: // кувырок
                        rotZ = -360f * Mathf.Clamp01(kt / 0.45f) * facing;
                        offset.y = -0.2f;
                        break;
                    default: // Cast: руки вперёд
                        armAngle = 10f;
                        armStretch = 1.5f;
                        backArmAngle = 60f;
                        break;
                }
                break;
            case FighterState.Attacking when fighter.Combat.IsDashAttack: // удар с разбега: наклон вперёд, рука вытянута
                armAngle = 15f;
                armStretch = 1.7f;
                rotZ = -22f * facing;
                offset.y = 0.12f * Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI); // небольшой прыжок
                break;
            case FighterState.Attacking:
                float p = Mathf.Clamp01(t / fighter.attackDuration);
                float punch = Mathf.Sin(p * Mathf.PI); // 0 → 1 → 0
                bool fin = fighter.Combat.IsFinisher;   // добивающий — шире замах и рывок вперёд
                armAngle = Mathf.Lerp(-70f, fin ? 20f : 0f, punch);
                armStretch = 1f + punch * (fin ? 0.9f : 0.6f);
                rotZ = (fin ? -16f : -8f) * punch * facing;
                if (fin) offset.x = 0.12f * punch * facing;
                break;
            case FighterState.Hurt:
                rotZ = 14f * facing;
                offset.x = -0.05f * facing;
                armAngle = -20f;
                break;
            case FighterState.Jumping: // прыжок: ноги поджаты; удар в прыжке — наклон вперёд, рука вытянута
                legSwing = 0.08f;
                if (fighter.Combat.IsJumpAttacking)
                {
                    rotZ = -20f * facing;
                    armAngle = 10f;
                    armStretch = 1.7f;
                }
                break;
            case FighterState.Airborne: // подброшен: откинулся назад, медленно переворачивается
                rotZ = (30f + fighter.StateTime * 120f) * facing;
                armAngle = 40f;
                break;
            case FighterState.Dizzy: // шатается: раскачивается из стороны в сторону
                rotZ = Mathf.Sin(Time.time * 7f) * 10f;
                offset.x = Mathf.Sin(Time.time * 7f) * 0.05f;
                armAngle = -40f + Mathf.Sin(Time.time * 7f) * 20f;
                break;
            case FighterState.Held: // схвачен: согнулся вперёд, вздрагивает от ударов
                rotZ = -18f * facing;
                offset.y = 0.03f;
                break;
            case FighterState.Holding: // держит: руки вперёд, на удар коленом — толчок вперёд
                float sk = fighter.Combat.SinceKnee;
                float kick = sk >= 0f && sk < 0.25f ? Mathf.Sin(sk / 0.25f * Mathf.PI) : 0f;
                armAngle = 0f;
                armStretch = 1.3f;
                backArmAngle = 60f;
                rotZ = -10f * kick * facing;
                offset.x = 0.08f * kick * facing;
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

        offset.y += fighter.Movement.Height; // в воздухе — картинка выше (тень остаётся на земле)
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

    /// <summary>Полоска HP, имя и шар щита поднимаются вместе с бойцом, когда он в воздухе.</summary>
    private void LiftWithHeight()
    {
        float h = fighter.Movement.Height;
        if (healthBar != null) healthBar.localPosition = new Vector3(healthBar.localPosition.x, barBaseY + h, 0f);
        if (nameTagRoot != null) nameTagRoot.localPosition = new Vector3(nameTagRoot.localPosition.x, tagBaseY + h, 0f);
        if (shieldBubble != null) shieldBubble.transform.localPosition = new Vector3(0f, bubbleBaseY + h, 0f);
    }

    /// <summary>Пульсация ауры баффа.</summary>
    private void UpdateEffects()
    {
        if (aura != null && aura.gameObject.activeSelf)
        {
            float pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 6f);
            aura.color = new Color(buffColor.r, buffColor.g, buffColor.b, fighter.IsAlive ? pulse : 0f);
        }
        if (healTimer > 0f) healTimer -= Time.deltaTime;
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
            else if (healTimer > 0f) c = Color.Lerp(c, new Color(0.4f, 1f, 0.5f, c.a), 0.6f * healTimer / 0.45f);
            if (dead)
            {
                float grey = c.grayscale;
                c = Color.Lerp(c, new Color(grey, grey, grey, c.a), deadFade);
                c.a *= Mathf.Lerp(1f, 0.4f, deadFade);
            }
            if (fighter.IsStunned && !dead) c = Color.Lerp(c, new Color(0.55f, 0.75f, 1f, c.a), 0.45f); // оглушён — голубоватый
            else if (fighter.IsCharmed && !dead) c = Color.Lerp(c, new Color(1f, 0.55f, 0.85f, c.a), 0.4f); // зачарован — розоватый
            if (blink) c.a *= 0.4f;
            allParts[i].color = c;
        }
    }

    private void OnDestroy()
    {
        if (fighter == null) return;
        fighter.StateChanged -= OnStateChanged;
        fighter.BuffChanged -= OnBuffChanged;
        fighter.StatusChanged -= OnStatusChanged;
        if (fighter.Combat != null) fighter.Combat.AttackStarted -= OnAttackStarted;
    }
}
