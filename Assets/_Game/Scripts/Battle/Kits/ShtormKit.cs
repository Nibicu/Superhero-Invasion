using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Шторм (злодейка, класс Маг, урон от Магии). Навыки:
///  1) "Удар молнии" (ур. 1) — 2–5 разрядов молнии с неба по врагам рядом (по очереди), каждый — 50% Магии и гроги (шатается).
///  2) "Шаровая молния" (ур. 3) — самонаводящийся шар: 120% Магии и отбрасывает.
///  3) "Разряд" (ур. 5) — вокруг Шторм в небольшой области разряд: 80% Магии и гроги.
/// Перезарядки — в файле героя (Data/Villains/Villain_Shtorm). Кадры — в Data/Characters/Sprites_Shtorm.
/// </summary>
public class ShtormKit : HeroKit
{
    private const int Lightning = 0, BallLightning = 1, Discharge = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        boltsMin = Mathf.Max(1, Count(Lightning, "min"));
        boltsMax = Mathf.Max(boltsMin, Count(Lightning, "max"));
        boltDamage = Num(Lightning, "damage");
        ballDamage = Num(BallLightning, "damage");
        dischargeDamage = Num(Discharge, "damage");
        dischargeRadius = Num(Discharge, "radius");
    }

    [Header("Удар молнии")]
    [SerializeField] private int boltsMin = 2;              // Разрядов минимум
    [SerializeField] private int boltsMax = 5;              // Разрядов максимум
    [SerializeField] private float boltDamage = 0.5f;       // Урон разряда (доля Магии)
    [SerializeField] private float boltInterval = 0.22f;    // Интервал между разрядами
    [SerializeField] private float boltRange = 7f;          // По кому бьёт: враги не дальше этого

    [Header("Шаровая молния")]
    [SerializeField] private float ballDamage = 1.2f;       // Урон (доля Магии)
    [SerializeField] private float ballSpeed = 7f;          // Скорость
    [SerializeField] private float ballRange = 12f;         // Дальность
    [SerializeField] private float ballKnock = 4f;          // Отбрасывание

    [Header("Разряд")]
    [SerializeField] private float dischargeDamage = 0.8f;  // Урон (доля Магии)
    [SerializeField] private float dischargeRadius = 1.8f;  // Радиус

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;    // Сколько длится действие
    private int boltsTotal;        // Разрядов в этом ударе
    private int boltsDone;         // Сколько уже ударило
    private bool done;             // Шар / разряд уже выпущены
    private Fighter skillTarget;   // Главная цель

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive || !target.IsTargetable) return false;
        Vector2 d = target.Position - self.Position;
        float dist = d.magnitude;

        // 3) Разряд — враг вплотную
        if (IsReady(Discharge) && Mathf.Abs(d.x) <= dischargeRadius && Mathf.Abs(d.y) <= dischargeRadius * 0.5f)
            return Begin(Discharge, 0.4f, target);

        // 2) Шаровая молния — враг не вплотную (шар наводится сам)
        if (IsReady(BallLightning) && dist >= 1.5f && dist <= ballRange * 0.8f)
            return Begin(BallLightning, 0.35f, target);

        // 1) Удар молнии — враг в досягаемости
        if (IsReady(Lightning) && dist <= boltRange)
        {
            boltsTotal = Random.Range(boltsMin, boltsMax + 1);
            if (!Begin(Lightning, 0.15f + boltInterval * boltsTotal + 0.1f, target)) return false;
            boltsDone = 0;
            return true;
        }
        return false;
    }

    /// <summary>Начать действие скила skill длиной length против target.</summary>
    private bool Begin(int skill, float length, Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        actionLength = length;
        done = false;
        skillTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Один разряд молнии: первый — в главную цель, остальные — в случайных врагов рядом.</summary>
    private void Bolt()
    {
        boltsDone++;
        AnimCycle++;
        Fighter victim = null;
        if (boltsDone == 1 && skillTarget != null && skillTarget.IsAlive && !skillTarget.IsInvulnerable) victim = skillTarget;
        if (victim == null)
        {
            var list = new List<Fighter>();
            foreach (Fighter e in EnemiesNear(self.Position, boltRange))
                if (!e.IsInvulnerable) list.Add(e);
            if (list.Count == 0) return;
            victim = list[Random.Range(0, list.Count)];
        }
        HitEnemy(victim, self.Stats.specialAttack * boltDamage, true, Vector2.zero, 1);
        MakeDizzy(victim);
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
        {
            // Молния: высокая тонкая полоса с неба и вспышка у ног
            Vector3 p = victim.transform.position;
            SkillFx.Spawn(circle, p + new Vector3(0f, 2.5f, 0f), new Color(1f, 1f, 0.6f), new Vector2(0.18f, 5f), new Vector2(0.05f, 5f), 0.25f, 70).Fade(1f, 0f);
            SkillFx.Spawn(circle, p, new Color(0.6f, 0.9f, 1f), new Vector2(0.4f, 0.2f), new Vector2(1.2f, 0.6f), 0.25f, 69).Fade(0.9f, 0f);
        }
    }

    /// <summary>Шаровая молния: самонаводящийся шар в главную цель.</summary>
    private void FireBall()
    {
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * ballDamage,
            vsSpecial = true,
            speed = ballSpeed,
            range = ballRange,
            size = 0.7f,
            stagger = 2,
            knockback = ballKnock,
            color = new Color(0.55f, 0.85f, 1f),
            flicker = new Color(1f, 1f, 0.6f),
            homing = skillTarget != null && skillTarget.IsAlive ? skillTarget : null,
            lifetime = 3f
        });
        Pop("ШАРОВАЯ МОЛНИЯ", new Color(0.6f, 0.9f, 1f));
    }

    /// <summary>Разряд вокруг Шторм: урон и гроги всем врагам рядом.</summary>
    private void DoDischarge()
    {
        foreach (Fighter e in EnemiesNear(self.Position, dischargeRadius))
        {
            if (e.IsInvulnerable) continue;
            HitEnemy(e, self.Stats.specialAttack * dischargeDamage, true, Vector2.zero, 1);
            MakeDizzy(e);
        }
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, self.transform.position, new Color(0.6f, 0.9f, 1f), new Vector2(0.6f, 0.3f),
                new Vector2(dischargeRadius * 2f, dischargeRadius), 0.35f, -5).Fade(0.8f, 0f);
        Pop("РАЗРЯД", new Color(0.6f, 0.9f, 1f));
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Шторм попали — действие прервано
        ActionTime += Time.deltaTime;

        if (current == Lightning && boltsDone < boltsTotal && ActionTime >= 0.15f + boltsDone * boltInterval) Bolt();
        if (current == BallLightning && !done && ActionTime >= 0.2f) { done = true; FireBall(); }
        if (current == Discharge && !done && ActionTime >= 0.15f) { done = true; DoDischarge(); }

        if (ActionTime >= actionLength)
        {
            current = -1;
            skillTarget = null;
            EndAction();
        }
    }
}
