using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Строка героя в колонке драфта: номер, портрет, имя, класс и звёзды.
/// В колонке игрока строку можно перетащить мышкой выше или ниже — так задаётся иерархия (порядок героев).
/// </summary>
public class DraftRowUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text indexText;     // Номер в иерархии
    [SerializeField] private HeroPortraitUI portrait; // Портрет
    [SerializeField] private TMP_Text nameText;      // Имя
    [SerializeField] private TMP_Text infoText;      // Класс и звёзды
    [SerializeField] private Image background;       // Фон строки

    private bool draggable;            // Можно ли перетаскивать (только свои герои)
    private Action<DraftRowUI, float> onDropped; // Отпустили: строка и высота курсора (Y на экране)
    private Action onClick;            // Клик — подробная карточка героя
    private LayoutElement layout;      // Чтобы на время перетаскивания выйти из раскладки
    private Vector3 dragOffset;        // Сдвиг строки от курсора при захвате
    private Color normalColor;         // Обычный цвет фона
    private bool glow;                 // Строка светится (герой готов повысить звезду)

    /// <summary>Герой этой строки.</summary>
    public HeroInstance Hero { get; private set; }

    /// <summary>Заполнить строку.</summary>
    public void Setup(int index, HeroInstance hero, bool canDrag, Action<DraftRowUI, float> dropped, Action clicked)
    {
        Hero = hero;
        draggable = canDrag;
        onDropped = dropped;
        onClick = clicked;
        indexText.text = (index + 1).ToString();
        portrait.Show(hero.Data);
        nameText.text = hero.Data.displayName;
        infoText.text = $"{UnitClasses.Name(hero.Data.unitClass)}  •  <color=#FFD84A>{hero.StarsText}</color>";
        layout = GetComponent<LayoutElement>();
        normalColor = background.color;
    }

    /// <summary>Герой готов повысить звезду (5 уровень): строка светится, клик — карточка героя с кнопкой.</summary>
    public void MarkStarReady()
    {
        glow = true;
        infoText.text += "  •  <color=#FFE066><b>ЗВЕЗДА ГОТОВА!</b></color>";
    }

    /// <summary>Свечение строки (мягко пульсирует золотым).</summary>
    private void Update()
    {
        if (!glow) return;
        float k = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        background.color = Color.Lerp(normalColor, new Color(1f, 0.85f, 0.2f, 1f), k);
    }

    /// <summary>Отметить строку: лидер (первый в иерархии) — жёлтая ячейка и подпись "ЛИДЕР".</summary>
    public void MarkLeader()
    {
        nameText.text += "  <color=#2A1E00><b>ЛИДЕР</b></color>";
        nameText.color = new Color(0.16f, 0.12f, 0f);
        infoText.text = infoText.text.Replace("#FFD84A", "#7A4A00"); // Звёзды тёмным, иначе жёлтое на жёлтом
        infoText.color = new Color(0.25f, 0.2f, 0.05f);
        background.color = normalColor = new Color(1f, 0.82f, 0.25f, 0.95f);
    }

    /// <summary>Отметить строку: герой в запасе ("не в команде").</summary>
    public void MarkReserve()
    {
        infoText.text += "  •  <color=#FF9A6B>ЗАПАС</color>";
        background.color = normalColor = new Color(0.55f, 0.3f, 0.2f, 0.35f);
    }

    /// <summary>Начали тянуть: строка выходит из раскладки и следует за курсором.</summary>
    public void OnBeginDrag(PointerEventData e)
    {
        if (!draggable) return;
        if (layout != null) layout.ignoreLayout = true;
        transform.SetAsLastSibling();
        dragOffset = transform.position - (Vector3)e.position;
        background.color = Color.Lerp(normalColor, Color.white, 0.25f);
    }

    /// <summary>Тянем: строка двигается по вертикали за курсором.</summary>
    public void OnDrag(PointerEventData e)
    {
        if (!draggable) return;
        Vector3 p = transform.position;
        p.y = e.position.y + dragOffset.y;
        transform.position = p;
    }

    /// <summary>Отпустили: окно решает, на какое место поставить героя.</summary>
    public void OnEndDrag(PointerEventData e)
    {
        if (!draggable) return;
        if (layout != null) layout.ignoreLayout = false;
        background.color = normalColor;
        onDropped?.Invoke(this, e.position.y);
    }

    /// <summary>Клик (без перетаскивания) — подробная карточка героя.</summary>
    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging) return;
        onClick?.Invoke();
    }
}
