using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Энергетический снаряд спец. атаки.
/// Летит горизонтально в сторону взгляда стрелка, попадает в первого противника
/// на той же глубине и исчезает. Союзников и стрелка не задевает.
/// Исчезает и по истечении времени жизни или за краем арены.
/// Положение объекта — "на земле" (для глубины), картинка поднята на высоту груди.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Tooltip("Сколько секунд снаряд летит, если ни в кого не попал")]
    [SerializeField] private float lifetime = 2.2f;
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

    private Fighter owner;   // Кто выстрелил
    private Team team;       // Его команда (снаряд не бьёт своих)
    private int specialPower; // Спец. атака стрелка (запоминаем при выстреле)
    private int direction;   // 1 — вправо, -1 — влево
    private float speed;
    private int attackId;
    private float age;

    /// <summary>Запустить снаряд.</summary>
    public void Launch(Fighter shooter, int dir, float flySpeed, int id, float height)
    {
        owner = shooter;
        team = shooter.Team;
        specialPower = shooter.Stats.specialAttack;
        direction = dir;
        speed = flySpeed;
        attackId = id;
        if (visual != null)
        {
            visual.localPosition = new Vector3(0f, height, 0f);
            visual.localScale = new Vector3(dir, 1f, 1f); // "хвост" позади
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

        float dt = Time.deltaTime;
        age += dt;
        Vector3 pos = transform.position;
        pos.x += direction * speed * dt;
        transform.position = pos;
        if (sortingGroup != null) sortingGroup.sortingOrder = bm.DepthSortingOrder(pos.y) + 50;

        if (age > lifetime || !bm.IsInsideArenaX(pos.x)) { Destroy(gameObject); return; }

        foreach (Fighter enemy in bm.GetOpponents(team))
        {
            if (!enemy.IsAlive) continue;
            Vector2 d = enemy.Position - (Vector2)pos;
            if (Mathf.Abs(d.x) > hitRadius || Mathf.Abs(d.y) > depthTolerance) continue;

            var hit = new HitInfo
            {
                attacker = owner,
                attackId = attackId,
                damage = FighterCombat.CalcDamage(specialPower, enemy.Stats.specialDefense),
                knockback = new Vector2(direction * 5f, 0f),
                stagger = 2
            };
            if (enemy.Health.TakeHit(hit))
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}
