using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Дерево (герой, класс Танк, урон от Магии). Навыки:
///  1) "Корни" (ур. 1) — 3 корня вырываются из земли волной перед Деревом (один ближе, другой дальше, третий ещё дальше):
///     каждый подбрасывает врагов (не отбрасывает), урон от Магии.
///  2) "Новая жизнь" (ур. 3) — 2 цветка из земли: союзник подобрал — +30% недостающего HP;
///     враг подошёл — цветок взрывается дымом, враги в дыму шатаются (DerevoFlower).
///  3) "Сила природы" (ур. 5, пассивный) — чем больше урона Дерево получило в бою, тем сильнее до конца боя:
///     1000 урона — +20% ко всем характеристикам, 2500 — ещё +20%.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Derevo). Кадры — в Data/Characters/Sprites_Derevo.
/// </summary>
public class DerevoKit : HeroKit
{
    private const int Roots = 0, NewLife = 1, Nature = 2; // номера скилов

    [Header("Корни")]
    [SerializeField] private float rootDamage = 0.6f;          // Урон каждого корня (доля Магии)
    [SerializeField] private float[] rootDistances = { 1.2f, 2.5f, 3.8f }; // Где вырастают корни (перед Деревом)
    [SerializeField] private float rootInterval = 0.25f;       // Через сколько вырастает следующий
    [SerializeField] private float rootRadius = 0.9f;          // Кого задевает корень (по горизонтали)
    [SerializeField] private float rootLaunch = 9f;            // Как высоко подбрасывает
    [SerializeField] private float rootsRange = 4.2f;          // Применяет, когда враг впереди ближе этого

    [Header("Новая жизнь")]
    [SerializeField] private float flowerSpread = 1.3f;        // Насколько далеко от Дерева растут цветки
    [SerializeField] private float allyHurtHp = 0.75f;         // Сажает, когда кто-то из своих рядом ранен ниже этого
    [SerializeField] private float allySearchRadius = 6f;      // Каких союзников считать "рядом"

    [Header("Сила природы (пассивка)")]
    [SerializeField] private float[] natureThresholds = { 1000f, 2500f }; // Сколько урона получить для каждой ступени
    [SerializeField] private float natureBonus = 0.2f;          // Прибавка за каждую ступень (+20%)

    private int current = -1;          // Какой скил сейчас применяется (-1 — никакой)
    private int rootsDone;             // Сколько корней уже выросло
    private float actionLength;        // Сколько длится действие
    private int facing;                // Куда растут корни
    private int natureLevel;           // Сколько ступеней Силы природы набрано
    private readonly List<Fighter> rootHit = new List<Fighter>(); // Кого корни уже задели в этом применении

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Пассивка ----------

    /// <summary>Сила природы: +20% ко всем характеристикам за каждую набранную ступень.</summary>
    public override HeroStats ModifyStats(HeroStats s) => natureLevel > 0 ? s.Scaled(1f + natureBonus * natureLevel) : s;

    /// <summary>Сколько ступеней Силы природы набрано по полученному урону.</summary>
    private void UpdateNature()
    {
        if (!IsUnlocked(Nature) || natureLevel >= natureThresholds.Length) return;
        if (self.DamageTaken < natureThresholds[natureLevel]) return;
        natureLevel++;
        Pop($"СИЛА ПРИРОДЫ +{natureBonus * 100f * natureLevel:0}%", new Color(0.5f, 0.9f, 0.35f));
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;

        // 2) Новая жизнь — кто-то из своих (или сам) ранен
        if (IsReady(NewLife) && SomeoneHurt()) return StartNewLife();

        // 1) Корни — враг впереди недалеко, примерно на одной глубине
        if (IsReady(Roots) && !retreating && target.IsTargetable && Mathf.Abs(d.x) <= rootsRange && Mathf.Abs(d.y) <= 0.7f)
            return StartRoots(d.x >= 0f ? 1 : -1);

        return false;
    }

    /// <summary>Кто-то из союзников рядом (или сам Дерево) ранен ниже allyHurtHp.</summary>
    private bool SomeoneHurt()
    {
        foreach (Fighter a in BattleManager.Instance.GetAllies(self.Faction))
            if (a.IsAlive && a.Health.Fraction < allyHurtHp && Vector2.Distance(a.Position, self.Position) <= allySearchRadius) return true;
        return false;
    }

    // ---------- Скилы ----------

    /// <summary>Корни: волна из 3 корней перед Деревом.</summary>
    private bool StartRoots(int dir)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(Roots);
        current = Roots;
        rootsDone = 0;
        facing = dir;
        rootHit.Clear();
        actionLength = rootInterval * rootDistances.Length + 0.2f;
        self.Movement.FaceTowards(self.Position.x + dir);
        Pop("КОРНИ", new Color(0.6f, 0.45f, 0.25f));
        return true;
    }

    /// <summary>Вырастить корень номер i: картинка и подбрасывание врагов рядом с ним.</summary>
    private void GrowRoot(int i)
    {
        BattleManager bm = BattleManager.Instance;
        Vector3 spot = bm.ClampToArena(self, self.transform.position + new Vector3(facing * rootDistances[i], 0f, 0f));
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, spot + new Vector3(0f, 0.5f, 0f), new Color(0.45f, 0.3f, 0.15f, 0.95f),
                new Vector2(0.35f, 0.3f), new Vector2(0.5f, 1.6f), 0.45f, 5).Fade(0.95f, 0.2f);
        foreach (Fighter e in Enemies)
        {
            if (!e.IsAlive || rootHit.Contains(e)) continue;
            Vector2 d = e.Position - (Vector2)spot;
            if (Mathf.Abs(d.x) > rootRadius || Mathf.Abs(d.y) > self.depthTolerance * 1.6f) continue;
            rootHit.Add(e);
            e.Health.TakeHit(new HitInfo
            {
                attacker = self,
                attackId = FighterCombat.NewAttackId(),
                damage = FighterCombat.CalcDamage(self.Stats.specialAttack * rootDamage, e.Stats.specialDefense),
                launch = rootLaunch,
                special = true
            });
        }
    }

    /// <summary>Новая жизнь: 2 цветка по сторонам от Дерева.</summary>
    private bool StartNewLife()
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(NewLife);
        current = NewLife;
        actionLength = 0.4f;
        BattleManager bm = BattleManager.Instance;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle == null) return true;
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 p = bm.ClampToArena(self, self.transform.position + new Vector3(side * flowerSpread, Random.Range(-0.4f, 0.4f), 0f));
            DerevoFlower.Grow(self, p, circle);
        }
        Pop("НОВАЯ ЖИЗНЬ", new Color(1f, 0.55f, 0.8f));
        return true;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (self != null && self.IsAlive) UpdateNature();

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Дереву попали — действие прервано
        ActionTime += Time.deltaTime;

        if (current == Roots && rootsDone < rootDistances.Length && ActionTime >= rootsDone * rootInterval + 0.1f)
            GrowRoot(rootsDone++);

        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }
}
