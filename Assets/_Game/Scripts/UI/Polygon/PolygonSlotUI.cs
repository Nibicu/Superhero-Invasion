using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ячейка команды на полигоне «Битва»: боец (портрет, имя, класс), уровень «‹ Ур. 3 ›» и кнопка «X» (убрать).
/// Пустая ячейка показывает подпись «пусто».
/// </summary>
public class PolygonSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject filled;       // Всё, что видно, когда в ячейке есть боец
    [SerializeField] private TMP_Text emptyText;      // "пусто"
    [SerializeField] private HeroPortraitUI portrait; // Портрет
    [SerializeField] private TMP_Text nameText;       // Имя
    [SerializeField] private TMP_Text infoText;       // Класс и звёзды
    [SerializeField] private TMP_Text levelText;      // "Ур. 3"
    [SerializeField] private Button levelDown;        // "‹"
    [SerializeField] private Button levelUp;          // "›"
    [SerializeField] private Button removeButton;     // "X"

    private Action<int> onLevelDelta; // Изменить уровень на +1 / −1
    private Action onRemove;          // Убрать бойца из команды

    /// <summary>Подписать кнопки (один раз).</summary>
    private void Awake()
    {
        levelDown.onClick.AddListener(() => onLevelDelta?.Invoke(-1));
        levelUp.onClick.AddListener(() => onLevelDelta?.Invoke(1));
        removeButton.onClick.AddListener(() => onRemove?.Invoke());
    }

    /// <summary>Показать бойца data уровня level (null — пустая ячейка).</summary>
    public void Show(HeroData data, int level, int maxLevel, Action<int> levelDelta, Action remove)
    {
        onLevelDelta = levelDelta;
        onRemove = remove;
        filled.SetActive(data != null);
        emptyText.gameObject.SetActive(data == null);
        if (data == null) return;
        portrait.Show(data);
        nameText.text = data.displayName;
        infoText.text = $"{UnitClasses.Name(data.unitClass)} • {data.stars} зв.";
        levelText.text = $"Ур. {level}";
        levelDown.interactable = level > 1;
        levelUp.interactable = level < maxLevel;
    }
}
