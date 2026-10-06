using System.Text;
using UnityEngine;

/// <summary>
/// Описание объекта на карте (Банк, Завод, Военная база, Институт...).
/// Создать новый: ПКМ в Project → Create → Супергеройское бюро → Объект карты.
/// Затем поставить в сцену объект с компонентом MapObject и указать ему этот файл.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Объект карты", fileName = "MapObject_")]
public class MapObjectData : ScriptableObject
{
    [Header("Внешний вид")]
    [Tooltip("Название объекта")]
    public string displayName = "Объект";

    [Tooltip("Описание в окне объекта")]
    [TextArea(2, 4)] public string description = "";

    [Tooltip("Иконка для окна объекта. Если пусто — рисуется буква")]
    public Sprite icon;

    [Tooltip("Буква/символ вместо иконки (временная графика)")]
    public string iconLetter = "?";

    [Tooltip("Основной цвет объекта")]
    public Color color = Color.gray;

    [Header("Захват")]
    [Tooltip("Сколько секунд команда захватывает объект")]
    public float captureTime = 15f;

    [Header("Охрана (гарнизон)")]
    [Tooltip("Волны охраны — по одной на каждую территорию арены (обычно 3)")]
    public GuardWave[] waves = new GuardWave[0];

    [Header("Бонусы владельцу")]
    [Tooltip("Золото за одно начисление (раз в 10 с)")]
    public int goldIncome = 0;

    [Tooltip("Плутоний за одно начисление (раз в 10 с)")]
    public int plutoniumIncome = 0;

    [Tooltip("На сколько увеличивает лимит героев")]
    public int heroLimitBonus = 0;

    [Tooltip("Разрешает улучшать доходные постройки до ур. 2 (Завод)")]
    public bool unlocksFactoryUpgrades = false;

    [Tooltip("Разрешает усиливать героев за плутоний (Институт)")]
    public bool allowsHeroBoost = false;

    [Tooltip("Дополнительный текст бонусов (то, что пока не работает в коде — например, ракеты)")]
    [TextArea(1, 3)] public string extraBonusText = "";

    /// <summary>Сколько всего охранников во всех волнах.</summary>
    public int GuardCount
    {
        get
        {
            int n = 0;
            if (waves != null)
                foreach (GuardWave w in waves)
                    foreach (GuardEntry g in w.guards)
                        if (g != null && g.unit != null) n++;
            return n;
        }
    }

    /// <summary>Краткое описание охраны: "3 территории, 5 бойцов, сила 480".</summary>
    public string GetGuardText() =>
        $"{(waves != null ? waves.Length : 0)} территории, {GuardCount} бойцов, сила {BattleCalculator.GarrisonPower(this)}";

    /// <summary>Собрать текст всех бонусов для окна объекта.</summary>
    public string GetBonusText()
    {
        var sb = new StringBuilder();
        if (goldIncome > 0) sb.AppendLine($"<color=#FFD84A>+{goldIncome} золота</color> каждые 10 с");
        if (plutoniumIncome > 0) sb.AppendLine($"<color=#C58BFF>+{plutoniumIncome} плутония</color> каждые 10 с");
        if (heroLimitBonus > 0) sb.AppendLine($"Лимит героев <color=#6EE07A>+{heroLimitBonus}</color>");
        if (unlocksFactoryUpgrades) sb.AppendLine("Доходные постройки можно улучшать до ур. 2");
        if (allowsHeroBoost) sb.AppendLine("Усиление героев за плутоний (в карточке героя)");
        if (!string.IsNullOrEmpty(extraBonusText)) sb.AppendLine(extraBonusText);
        return sb.ToString().TrimEnd();
    }
}
