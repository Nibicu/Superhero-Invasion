using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Лечебное место на арене от скила (цветок Дерева, волшебная пыль Мотылька).
/// Раненые союзники стороны Faction сами идут к нему (FighterAI), лечение — в наследнике.
/// Все такие места — в списке All (появились — добавились, исчезли — убрались).
/// </summary>
public abstract class HealSpot : MonoBehaviour
{
    /// <summary>Все лечебные места на арене сейчас.</summary>
    public static readonly List<HealSpot> All = new List<HealSpot>();

    /// <summary>Чьи бойцы могут здесь лечиться.</summary>
    public BattleFaction Faction { get; protected set; }

    /// <summary>Где место (на земле).</summary>
    public Vector2 Position => transform.position;

    /// <summary>Может ли боец f здесь вылечиться (свой, ранен, ещё не лечился здесь).</summary>
    public abstract bool CanHeal(Fighter f);

    protected virtual void OnEnable() => All.Add(this);

    protected virtual void OnDisable() => All.Remove(this);
}
