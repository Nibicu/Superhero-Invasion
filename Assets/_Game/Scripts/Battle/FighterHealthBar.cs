using UnityEngine;

/// <summary>
/// Полоска здоровья над головой бойца (из спрайтов, в мире арены).
/// Обновляется только когда здоровье меняется (не каждый кадр). После смерти прячется.
/// </summary>
public class FighterHealthBar : MonoBehaviour
{
    [SerializeField] private FighterHealth health;   // Чьё здоровье
    [SerializeField] private Fighter fighter;
    [SerializeField] private Transform fill;         // Заполнение (растягивается по X)
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private float width = 1.04f;    // Полная ширина полоски
    [SerializeField] private Color playerColor = new Color(0.35f, 0.85f, 0.4f);
    [SerializeField] private Color enemyColor = new Color(0.95f, 0.3f, 0.3f);
    [SerializeField] private Color neutralColor = new Color(0.95f, 0.75f, 0.25f); // Охрана в битве за флаг

    private void OnEnable()
    {
        health.Changed += Refresh;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Changed -= Refresh;
        health.Died -= OnDied;
    }

    /// <summary>Перерисовать полоску.</summary>
    private void Refresh(FighterHealth h)
    {
        float w = width * h.Fraction;
        fill.localScale = new Vector3(w, fill.localScale.y, 1f);
        fill.localPosition = new Vector3(-width / 2f + w / 2f, fill.localPosition.y, 0f);
        if (fillRenderer != null) fillRenderer.color = fighter.NeutralLook ? neutralColor : fighter.Team == Team.Player ? playerColor : enemyColor;
    }

    private void OnDied(Fighter f) => gameObject.SetActive(false);
}
