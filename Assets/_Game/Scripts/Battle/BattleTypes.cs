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

/// <summary>Итог ручного боя.</summary>
public struct BattleResult
{
    public bool win;          // Победили ли атакующие (наши)
    public float attackerHp;  // Доля HP команды атакующих после боя
    public float defenderHp;  // Доля HP защитников после боя
}

/// <summary>
/// "Место боя" — то, за что можно сражаться: объект карты или главная база.
/// Окно перед боем и BattleManager работают с любым местом через этот интерфейс.
/// </summary>
public interface IBattleSite
{
    /// <summary>Название (для заголовков).</summary>
    string SiteName { get; }

    /// <summary>Цвет арены.</summary>
    Color SiteColor { get; }

    /// <summary>Защитники по волнам (одна волна = одна территория арены). Пусто — защитников нет.</summary>
    List<List<BattleUnit>> GetDefenderWaves();

    /// <summary>Общая сила защитников.</summary>
    int DefenderPower { get; }

    /// <summary>Игрок выбрал автобой.</summary>
    void BeginAutoBattle(SquadUnit unit, BattleForecast forecast);

    /// <summary>Игрок выбрал ручной бой (он начинается).</summary>
    void OnManualBattleStarted();

    /// <summary>Ручной бой закончился.</summary>
    void OnManualBattleFinished(SquadUnit unit, BattleResult result);
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

/// <summary>Данные одного попадания (удар или снаряд).</summary>
public struct HitInfo
{
    public Fighter attacker;  // Кто ударил
    public int attackId;      // Номер атаки — защита от повторного урона одним ударом
    public float damage;      // Урон
    public Vector2 knockback; // Скорость отбрасывания (X — от атакующего, Y — по глубине)
    public int stagger;       // "Очки сбивания": набралось много подряд — боец падает
}
