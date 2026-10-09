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
        Color c = shooter.Data.color;
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
                stagger = shot.stagger
            };
            if (!enemy.Health.TakeHit(hit)) continue;
            alreadyHit.Add(enemy);
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
