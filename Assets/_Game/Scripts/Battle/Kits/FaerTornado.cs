using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Огненное торнадо Фаер Герл: движется вперёд 3 с и исчезает. Враг, которого оно задело, подхвачен:
/// летит по спирали вокруг торнадо и всё это время получает урон (раз в 0.5 с). Торнадо кончилось — враги падают.
/// Сильный удар по подхваченному (сбивает с ног) — он вылетает из торнадо. Как "Буря" в Little Fighter 2.
/// Пока это стопка оранжевых кругов (временная графика).
/// </summary>
public class FaerTornado : MonoBehaviour
{
    private const float CatchRadius = 0.9f;    // Кого задевает (по горизонтали)
    private const float TickEvery = 0.5f;      // Как часто урон подхваченным

    private Fighter owner;          // Фаер Герл
    private float life;             // Сколько живёт
    private float age;              // Сколько уже прожило
    private float speed;            // Скорость движения (со знаком — куда)
    private float tickPower;        // Урон за раз (Магия × доля)
    private float tickTimer;        // До следующего урона
    private readonly List<Fighter> caught = new List<Fighter>(); // Кто крутится в торнадо

    /// <summary>Запустить торнадо Фаер Герл owner из точки pos в сторону dir.</summary>
    public static FaerTornado Launch(Fighter owner, Vector3 pos, int dir, float life, float speed, float tickPower, Sprite circle)
    {
        var go = new GameObject("FaerTornado");
        go.transform.SetParent(BattleManager.Instance.RuntimeRoot, false);
        go.transform.position = pos;
        var t = go.AddComponent<FaerTornado>();
        t.owner = owner;
        t.life = life;
        t.speed = dir * speed;
        t.tickPower = tickPower;
        t.tickTimer = TickEvery;
        if (circle != null)
            for (int i = 0; i < 5; i++) // воронка: снизу узко, сверху широко
            {
                float w = 0.5f + i * 0.3f;
                SkillFx.Spawn(circle, pos, Color.Lerp(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.85f, 0.3f), i / 4f),
                    new Vector2(w, w * 0.45f), new Vector2(w * 1.1f, w * 0.5f), life, 70 + i).Fade(0.75f, 0.2f)
                    .Follow(go.transform, new Vector3(0f, 0.3f + i * 0.45f, 0f));
            }
        return t;
    }

    private void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || !bm.IsRunning) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;

        // Движется вперёд (в пределах арены)
        Vector3 p = transform.position + new Vector3(speed * dt, 0f, 0f);
        if (!bm.IsInsideArenaX(p.x)) age = life;
        transform.position = p;

        if (age >= life) { ReleaseAll(); Destroy(gameObject); return; }

        // Подхватывает новых врагов
        foreach (Fighter e in bm.GetOpponents(owner.Faction))
        {
            if (!e.IsAlive || e.IsInvulnerable || caught.Contains(e) || e.HeldBy != null) continue;
            Vector2 d = e.Position - (Vector2)transform.position;
            if (Mathf.Abs(d.x) > CatchRadius || Mathf.Abs(d.y) > 0.6f) continue;
            e.Launch(1f, Vector2.zero);
            if (e.State == FighterState.Airborne) caught.Add(e);
        }

        // Крутит подхваченных по спирали; сбитый сильным ударом (уже не Airborne) — вылетает
        caught.RemoveAll(e => e == null || !e.IsAlive || e.State != FighterState.Airborne);
        for (int i = 0; i < caught.Count; i++)
        {
            Fighter e = caught[i];
            float a = age * 7f + i * 2.1f;
            Vector3 spot = transform.position + new Vector3(Mathf.Cos(a) * 0.6f, Mathf.Sin(a) * 0.25f, 0f);
            e.transform.position = bm.ClampToArena(e, spot);
            e.Movement.HoldInAir(1.2f + Mathf.Sin(age * 3f + i) * 0.4f + age * 0.15f);
        }

        // Урон раз в 0.5 с
        tickTimer -= dt;
        if (tickTimer > 0f) return;
        tickTimer = TickEvery;
        foreach (Fighter e in caught)
            e.Health.TakeHit(new HitInfo
            {
                attacker = owner,
                attackId = FighterCombat.NewAttackId(),
                damage = FighterCombat.CalcDamage(tickPower, e.Stats.specialDefense),
                special = true
            });
    }

    /// <summary>Торнадо кончилось — подхваченные падают сами (сила тяжести), приземлятся — лягут.</summary>
    private void ReleaseAll() => caught.Clear();
}
