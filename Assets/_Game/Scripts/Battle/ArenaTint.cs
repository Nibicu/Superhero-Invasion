using UnityEngine;

/// <summary>
/// Деталь арены, которая перекрашивается в цвет захватываемого объекта
/// (стены и здания на заднем плане): у Банка арена золотистая, у Института — фиолетовая.
/// shade меньше 1 — темнее цвета объекта, больше 1 — светлее.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaTint : MonoBehaviour
{
    [Tooltip("Оттенок: 0.5 — вдвое темнее цвета объекта, 1 — как есть, 1.5 — заметно светлее")]
    [SerializeField] private float shade = 1f;

    /// <summary>Перекрасить в цвет объекта.</summary>
    public void Apply(Color objectColor)
    {
        var r = GetComponent<SpriteRenderer>();
        Color c = shade <= 1f
            ? Color.Lerp(Color.black, objectColor, shade)
            : Color.Lerp(objectColor, Color.white, shade - 1f);
        c.a = r.color.a;
        r.color = c;
    }
}
