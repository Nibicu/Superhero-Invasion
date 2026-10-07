using TMPro;
using UnityEngine;

/// <summary>
/// Зона флага в центре арены (битва за флаг): овал на полу, флажок и полоска захвата над ним.
/// Только внешний вид — сам захват считает BattleManager.
/// Цвета: ничей — белый, наш — синий, вражеский — красный, спорный (обе стороны в зоне) — жёлтый.
/// Временная графика: позже можно заменить спрайты, скрипт не изменится.
/// </summary>
public class FlagZoneView : MonoBehaviour
{
    [Tooltip("Овал зоны на полу")]
    [SerializeField] private SpriteRenderer zone;
    [Tooltip("Полотнище флага")]
    [SerializeField] private SpriteRenderer flagCloth;
    [Tooltip("Заполнение полоски захвата (растягивается по X)")]
    [SerializeField] private Transform barFill;
    [SerializeField] private SpriteRenderer barFillRenderer;
    [Tooltip("Ширина полоски захвата")]
    [SerializeField] private float barWidth = 3f;
    [Tooltip("Надпись над флагом")]
    [SerializeField] private TMP_Text label;

    [Header("Цвета")]
    [SerializeField] private Color neutralColor = new Color(1f, 1f, 1f);
    [SerializeField] private Color playerColor = new Color(0.25f, 0.6f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color contestedColor = new Color(1f, 0.85f, 0.25f);

    /// <summary>Растянуть овал под размер зоны (полуширина и полувысота).</summary>
    public void Setup(Vector2 radius)
    {
        if (zone != null) zone.transform.localScale = new Vector3(radius.x * 2f, radius.y * 2f, 1f);
    }

    /// <summary>
    /// Показать захват: progress от −1 (враг удержал) до +1 (мы удержали),
    /// fighting — в зоне обе стороны (отсчёт стоит).
    /// </summary>
    public void SetProgress(float progress, bool fighting)
    {
        Color owner = progress > 0.001f ? playerColor : progress < -0.001f ? enemyColor : neutralColor;
        Color c = fighting ? contestedColor : owner;
        if (zone != null) zone.color = new Color(c.r, c.g, c.b, 0.3f);
        if (flagCloth != null) flagCloth.color = owner;

        float a = Mathf.Clamp01(Mathf.Abs(progress));
        if (barFill != null)
        {
            float w = barWidth * a;
            barFill.localScale = new Vector3(w, barFill.localScale.y, 1f);
            barFill.localPosition = new Vector3(-barWidth / 2f + w / 2f, barFill.localPosition.y, 0f);
        }
        if (barFillRenderer != null) barFillRenderer.color = owner;

        if (label != null)
            label.text = fighting ? "<color=#FFD84A>ФЛАГ: БОЙ!</color>"
                : progress > 0.001f ? "<color=#7FB8FF>ФЛАГ: НАШИ</color>"
                : progress < -0.001f ? "<color=#FF7A7A>ФЛАГ: ВРАГ</color>"
                : "ФЛАГ";
    }
}
