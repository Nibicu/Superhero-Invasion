using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Окно "Состав команды" — открывается кликом по фишке команды на карте.
/// Показывает героев команды (портрет, имя, класс, уровень).
/// Свои команды видно всегда, команды врага — только если на нашей базе построен Радар.
/// </summary>
public class SquadInfoWindowUI : WindowUI
{
    /// <summary>Единственное окно в сцене.</summary>
    public static SquadInfoWindowUI Instance { get; private set; }

    [Header("Состав команды")]
    [SerializeField] private TMP_Text titleText;          // "КОМАНДА 2" / "ВРАЖЕСКАЯ КОМАНДА 1"
    [SerializeField] private Transform listParent;        // Куда складывать строки героев
    [SerializeField] private BattleUnitRowUI rowTemplate; // Шаблон строки (выключен)

    private readonly List<GameObject> rows = new List<GameObject>(); // Созданные строки
    private Squad current;                                           // Какая команда показана

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        rowTemplate.gameObject.SetActive(false);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    /// <summary>Показать состав команды. Чужую — только с Радаром.</summary>
    public void Show(Squad squad)
    {
        if (squad == null) return;
        if (squad.Owner != Team.Player)
        {
            MainBase mine = MainBase.Get(Team.Player);
            if (mine == null || !mine.HasRadar)
            {
                ToastUI.Show("Чтобы видеть состав команд врага, постройте Радар");
                return;
            }
        }
        current = squad;
        Open();
    }

    /// <summary>Перерисовать список героев.</summary>
    public override void Refresh()
    {
        if (current == null) return;
        bool mine = current.Owner == Team.Player;
        titleText.text = mine
            ? $"<color=#7FB8FF>КОМАНДА {current.Number}</color>"
            : $"<color=#FF7A7A>ВРАЖЕСКАЯ КОМАНДА {current.Number}</color>";

        foreach (GameObject g in rows) Destroy(g);
        rows.Clear();
        foreach (HeroInstance h in current.AllHeroes)
        {
            BattleUnitRowUI row = Instantiate(rowTemplate, listParent);
            row.gameObject.SetActive(true);
            string captain = h == current.Captain ? "Капитан  •  " : "";
            row.Setup(h.Data, $"{captain}{UnitClasses.Name(h.Data.unitClass)}  •  Ур. {h.Level}");
            rows.Add(row.gameObject);
        }
    }
}
