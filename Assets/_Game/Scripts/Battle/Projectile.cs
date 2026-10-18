using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Снаряд: авто атака Стрелка (пуля), Мага и Поддержки (энергетический сгусток)
/// или скил (снаряд Мага, прицельный выстрел Стрелка).
/// Летит горизонтально в сторону взгляда стрелка, попадает в первого противника
/// на той же глубине и исчезает (пробивающий — летит дальше). Союзников не задевает.
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
    private ShotInfo shot;          // Параметры выстрела
    private int direction;          // 1 — вправо, -1 — влево
    private int attackId;           // Номер атаки (защита от двойного урона)
    private float travelled;        // Сколько уже пролетел
    private readonly List<Fighter> alreadyHit = new List<Fighter>(); // Кого уже задел (для пробивающих)
    private SpriteRenderer effect;  // Картинка-кадры снаряда (если у скила есть свои кадры)
    private Color baseTint;         // Цвет снаряда (для мерцания)
    private float clock;            // Время полёта (для кадров)

    private const float EffectFps = 12f; // Скорость кадров снаряда

    /// <summary>Запустить снаряд.</summary>
    public void Launch(Fighter shooter, int dir, ShotInfo info, int id, float height)
    {
        owner = shooter;
        faction = shooter.Faction;
        shot = info;
        direction = dir;
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

        float step = direction * shot.speed * Time.deltaTime;
        Vector3 pos = transform.position;
        pos.x += step;
        travelled += Mathf.Abs(step);
        transform.position = pos;
        if (sortingGroup != null) sortingGroup.sortingOrder = bm.DepthSortingOrder(pos.y) + 50;
        clock += Time.deltaTime;
        if (effect != null) effect.sprite = CharacterSprites.Frame(shot.frames, clock, EffectFps);
        if (shot.flicker.a > 0f && tinted != null) // мерцание между двумя цветами
        {
            Color c = Color.Lerp(baseTint, shot.flicker, Mathf.PingPong(clock * 8f, 1f));
            foreach (SpriteRenderer r in tinted)
                if (r != null) r.color = new Color(c.r, c.g, c.b, r.color.a);
        }

        if (travelled > shot.range || !bm.IsInsideArenaX(pos.x)) { Destroy(gameObject); return; }

        foreach (Fighter enemy in bm.GetOpponents(faction))
        {
            if (!enemy.IsAlive || alreadyHit.Contains(enemy)) continue;
            Vector2 d = enemy.Position - (Vector2)pos;
            if (Mathf.Abs(d.x) > hitRadius || Mathf.Abs(d.y) > depthTolerance) continue;

            var hit = new HitInfo
            {
                attacker = owner,
                attackId = attackId,
                damage = FighterCombat.CalcDamage(shot.power, shot.vsSpecial ? enemy.Stats.specialDefense : enemy.Stats.defense),
                knockback = new Vector2(direction * shot.knockback, 0f),
                stagger = shot.stagger,
                special = shot.vsSpecial,
                auto = shot.auto
            };
            if (!enemy.Health.TakeHit(hit)) continue;
            alreadyHit.Add(enemy);
            // Эффекты снаряда (Энерго оковы: стан и немота)
            enemy.ApplyStun(shot.stun);
            enemy.ApplySilence(shot.silence);
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
