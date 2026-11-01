using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Суккуб (злодейка, класс Маг, урон от Магии, ближний бой). Навыки:
///  1) "Шарм" (ур. 1) — сердце летит во врага, урона нет. Рядом с целью никого из её союзников —
///     цель оглушена на 2 с; рядом есть её союзники — цель и все её союзники рядом 2 с бьют друг друга авто атаками.
///  2) "Сай" (ур. 3) — самонаводящийся снаряд в ближайшего врага (летит до 5 с, не нашёл цель — исчезает),
///     урон от Магии. Пока это красный огненный шар (картинку сая автор нарисует).
///  3) "Проклятье" (ур. 5, пассивный) — враг, в которого попал скил Суккуба, получает метку до конца боя.
///     Когда у помеченного врага остаётся 10% HP или меньше — Суккуб телепортируется к нему и сразу бьёт.
/// Перезарядки — в файле героя (Data/Villains/Villain_Sukub). Кадры — в Data/Characters/Sprites_Sukub.
/// </summary>
public class SukubKit : HeroKit
{
    private const int Charm = 0, Sai = 1, Curse = 2; // номера скилов

    [Header("Шарм")]
    [SerializeField] private float charmRange = 8f;          // Дальность сердца
    [SerializeField] private float charmMinRange = 1.5f;     // Ближе — бьёт рукой
    [SerializeField] private float charmTime = 2f;           // Сколько длится стан или шарм
    [SerializeField] private float charmAllyRadius = 2.5f;   // Кого из союзников цели считать "рядом"
    [SerializeField] private float charmSpeed = 9f;          // Скорость сердца

    [Header("Сай")]
    [SerializeField] private float saiDamage = 1f;           // Урон (доля Магии)
    [SerializeField] private float saiRange = 9f;            // Бросает, когда враг ближе этого
    [SerializeField] private float saiSpeed = 8f;            // Скорость полёта
    [SerializeField] private float saiLifetime = 5f;         // Сколько секунд летит

    [Header("Проклятье (пассивка)")]
    [SerializeField] private float curseHp = 0.1f;           // При какой доле HP помеченного врага телепортируется к нему

    private const float AimTolerance = 0.35f; // Насколько точно встать на линию цели для сердца

    private int current = -1;        // Какой скил сейчас применяется (-1 — никакой)
    private bool fired;              // Снаряд уже вылетел (в текущем действии)
    private float actionLength;      // Сколько длится текущее действие
    private Fighter skillTarget;     // В кого скил
    private float curseCheck;        // Таймер проверки Проклятья

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 1) Шарм — цель на одной линии, не вплотную
        if (IsReady(Charm) && target.IsTargetable && absY <= AimTolerance && absX >= charmMinRange && absX <= charmRange)
            return Begin(Charm, target);

        // 2) Сай — самонаводящийся, линия не нужна
        if (IsReady(Sai) && target.IsTargetable && d.magnitude <= saiRange)
            return Begin(Sai, target);

        return false;
    }

    /// <summary>Начать бросок скила skill в target.</summary>
    private bool Begin(int skill, Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        fired = false;
        skillTarget = target;
        actionLength = 0.4f;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Сердце Шарма: без урона, действие — при попадании.</summary>
    private void FireCharm()
    {
        self.Combat.FireShot(new ShotInfo
        {
            speed = charmSpeed,
            range = charmRange + 0.5f,
            size = 0.75f,
            noDamage = true,
            color = new Color(1f, 0.35f, 0.7f),
            flicker = new Color(1f, 0.75f, 0.9f),
            onHit = OnCharmHit
        });
    }

    /// <summary>Сердце попало: одна цель — стан 2 с; рядом её союзники — все они 2 с бьют друг друга.</summary>
    private void OnCharmHit(Fighter e)
    {
        if (e == null || !e.IsAlive) return;
        Mark(e);
        var near = new List<Fighter>();
        foreach (Fighter a in BattleManager.Instance.GetAllies(e.Faction))
            if (a != e && a.IsAlive && Vector2.Distance(a.Position, e.Position) <= charmAllyRadius) near.Add(a);
        if (near.Count == 0)
        {
            e.ApplyStun(charmTime);
            return;
        }
        e.ApplyCharm(charmTime);
        foreach (Fighter a in near) a.ApplyCharm(charmTime);
    }

    /// <summary>Сай: самонаводящийся огненный шар (урон от Магии).</summary>
    private void FireSai()
    {
        Fighter target = skillTarget != null && skillTarget.IsAlive ? skillTarget : NearestEnemy();
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * saiDamage,
            vsSpecial = true,
            speed = saiSpeed,
            range = 999f,
            lifetime = saiLifetime,
            homing = target,
            size = 0.6f,
            stagger = 1,
            knockback = 3f,
            color = new Color(1f, 0.15f, 0.1f),
            flicker = new Color(1f, 0.55f, 0.15f),
            onHit = Mark
        });
    }

    /// <summary>Ближайший живой враг (null — нет).</summary>
    private Fighter NearestEnemy()
    {
        Fighter best = null;
        float bestDist = float.MaxValue;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsTargetable) continue;
            float d = Vector2.Distance(e.Position, self.Position);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    // ---------- Проклятье ----------

    /// <summary>Поставить метку проклятья (если Проклятье открыто).</summary>
    private void Mark(Fighter e)
    {
        if (IsUnlocked(Curse) && e != null && e.IsAlive) e.MarkCursed();
    }

    /// <summary>Раз в 0.2 с: помеченный враг ослаб до 10% HP — телепорт к нему и удар.</summary>
    private void UpdateCurse(float dt)
    {
        curseCheck -= dt;
        if (curseCheck > 0f) return;
        curseCheck = 0.2f;
        if (!IsReady(Curse) || Pose != KitPose.None) return;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsCursed || !e.IsTargetable || e.Health.Fraction > curseHp) continue;
            StartCooldown(Curse);
            float side = Mathf.Sign(self.Position.x - e.Position.x);
            if (side == 0f) side = 1f;
            Vector3 p = e.transform.position + new Vector3(side * self.meleeRange * 0.8f, 0f, 0f);
            self.transform.position = BattleManager.Instance.ClampToArena(self, p);
            self.SetInvulnerable(0.2f);
            self.Movement.FaceTowards(e.Position.x);
            Pop("ПРОКЛЯТЬЕ", new Color(0.75f, 0.45f, 1f));
            self.Combat.TryAuto(true); // сразу бьёт
            return;
        }
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (self.IsAlive) UpdateCurse(dt);

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Суккубу попали — действие прервано
        ActionTime += dt;

        if (!fired && ActionTime >= 0.2f)
        {
            fired = true;
            if (skillTarget != null && skillTarget.IsAlive) self.Movement.FaceTowards(skillTarget.Position.x);
            if (current == Charm) FireCharm();
            else if (current == Sai) FireSai();
        }

        if (ActionTime >= actionLength)
        {
            current = -1;
            skillTarget = null;
            EndAction();
        }
    }
}
