using UnityEngine;

/// <summary>Прогноз боя по соотношению сил.</summary>
public enum BattleForecast
{
    Win,   // Мы сильнее на 20% и больше
    Equal, // Силы примерно равны
    Lose   // Враг сильнее на 20% и больше
}

/// <summary>
/// Все расчёты "силы" и автобоя в одном месте.
/// Используется окном перед боем, захватом объектов и ИИ врага на карте.
/// Цифры (20%, потери HP) можно менять здесь.
/// </summary>
public static class BattleCalculator
{
    /// <summary>Во сколько раз нужно быть сильнее для уверенной победы (1.2 = на 20%).</summary>
    public const float Advantage = 1.2f;

    /// <summary>Потеря HP команды при уверенной победе в автобое.</summary>
    public const float WinHpLoss = 0.3f;

    /// <summary>Потеря HP при победе в равном автобое.</summary>
    public const float EqualWinHpLoss = 0.5f;

    /// <summary>Потеря HP при поражении.</summary>
    public const float LossHpLoss = 0.8f;

    /// <summary>Минимальное HP команды после боя (команда не "умирает", а лечится на базе).</summary>
    public const float MinHpAfterBattle = 0.05f;

    /// <summary>Сила по характеристикам: атака + спец. атака + защита + спец. защита.</summary>
    public static int StatsPower(HeroStats s) => s.attack + s.specialAttack + s.defense + s.specialDefense;

    /// <summary>
    /// Боевая сила команды с учётом здоровья: раненая команда слабее
    /// (при 0% HP — 40% силы, при 100% HP — вся сила).
    /// </summary>
    public static int SquadPower(Squad squad) => Mathf.RoundToInt(squad.Power * (0.4f + 0.6f * squad.HpFraction));

    /// <summary>Характеристики охранника с учётом уровня.</summary>
    public static HeroStats GuardStats(GuardEntry g) =>
        g.unit.baseStats.Scaled(1f + HeroInstance.StatGrowthPerLevel * (g.level - 1));

    /// <summary>Сила всей охраны объекта (все волны).</summary>
    public static int GarrisonPower(MapObjectData data)
    {
        int sum = 0;
        if (data.waves == null) return 0;
        foreach (GuardWave w in data.waves)
            foreach (GuardEntry g in w.guards)
                if (g != null && g.unit != null) sum += StatsPower(GuardStats(g));
        return sum;
    }

    /// <summary>Прогноз: сравниваем нашу силу с силой противника.</summary>
    public static BattleForecast Forecast(int ourPower, int enemyPower)
    {
        if (enemyPower <= 0) return BattleForecast.Win;
        float ratio = ourPower / (float)enemyPower;
        if (ratio >= Advantage) return BattleForecast.Win;
        if (ratio <= 1f / Advantage) return BattleForecast.Lose;
        return BattleForecast.Equal;
    }

    /// <summary>Бросок результата автобоя: уверенная победа — всегда, равные силы — 50/50, иначе поражение.</summary>
    public static bool RollAutoBattle(BattleForecast f) =>
        f == BattleForecast.Win || (f == BattleForecast.Equal && Random.value < 0.5f);

    /// <summary>Сколько HP (доля) команда теряет после автобоя.</summary>
    public static float AutoBattleHpLoss(BattleForecast f, bool won)
    {
        if (!won) return LossHpLoss;
        return f == BattleForecast.Win ? WinHpLoss : EqualWinHpLoss;
    }

    /// <summary>Надпись прогноза для окна перед боем.</summary>
    public static string ForecastTitle(BattleForecast f)
    {
        switch (f)
        {
            case BattleForecast.Win: return "<color=#6EE07A>ПОБЕДА С НЕБОЛЬШИМИ ПОТЕРЯМИ</color>";
            case BattleForecast.Equal: return "<color=#FFD84A>СИЛЫ РАВНЫ</color>";
            default: return "<color=#FF6B6B>ВЫ ТОЧНО ПРОИГРАЕТЕ</color>";
        }
    }

    /// <summary>Подсказка под прогнозом.</summary>
    public static string ForecastHint(BattleForecast f)
    {
        switch (f)
        {
            case BattleForecast.Win: return "Смело жмите «Автобой»: захват гарантирован, команда потеряет 30% HP.";
            case BattleForecast.Equal: return "Автобой — 50 на 50: победа (−50% HP) или провал (−80% HP). Лучше сразиться самому!";
            default: return "Автобой проигран: −80% HP, объект не захвачен. Можно рискнуть и сразиться самому.";
        }
    }
}
