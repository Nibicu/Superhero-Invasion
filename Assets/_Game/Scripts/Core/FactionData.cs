using UnityEngine;

/// <summary>
/// Фракция (ScriptableObject): за героев — Трон и Стражи, за злодеев — Разрушители и Тайный порядок.
/// Все фракции одной стороны нанимают всех героев своей стороны; различаться будут задачами,
/// бонусами и некоторыми зданиями (пока у всех одинаково).
/// 3-звёздочного героя (Faction Hero) нанимает только своя фракция, и только после 5 иконок фракции (FactionManager).
/// Создать: ПКМ → Create → Супергеройское бюро → Фракция.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Фракция", fileName = "Faction_")]
public class FactionData : ScriptableObject
{
    [Tooltip("Название фракции")]
    public string displayName = "Фракция";

    [Tooltip("Сторона конфликта, к которой относится фракция")]
    public Side side = Side.Heroes;

    [Tooltip("Цвет фракции (кнопки и подписи)")]
    public Color color = Color.gray;

    [Tooltip("Коротко о фракции (для экрана выбора)")]
    [TextArea(2, 5)] public string description = "";

    [Tooltip("3-звёздочный герой фракции: нанять его можно, только когда открыты все 5 иконок фракции")]
    public HeroData factionHero;
}
