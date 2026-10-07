using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Миссия на карте — круг со знаком "?".
/// - Пока никто не едет, тикает таймер жизни; когда он кончится — миссия исчезает.
/// - Клик открывает окно миссии (MissionWindowUI).
/// - Команда приехала → идёт таймер выполнения → бросок шанса успеха →
///   награда (или ранение команды при провале) → команда едет домой, миссия исчезает.
/// Создаётся MissionManager'ом из префаба MissionMarker.
/// </summary>
public class MissionMarker : MonoBehaviour, ISquadTarget
{
    [Header("Внешний вид")]
    [SerializeField] private SpriteRenderer glow;       // Пульсирующее свечение
    [SerializeField] private SpriteRenderer circle;     // Цветной круг
    [SerializeField] private TMP_Text questionMark;     // Знак "?"
    [SerializeField] private Transform pulse;           // Что пульсирует (масштаб)
    [SerializeField] private GameObject barRoot;        // Полоска (таймер жизни или прогресс)
    [SerializeField] private SpriteRenderer barFill;    // Заполнение полоски
    [SerializeField] private float barWidth = 1.3f;     // Ширина полоски

    private MissionManager manager; // Кто создал миссию
    private float lifeLeft;         // Сколько секунд миссия ещё провисит
    private Squad assigned;         // Команда, которая едет / выполняет (null — свободна)
    private SquadUnit worker;       // Фишка команды на месте
    private float progress;         // Прогресс выполнения 0..1

    /// <summary>Данные миссии.</summary>
    public MissionData Data { get; private set; }

    /// <summary>Едет ли / работает ли здесь команда.</summary>
    public Squad AssignedSquad => assigned;

    /// <summary>Выполняется ли миссия прямо сейчас.</summary>
    public bool IsInProgress => worker != null;

    public float Progress => progress;
    public float LifeLeft => lifeLeft;

    // ISquadTarget
    public string TargetName => Data.title;

    /// <summary>Команда встаёт чуть ближе к главной дороге, чтобы не закрывать "?".</summary>
    public Vector3 ApproachPoint
    {
        get
        {
            Vector3 p = transform.position;
            p.y -= Mathf.Sign(p.y) * 1.0f;
            return p;
        }
    }

    /// <summary>Запустить миссию (вызывает MissionManager).</summary>
    public void Init(MissionData data, MissionManager owner)
    {
        Data = data;
        manager = owner;
        lifeLeft = data.lifetime;
        if (circle != null) circle.color = data.color;
        if (glow != null) glow.color = new Color(data.color.r, data.color.g, data.color.b, 0.35f);
        if (questionMark != null) questionMark.color = data.color;
        SetBar(1f, Color.white);
    }

    /// <summary>Пульсация, таймер жизни или прогресс выполнения.</summary>
    private void Update()
    {
        if (pulse != null)
            pulse.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 4f));

        if (worker != null)
        {
            progress += WorldTime.DeltaTime / Mathf.Max(0.1f, Data.duration);
            SetBar(progress, new Color(0.35f, 0.85f, 0.4f));
            if (progress >= 1f) Finish();
            return;
        }

        if (assigned != null) return; // команда в пути — миссия ждёт

        lifeLeft -= WorldTime.DeltaTime;
        SetBar(lifeLeft / Data.lifetime, Color.white);
        if (lifeLeft <= 0f) manager.RemoveMission(this, false);
    }

    /// <summary>Клик по "?" — открыть окно миссии.</summary>
    private void OnMouseUpAsButton()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (MissionWindowUI.Instance != null) MissionWindowUI.Instance.Open(this);
    }

    // ---------- ISquadTarget ----------

    /// <summary>На миссию можно отправить только одну команду.</summary>
    public bool CanAccept(Squad squad, out string reason)
    {
        reason = null;
        if (assigned != null) { reason = "Сюда уже едет команда"; return false; }
        return true;
    }

    /// <summary>Команду отправили — миссия больше не исчезает.</summary>
    public void OnSquadDispatched(Squad squad) => assigned = squad;

    /// <summary>Команда приехала — начинаем выполнение.</summary>
    public void OnSquadArrived(SquadUnit unit)
    {
        worker = unit;
        progress = 0f;
        SquadManager.Instance.SetStatus(unit.Squad, SquadStatus.OnMission);
        if (unit.Squad.Owner == Team.Player)
            ToastUI.Show($"Команда {unit.Squad.Number} приступила к миссии «{Data.title}»");
    }

    /// <summary>Команда отступила — миссия снова свободна (и снова тикает таймер жизни).</summary>
    public void OnSquadRecalled(SquadUnit unit)
    {
        if (assigned != unit.Squad) return;
        assigned = null;
        worker = null;
        progress = 0f;
        SetBar(lifeLeft / Data.lifetime, Color.white);
    }

    // ---------- Итог ----------

    /// <summary>Миссия выполнена: бросаем шанс, выдаём награду или раним команду, команда едет домой.</summary>
    private void Finish()
    {
        SquadUnit unit = worker;
        worker = null;
        Squad squad = unit.Squad;
        Team team = squad.Owner;

        bool success = Random.value <= Data.GetSuccessChance(squad.Power);
        if (success)
        {
            ResourceManager.Instance.Add(team, Data.rewardGold, Data.rewardPlutonium);
            if (Data.levelUpTeam)
                foreach (HeroInstance h in squad.AllHeroes) HeroManager.Instance.FreeLevelUp(h);
            if (Data.unlockBuilding != null)
            {
                MainBase b = MainBase.Get(team);
                if (b != null) b.UnlockBuilding(Data.unlockBuilding);
            }
            if (team == Team.Player)
            {
                string extra = Data.unlockBuilding != null ? $" Открыта постройка: {Data.unlockBuilding.displayName}!" : "";
                ToastUI.Show($"Миссия «{Data.title}» выполнена! {StripTags(Data.GetRewardText())}.{extra}");
            }
        }
        else
        {
            squad.HpFraction = Mathf.Min(squad.HpFraction, 0.4f); // команда ранена, подлечится на базе
            if (team == Team.Player)
                ToastUI.Show($"Миссия «{Data.title}» провалена! Команда {squad.Number} ранена");
        }

        unit.ReturnHome();
        manager.RemoveMission(this, success);
    }

    /// <summary>Нарисовать полоску (fill 0..1).</summary>
    private void SetBar(float fill, Color color)
    {
        if (barFill == null) return;
        fill = Mathf.Clamp01(fill);
        Transform t = barFill.transform;
        t.localScale = new Vector3(barWidth * fill, t.localScale.y, 1f);
        t.localPosition = new Vector3(-barWidth / 2f + barWidth * fill / 2f, t.localPosition.y, 0f);
        barFill.color = color;
    }

    /// <summary>Убрать теги цвета из текста (для всплывающего сообщения).</summary>
    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("   ", ", ");
}
