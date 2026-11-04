using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Мозг (злодей, класс Маг). Навыки:
///  1) "Щупальца" (ур. 1) — бьёт длинными щупальцами вперёд на среднее расстояние: всех задетых отбрасывает, урон 100% Силы атаки.
///  2) "Паразит" (ур. 3, пассивный) — когда враг выбывает, Мозг пускает к нему паразита (маленький мозг). Паразит вселяется
///     в выбывшего и поднимает его зомби на стороне Мозга до конца боя: 50% характеристик погибшего (скорость — как была),
///     скилы есть, но бьют вдвое слабее. Паразитов — сколько врагов выбудет.
///  3) "Интеллект" (ур. 5, пассивный) — за каждого паразита +40 к Магии до конца боя.
/// Перезарядки — в файле героя (Data/Villains/Villain_Mozg). Кадры — в Data/Characters/Sprites_Mozg.
/// </summary>
public class MozgKit : HeroKit
{
    private const int Tentacles = 0, Parasite = 1, Intellect = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        tentacleDamage = Num(Tentacles, "damage");
        tentacleRange = Num(Tentacles, "range");
        zombieStats = Num(Parasite, "stats");
        intellectBonus = Num(Intellect, "bonus");
    }

    [Header("Щупальца")]
    [SerializeField] private float tentacleDamage = 1f;     // Урон (доля Силы атаки)
    [SerializeField] private float tentacleRange = 3.5f;    // Длина
    [SerializeField] private float tentacleKnock = 4f;      // Отбрасывание

    [Header("Паразит (пассивка)")]
    [SerializeField] private float zombieStats = 0.5f;      // Характеристики зомби (доля от погибшего)
    [SerializeField] private float parasiteSpeed = 6f;      // Как быстро летит паразит

    [Header("Интеллект (пассивка)")]
    [SerializeField] private float intellectBonus = 40f;    // +Магия за каждого паразита

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;    // Сколько длится действие
    private bool done;             // Щупальца уже ударили
    private int parasites;         // Сколько зомби подняли паразиты за бой

    /// <summary>Паразит в полёте: картинка, к кому летит и где он сейчас.</summary>
    private class Flight
    {
        public Fighter corpse;  // Выбывший враг
        public Vector3 pos;     // Где паразит
        public SkillFx body;    // Картинка (розовый мозг)
    }

    private readonly List<Flight> flights = new List<Flight>();       // Паразиты в полёте
    private readonly HashSet<Fighter> claimed = new HashSet<Fighter>(); // К кому паразит уже летел

    // ---------- Внешний вид и пассивки ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    /// <summary>Интеллект: +Магия за каждого паразита.</summary>
    public override HeroStats ModifyStats(HeroStats s)
    {
        if (self != null && parasites > 0 && IsUnlocked(Intellect)) s.specialAttack += Mathf.RoundToInt(intellectBonus * parasites);
        return s;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive || !target.IsTargetable) return false;
        Vector2 d = target.Position - self.Position;
        if (IsReady(Tentacles) && Mathf.Abs(d.x) <= tentacleRange && Mathf.Abs(d.y) <= 0.6f)
        {
            if (!BeginAction(KitPose.Cast)) return false;
            StartCooldown(Tentacles);
            current = Tentacles;
            actionLength = 0.45f;
            done = false;
            self.Movement.FaceTowards(target.Position.x);
            return true;
        }
        return false;
    }

    // ---------- Щупальца ----------

    /// <summary>Удар щупальцами вперёд: всех на линии — урон и отбрасывание.</summary>
    private void Lash()
    {
        int dir = self.Movement.Facing;
        foreach (Fighter e in Enemies)
        {
            if (!e.IsAlive || e.IsInvulnerable) continue;
            Vector2 d = e.Position - self.Position;
            if (d.x * dir < -0.2f || Mathf.Abs(d.x) > tentacleRange || Mathf.Abs(d.y) > 0.6f) continue; // только впереди
            HitEnemy(e, self.Stats.attack * tentacleDamage, false, new Vector2(dir * tentacleKnock, 0f), 2);
        }
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            for (int i = 0; i < 2; i++) // два щупальца: длинные розовые полосы вперёд
                SkillFx.Spawn(circle, self.transform.position + new Vector3(dir * tentacleRange * 0.5f, 0.7f + i * 0.35f, 0f),
                    new Color(0.95f, 0.45f, 0.65f), new Vector2(0.5f, 0.15f), new Vector2(tentacleRange, 0.2f), 0.3f, 60).Fade(0.95f, 0f);
        Pop("ЩУПАЛЬЦА", new Color(0.95f, 0.5f, 0.7f));
    }

    // ---------- Паразит ----------

    /// <summary>Выбыл враг — пускаем к нему паразита (один на каждого выбывшего; клонов и зомби не трогаем).</summary>
    private void FindCorpses()
    {
        if (!self.IsAlive || !IsUnlocked(Parasite)) return;
        foreach (Fighter e in Enemies)
        {
            if (e == null || e.IsAlive || e.IsClone || claimed.Contains(e)) continue;
            claimed.Add(e);
            var f = new Flight { corpse = e, pos = self.transform.position + new Vector3(0f, 1.2f, 0f) };
            Sprite circle = self.View != null ? self.View.CircleSprite : null;
            if (circle != null)
                f.body = SkillFx.Spawn(circle, f.pos, new Color(1f, 0.55f, 0.75f), new Vector2(0.35f, 0.28f), new Vector2(0.35f, 0.28f), 30f, 75);
            flights.Add(f);
            Pop("ПАРАЗИТ", new Color(1f, 0.55f, 0.75f));
        }
    }

    /// <summary>Паразиты летят к выбывшим; долетел — выбывший встаёт зомби на стороне Мозга.</summary>
    private void UpdateFlights()
    {
        BattleManager bm = BattleManager.Instance;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            if (f.corpse == null || bm == null || !bm.IsRunning) { Drop(i); continue; }
            Vector3 to = f.corpse.transform.position + new Vector3(0f, 0.3f, 0f);
            f.pos = Vector3.MoveTowards(f.pos, to, parasiteSpeed * Time.deltaTime);
            if (f.body != null) f.body.transform.position = f.pos + new Vector3(0f, Mathf.Sin(Time.time * 10f + i) * 0.08f, 0f);
            if ((f.pos - to).sqrMagnitude > 0.04f) continue;

            // Вселился: выбывший встаёт зомби
            Fighter z = bm.SpawnZombie(self, f.corpse, zombieStats);
            parasites++;
            if (z != null && z.View != null) z.View.Pop("ЗОМБИ", new Color(0.55f, 1f, 0.45f));
            Drop(i);
        }
    }

    /// <summary>Убрать паразита из полёта (вместе с картинкой).</summary>
    private void Drop(int i)
    {
        if (flights[i].body != null) flights[i].body.Kill();
        flights.RemoveAt(i);
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (self != null)
        {
            FindCorpses();
            UpdateFlights();
        }
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Мозгу попали — действие прервано
        ActionTime += Time.deltaTime;
        if (current == Tentacles && !done && ActionTime >= 0.2f) { done = true; Lash(); }
        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }
}
