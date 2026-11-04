using UnityEngine;

/// <summary>
/// Фризен (героиня, класс Маг, урон от Магии). Навыки:
///  1) "Ледяной шар" (ур. 1) — шар льда: 100% Магии и заморозка (стан) на 3 с.
///  2) "Ледяной смерч" (ур. 3) — как Огненное торнадо Фаер Герл, только голубое: задетых подхватывает (80% Магии),
///     а когда смерч кончается — замёрзшие враги падают на землю и 2 с не двигаются (стан).
///  3) "Ледяной покров" (ур. 5, пассивный) — +30 к Защите и +30 к Спец. защите.
/// Перезарядки — в файле героя (Data/Heroes/Hero_Frizen). Кадры — в Data/Characters/Sprites_Frizen.
/// </summary>
public class FrizenKit : HeroKit
{
    private const int IceBall = 0, IceTornado = 1, IceCover = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        ballDamage = Num(IceBall, "damage");
        ballStun = Num(IceBall, "stun");
        tornadoDamage = Num(IceTornado, "damage");
        tornadoTime = Num(IceTornado, "time");
        tornadoStun = Num(IceTornado, "stun");
        coverDefense = Count(IceCover, "defense");
        coverSpDefense = Count(IceCover, "spdefense");
    }

    [Header("Ледяной шар")]
    [SerializeField] private float ballDamage = 1f;         // Урон (доля Магии)
    [SerializeField] private float ballStun = 3f;           // Заморозка (стан), сек
    [SerializeField] private float ballSpeed = 9f;          // Скорость
    [SerializeField] private float ballRange = 8f;          // Дальность
    [SerializeField] private float ballMinRange = 1.2f;     // Ближе — бьёт рукой

    [Header("Ледяной смерч")]
    [SerializeField] private float tornadoDamage = 0.8f;    // Урон при попадании (доля Магии)
    [SerializeField] private float tornadoTime = 3f;        // Сколько живёт
    [SerializeField] private float tornadoSpeed = 3f;       // Как быстро движется
    [SerializeField] private float tornadoStun = 2f;        // Стан после падения
    [SerializeField] private float tornadoRange = 6f;       // Пускает, когда враг впереди ближе этого

    [Header("Ледяной покров (пассивка)")]
    [SerializeField] private int coverDefense = 30;         // +Защита
    [SerializeField] private int coverSpDefense = 30;       // +Спец. защита

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;    // Сколько длится действие
    private bool fired;            // Шар / смерч уже выпущены

    // ---------- Внешний вид и пассивки ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    /// <summary>Ледяной покров: +Защита и +Спец. защита (когда скил открыт).</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (self == null || !IsUnlocked(IceCover)) return s;
        s.defense += coverDefense;
        s.specialDefense += coverSpDefense;
        return s;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive || !target.IsTargetable) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 2) Ледяной смерч — враг впереди недалеко, примерно на одной глубине
        if (IsReady(IceTornado) && absX <= tornadoRange && absY <= 0.7f)
            return Begin(IceTornado, 0.45f, target);

        // 1) Ледяной шар — враг на одной линии, не вплотную (оглушённого не морозим повторно)
        if (IsReady(IceBall) && !target.IsStunned && absY <= 0.6f && absX >= ballMinRange && absX <= ballRange)
            return Begin(IceBall, 0.35f, target);
        return false;
    }

    /// <summary>Начать действие скила skill длиной length против target.</summary>
    private bool Begin(int skill, float length, Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        actionLength = length;
        fired = false;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Скилы ----------

    /// <summary>Ледяной шар: урон и заморозка (стан).</summary>
    private void FireIceBall()
    {
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.specialAttack * ballDamage,
            vsSpecial = true,
            speed = ballSpeed,
            range = ballRange,
            size = 0.6f,
            stagger = 1,
            knockback = 0.5f,
            stun = ballStun,
            color = new Color(0.6f, 0.9f, 1f),
            flicker = new Color(0.9f, 1f, 1f)
        });
        Pop("ЛЕДЯНОЙ ШАР", new Color(0.6f, 0.9f, 1f));
    }

    /// <summary>Ледяной смерч: появляется перед Фризен и движется вперёд.</summary>
    private void FireTornado()
    {
        int dir = self.Movement.Facing;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        Vector3 start = BattleManager.Instance.ClampToArena(self, self.transform.position + new Vector3(dir * 1f, 0f, 0f));
        FaerTornado.Launch(self, start, dir, tornadoTime, tornadoSpeed, self.Stats.specialAttack * tornadoDamage, 0f, circle,
                           new Color(0.35f, 0.65f, 1f), new Color(0.85f, 0.97f, 1f), tornadoStun);
        Pop("ЛЕДЯНОЙ СМЕРЧ", new Color(0.6f, 0.9f, 1f));
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Фризен попали — действие прервано
        ActionTime += Time.deltaTime;

        if (!fired && ActionTime >= 0.2f)
        {
            fired = true;
            if (current == IceBall) FireIceBall();
            else if (current == IceTornado) FireTornado();
        }

        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }
}
