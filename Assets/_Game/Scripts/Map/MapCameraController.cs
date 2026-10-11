using UnityEngine;

/// <summary>
/// Камера глобальной карты: двигается стрелками на клавиатуре и не выходит за края карты.
/// Во время боя на арене не работает (камерой управляет BattleManager).
/// Висит на Main Camera. Границы карты — mapMin / mapMax (в единицах мира).
/// </summary>
public class MapCameraController : MonoBehaviour
{
    [Tooltip("Скорость движения камеры (единиц в секунду)")]
    [SerializeField] private float speed = 14f;
    [Tooltip("Левый нижний угол карты")]
    [SerializeField] private Vector2 mapMin = new Vector2(-30f, -16.5f);
    [Tooltip("Правый верхний угол карты")]
    [SerializeField] private Vector2 mapMax = new Vector2(30f, 16.5f);
    [Tooltip("Где камера стоит в начале игры (обычно — у базы игрока)")]
    [SerializeField] private Vector2 startPosition = new Vector2(-7f, 0f);

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        transform.position = Clamp(new Vector3(startPosition.x, startPosition.y, transform.position.z));
    }

    /// <summary>Стрелки двигают камеру (по реальному времени — работает и на паузе карты).</summary>
    private void LateUpdate()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.IsRunning) return;
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
        if (Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
        if (move == Vector3.zero) return;
        transform.position = Clamp(transform.position + move.normalized * speed * Time.unscaledDeltaTime);
    }

    /// <summary>Не дать камере показать то, что за краем карты.</summary>
    private Vector3 Clamp(Vector3 p)
    {
        float halfH = cam != null ? cam.orthographicSize : 10f;
        float halfW = halfH * (cam != null ? cam.aspect : 1.8f);
        float minX = mapMin.x + halfW, maxX = mapMax.x - halfW;
        float minY = mapMin.y + halfH, maxY = mapMax.y - halfH;
        p.x = minX > maxX ? (mapMin.x + mapMax.x) / 2f : Mathf.Clamp(p.x, minX, maxX);
        p.y = minY > maxY ? (mapMin.y + mapMax.y) / 2f : Mathf.Clamp(p.y, minY, maxY);
        return p;
    }
}
