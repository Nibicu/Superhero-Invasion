/// <summary>
/// Рекомендация по числу героев вместо показателя силы (сила и реальный исход боя — разные вещи).
/// Пишется так: "1 и более героев", "2 и более героев", "3 и более героев", "4 героя"
/// (у злодеев — "злодеев" / "злодея").
/// </summary>
public static class Recommendation
{
    /// <summary>Больше этого героев в команде не бывает (капитан + 3).</summary>
    public const int MaxHeroes = 4;

    /// <summary>Текст рекомендации для стороны team (её герои или злодеи).</summary>
    public static string Text(int heroes, Team team)
    {
        bool villains = HeroManager.Instance != null && HeroManager.Instance.GetSide(team) == Side.Villains;
        if (heroes < 1) heroes = 1;
        if (heroes >= MaxHeroes) return villains ? $"{MaxHeroes} злодея" : $"{MaxHeroes} героя";
        return villains ? $"{heroes} и более злодеев" : $"{heroes} и более героев";
    }

    /// <summary>Строка для окон: "Рекомендуется: 3 и более героев" (белым).</summary>
    public static string Line(int heroes, Team team) =>
        $"Рекомендуется: <color=#FFFFFF>{Text(heroes, team)}</color>";

    /// <summary>Хватает ли в команде героев по рекомендации.</summary>
    public static bool Enough(int squadSize, int heroes) => squadSize >= UnityEngine.Mathf.Clamp(heroes, 1, MaxHeroes);
}
