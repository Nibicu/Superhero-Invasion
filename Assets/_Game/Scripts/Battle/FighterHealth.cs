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

    /// <summary>Здоровье изменилось (для полоски HP).</summary>
    public event Action<FighterHealth> Changed;

    /// <summary>Боец погиб.</summary>
    public event Action<Fighter> Died;

    private void Awake()
    {
        fighter = GetComponent<Fighter>();
        lastHitTime = -100f;
    }

    /// <summary>Реген HP: работает, только если бойца давно не били (Реген HP × regenRate единиц в секунду).</summary>
    private void Update()
    {
        if (!fighter.IsAlive || Current >= Max || TimeSinceHit < regenDelay) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.IsRunning) return;
        Heal(fighter.Stats.hpRegen * regenRate * Time.deltaTime);
    }

    /// <summary>Вылечить на amount единиц (не больше максимума). Мёртвых не лечит.</summary>
    public void Heal(float amount)
    {
        if (!fighter.IsAlive || amount <= 0f) return;
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
        if (recentHits.Contains(hit.attackId)) return false;
        Remember(hit.attackId);

        Current = Mathf.Max(0f, Current - hit.damage);
        Changed?.Invoke(this);
        if (fighter.View != null) fighter.View.FlashHit();

        if (Current <= 0f)
        {
            fighter.SetState(FighterState.Dead);
            fighter.Movement.ApplyKnockback(hit.knockback * 1.3f);
            Died?.Invoke(fighter);
            return true;
        }

        // Удары подряд копят "сбивание"; набралось — боец падает
        if (Time.time - lastHitTime > fighter.staggerResetTime) stagger = 0;
        lastHitTime = Time.time;
        stagger += hit.stagger;

        if (stagger >= fighter.staggerToKnockdown)
        {
            stagger = 0;
            fighter.SetState(FighterState.KnockedDown);
            fighter.Movement.ApplyKnockback(hit.knockback * 1.6f);
        }
        else
        {
            fighter.SetState(FighterState.Hurt);
            fighter.Movement.ApplyKnockback(hit.knockback);
        }
        return true;
    }

    /// <summary>Запомнить номер удара (храним только последние).</summary>
    private void Remember(int attackId)
    {
        recentHits.Add(attackId);
        recentHitOrder.Enqueue(attackId);
        if (recentHitOrder.Count > RememberedHits) recentHits.Remove(recentHitOrder.Dequeue());
    }
}
