using UnityEngine;

/// <summary>
/// Нижняя панель команд (по центру внизу экрана): 5 ячеек.
/// Созданные команды показываются аватарками капитанов,
/// следующая свободная ячейка — "+", открывает окно создания команды.
/// </summary>
public class SquadBarUI : MonoBehaviour
{
    [SerializeField] private SquadSlotUI[] slots;       // 5 ячеек слева направо
    [SerializeField] private SquadWindowUI squadWindow; // Окно команды

    /// <summary>Подписываемся на изменения команд и героев.</summary>
    private void Start()
    {
        SquadManager.Instance.SquadsChanged += OnChanged;
        HeroManager.Instance.HeroesChanged += OnChanged; // прокачка героя меняет HP команды
        Refresh();
    }

    /// <summary>Отписываемся.</summary>
    private void OnDestroy()
    {
        if (SquadManager.Instance != null) SquadManager.Instance.SquadsChanged -= OnChanged;
        if (HeroManager.Instance != null) HeroManager.Instance.HeroesChanged -= OnChanged;
    }

    private void OnChanged(Team team)
    {
        if (team == Team.Player) Refresh();
    }

    /// <summary>Перерисовать все ячейки.</summary>
    public void Refresh()
    {
        var squads = SquadManager.Instance.GetSquads(Team.Player);
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < squads.Count)
            {
                Squad s = squads[i]; // копия для лямбды
                slots[i].gameObject.SetActive(true);
                slots[i].Show(s, () => squadWindow.OpenSquad(s));
            }
            else
            {
                // Показываем только одну пустую ячейку "+" — следующую по порядку
                bool showPlus = i == squads.Count;
                slots[i].gameObject.SetActive(showPlus);
                if (showPlus) slots[i].Show(null, squadWindow.OpenNew);
            }
        }
    }
}
