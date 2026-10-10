using System.Text;
using UnityEngine;

/// <summary>
/// Описание общей миссии ("!" на карте): её видят обе стороны.
/// Чтобы выполнить, нужно пройти территории с волнами охраны (как захват объекта).
/// Если за время подготовки приедет команда соперника — будет битва за флаг.
/// Создать новую: ПКМ в Project → Create → Супергеройское бюро → Общая миссия,
/// затем добавить её в список Pool у GlobalMissionManager (объект Managers).
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Общая миссия", fileName = "GlobalMission_")]
public class GlobalMissionData : ScriptableObject
{
    [Header("Описание")]
    [Tooltip("Название общей миссии")]
    public string title = "Общая миссия";

    [Tooltip("Что произошло")]
    [TextArea(2, 4)] public string description = "";

    [Tooltip("Цвет знака на карте и арены")]
    public Color color = new Color(1f, 0.55f, 0.15f);

    [Tooltip("Буква/символ в круге на карте (временная графика)")]
    public string iconLetter = "!";

    [Header("Охрана")]
    [Tooltip("Волны охраны — по одной на каждую территорию арены (обычно 3)")]
    public GuardWave[] waves = new GuardWave[0];

    [Header("Награда победителю")]
    [Tooltip("Золото")]
    public int rewardGold = 500;

    [Tooltip("Плутоний")]
    public int rewardPlutonium = 0;

    [Tooltip("Сколько случайных артефактов получает победитель")]
    [Min(0)] public int rewardArtifacts = 1;

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

    /// <summary>Краткое описание охраны: "3 территории, 6 бойцов, сила 900".</summary>
    public string GetGuardText() =>
        $"{(waves != null ? waves.Length : 0)} территории, {GuardCount} бойцов, сила {BattleCalculator.GarrisonPower(waves)}";

    /// <summary>Текст награды (с цветами).</summary>
    public string GetRewardText()
    {
        var sb = new StringBuilder();
        if (rewardGold > 0) sb.Append($"<color=#FFD84A>+{rewardGold} золота</color>   ");
        if (rewardPlutonium > 0) sb.Append($"<color=#C58BFF>+{rewardPlutonium} плутония</color>   ");
        if (rewardArtifacts > 0) sb.Append($"<color=#FF9EE6>+{rewardArtifacts} {(rewardArtifacts == 1 ? "артефакт" : "артефакта")}</color>");
        return sb.ToString().Trim();
    }
}
