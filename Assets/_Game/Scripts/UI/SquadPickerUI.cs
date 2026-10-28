using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// "Кого отправить?" — все окна целей (объект, миссия, событие, база врага) вызывают OpenFor.
/// Теперь команда собирается прямо под цель: OpenFor открывает окно команды (SquadWindowUI) в режиме
/// "собрать и отправить". Старый список готовых команд остаётся запасным (если окно команды не задано).
/// </summary>
public class SquadPickerUI : WindowUI
{
    [SerializeField] private TMP_Text titleText;          // "Кого отправить: Банк"
    [SerializeField] private Transform listParent;        // Куда складывать строки
    [SerializeField] private SquadPickRowUI rowTemplate;  // Шаблон строки (выключен)
    [SerializeField] private TMP_Text emptyText;          // "Нет команд..."
    [Tooltip("Окно сбора команды под цель (если задано — список готовых команд не используется)")]
    [SerializeField] private SquadWindowUI teamBuilder;

    private readonly List<SquadPickRowUI> rows = new List<SquadPickRowUI>(); // Созданные строки
    private ISquadTarget target; // Куда отправляем
    private Action onSent;       // Что сделать после отправки (например, закрыть окно объекта)

    /// <summary>Прячем шаблон.</summary>
    protected override void Awake()
    {
        base.Awake();
        rowTemplate.gameObject.SetActive(false);
    }

    /// <summary>Подписываемся на изменения команд и на нападения.</summary>
    private void Start()
    {
        SquadManager.Instance.SquadsChanged += OnSquadsChanged;
        AttackableSite.AttackChanged += OnAttackChanged;
    }

    /// <summary>Отписываемся.</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (SquadManager.Instance != null) SquadManager.Instance.SquadsChanged -= OnSquadsChanged;
        AttackableSite.AttackChanged -= OnAttackChanged;
    }

    /// <summary>
    /// На цель напали — список закрывается (окна места закрыты до конца боя),
    /// кроме случаев, когда ещё можно успеть вступить (битва за флаг) или прийти на защиту своего объекта.
    /// Нападение на любую базу запрещает отправку — перерисовываем кнопки.
    /// </summary>
    private void OnAttackChanged(AttackableSite site)
    {
        if (!IsOpen) return;
        if (site.IsUnderAttack && ReferenceEquals(site, target) && !site.CanJoin(Team.Player) && !site.CanReinforce(Team.Player)) Close();
        else Refresh();
    }

    private void OnSquadsChanged(Team team)
    {
        if (team == Team.Player && IsOpen) Refresh();
    }

    /// <summary>Открыть список команд для отправки к цели. sent — вызовется после отправки.</summary>
    public void OpenFor(ISquadTarget squadTarget, Action sent)
    {
        if (squadTarget is AttackableSite site && site.IsUnderAttack && !site.CanJoin(Team.Player) && !site.CanReinforce(Team.Player))
        {
            ToastUI.Show(site.AttackStatusText());
            return;
        }
        if (teamBuilder != null)
        {
            if (SquadManager.Instance.GetFreeHeroes(Team.Player).Count == 0)
            {
                ToastUI.Show("Все герои заняты — дождитесь, пока кто-нибудь вернётся на базу");
                return;
            }
            teamBuilder.OpenForTarget(squadTarget, sent);
            return;
        }
        target = squadTarget;
        onSent = sent;
        Open();
    }

    /// <summary>Пересоздать строки команд.</summary>
    public override void Refresh()
    {
        bool defend = target is AttackableSite ts && ts.CanReinforce(Team.Player);
        titleText.text = defend
            ? $"НА ЗАЩИТУ: {target.TargetName.ToUpper()}"
            : $"КОГО ОТПРАВИТЬ: {target.TargetName.ToUpper()}";

        foreach (SquadPickRowUI r in rows) Destroy(r.gameObject);
        rows.Clear();

        IReadOnlyList<Squad> squads = SquadManager.Instance.GetSquads(Team.Player);
        emptyText.gameObject.SetActive(squads.Count == 0);
        bool baseFight = MainBase.AnyUnderAttack(out _); // идёт бой за базу — отправлять нельзя никому

        foreach (Squad s in squads)
        {
            SquadPickRowUI row = Instantiate(rowTemplate, listParent);
            row.gameObject.SetActive(true);
            string reason = null;
            bool canSend = s.Status == SquadStatus.AtBase && !baseFight && target.CanAccept(s, out reason);
            if (s.Status != SquadStatus.AtBase) reason = "НЕ НА БАЗЕ";
            else if (baseFight) reason = "БОЙ ЗА БАЗУ";
            Squad captured = s; // копия для лямбды
            row.Setup(s, canSend, canSend ? "ОТПРАВИТЬ" : reason.ToUpper(), () => Send(captured));
            rows.Add(row);
        }
    }

    /// <summary>Отправить команду.</summary>
    private void Send(Squad squad)
    {
        if (SquadManager.Instance.SendSquad(squad, target, out string error))
        {
            ToastUI.Show($"Команда {squad.Number} выехала: {target.TargetName}");
            Close();
            onSent?.Invoke();
        }
        else
        {
            ToastUI.Show(error);
        }
    }
}
