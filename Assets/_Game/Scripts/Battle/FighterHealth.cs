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

    /// <summary>Здоровье изменилось (для полоски HP).</summary>
    public event Action<FighterHealth> Changed;

    /// <summary>Боец погиб.</summary>
    public event Action<Fighter> Died;

    private void Awake() => fighter = GetComponent<Fighter>();

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
