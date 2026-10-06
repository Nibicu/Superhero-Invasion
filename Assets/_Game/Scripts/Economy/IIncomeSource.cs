/// <summary>
/// "Источник дохода" — всё, что приносит ресурсы раз в 10 секунд:
/// главная база, Менеджерский отдел, захваченный Банк, Институт и т.д.
/// Любой скрипт, который реализует этот интерфейс и зарегистрирован
/// в ResourceManager, автоматически участвует в начислении дохода.
/// </summary>
public interface IIncomeSource
{
    /// <summary>Чей это источник (кому идут деньги).</summary>
    Team Owner { get; }

    /// <summary>Сколько золота даёт за одно начисление.</summary>
    int GoldIncome { get; }

    /// <summary>Сколько плутония даёт за одно начисление.</summary>
    int PlutoniumIncome { get; }

    /// <summary>Название источника (для отладки и подсказок).</summary>
    string SourceName { get; }
}
