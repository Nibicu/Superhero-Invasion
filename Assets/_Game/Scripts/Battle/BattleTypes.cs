using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один защитник для боя: кто, с какими характеристиками и с каким здоровьем.
/// Охрана объекта (мобы) и гарнизон базы (герои) превращаются в такие записи.
/// </summary>
public struct BattleUnit
{
    public HeroData data;     // Кто (имя, цвет, портрет)
    public HeroStats stats;   // Характеристики с учётом уровня и усилений
    public float hpFraction;  // С какой долей здоровья выходит (раненая команда)
    public string info;       // Подпись для окна перед боем
}

/// <summary>Итог ручного боя — с точки зрения игрока ("наши" — бойцы слева).</summary>
public struct BattleResult
{
    public bool win;       // Победили ли наши
    public float ourHp;    // Доля HP наших бойцов после боя
    public float theirHp;  // Доля HP охраны/защитников после боя
    public float rivalHp;  // Доля HP вражеской команды (только в битве за флаг)
}

/// <summary>
/// Сторона в бою на арене. Кто с кем дерётся:
///  Heroes (наши)        — с Guards и Rivals;
///  Guards (охрана)      — только с Heroes;
///  Rivals (команда врага в битве за флаг) — с RivalGuards и Heroes;
///  RivalGuards (копия охраны для врага)   — только с Rivals.
/// В обычном бою есть только Heroes и Guards.
/// </summary>
public enum BattleFaction
{
    Heroes,
    Guards,
    Rivals,
    RivalGuards
}

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

/// <summary>Какой параметр усиливает временный бафф из коробки.</summary>
public enum BoostStat
{
    AutoAttack,
    Attack,
    SpecialAttack,
    Defense,
    SpecialDefense,
    Speed
}

/// <summary>Названия баффов для надписей.</summary>
public static class BoostNames
{
    public static string Get(BoostStat s)
    {
        switch (s)
        {
            case BoostStat.AutoAttack: return "Авто атака";
            case BoostStat.Attack: return "Атака";
            case BoostStat.SpecialAttack: return "Спец. атака";
            case BoostStat.Defense: return "Защита";
            case BoostStat.SpecialDefense: return "Спец. защита";
            default: return "Скорость";
        }
    }
}

/// <summary>Параметры выстрела (авто атака стрелков и магов, скилы-снаряды).</summary>
public struct ShotInfo
{
    public float power;         // Сила (авто атака или атака/спец. атака стрелявшего, уже с множителем скила)
    public bool vsSpecial;      // true — ослабляется Спец. защитой цели, false — Защитой
    public float speed;         // Скорость полёта
    public float range;         // Дальность полёта
    public float size;          // Размер картинки (1 — обычный снаряд)
    public int stagger;         // "Очки сбивания" при попадании
    public float knockback;     // Сила отбрасывания
    public bool pierce;         // Пробивает насквозь (летит дальше после попадания)
    public bool hitsBoxes;      // Разбивает коробки (только авто атаки)
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
