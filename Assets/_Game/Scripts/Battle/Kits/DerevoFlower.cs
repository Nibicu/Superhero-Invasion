using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Цветок Дерева ("Новая жизнь"): растёт на арене 10 с.
///  - союзник Дерева подходит — цветок лечит его на 30% от недостающего HP и исчезает;
///  - враг подходит близко — цветок взрывается дымом на 2 с: все враги в дыму шатаются (гроги).
/// Пока это простые круги (временная графика).
/// </summary>
public class DerevoFlower : HealSpot
{
    private const float LifeTime = 10f;       // Сколько живёт цветок
    private const float PickRadius = 0.6f;    // Союзник ближе этого — подобрал
    private const float TriggerRadius = 1.2f; // Враг ближе этого — взрыв
    private const float SmokeRadius = 1.6f;   // Размер дыма
    private const float SmokeTime = 2f;       // Сколько держится дым
    private const float HealMissing = 0.3f;   // Лечение — доля недостающего HP

    private Fighter owner;           // Дерево (ему засчитывается лечение)
    private float age;               // Сколько уже живёт
    private float smokeTimer;        // Сколько ещё держится дым (0 — цветок ещё цел)
    private Sprite circle;           // Круглый спрайт для картинок
    private SkillFx bloom;           // Картинка цветка (лепестки)
    private SkillFx core;            // Серединка цветка
    private readonly HashSet<Fighter> dazed = new HashSet<Fighter>(); // Кого уже зашатало дымом

    /// <summary>Вырастить цветок Дерева owner в точке pos.</summary>
    public static DerevoFlower Grow(Fighter owner, Vector3 pos, Sprite circle)
    {
        var go = new GameObject("DerevoFlower");
        go.transform.SetParent(BattleManager.Instance.RuntimeRoot, false);
        go.transform.position = pos;
        var f = go.AddComponent<DerevoFlower>();
        f.owner = owner;
        f.Faction = owner.Faction;
        f.circle = circle;
        Vector3 head = pos + new Vector3(0f, 0.25f, 0f);
        f.bloom = SkillFx.Spawn(circle, head, new Color(1f, 0.45f, 0.75f, 0.95f), new Vector2(0.55f, 0.55f), new Vector2(0.55f, 0.55f), LifeTime, 5);
        f.core = SkillFx.Spawn(circle, head, new Color(1f, 0.9f, 0.3f, 1f), new Vector2(0.2f, 0.2f), new Vector2(0.2f, 0.2f), LifeTime, 6);
        return f;
    }

    /// <summary>Лечиться можно у целого цветка, если боец свой и ранен.</summary>
    public override bool CanHeal(Fighter f) => smokeTimer <= 0f && f.Faction == Faction && f.IsAlive && f.Health.Fraction < 0.95f;

    private void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || !bm.IsRunning) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        if (smokeTimer > 0f) { UpdateSmoke(bm, dt); return; }

        age += dt;
        if (age >= LifeTime) { Destroy(gameObject); return; }

        // Враг рядом — взрыв дымом
        foreach (Fighter e in bm.GetOpponents(Faction))
            if (e.IsAlive && Vector2.Distance(e.Position, Position) <= TriggerRadius) { Explode(); return; }

        // Союзник рядом — лечение
        foreach (Fighter a in bm.GetAllies(Faction))
        {
            if (!a.IsAlive || a.Health.Fraction >= 0.999f || Vector2.Distance(a.Position, Position) > PickRadius) continue;
            a.Health.Heal((a.Health.Max - a.Health.Current) * HealMissing, owner);
            if (a.View != null) a.View.FlashHeal();
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>Взрыв: цветок исчезает, появляется дым (зелёно-серый) на 2 с.</summary>
    private void Explode()
    {
        smokeTimer = SmokeTime;
        RemoveBloom();
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * 0.7f, Mathf.Sin(a) * 0.35f + 0.6f, 0f);
            SkillFx.Spawn(circle, p, new Color(0.55f, 0.65f, 0.45f, 0.35f), new Vector2(1f, 0.8f), new Vector2(2.2f, 1.6f), SmokeTime).Fade(0.35f, 0f);
        }
    }

    /// <summary>Убрать картинку цветка (подобрали, взорвался или отцвёл).</summary>
    private void RemoveBloom()
    {
        if (bloom != null) bloom.Kill();
        if (core != null) core.Kill();
    }

    private void OnDestroy() => RemoveBloom();

    /// <summary>Дым: каждый враг, попавший в него, один раз шатается.</summary>
    private void UpdateSmoke(BattleManager bm, float dt)
    {
        smokeTimer -= dt;
        if (smokeTimer <= 0f) { Destroy(gameObject); return; }
        foreach (Fighter e in bm.GetOpponents(Faction))
        {
            if (!e.IsAlive || dazed.Contains(e) || Vector2.Distance(e.Position, Position) > SmokeRadius) continue;
            dazed.Add(e);
            if (e.CanBecomeDizzy) e.BecomeDizzy();
        }
    }
}
