using UnityEngine;

/// <summary>
/// Простой эффект скила (временная графика): спрайт, который за время жизни меняет размер и прозрачность,
/// может следовать за бойцом или двигаться (падающая пыль), потом исчезает сам.
/// Создаётся в RuntimeRoot боя — после боя убирается вместе с бойцами.
/// Пример: SkillFx.Spawn(круг, точка, цвет, размер от, размер до, 0.5 с).Fade(0.6f, 0f).
/// </summary>
public class SkillFx : MonoBehaviour
{
    private SpriteRenderer sr;      // Картинка
    private float life;             // Сколько живёт (сек)
    private float age;              // Сколько уже прожил
    private Vector3 fromScale;      // Размер в начале
    private Vector3 toScale;        // Размер в конце
    private float fromAlpha = 1f;   // Прозрачность в начале
    private float toAlpha = 1f;     // Прозрачность в конце
    private Color color;            // Цвет (без прозрачности)
    private Vector3 velocity;       // Движение (в секунду)
    private Transform follow;       // За кем следовать (null — стоит на месте)
    private Vector3 followOffset;   // Сдвиг от того, за кем следует

    /// <summary>
    /// Создать эффект: sprite в точке pos, цвет color, размер (в единицах мира) от fromSize до toSize за life секунд.
    /// sortingOffset — поверх бойцов на этой глубине (больше — выше).
    /// </summary>
    public static SkillFx Spawn(Sprite sprite, Vector3 pos, Color color, Vector2 fromSize, Vector2 toSize, float life, int sortingOffset = 40)
    {
        BattleManager bm = BattleManager.Instance;
        var go = new GameObject("SkillFx");
        go.transform.SetParent(bm != null ? bm.RuntimeRoot : null, false);
        go.transform.position = pos;
        var fx = go.AddComponent<SkillFx>();
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprite;
        fx.color = new Color(color.r, color.g, color.b, 1f);
        fx.fromAlpha = fx.toAlpha = color.a;
        float w = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
        float h = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.y) : 1f;
        fx.fromScale = new Vector3(fromSize.x / w, fromSize.y / h, 1f);
        fx.toScale = new Vector3(toSize.x / w, toSize.y / h, 1f);
        fx.life = Mathf.Max(0.05f, life);
        fx.sr.sortingOrder = (bm != null ? bm.DepthSortingOrder(pos.y) : 0) + sortingOffset;
        fx.Apply(0f);
        return fx;
    }

    /// <summary>Прозрачность от from до to за время жизни.</summary>
    public SkillFx Fade(float from, float to)
    {
        fromAlpha = from;
        toAlpha = to;
        Apply(0f);
        return this;
    }

    /// <summary>Следовать за target (со сдвигом offset).</summary>
    public SkillFx Follow(Transform target, Vector3 offset)
    {
        follow = target;
        followOffset = offset;
        return this;
    }

    /// <summary>Двигаться со скоростью v (в секунду).</summary>
    public SkillFx Move(Vector3 v)
    {
        velocity = v;
        return this;
    }

    /// <summary>Убрать эффект сразу.</summary>
    public void Kill() => Destroy(gameObject);

    private void Update()
    {
        age += Time.deltaTime;
        if (follow != null) transform.position = follow.position + followOffset;
        else transform.position += velocity * Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        Apply(age / life);
    }

    /// <summary>Размер и прозрачность на доле жизни k (0..1).</summary>
    private void Apply(float k)
    {
        transform.localScale = Vector3.Lerp(fromScale, toScale, k);
        if (sr != null) sr.color = new Color(color.r, color.g, color.b, Mathf.Lerp(fromAlpha, toAlpha, k));
    }
}
