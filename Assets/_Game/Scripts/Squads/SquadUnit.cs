using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Фишка команды на карте: едет от базы к цели по дорогам,
/// ждёт, пока цель (объект/миссия) закончит, и едет обратно.
/// Создаётся SquadManager'ом из префаба SquadToken, после возвращения удаляется.
/// Внешний вид фишки можно менять в префабе (Assets/_Game/Prefabs/SquadToken).
/// </summary>
public class SquadUnit : MonoBehaviour
{
    [Header("Внешний вид")]
    [SerializeField] private SpriteRenderer ring;  // Кольцо — цвет стороны
    [SerializeField] private SpriteRenderer body;  // Круг — цвет капитана
    [SerializeField] private SpriteRenderer portrait; // Портрет капитана (если есть спрайт)
    [SerializeField] private TMP_Text label;       // Инициалы капитана
    [SerializeField] private TMP_Text numberLabel; // Номер команды

    [Header("Движение")]
    [Tooltip("Базовая скорость (единиц карты в секунду). Скорость героев немного её увеличивает")]
    [SerializeField] private float baseSpeed = 2f;

    private readonly List<Vector3> path = new List<Vector3>(); // Точки маршрута
    private int pathIndex;     // К какой точке едем
    private bool moving;       // Едет ли сейчас
    private bool returning;    // Едет ли домой
    private float speed;       // Итоговая скорость
    private Vector3 home;      // Точка базы

    /// <summary>Команда, которую изображает фишка.</summary>
    public Squad Squad { get; private set; }

    /// <summary>Куда едет команда.</summary>
    public ISquadTarget Target { get; private set; }

    /// <summary>Запустить фишку: команда squad едет от базы homePos к цели target.</summary>
    public void Init(Squad squad, Vector3 homePos, ISquadTarget target, Color sideColor)
    {
        Squad = squad;
        Target = target;
        home = homePos;
        transform.position = homePos;

        speed = SpeedOf(squad);

        // Внешний вид
        HeroData cap = squad.Captain.Data;
        if (ring != null) ring.color = sideColor;
        if (body != null) body.color = cap.portrait != null ? Color.white : cap.color;
        if (portrait != null) { portrait.sprite = cap.portrait; portrait.enabled = cap.portrait != null; }
        if (label != null) { label.text = cap.Initials; label.enabled = cap.portrait == null; }
        if (numberLabel != null) numberLabel.text = squad.Number.ToString();

        BuildPath(homePos, target.ApproachPoint);
        moving = true;
        returning = false;
    }

    /// <summary>Скорость команды на карте: базовая + немного от средней скорости героев.</summary>
    public float SpeedOf(Squad squad)
    {
        int sum = 0, count = 0;
        foreach (HeroInstance h in squad.AllHeroes) { sum += h.Stats.speed; count++; }
        return baseSpeed + (count > 0 ? sum / (float)count / 100f : 0f);
    }

    /// <summary>Сколько секунд команда будет ехать от from до to по дорогам (оценка для ИИ).</summary>
    public float EstimateTravelTime(Squad squad, Vector3 from, Vector3 to)
    {
        float length = Mathf.Abs(from.y) + Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y);
        return length / Mathf.Max(0.1f, SpeedOf(squad));
    }

    /// <summary>Отправить команду обратно на базу (вызывает цель после захвата/миссии).</summary>
    public void ReturnHome()
    {
        returning = true;
        BuildPath(transform.position, home);
        moving = true;
        SquadManager.Instance.SetStatus(Squad, SquadStatus.Returning);
    }

    /// <summary>
    /// Маршрут "по дорогам": сначала до главной дороги (y = 0),
    /// по ней до нужного X, потом к точке назначения.
    /// </summary>
    private void BuildPath(Vector3 from, Vector3 to)
    {
        path.Clear();
        path.Add(new Vector3(from.x, 0f, 0f));
        path.Add(new Vector3(to.x, 0f, 0f));
        path.Add(to);
        pathIndex = 0;
    }

    /// <summary>Едем по маршруту.</summary>
    private void Update()
    {
        if (!moving) return;
        Vector3 target = path[pathIndex];
        transform.position = Vector3.MoveTowards(transform.position, target, speed * WorldTime.DeltaTime);
        if ((transform.position - target).sqrMagnitude > 0.0001f) return;

        pathIndex++;
        if (pathIndex < path.Count) return;

        moving = false;
        if (returning)
        {
            SquadManager.Instance.OnUnitReturned(this);
            Destroy(gameObject);
        }
        else
        {
            Target.OnSquadArrived(this);
        }
    }
}
