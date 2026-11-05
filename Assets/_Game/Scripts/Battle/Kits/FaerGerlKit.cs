using UnityEngine;

/// <summary>
/// Фаер Герл (злодейка, класс Маг, урон от Магии). Навыки:
///  1) "Фаерболы" (ур. 1) — очередью 2–5 огненных шаров (как в Little Fighter 2: летят прямо и чуть доворачивают к цели) (сколько хватит энергии, каждый — 15 энергии), каждый — 45% Магии.
///  2) "Огненное торнадо" (ур. 3) — торнадо движется вперёд 3 с; задетый враг сразу получает 50% Магии,
///     крутится в нём по спирали и получает урон 20% Магии раз в 0.5 с, пока торнадо не кончится (FaerTornado).
///  3) "Полёт" (ур. 5, пассивный) — когда HP падает до 20%, взлетает и улетает в безопасное место (неуязвима в полёте).
/// Перезарядки — в файле героя (Data/Villains/Villain_FaerGerl). Кадры — в Data/Characters/Sprites_FaerGerl.
/// </summary>
public class FaerGerlKit : HeroKit
{
    private const int Fireballs = 0, Tornado = 1, Flight = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        ballEnergy = Num(Fireballs, "energy");
        ballDamage = Num(Fireballs, "damage");
        ballsMin = Count(Fireballs, "min");
        ballsMax = Mathf.Max(ballsMin, Count(Fireballs, "max"));
        tornadoTime = Num(Tornado, "time");
        tornadoHit = Num(Tornado, "hit");
        tornadoDamage = Num(Tornado, "damage");
        flightHp = Num(Flight, "hp");
    }

    [Header("Фаерболы")]
    [SerializeField] private int ballsMin = 2;              // Меньше этого не бросает (ждёт энергию)
    [SerializeField] private int ballsMax = 5;              // Больше этого не бросает
    [SerializeField] private float ballEnergy = 15f;        // Цена одного шара (энергия)
    [SerializeField] private float ballDamage = 0.45f;      // Урон одного шара (доля Магии)
    [SerializeField] private float ballInterval = 0.25f;    // Интервал между шарами
    [SerializeField] private float ballSpeed = 11f;         // Скорость
    [SerializeField] private float ballRange = 9f;          // Дальность
    [SerializeField] private float ballMinRange = 1.5f;     // Ближе — бьёт рукой

    [Header("Огненное торнадо")]
    [SerializeField] private float tornadoTime = 3f;        // Сколько живёт
    [SerializeField] private float tornadoSpeed = 3f;       // Как быстро движется
    private float tornadoHit = 0.5f;                        // Разовый урон при попадании (доля Магии)
    [SerializeField] private float tornadoDamage = 0.2f;    // Урон раз в 0.5 с (доля Магии)
    [SerializeField] private float tornadoRange = 6f;       // Пускает, когда враг впереди ближе этого

    [Header("Полёт (пассивка)")]
    [SerializeField] private float flightHp = 0.2f;         // При какой доле HP улетает
    [SerializeField] private float flightRadius = 8f;       // Как далеко ищет безопасное место
    [SerializeField] private float flightUp = 10f;          // Скорость взлёта (выше — дольше летит)
    [SerializeField] private float flightMaxSpeed = 12f;    // Самая большая скорость полёта по арене

    private int current = -1;        // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;      // Сколько длится действие
    private int ballsTotal;          // Сколько шаров в этом броске
    private int ballsThrown;         // Сколько уже брошено
    private Fighter skillTarget;     // В кого скил
    private bool tornadoFired;       // Торнадо уже запущено

    // ---------- Внешний вид ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 2) Торнадо — враг впереди недалеко, примерно на одной глубине
        if (IsReady(Tornado) && target.IsTargetable && absX <= tornadoRange && absY <= 0.7f)
        {
            if (!Begin(Tornado, 0.45f, target)) return false;
            tornadoFired = false;
            return true;
        }

        // 1) Фаерболы — цель не вплотную, примерно на одной линии, энергии хотя бы на 2
        if (IsReady(Fireballs) && target.IsTargetable && absY <= 0.8f && absX >= ballMinRange && absX <= ballRange
            && self.Combat.HasEnergy(ballEnergy * ballsMin))
        {
            ballsTotal = Mathf.Clamp(Mathf.FloorToInt(self.Combat.Energy / ballEnergy), ballsMin, ballsMax);
            if (!Begin(Fireballs, 0.1f + ballInterval * ballsTotal + 0.15f, target)) return false;
            ballsThrown = 0;
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
        skillTarget = target;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Бросить один фаербол (очередь: прямо, с доворотом к цели).</summary>
    private void ThrowBall()
    {
        // Очередь (как в Little Fighter 2 при зажатой кнопке): кончилась энергия или цель выбыла — очередь обрывается
        if (skillTarget == null || !skillTarget.IsAlive || !self.Combat.TrySpendEnergy(ballEnergy)) { ballsTotal = ballsThrown; actionLength = Mathf.Min(actionLength, ActionTime + 0.15f); return; }
        if (skillTarget != null && skillTarget.IsAlive) self.Movement.FaceTowards(skillTarget.Position.x);
        ballsThrown++;
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * ballDamage,
            vsSpecial = true,
            speed = ballSpeed,
            range = ballRange,
            size = 0.55f,
            stagger = 1,
            knockback = 1.5f,
            steerTarget = skillTarget, // летит прямо, но чуть доворачивает к цели
            color = new Color(1f, 0.4f, 0.1f),
            flicker = new Color(1f, 0.85f, 0.25f)
        });
    }

    /// <summary>Огненное торнадо: появляется перед Фаер Герл и движется вперёд.</summary>
    private void FireTornado()
    {
        int dir = self.Movement.Facing;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        Vector3 start = BattleManager.Instance.ClampToArena(self, self.transform.position + new Vector3(dir * 1f, 0f, 0f));
        FaerTornado.Launch(self, start, dir, tornadoTime, tornadoSpeed, self.Stats.specialAttack * tornadoHit, self.Stats.specialAttack * tornadoDamage, circle);
        Pop("ТОРНАДО", new Color(1f, 0.55f, 0.15f));
    }

    // ---------- Полёт ----------

    /// <summary>Полёт: HP упало до 20% — взлетает и улетает в безопасное место (неуязвима, пока летит).</summary>
    private void UpdateFlight()
    {
        if (!IsUnlocked(Flight) || IsOnCooldown(Flight) || !self.IsAlive || !self.CanAct) return;
        if (self.Health.Fraction > flightHp) return;
        Vector3 to = SafePoint(flightRadius);
        float airTime = 2f * flightUp / Mathf.Max(1f, self.Movement.Gravity);
        Vector2 ground = (to - self.transform.position) / airTime;
        if (ground.magnitude > flightMaxSpeed) ground = ground.normalized * flightMaxSpeed;
        self.Movement.FaceTowards(to.x);
        if (!self.StartJump(flightUp, ground)) return;
        self.Combat.SkipJumpAttack();
        self.SetInvulnerable(airTime);
        StartCooldown(Flight);
        Pop("ПОЛЁТ", new Color(1f, 0.7f, 0.3f));
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (self != null) UpdateFlight();

        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Фаер Герл попали — действие прервано
        ActionTime += Time.deltaTime;

        if (current == Fireballs && ballsThrown < ballsTotal && ActionTime >= 0.1f + ballsThrown * ballInterval) ThrowBall();
        if (current == Tornado && !tornadoFired && ActionTime >= 0.25f) { tornadoFired = true; FireTornado(); }

        if (ActionTime >= actionLength)
        {
            current = -1;
            skillTarget = null;
            EndAction();
        }
    }
}
