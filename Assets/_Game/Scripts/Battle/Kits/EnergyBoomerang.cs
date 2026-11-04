using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Энергетический бумеранг Америки: кольцо летит вперёд на среднее расстояние и возвращается к хозяйке.
/// Каждого врага, которого задело по пути туда, и каждого — по пути обратно, ранит и вводит в гроги (шатается).
/// Пока это голубое кольцо (временная графика).
/// </summary>
public class EnergyBoomerang : MonoBehaviour
{
    private const float HitRadiusX = 0.6f;  // Кого задевает по горизонтали
    private const float HitRadiusY = 0.5f;  // и по глубине
    private const float MaxLife = 5f;       // Самое долгое время полёта (если хозяйку не догнать)

    private Fighter owner;          // Америка
    private float power;            // Урон за попадание (Сила атаки × доля)
    private float speed;            // Скорость полёта
    private float range;            // Как далеко летит вперёд
    private int dir;                // Куда летит сначала (+1 вправо, −1 влево)
    private float travelled;        // Сколько пролетело вперёд
    private bool back;              // Уже возвращается
    private float age;              // Сколько летит
    private readonly HashSet<Fighter> hitOut = new HashSet<Fighter>();  // Кого задело по пути туда
    private readonly HashSet<Fighter> hitBack = new HashSet<Fighter>(); // Кого задело по пути обратно

    /// <summary>Бросить бумеранг Америки owner из точки pos в сторону dir.</summary>
    public static EnergyBoomerang Throw(Fighter owner, Vector3 pos, int dir, float power, float range, float speed, Sprite circle)
    {
        var go = new GameObject("EnergyBoomerang");
        go.transform.SetParent(BattleManager.Instance.RuntimeRoot, false);
        go.transform.position = pos;
        var b = go.AddComponent<EnergyBoomerang>();
        b.owner = owner;
        b.power = power;
        b.range = range;
        b.speed = speed;
        b.dir = dir;
        if (circle != null)
        {
            // Кольцо: яркий обод и тёмная середина — висит на высоте груди
            SkillFx.Spawn(circle, pos, new Color(0.35f, 0.9f, 1f), new Vector2(0.75f, 0.4f), new Vector2(0.75f, 0.4f), MaxLife, 60)
                .Follow(go.transform, new Vector3(0f, 0.9f, 0f));
            SkillFx.Spawn(circle, pos, new Color(0.05f, 0.15f, 0.3f), new Vector2(0.4f, 0.2f), new Vector2(0.4f, 0.2f), MaxLife, 61)
                .Follow(go.transform, new Vector3(0f, 0.9f, 0f));
        }
        return b;
    }

    private void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || !bm.IsRunning || owner == null) { Finish(); return; }
        float dt = Time.deltaTime;
        age += dt;
        if (age >= MaxLife) { Finish(); return; }

        Vector3 p = transform.position;
        if (!back)
        {
            // Летит вперёд, пока не пролетит свою дальность (или не упрётся в край арены)
            float step = speed * dt;
            p.x += dir * step;
            travelled += step;
            if (travelled >= range || !bm.IsInsideArenaX(p.x)) back = true;
        }
        else
        {
            // Возвращается к хозяйке (выбыла — летит туда, где она была, и исчезает)
            Vector3 home = owner.transform.position;
            p = Vector3.MoveTowards(p, home, speed * 1.1f * dt);
            if ((p - home).sqrMagnitude < 0.09f) { transform.position = p; Finish(); return; }
        }
        transform.position = p;

        // Задевает врагов: каждого — один раз туда и один раз обратно
        HashSet<Fighter> done = back ? hitBack : hitOut;
        foreach (Fighter e in bm.GetOpponents(owner.Faction))
        {
            if (!e.IsAlive || e.IsInvulnerable || done.Contains(e)) continue;
            Vector2 d = e.Position - (Vector2)p;
            if (Mathf.Abs(d.x) > HitRadiusX || Mathf.Abs(d.y) > HitRadiusY) continue;
            done.Add(e);
            bool hit = e.Health.TakeHit(new HitInfo
            {
                attacker = owner,
                attackId = FighterCombat.NewAttackId(),
                damage = FighterCombat.CalcDamage(power, e.Stats.defense),
                knockback = new Vector2(Mathf.Sign(d.x + 0.001f) * 1f, 0f),
                stagger = 1
            });
            // Гроги: враг шатается
            if (hit && e.IsAlive && e.CanBecomeDizzy && e.State != FighterState.KnockedDown && e.State != FighterState.Airborne)
                e.BecomeDizzy();
        }
    }

    /// <summary>Бумеранг вернулся (или бой кончился) — исчезает вместе с картинкой.</summary>
    private void Finish()
    {
        foreach (SkillFx fx in FindFollowers()) fx.Kill();
        Destroy(gameObject);
    }

    /// <summary>Картинки, которые следуют за бумерангом (их нужно убрать вместе с ним).</summary>
    private List<SkillFx> FindFollowers()
    {
        var list = new List<SkillFx>();
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return list;
        foreach (SkillFx fx in bm.RuntimeRoot.GetComponentsInChildren<SkillFx>())
            if (fx.IsFollowing(transform)) list.Add(fx);
        return list;
    }
}
