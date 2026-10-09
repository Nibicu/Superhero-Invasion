using System.Text;
using UnityEngine;

/// <summary>Вид артефакта.</summary>
public enum ArtifactKind
{
    Activatable, // Активируемый: используется один раз из инвентаря и исчезает
    Item         // Вещь: надевается на героя (3 ячейки), даёт прибавку к характеристикам
}

/// <summary>Что делает активируемый артефакт.</summary>
public enum ArtifactEffect
{
    None,          // Ничего (для вещей)
    HeroLimit,     // +amount к лимиту найма героев (навсегда)
    BaseIncome,    // +amount золота к доходу главной базы (навсегда)
    FreeHeroLevel  // Бесплатно +1 уровень выбранному герою (без Лаборатории)
}

/// <summary>
/// Описание артефакта (ScriptableObject — файл-ассет).
/// Создать новый: ПКМ в Project → Create → Супергеройское бюро → Артефакт,
/// затем добавить его в список Pool у ArtifactManager (объект Managers),
/// чтобы он мог выпадать в наградах.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Артефакт", fileName = "Artifact_")]
public class ArtifactData : ScriptableObject
{
    [Header("Внешний вид")]
    [Tooltip("Название артефакта")]
    public string displayName = "Артефакт";

    [Tooltip("Описание для инвентаря")]
    [TextArea(2, 4)] public string description = "";

    [Tooltip("Иконка. Если пусто — рисуется цветной квадрат с буквой")]
    public Sprite icon;

    [Tooltip("Буква/символ вместо иконки (временная графика)")]
    public string iconLetter = "?";

    [Tooltip("Цвет квадрата-иконки")]
    public Color color = new Color(0.6f, 0.5f, 0.9f);

    [Header("Действие")]
    [Tooltip("Активируемый (одноразовый) или вещь (надевается на героя)")]
    public ArtifactKind kind = ArtifactKind.Item;

    [Tooltip("Что делает активируемый артефакт (для вещей — None)")]
    public ArtifactEffect effect = ArtifactEffect.None;

    [Tooltip("Сила эффекта: на сколько больше лимит найма / доход базы")]
    public int amount = 1;

    [Tooltip("Прибавка к характеристикам героя, на которого надета вещь")]
    public HeroStats bonus;

    /// <summary>Короткий текст эффекта: "+50 Атака", "+1 к лимиту найма" и т.п.</summary>
    public string EffectText()
    {
        if (kind == ArtifactKind.Activatable)
        {
            switch (effect)
            {
                case ArtifactEffect.HeroLimit: return $"+{amount} к лимиту найма героев (навсегда)";
                case ArtifactEffect.BaseIncome: return $"+{amount} золота к доходу базы (навсегда)";
                case ArtifactEffect.FreeHeroLevel: return "+1 уровень выбранному герою (без Лаборатории)";
                default: return "";
            }
        }
        var sb = new StringBuilder();
        int[] v = bonus.ToArray();
        for (int i = 0; i < v.Length; i++)
            if (v[i] != 0) sb.Append($"{(v[i] > 0 ? "+" : "")}{v[i]} {HeroStats.Names[i]}  ");
        return sb.ToString().Trim();
    }
}
