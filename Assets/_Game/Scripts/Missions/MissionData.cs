using System.Text;
using UnityEngine;

/// <summary>
/// Описание миссии ("?" на карте): что случилось, сколько длится, какая нужна сила и какая награда.
/// Создать новую: ПКМ в Project → Create → Супергеройское бюро → Миссия,
/// затем добавить её в список Mission Pool у MissionManager.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Миссия", fileName = "Mission_")]
public class MissionData : ScriptableObject
{
    [Header("Описание")]
    [Tooltip("Название миссии")]
    public string title = "Миссия";

    [Tooltip("Что произошло")]
    [TextArea(2, 4)] public string description = "";

    [Tooltip("Цвет круга на карте")]
    public Color color = new Color(0.96f, 0.77f, 0.26f);

    [Header("Время")]
    [Tooltip("Сколько секунд команда выполняет миссию на месте")]
    public float duration = 20f;

    [Tooltip("Сколько секунд миссия висит на карте, если на неё никого не отправили")]
    public float lifetime = 60f;

    [Header("Сложность")]
    [Tooltip("Рекомендуемая сила команды. Если сила меньше — есть шанс провала")]
    public int requiredPower = 300;

    [Header("Награда")]
    public int rewardGold = 300;
    public int rewardPlutonium = 0;

    [Tooltip("Все герои команды получают +1 уровень")]
    public bool levelUpTeam = false;

    [Tooltip("Открывает особую постройку на базе (например, Лабораторию)")]
    public BuildingData unlockBuilding;

    [Tooltip("Миссия появляется только до первого успешного выполнения")]
    public bool oneTime = false;

    /// <summary>Шанс успеха для команды с силой power (0..1). Сила не ниже рекомендуемой — 100%.</summary>
    public float GetSuccessChance(int power)
    {
        if (requiredPower <= 0) return 1f;
        return Mathf.Clamp((float)power / requiredPower, 0.2f, 1f);
    }

    /// <summary>Текст награды для окна миссии.</summary>
    public string GetRewardText()
    {
        var sb = new StringBuilder();
        if (rewardGold > 0) sb.Append($"<color=#FFD84A>+{rewardGold} золота</color>   ");
        if (rewardPlutonium > 0) sb.Append($"<color=#C58BFF>+{rewardPlutonium} плутония</color>   ");
        if (levelUpTeam) sb.Append("<color=#6EE07A>+1 уровень героям команды</color>   ");
        if (unlockBuilding != null) sb.Append($"<color=#7FD8FF>Новая постройка: {unlockBuilding.displayName}</color>");
        return sb.ToString().Trim();
    }
}
