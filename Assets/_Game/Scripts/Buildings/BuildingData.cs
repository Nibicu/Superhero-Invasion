using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Вид постройки — нужен, чтобы код понимал, что делает здание
/// (например, Бараки разрешают нанимать 2-3 звезды).
/// </summary>
public enum BuildingType
{
    Other,        // Без особого эффекта (только доход/описание)
    Management,   // Менеджерский отдел — приносит золото
    Radar,        // Радар — позволяет смотреть базу и объекты врага
    ReserveRooms, // Резервные комнаты — увеличивают лимит героев
    Barracks      // Бараки — разрешают нанимать героев 2 и 3 звёзд
}

/// <summary>
/// Параметры одного уровня постройки.
/// Уровень 1 — это сама постройка, уровни 2, 3... — улучшения.
/// </summary>
[Serializable]
public class BuildingLevel
{
    [Tooltip("Цена в золоте: для ур.1 — цена постройки, для ур.2+ — цена улучшения")]
    public int goldCost = 300;

    [Tooltip("Цена в плутонии (обычно 0)")]
    public int plutoniumCost = 0;

    [Tooltip("Золото за одно начисление (раз в 10 секунд)")]
    public int goldIncome = 0;

    [Tooltip("Плутоний за одно начисление")]
    public int plutoniumIncome = 0;

    [Tooltip("На сколько увеличивает лимит героев (Резервные комнаты)")]
    public int heroCapacityBonus = 0;

    [Tooltip("Героев скольких звёзд можно нанимать (Бараки). 0 — не влияет")]
    public int maxHeroStars = 0;

    [Tooltip("Для этого уровня нужен захваченный Завод")]
    public bool requiresFactory = false;

    [Tooltip("Дополнительный текст эффекта для окна базы")]
    [TextArea(1, 3)] public string effectText = "";
}

/// <summary>
/// Описание постройки главной базы (ScriptableObject — файл-ассет с данными).
/// Создать новую: ПКМ в Project → Create → Супергеройское бюро → Постройка.
/// Здесь задаются название, иконка, цены и бонусы по уровням.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Постройка", fileName = "Building_")]
public class BuildingData : ScriptableObject
{
    [Header("Внешний вид")]
    [Tooltip("Название, которое видит игрок")]
    public string displayName = "Постройка";

    [Tooltip("Короткое описание в меню строительства")]
    [TextArea(2, 4)] public string description = "";

    [Tooltip("Иконка постройки. Если пусто — рисуется цветная плашка с буквой")]
    public Sprite icon;

    [Tooltip("Цвет плашки (если нет иконки) и рамки карточки")]
    public Color color = Color.white;

    [Header("Логика")]
    [Tooltip("Что делает постройка (для кода)")]
    public BuildingType type = BuildingType.Other;

    [Tooltip("Можно строить с самого начала. Если выключено — нужно открыть (герой, миссия)")]
    public bool availableFromStart = true;

    [Tooltip("Можно построить несколько штук на одной базе (например, 5 Менеджерских отделов)")]
    public bool allowMultiple = false;

    [Header("Уровни (первый элемент = уровень 1)")]
    public BuildingLevel[] levels = { new BuildingLevel() };

    /// <summary>Максимальный уровень постройки.</summary>
    public int MaxLevel => levels.Length;

    /// <summary>Параметры уровня (нумерация с 1).</summary>
    public BuildingLevel GetLevel(int level) => levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

    /// <summary>
    /// Собрать текст эффектов уровня для интерфейса,
    /// например: "+200 золота" или "Лимит героев +2".
    /// </summary>
    public string GetEffectText(int level)
    {
        BuildingLevel l = GetLevel(level);
        var sb = new StringBuilder();
        if (l.goldIncome > 0) sb.AppendLine($"<color=#FFD84A>+{l.goldIncome} золота</color>");
        if (l.plutoniumIncome > 0) sb.AppendLine($"<color=#C58BFF>+{l.plutoniumIncome} плутония</color>");
        if (l.heroCapacityBonus > 0) sb.AppendLine($"Лимит героев +{l.heroCapacityBonus}");
        if (l.maxHeroStars > 0) sb.AppendLine($"Найм героев до {l.maxHeroStars} звёзд");
        if (!string.IsNullOrEmpty(l.effectText)) sb.AppendLine(l.effectText);
        return sb.ToString().TrimEnd();
    }
}
