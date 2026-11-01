using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Снаряд: авто атака Стрелка (пуля) или снаряд личного скила (волна, звёздочка, сердце, мышь...).
/// Обычно летит горизонтально в сторону взгляда стрелка (можно с уклоном по глубине — веер звёздочек),
/// попадает в первого противника на той же глубине и исчезает (пробивающий — летит дальше). Союзников не задевает
/// (снаряд зачарованного Шармом бойца, наоборот, бьёт его союзников).
/// Самонаводящийся (ShotInfo.homing) летит за целью; ShotInfo.lifetime — сколько секунд живёт.
/// ShotInfo.noDamage — не ранит (только ShotInfo.onHit: например, Шарм). onHit вызывается после попадания.
/// Авто атаки разбивают коробки. Исчезает, пролетев свою дальность, или за краем арены.
/// Положение объекта — "на земле" (для глубины), картинка поднята на высоту груди.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Tooltip("Радиус попадания по горизонтали")]
    [SerializeField] private float hitRadius = 0.5f;
    [Tooltip("Насколько цель может отличаться по глубине")]
    [SerializeField] private float depthTolerance = 0.45f;
    [Tooltip("Картинка снаряда (поднимается на высоту груди)")]
    [SerializeField] private Transform visual;
    [Tooltip("Части, которые красятся в цвет стрелка")]
    [SerializeField] private SpriteRenderer[] tinted;
    [Tooltip("Для правильного перекрытия по глубине")]
    [SerializeField] private SortingGroup sortingGroup;

    private Fighter owner;          // Кто выстрелил
    private BattleFaction faction;  // Его сторона (снаряд бьёт только её противников)
    private bool charmed;           // Стрелок был зачарован — снаряд бьёт его союзников
    private ShotInfo shot;          // Параметры выстрела
    private Vector2 velocity;       // Скорость полёта (X — по горизонтали, Y — по глубине)
    private int attackId;           // Номер атаки (защита от двойного урона)
    private float travelled;        // Сколько уже пролетел
    private readonly List<Fighter> alreadyHit = new List<Fighter>(); // Кого уже задел (для пробивающих)
    private SpriteRenderer effect;  // Картинка-кадры снаряда (если у скила есть свои кадры)
    private Color baseTint;         // Цвет снаряда (для мерцания)
    private float clock;            // Время полёта (для кадров и срока жизни)

    private const float EffectFps = 12f;   // Скорость кадров снаряда
    private const float HomingTurn = 6f;   // Как быстро самонаводящийся снаряд поворачивает к цели

    /// <summary>Где сейчас картинка снаряда (на высоте груди) — к ней тянется верёвка Энерго стрелы.</summary>
    public Vector3 VisualPosition => visual != null ? visual.position : transform.position;

    /// <summary>Запустить снаряд.</summary>
    public void Launch(Fighter shooter, int dir, ShotInfo info, int id, float height)
    {
        owner = shooter;
        faction = shooter.Faction;
        charmed = shooter.IsCharmed;
        shot = info;
        velocity = new Vector2(dir * info.speed, info.driftY);
        attackId = id;
        if (visual != null)
        {
            visual.localPosition = new Vector3(0f, height, 0f);
            visual.localScale = new Vector3(dir * info.size * (info.pierce ? 1.6f : 1f), info.size, 1f); // "хвост" позади
        }
        // Свои кадры снаряда (волна Стажёра): обычная картинка прячется
        if (info.frames != null && info.frames.Length > 0 && visual != null)
        {
            foreach (SpriteRenderer r in visual.GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = false; // прячем все части обычного снаряда (тень на земле остаётся)
            var go = new GameObject("Effect");
            go.transform.SetParent(visual, false);
            effect = go.AddComponent<SpriteRenderer>();
            effect.sprite = info.frames[0];
            effect.sortingOrder = 10;
            return;
        }
        Color c = info.color.a > 0f ? info.color : shooter.Data.color;
        baseTint = c;
        if (tinted != null)
            foreach (SpriteRenderer r in tinted)
                if (r != null) r.color = new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), r.color.a);
    }

    /// <summary>Полёт и проверка попадания.</summary>
    private void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || !bm.IsRunning) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        clock += dt;

        // Самонаведение: плавно поворачиваем к цели (цель выбыла — летим прямо)
        if (shot.homing != null && shot.homing.IsAlive)
        {
            Vector2 want = (shot.homing.Position - (Vector2)transform.position).normalized * shot.speed;
            velocity = Vector2.Lerp(velocity, want, 1f - Mathf.Exp(-HomingTurn * dt));
            if (visual != null && Mathf.Abs(velocity.x) > 0.05f)
                visual.localScale = new Vector3(Mathf.Sign(velocity.x) * Mathf.Abs(visual.localScale.x), visual.localScale.y, 1f);
        }

        Vector3 pos = transform.position + (Vector3)(velocity * dt);
        travelled += velocity.magnitude * dt;
        transform.position = pos;
        if (sortingGroup != null) sortingGroup.sortingOrder = bm.DepthSortingOrder(pos.y) + 50;
        if (effect != null) effect.sprite = CharacterSprites.Frame(shot.frames, clock, EffectFps);
        if (shot.flicker.a > 0f && tinted != null) // мерцание между двумя цветами
        {
            Color c = Color.Lerp(baseTint, shot.flicker, Mathf.PingPong(clock * 8f, 1f));
            foreach (SpriteRenderer r in tinted)
                if (r != null) r.color = new Color(c.r, c.g, c.b, r.color.a);
        }

        bool expired = shot.lifetime > 0f ? clock > shot.lifetime : travelled > shot.range;
        if (expired || !bm.IsInsideArenaX(pos.x)) { Destroy(gameObject); return; }

        IReadOnlyList<Fighter> targets = charmed ? bm.GetAllies(faction) : bm.GetOpponents(faction);
        foreach (Fighter enemy in targets)
        {
            if (enemy == owner || !enemy.IsAlive || alreadyHit.Contains(enemy)) continue;
            Vector2 d = enemy.Position - (Vector2)pos;
            if (Mathf.Abs(d.x) > hitRadius || Mathf.Abs(d.y) > depthTolerance) continue;

            if (shot.noDamage)
            {
                if (!enemy.IsTargetable) continue; // лежачий и неуязвимый не ловят снаряд
            }
            else
            {
                var hit = new HitInfo
                {
                    attacker = owner,
                    attackId = attackId,
                    damage = FighterCombat.CalcDamage(shot.power, shot.vsSpecial ? enemy.Stats.specialDefense : enemy.Stats.defense),
                    knockback = new Vector2(Mathf.Sign(velocity.x) * shot.knockback, 0f),
                    stagger = shot.stagger,
                    special = shot.vsSpecial,
                    auto = shot.auto
                };
                if (!enemy.Health.TakeHit(hit)) continue;
                enemy.ApplyStun(shot.stun);
                enemy.ApplySilence(shot.silence);
                if (shot.auto && owner != null && owner.Kit != null) owner.Kit.OnAutoHit(enemy);
            }
            alreadyHit.Add(enemy);
            shot.onHit?.Invoke(enemy);
            if (!shot.pierce) { Destroy(gameObject); return; }
        }

        // Авто атаки разбивают коробки
        if (shot.hitsBoxes)
            foreach (BattleBox box in bm.Boxes)
            {
                if (box == null || !box.CanBeHit) continue;
                Vector2 d = (Vector2)box.transform.position - (Vector2)pos;
                if (Mathf.Abs(d.x) > hitRadius || Mathf.Abs(d.y) > depthTolerance) continue;
                box.Hit();
                Destroy(gameObject);
                return;
            }
    }
}
