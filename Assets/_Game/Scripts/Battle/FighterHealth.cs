using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Здоровье бойца: получение урона, реакция (Hurt), сбивание с ног (KnockedDown), смерть (Dead).
/// Защита от повторного урона: каждый удар имеет номер (attackId),
/// и один и тот же удар не может ранить бойца дважды.
/// Лежачий и только что поднявшийся боец неуязвим.
/// </summary>
[RequireComponent(typeof(Fighter))]
public class FighterHealth : MonoBehaviour
{
    private const int RememberedHits = 16; // Сколько последних ударов помнить

    private Fighter fighter;
    private readonly Queue<int> recentHitOrder = new Queue<int>(); // Порядок запомненных ударов
    private readonly HashSet<int> recentHits = new HashSet<int>();  // Номера недавних ударов
    private int stagger;        // Набранные "очки сбивания"
    private float lastHitTime;  // Когда был последний удар

    /// <summary>Максимум здоровья.</summary>
    public float Max { get; private set; }

    /// <summary>Сколько здоровья сейчас.</summary>
    public float Current { get; private set; }

    /// <summary>Доля здоровья 0..1.</summary>
    public float Fraction => Max > 0 ? Current / Max : 0f;

    /// <summary>Сколько секунд назад бойца ударили в последний раз.</summary>
    public float TimeSinceHit => Time.time - lastHitTime;

    [Tooltip("Через сколько секунд без ударов начинает работать реген HP")]
    [SerializeField] private float regenDelay = 3f;
    [Tooltip("Какая доля параметра 'Реген HP' восстанавливается в секунду (0.5 = половина)")]
    [SerializeField] private float regenRate = 0.5f;
    [Tooltip("Сбитый с ног отлетает не слабее этого (скорость отлёта)")]
    [SerializeField] private float minFallKnockback = 6f;

    /// <summary>Здоровье изменилось (для полоски HP).</summary>
    public event Action<FighterHealth> Changed;

    /// <summary>Боец погиб.</summary>
    public event Action<Fighter> Died;

    private void Awake()
    {
        fighter = GetComponent<Fighter>();
        lastHitTime = -100f;
    }

    /// <summary>
    /// Реген HP: работает, только если бойца давно не били (Реген HP × regenRate единиц в секунду).
    /// Навык "Геном" (Солдат) — реген идёт всегда, даже под ударами.
    /// </summary>
    private void Update()
    {
        bool always = fighter.Kit != null && fighter.Kit.RegenAlways;
        if (!fighter.IsAlive || Current >= Max || (TimeSinceHit < regenDelay && !always)) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.IsRunning) return;
        Heal(fighter.Stats.hpRegen * regenRate * Time.deltaTime);
    }

    /// <summary>Вылечить на amount единиц (не больше максимума). Мёртвых не лечит. healer — кто лечит (для статистики).</summary>
    public void Heal(float amount, Fighter healer = null)
    {
        if (!fighter.IsAlive || amount <= 0f) return;
        float healed = Mathf.Min(Max - Current, amount);
        if (healer != null) healer.StatsOwner.HealingDone += healed;
        Current = Mathf.Min(Max, Current + amount);
        Changed?.Invoke(this);
    }

    /// <summary>Задать здоровье. fraction — с какой долей начинать (раненая команда).</summary>
    public void Setup(float max, float fraction)
    {
        Max = Mathf.Max(1f, max);
        Current = Mathf.Max(1f, Max * Mathf.Clamp01(fraction));
        Changed?.Invoke(this);
    }

    /// <summary>
    /// Получить удар. Вернёт false, если удар не прошёл
    /// (боец мёртв, лежит, неуязвим или уже получал этот удар).
    /// </summary>
    public bool TakeHit(HitInfo hit)
    {
        if (!fighter.IsAlive || fighter.IsInvulnerable) return false;
        if (fighter.HeldBy != null && hit.attacker != fighter.HeldBy) return false; // схваченного бьёт только тот, кто держит
        if (recentHits.Contains(hit.attackId)) return false;
        Remember(hit.attackId);

        // Личные навыки: блок или парирование — удар не проходит (ни урона, ни отбрасывания)
        if (fighter.Kit != null && fighter.Kit.TryBlock(hit)) return true;

        // Щит "Барьер" впитывает урон
        float damage = fighter.AbsorbShield(hit.damage);
        float lost = Mathf.Min(Current, damage); // Сколько здоровья реально снял удар (для статистики)
        fighter.DamageTaken += lost;
        if (hit.attacker != null) hit.attacker.StatsOwner.DamageDealt += lost;
        Current = Mathf.Max(0f, Current - damage);
        Changed?.Invoke(this);
        if (fighter.View != null) fighter.View.FlashHit();

        if (Current <= 0f)
        {
            Die(hit.attacker);
            fighter.Movement.ApplyKnockback(hit.knockback * 1.3f);
            return true;
        }

        // С ног валит только сильный удар (добивающий, бросок, скил). Лёгкие удары подряд копят "сбивание" —
        // набралось — боец шатается (его можно схватить)
        bool knockdown = hit.stagger >= Fighter.KnockdownStagger;
        if (Time.time - lastHitTime > fighter.staggerResetTime) stagger = 0;
        lastHitTime = Time.time;
        if (!knockdown) stagger += hit.stagger;
        FighterState st = fighter.State;

        if (knockdown)
        {
            stagger = 0;
            fighter.SetState(FighterState.KnockedDown);
            fighter.Movement.ApplyKnockback(FallKnockback(hit));
        }
        else if (st == FighterState.Held)
        {
            // Схваченный: урон проходит, стоит на месте (держит захват)
        }
        else if (stagger >= fighter.staggerToDizzy && fighter.CanBecomeDizzy && st != FighterState.Dizzy)
        {
            stagger = 0;
            fighter.BecomeDizzy();
            if (st != FighterState.Holding) fighter.Movement.ApplyKnockback(hit.knockback);
        }
        else if (st == FighterState.Dizzy || st == FighterState.Holding)
        {
            // Шатается — продолжает шататься; держит захват — не отпускает от обычного удара
            if (st == FighterState.Dizzy) fighter.Movement.ApplyKnockback(hit.knockback);
        }
        else if (fighter.Kit != null && fighter.Kit.Unstoppable)
        {
            // Залп, рывок и т.п.: обычный удар не прерывает (урон проходит), сбить с ног — может
        }
        else
        {
            fighter.SetState(FighterState.Hurt);
            fighter.Movement.ApplyKnockback(hit.knockback);
        }
        return true;
    }

    /// <summary>
    /// Отлёт при падении: сила удара × 1.6, но не меньше minFallKnockback — сбитый с ног всегда отлетает
    /// (от того, кто ударил), а не падает на месте.
    /// </summary>
    private Vector2 FallKnockback(HitInfo hit)
    {
        Vector2 k = hit.knockback * 1.6f;
        if (Mathf.Abs(k.x) >= minFallKnockback) return k;
        float dir = Mathf.Abs(k.x) > 0.01f ? Mathf.Sign(k.x)
            : hit.attacker != null ? Mathf.Sign(fighter.Position.x - hit.attacker.Position.x + 0.001f) : -fighter.Movement.Facing;
        return new Vector2(dir * minFallKnockback, k.y);
    }

    /// <summary>
    /// Урон со временем (кровотечение): без реакции на удар, блока и сбивания. Щит впитывает.
    /// source — кто наложил (ему засчитывается урон и добивание).
    /// </summary>
    public void TakeDamageOverTime(float amount, Fighter source)
    {
        if (!fighter.IsAlive || amount <= 0f) return;
        float damage = fighter.AbsorbShield(amount);
        float lost = Mathf.Min(Current, damage);
        fighter.DamageTaken += lost;
        if (source != null) source.StatsOwner.DamageDealt += lost;
        Current = Mathf.Max(0f, Current - damage);
        Changed?.Invoke(this);
        if (Current <= 0f) Die(source);
    }

    /// <summary>Боец выбыл: добивание засчитывается killer, время выбывания — для статистики.</summary>
    private void Die(Fighter killer)
    {
        if (killer != null && !fighter.IsClone) killer.StatsOwner.Kills++;
        if (BattleManager.Instance != null) fighter.DeathTime = BattleManager.Instance.Elapsed;
        fighter.SetState(FighterState.Dead);
        Died?.Invoke(fighter);
    }

    /// <summary>Запомнить номер удара (храним только последние).</summary>
    private void Remember(int attackId)
    {
        recentHits.Add(attackId);
        recentHitOrder.Enqueue(attackId);
        if (recentHitOrder.Count > RememberedHits) recentHits.Remove(recentHitOrder.Dequeue());
    }
}
