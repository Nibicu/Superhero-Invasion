using UnityEngine;

/// <summary>
/// Настройки портала (событие "Портал"): название, описание, охрана по территориям,
/// урон базам и награда тому, кто закроет.
/// Создать: ПКМ в Project → Create → Супергеройское бюро → Портал,
/// затем указать файл у объекта Portal в сцене.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Портал", fileName = "Portal_")]
public class PortalData : ScriptableObject
{
    [Header("Описание")]
    [Tooltip("Название портала")]
    public string title = "Портал";

    [Tooltip("Что происходит")]
    [TextArea(2, 4)] public string description = "";

    [Tooltip("Цвет портала и арены")]
    public Color color = new Color(0.62f, 0.3f, 1f);

    [Tooltip("Символ в центре портала (временная графика)")]
    public string iconLetter = "Ω";

    [Header("Охрана (в 2 раза больше, чем у объектов)")]
    [Tooltip("Волны охраны — по одной на каждую территорию (обычно 3). Пройденные территории портал запоминает для каждой стороны")]
    public GuardWave[] waves = new GuardWave[0];

    [Header("Итог")]
    [Tooltip("Урон базе: не закрыли — обеим базам, закрыла одна сторона — базе другой")]
    public int baseDamage = 400;

    [Tooltip("Сколько случайных артефактов получает закрывший портал")]
    [Min(0)] public int rewardArtifacts = 1;

    /// <summary>Сколько территорий у портала.</summary>
    public int TerritoryCount => waves != null ? waves.Length : 0;
}
