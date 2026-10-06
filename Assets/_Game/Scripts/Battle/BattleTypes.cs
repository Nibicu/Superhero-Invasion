using System;
using UnityEngine;

/// <summary>Один охранник объекта: кто (HeroData) и какого уровня.</summary>
[Serializable]
public class GuardEntry
{
    [Tooltip("Кто охраняет (злодей или моб из Assets/_Game/Data/Villains)")]
    public HeroData unit;

    [Tooltip("Уровень охранника (каждый уровень +10% к характеристикам)")]
    [Min(1)] public int level = 1;
}

/// <summary>Охрана одной территории арены (волна врагов).</summary>
[Serializable]
public class GuardWave
{
    public GuardEntry[] guards = new GuardEntry[0];
}

/// <summary>Состояние бойца на арене.</summary>
public enum FighterState
{
    Idle,        // Стоит
    Moving,      // Идёт
    Attacking,   // Бьёт или стреляет (короткая фаза)
    Hurt,        // Получил удар (короткая реакция)
    KnockedDown, // Сбит с ног — лежит, неуязвим, враги теряют его как цель
    Dead         // Выбыл из боя
}

/// <summary>Данные одного попадания (удар или снаряд).</summary>
public struct HitInfo
{
    public Fighter attacker;  // Кто ударил
    public int attackId;      // Номер атаки — защита от повторного урона одним ударом
    public float damage;      // Урон
    public Vector2 knockback; // Скорость отбрасывания (X — от атакующего, Y — по глубине)
    public int stagger;       // "Очки сбивания": набралось много подряд — боец падает
}
