using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Волшебная пыль Мотылька: облако сверкающей пыли вокруг Мотылька, постепенно опадает (4 с).
/// Союзник, зашедший в пыль, один раз лечится на 30% от максимума HP.
/// Пока это падающие блёстки из кругов (временная графика).
/// </summary>
public class MotelekDust : HealSpot
{
    private const float LifeTime = 4f;      // Сколько держится пыль
    private const float Radius = 1.8f;      // Размер облака
    private const float HealMax = 0.3f;     // Лечение — доля макс. HP
    private const float SparkleEvery = 0.06f; // Как часто появляется блёстка

    private Fighter owner;          // Мотылёк (ему засчитывается лечение)
    private float age;              // Сколько уже держится
    private float sparkleTimer;     // До следующей блёстки
    private Sprite circle;          // Круглый спрайт
    private readonly HashSet<Fighter> healed = new HashSet<Fighter>(); // Кто уже вылечился этой пылью

    /// <summary>Рассыпать пыль Мотылька owner вокруг точки pos.</summary>
    public static MotelekDust Spread(Fighter owner, Vector3 pos, Sprite circle)
    {
        var go = new GameObject("MotelekDust");
        go.transform.SetParent(BattleManager.Instance.RuntimeRoot, false);
        go.transform.position = pos;
        var d = go.AddComponent<MotelekDust>();
        d.owner = owner;
        d.Faction = owner.Faction;
        d.circle = circle;
        SkillFx.Spawn(circle, pos + new Vector3(0f, 0.8f, 0f), new Color(1f, 0.95f, 0.6f, 0.25f),
            new Vector2(Radius * 1.2f, Radius), new Vector2(Radius * 2.2f, Radius * 1.4f), LifeTime).Fade(0.25f, 0f);
        return d;
    }

    /// <summary>Лечиться можно, если свой, ранен и ещё не лечился этой пылью.</summary>
    public override bool CanHeal(Fighter f) => f.Faction == Faction && f.IsAlive && !healed.Contains(f) && f.Health.Fraction < 0.95f;

    private void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || !bm.IsRunning) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;
        if (age >= LifeTime) { Destroy(gameObject); return; }

        // Блёстки сыплются сверху вниз
        sparkleTimer -= dt;
        if (sparkleTimer <= 0f && circle != null)
        {
            sparkleTimer = SparkleEvery;
            Vector3 p = transform.position + new Vector3(Random.Range(-Radius, Radius), Random.Range(1.2f, 2.4f), 0f);
            SkillFx.Spawn(circle, p, new Color(1f, 0.95f, 0.55f, 0.9f), new Vector2(0.12f, 0.12f), new Vector2(0.06f, 0.06f), 1f, 60)
                .Fade(0.9f, 0f).Move(new Vector3(0f, -1.6f, 0f));
        }

        // Союзник в пыли — лечится один раз
        foreach (Fighter a in bm.GetAllies(Faction))
        {
            if (!a.IsAlive || healed.Contains(a) || a.Health.Fraction >= 0.999f || Vector2.Distance(a.Position, Position) > Radius) continue;
            healed.Add(a);
            a.Health.Heal(a.Health.Max * HealMax, owner);
            if (a.View != null) a.View.FlashHeal();
        }
    }
}
