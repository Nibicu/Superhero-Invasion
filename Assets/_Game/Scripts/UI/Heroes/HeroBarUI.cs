using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Ряд купленных героев внизу экрана (вместо кнопок команд): аватарки по порядку иерархии, до 20 штук.
/// Если не помещаются в ряд — аватарки немного заходят друг на друга (внахлёст).
/// Клик по аватарке — вся информация о герое (прокачка, усиление, вещи).
/// Герой на задании — аватарка затемнена.
/// </summary>
public class HeroBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform row;          // Где стоят аватарки (их ширина — место ряда)
    [SerializeField] private HeroAvatarUI template;      // Шаблон аватарки (выключен)
    [SerializeField] private TMP_Text counterText;       // "ГЕРОИ 3 / 6"
    [SerializeField] private HeroInfoWindowUI heroInfo;  // Подробная карточка героя
    [Tooltip("Расстояние между аватарками, если все помещаются")]
    [SerializeField] private float spacing = 8f;
    [Tooltip("Рамка ряда (подложка): её ширина подстраивается под число героев")]
    [SerializeField] private RectTransform frame;
    [Tooltip("Самая большая ширина рамки ряда")]
    [SerializeField] private float maxWidth = 1180f;
    [Tooltip("Самая маленькая ширина рамки ряда")]
    [SerializeField] private float minWidth = 300f;
    [Tooltip("Отступы ряда внутри рамки (слева + справа)")]
    [SerializeField] private float padding = 32f;

    private readonly List<HeroAvatarUI> avatars = new List<HeroAvatarUI>(); // Созданные аватарки
    private float stateTimer;                                               // Таймер обновления здоровья

    /// <summary>Спрятать шаблон.</summary>
    private void Awake() => template.gameObject.SetActive(false);

    /// <summary>Подписаться на изменения героев и команд.</summary>
    private void Start()
    {
        HeroManager.Instance.HeroesChanged += OnChanged;
        if (SquadManager.Instance != null) SquadManager.Instance.SquadsChanged += OnChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed += Refresh; // лимит героев меняется от построек
        Refresh();
    }

    /// <summary>Отписаться.</summary>
    private void OnDestroy()
    {
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnChanged;
        if (SquadManager.Instance != null) SquadManager.Instance.SquadsChanged -= OnChanged;
        MainBase b = MainBase.Get(Team.Player);
        if (b != null) b.Changed -= Refresh;
    }

    private void OnChanged(Team team)
    {
        if (team == Team.Player) Refresh();
    }

    /// <summary>Здоровье героев меняется понемногу (лечение) — обновляем аватарки пару раз в секунду.</summary>
    private void Update()
    {
        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer > 0f) return;
        stateTimer = 0.5f;
        foreach (HeroAvatarUI a in avatars) a.UpdateState();
    }

    /// <summary>Пересоздать аватарки и расставить их в ряд (внахлёст, если не помещаются).</summary>
    public void Refresh()
    {
        foreach (HeroAvatarUI a in avatars) Destroy(a.gameObject);
        avatars.Clear();
        HeroManager hm = HeroManager.Instance;
        IReadOnlyList<HeroInstance> heroes = hm.GetHeroes(Team.Player);
        if (counterText != null) counterText.text = $"ГЕРОИ  {heroes.Count} / {hm.GetHeroLimit(Team.Player)}";

        float size = ((RectTransform)template.transform).rect.width;
        float width = frame != null ? maxWidth - padding : row.rect.width;
        int n = heroes.Count;
        float step = size + spacing;
        if (n > 1 && step * (n - 1) + size > width) step = (width - size) / (n - 1); // внахлёст
        float total = n > 0 ? step * (n - 1) + size : 0f;
        float x = -total / 2f + size / 2f;
        if (frame != null) frame.sizeDelta = new Vector2(Mathf.Clamp(total + padding, minWidth, maxWidth), frame.sizeDelta.y); // рамка по числу героев

        for (int i = 0; i < n; i++)
        {
            HeroAvatarUI a = Instantiate(template, row);
            a.gameObject.SetActive(true);
            var rt = (RectTransform)a.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + i * step, 0f);
            HeroData data = heroes[i].Data;
            a.Setup(heroes[i], () => heroInfo.ShowHero(data));
            a.SetReserve(hm.IsReserve(heroes[i]));
            avatars.Add(a);
        }
    }
}
