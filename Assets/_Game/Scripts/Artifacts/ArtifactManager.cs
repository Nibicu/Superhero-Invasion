using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Артефакты обеих сторон.
/// - Инвентарь стороны — артефакты, которые ещё не использованы и не надеты.
/// - Активируемый артефакт используется один раз и исчезает:
///   +1 к лимиту найма, +20 к доходу базы (навсегда) или бесплатный +1 уровень герою.
/// - Вещь надевается в одну из 3 ячеек героя (HeroInstance.Slots), её можно снять и надеть на другого.
/// - Награды: GiveRandom(сторона) выдаёт случайный артефакт из Pool (вызывают задания, миссии, портал).
/// Отладка: клавиша I — случайный артефакт игроку.
/// </summary>
[DefaultExecutionOrder(-90)]
public class ArtifactManager : MonoBehaviour
{
    /// <summary>Единственный экземпляр.</summary>
    public static ArtifactManager Instance { get; private set; }

    [Tooltip("Все артефакты, которые могут выпадать в наградах")]
    [SerializeField] private ArtifactData[] pool;
    [Tooltip("Чит для проверки: I = случайный артефакт игроку")]
    [SerializeField] private bool debugCheats = true;

    private readonly List<ArtifactData> playerStash = new List<ArtifactData>(); // Инвентарь игрока
    private readonly List<ArtifactData> enemyStash = new List<ArtifactData>();  // Инвентарь врага
    private int playerBonusIncome; // Сколько золота к доходу базы дали артефакты игрока
    private int enemyBonusIncome;  // То же у врага

    /// <summary>Инвентарь или ячейки героев стороны изменились (для окон).</summary>
    public event Action<Team> Changed;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (debugCheats && Input.GetKeyDown(KeyCode.I))
        {
            ArtifactData a = GiveRandom(Team.Player);
            if (a != null) ToastUI.Show($"Получен артефакт: {a.displayName}");
        }
    }

    // ---------- Инвентарь ----------

    /// <summary>Инвентарь стороны (неиспользованные и неснятые артефакты).</summary>
    public IReadOnlyList<ArtifactData> GetStash(Team team) => Stash(team);

    private List<ArtifactData> Stash(Team team) => team == Team.Player ? playerStash : enemyStash;

    /// <summary>Положить артефакт в инвентарь стороны.</summary>
    public void Give(Team team, ArtifactData data)
    {
        if (data == null) return;
        Stash(team).Add(data);
        Changed?.Invoke(team);
    }

    /// <summary>Выдать стороне случайный артефакт из Pool. Вернёт его (или null, если Pool пуст).</summary>
    public ArtifactData GiveRandom(Team team)
    {
        if (pool == null || pool.Length == 0) return null;
        ArtifactData a = pool[UnityEngine.Random.Range(0, pool.Length)];
        Give(team, a);
        return a;
    }

    /// <summary>Сколько золота артефакты добавили к доходу базы стороны.</summary>
    public int GetBonusIncome(Team team) => team == Team.Player ? playerBonusIncome : enemyBonusIncome;

    // ---------- Активируемые ----------

    /// <summary>
    /// Использовать активируемый артефакт из инвентаря. hero — для "+1 уровень" (иначе можно null).
    /// После использования артефакт исчезает.
    /// </summary>
    public bool TryActivate(Team team, ArtifactData data, HeroInstance hero, out string error)
    {
        error = null;
        if (data == null || data.kind != ArtifactKind.Activatable) { error = "Это не активируемый артефакт"; return false; }
        if (!Stash(team).Contains(data)) { error = "Артефакта нет в инвентаре"; return false; }

        switch (data.effect)
        {
            case ArtifactEffect.HeroLimit:
                HeroManager.Instance.AddExtraLimit(team, data.amount);
                break;
            case ArtifactEffect.BaseIncome:
                if (team == Team.Player) playerBonusIncome += data.amount;
                else enemyBonusIncome += data.amount;
                break;
            case ArtifactEffect.FreeHeroLevel:
                if (hero == null || hero.Owner != team) { error = "Выберите героя"; return false; }
                if (!HeroManager.Instance.FreeLevelUp(hero)) { error = "У героя уже максимальный уровень"; return false; }
                break;
            default:
                error = "Артефакт ничего не делает";
                return false;
        }
        Stash(team).Remove(data);
        Changed?.Invoke(team);
        return true;
    }

    // ---------- Вещи ----------

    /// <summary>
    /// Надеть вещь из инвентаря в ячейку героя. Если ячейка занята — старая вещь возвращается в инвентарь.
    /// Герою на задании надевать нельзя.
    /// </summary>
    public bool TryEquip(HeroInstance hero, int slot, ArtifactData data, out string error)
    {
        error = null;
        if (data == null || data.kind != ArtifactKind.Item) { error = "Надевать можно только вещи"; return false; }
        if (slot < 0 || slot >= HeroInstance.SlotCount) { error = "Нет такой ячейки"; return false; }
        if (hero.Status == HeroStatus.OnMission) { error = "Герой на задании — дождитесь возвращения"; return false; }
        List<ArtifactData> stash = Stash(hero.Owner);
        if (!stash.Contains(data)) { error = "Этой вещи нет в инвентаре"; return false; }

        stash.Remove(data);
        if (hero.Slots[slot] != null) stash.Add(hero.Slots[slot]);
        hero.Slots[slot] = data;
        NotifyChanged(hero.Owner);
        return true;
    }

    /// <summary>Надеть вещь в первую свободную ячейку героя.</summary>
    public bool TryEquipFree(HeroInstance hero, ArtifactData data, out string error)
    {
        for (int i = 0; i < HeroInstance.SlotCount; i++)
            if (hero.Slots[i] == null) return TryEquip(hero, i, data, out error);
        error = "У героя заняты все 3 ячейки — снимите что-нибудь в карточке героя";
        return false;
    }

    /// <summary>Снять вещь из ячейки героя обратно в инвентарь.</summary>
    public bool TryUnequip(HeroInstance hero, int slot, out string error)
    {
        error = null;
        if (slot < 0 || slot >= HeroInstance.SlotCount || hero.Slots[slot] == null) { error = "Ячейка пуста"; return false; }
        if (hero.Status == HeroStatus.OnMission) { error = "Герой на задании — дождитесь возвращения"; return false; }
        Stash(hero.Owner).Add(hero.Slots[slot]);
        hero.Slots[slot] = null;
        NotifyChanged(hero.Owner);
        return true;
    }

    /// <summary>Сообщить окнам (характеристики героев тоже изменились).</summary>
    private void NotifyChanged(Team team)
    {
        Changed?.Invoke(team);
        HeroManager.Instance.NotifyChanged(team);
    }
}
