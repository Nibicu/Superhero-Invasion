using UnityEngine;

/// <summary>
/// Зед Рок (злодей, класс Маг, урон от Магии). Навыки:
///  1) "Тяжёлый рок" (ур. 1) — две ударные мелодии-снаряда с интервалом 0.5 с; каждая бьёт первого, в кого влетит (80% Магии).
///  2) "Звуковой удар" (ур. 3) — вокруг Зед Рока расходится звуковое кольцо: урон 70% Магии и подбрасывает всех врагов рядом.
///  3) "Энерго темп" (ур. 5) — союзникам рядом (и себе) +20% к скорости бега и авто атак на 3 с.
/// Перезарядки — в файле героя (Data/Villains/Villain_ZedRock). Кадры — в Data/Characters/Sprites_ZedRock.
/// </summary>
public class ZedRockKit : HeroKit
{
    private const int HeavyRock = 0, SoundStrike = 1, Tempo = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        rockShots = Mathf.Max(1, Count(HeavyRock, "shots"));
        rockInterval = Num(HeavyRock, "interval");
        rockDamage = Num(HeavyRock, "damage");
        soundDamage = Num(SoundStrike, "damage");
        soundRadius = Num(SoundStrike, "radius");
        tempoBonus = Num(Tempo, "bonus");
        tempoTime = Num(Tempo, "time");
        tempoRadius = Num(Tempo, "radius");
    }

    [Header("Тяжёлый рок")]
    [SerializeField] private int rockShots = 2;             // Снарядов
    [SerializeField] private float rockInterval = 0.5f;     // Интервал между снарядами
    [SerializeField] private float rockDamage = 0.8f;       // Урон снаряда (доля Магии)
    [SerializeField] private float rockSpeed = 10f;         // Скорость
    [SerializeField] private float rockRange = 8f;          // Дальность
    [SerializeField] private float rockMinRange = 1.5f;     // Ближе — бьёт рукой

    [Header("Звуковой удар")]
    [SerializeField] private float soundDamage = 0.7f;      // Урон (доля Магии)
    [SerializeField] private float soundRadius = 2.2f;      // Радиус кольца
    [SerializeField] private float soundLaunch = 7f;        // Как высоко подлетают враги

    [Header("Энерго темп")]
    [SerializeField] private float tempoBonus = 1.2f;       // Множитель скорости бега и атак
    [SerializeField] private float tempoTime = 3f;          // Сколько длится
    [SerializeField] private float tempoRadius = 4f;        // Радиус

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;    // Сколько длится действие
    private int shotsFired;        // Сколько снарядов уже выпущено
    private bool done;             // Кольцо / темп уже применены

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 3) Энерго темп — идёт бой (враг недалеко), рядом есть союзники или сам дерётся
        if (IsReady(Tempo) && d.magnitude <= 6f)
            return Begin(Tempo, 0.35f, target);

        // 2) Звуковой удар — враг рядом
        if (IsReady(SoundStrike) && target.IsTargetable && absX <= soundRadius * 0.9f && absY <= soundRadius * 0.5f)
            return Begin(SoundStrike, 0.45f, target);

        // 1) Тяжёлый рок — враг на одной линии, не вплотную
        if (IsReady(HeavyRock) && target.IsTargetable && absY <= 0.6f && absX >= rockMinRange && absX <= rockRange)
        {
            if (!Begin(HeavyRock, 0.1f + rockInterval * (rockShots - 1) + 0.25f, target)) return false;
            shotsFired = 0;
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
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Выпустить одну ударную мелодию (снаряд бьёт первого, в кого влетит).</summary>
    private void FireNote()
    {
        shotsFired++;
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * rockDamage,
            vsSpecial = true,
            speed = rockSpeed,
            range = rockRange,
            size = 0.5f,
            stagger = 1,
            knockback = 1.5f,
            color = new Color(0.85f, 0.25f, 1f),
            flicker = new Color(1f, 0.9f, 0.3f)
        });
    }

    /// <summary>Звуковое кольцо: урон и подбрасывание всех врагов рядом.</summary>
    private void SoundRing()
    {
        foreach (Fighter e in EnemiesNear(self.Position, soundRadius))
        {
            if (e.IsInvulnerable) continue;
            float side = Mathf.Sign(e.Position.x - self.Position.x + 0.001f);
            LaunchEnemy(e, self.Stats.specialAttack * soundDamage, true, soundLaunch, new Vector2(side * 2f, 0f));
        }
        Ring(new Color(0.85f, 0.3f, 1f), soundRadius, 0.4f);
        Pop("ЗВУКОВОЙ УДАР", new Color(0.85f, 0.4f, 1f));
    }

    /// <summary>Энерго темп: союзникам рядом (и себе) быстрее бег и авто атаки.</summary>
    private void EnergyTempo()
    {
        foreach (Fighter a in BattleManager.Instance.GetAllies(self.Faction))
        {
            if (!a.IsAlive || Vector2.Distance(a.Position, self.Position) > tempoRadius) continue;
            a.ApplyTempo(tempoBonus, tempoTime);
            if (a.View != null && a != self) a.View.Pop("ТЕМП", new Color(1f, 0.85f, 0.3f));
        }
        Ring(new Color(1f, 0.85f, 0.3f), tempoRadius, 0.5f);
        Pop("ЭНЕРГО ТЕМП", new Color(1f, 0.85f, 0.3f));
    }

    /// <summary>Расходящееся кольцо вокруг Зед Рока (временная графика).</summary>
    private void Ring(Color color, float radius, float time)
    {
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle == null) return;
        SkillFx.Spawn(circle, self.transform.position, color, new Vector2(0.5f, 0.25f), new Vector2(radius * 2f, radius), time, -5)
            .Fade(0.7f, 0f);
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Зед Року попали — действие прервано
        ActionTime += Time.deltaTime;

        if (current == HeavyRock && shotsFired < rockShots && ActionTime >= 0.1f + shotsFired * rockInterval) FireNote();
        if (current == SoundStrike && !done && ActionTime >= 0.2f) { done = true; SoundRing(); }
        if (current == Tempo && !done && ActionTime >= 0.15f) { done = true; EnergyTempo(); }

        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }
}
