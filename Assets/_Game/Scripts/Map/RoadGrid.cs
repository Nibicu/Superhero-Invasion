using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Дороги карты-картинки (изометрический город): сетка клеток, где отмечено, какая клетка — асфальт.
/// Сетка строится из картинки города (по цвету асфальта) один раз в редакторе и хранится в этом файле.
/// Команды ищут путь по клеткам дороги (A*), держась середины улицы, потом путь сглаживается.
/// Создать: строится командой в редакторе (Data/Map/RoadGrid_City).
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Сетка дорог", fileName = "RoadGrid_")]
public class RoadGrid : ScriptableObject
{
    [Tooltip("Ширина сетки в клетках")]
    public int width;
    [Tooltip("Высота сетки в клетках")]
    public int height;
    [Tooltip("Размер клетки в единицах мира")]
    public float cellSize = 0.2f;
    [Tooltip("Левый нижний угол сетки в мире")]
    public Vector2 origin;
    [Tooltip("Клетки: 1 — дорога, 0 — нет (строка за строкой снизу вверх)")]
    public byte[] cells = new byte[0];

    private int[] edgeDist;                 // Сколько клеток до края дороги (для каждой клетки дороги)
    private readonly Dictionary<int, int> nearestCache = new Dictionary<int, int>();   // Ближайшая клетка дороги
    private readonly Dictionary<long, float> lengthCache = new Dictionary<long, float>(); // Длины путей

    private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    private static readonly float[] DC = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

    /// <summary>Сетка заполнена.</summary>
    public bool IsValid => width > 0 && height > 0 && cells != null && cells.Length == width * height;

    /// <summary>Клетка (x, y) — дорога.</summary>
    public bool IsRoad(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && cells[y * width + x] != 0;

    /// <summary>Клетка, в которую попадает точка мира.</summary>
    public Vector2Int WorldToCell(Vector3 p) =>
        new Vector2Int(Mathf.FloorToInt((p.x - origin.x) / cellSize), Mathf.FloorToInt((p.y - origin.y) / cellSize));

    /// <summary>Центр клетки в мире.</summary>
    public Vector3 CellToWorld(int x, int y) =>
        new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y + (y + 0.5f) * cellSize, 0f);

    private void OnEnable()
    {
        edgeDist = null;
        nearestCache.Clear();
        lengthCache.Clear();
    }

    /// <summary>Расстояние до края дороги для каждой клетки (считается один раз).</summary>
    private void EnsureEdgeDist()
    {
        if (edgeDist != null) return;
        edgeDist = new int[width * height];
        var queue = new Queue<int>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == 0) { edgeDist[i] = 0; queue.Enqueue(i); }
            else edgeDist[i] = int.MaxValue;
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % width, y = i / width;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int j = ny * width + nx;
                if (edgeDist[j] <= edgeDist[i] + 1) continue;
                edgeDist[j] = edgeDist[i] + 1;
                queue.Enqueue(j);
            }
        }
    }

    /// <summary>Ближайшая к точке клетка дороги (индекс), или -1, если дорог нет.</summary>
    private int NearestRoadCell(Vector3 p)
    {
        Vector2Int c = WorldToCell(p);
        c.x = Mathf.Clamp(c.x, 0, width - 1);
        c.y = Mathf.Clamp(c.y, 0, height - 1);
        int start = c.y * width + c.x;
        if (nearestCache.TryGetValue(start, out int cached)) return cached;

        EnsureEdgeDist();
        // Поиск по кольцам: ближайшая клетка дороги, по возможности не у самого края
        int best = -1;
        float bestD = float.MaxValue;
        int maxR = Mathf.Max(width, height);
        for (int r = 0; r <= maxR; r++)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int x = c.x + dx, y = c.y + dy;
                    if (!IsRoad(x, y)) continue;
                    float d = dx * dx + dy * dy + (edgeDist[y * width + x] < 2 ? 4f : 0f);
                    if (d < bestD) { bestD = d; best = y * width + x; }
                }
            if (best >= 0 && r * r > bestD) break;
        }
        nearestCache[start] = best;
        return best;
    }

    /// <summary>Ближайшая к точке точка дороги.</summary>
    public Vector3 NearestRoadPoint(Vector3 p)
    {
        int i = NearestRoadCell(p);
        return i < 0 ? p : CellToWorld(i % width, i / width);
    }

    /// <summary>Случайная точка на дороге внутри прямоугольника (не у самого края дороги).</summary>
    public bool TryRandomRoadPoint(Vector2 min, Vector2 max, out Vector3 pos)
    {
        EnsureEdgeDist();
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector3 p = new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), 0f);
            Vector2Int c = WorldToCell(p);
            if (!IsRoad(c.x, c.y) || edgeDist[c.y * width + c.x] < 3) continue;
            pos = CellToWorld(c.x, c.y);
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Путь по дорогам от from до to: точки, через которые ехать (последняя — сама точка to).
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        var result = new List<Vector3>();
        int a = NearestRoadCell(from), b = NearestRoadCell(to);
        if (a < 0 || b < 0) { result.Add(to); return result; }

        List<int> cellsPath = AStar(a, b);
        if (cellsPath == null) { result.Add(CellToWorld(a % width, a / width)); result.Add(CellToWorld(b % width, b / width)); result.Add(to); return result; }

        // Сглаживание: от текущей точки — сразу к самой дальней, до которой видно по дороге
        int cur = 0;
        result.Add(CellToWorld(cellsPath[0] % width, cellsPath[0] / width));
        while (cur < cellsPath.Count - 1)
        {
            int next = cur + 1;
            for (int j = cellsPath.Count - 1; j > cur + 1; j--)
                if (ClearLine(cellsPath[cur], cellsPath[j])) { next = j; break; }
            cur = next;
            result.Add(CellToWorld(cellsPath[cur] % width, cellsPath[cur] / width));
        }
        result.Add(to);
        return result;
    }

    /// <summary>Длина пути по дорогам (запоминается — её часто спрашивает ИИ).</summary>
    public float PathLength(Vector3 from, Vector3 to)
    {
        int a = NearestRoadCell(from), b = NearestRoadCell(to);
        long key = ((long)a << 32) | (uint)b;
        if (!lengthCache.TryGetValue(key, out float roadLen))
        {
            roadLen = 0f;
            List<Vector3> path = FindPath(CellToWorld(a % width, a / width), CellToWorld(b % width, b / width));
            for (int i = 1; i < path.Count; i++) roadLen += Vector3.Distance(path[i - 1], path[i]);
            lengthCache[key] = roadLen;
        }
        return roadLen + Vector3.Distance(from, NearestRoadPoint(from)) + Vector3.Distance(NearestRoadPoint(to), to);
    }

    /// <summary>Можно ли проехать по прямой между клетками, не съезжая с середины дороги.</summary>
    private bool ClearLine(int i, int j)
    {
        int x0 = i % width, y0 = i / width, x1 = j % width, y1 = j / width;
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        for (int s = 1; s < steps; s++)
        {
            float t = (float)s / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            if (!IsRoad(x, y) || edgeDist[y * width + x] < 2) return false;
        }
        return true;
    }

    /// <summary>Поиск пути A* по клеткам дороги (у края дороги ехать "дороже" — держимся середины).</summary>
    private List<int> AStar(int start, int goal)
    {
        EnsureEdgeDist();
        int n = width * height;
        var g = new float[n];
        var came = new int[n];
        var closed = new bool[n];
        for (int i = 0; i < n; i++) { g[i] = float.MaxValue; came[i] = -1; }
        var open = new MinHeap();
        g[start] = 0f;
        open.Push(start, Heuristic(start, goal));
        while (open.Count > 0)
        {
            int cur = open.Pop();
            if (cur == goal) break;
            if (closed[cur]) continue;
            closed[cur] = true;
            int x = cur % width, y = cur / width;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (!IsRoad(nx, ny)) continue;
                int j = ny * width + nx;
                if (closed[j]) continue;
                float edge = 1f + 2.5f / Mathf.Max(1, edgeDist[j]); // у края дороже
                float ng = g[cur] + DC[k] * edge;
                if (ng >= g[j]) continue;
                g[j] = ng;
                came[j] = cur;
                open.Push(j, ng + Heuristic(j, goal));
            }
        }
        if (start != goal && came[goal] < 0) return null;
        var path = new List<int>();
        for (int c = goal; c >= 0; c = came[c]) { path.Add(c); if (c == start) break; }
        path.Reverse();
        return path;
    }

    private float Heuristic(int a, int b)
    {
        float dx = a % width - b % width, dy = a / width - b / width;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Простая очередь с приоритетом (меньший — первым).</summary>
    private class MinHeap
    {
        private readonly List<int> items = new List<int>();
        private readonly List<float> keys = new List<float>();

        public int Count => items.Count;

        public void Push(int item, float key)
        {
            items.Add(item);
            keys.Add(key);
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (keys[p] <= keys[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        public int Pop()
        {
            int top = items[0];
            int last = items.Count - 1;
            Swap(0, last);
            items.RemoveAt(last);
            keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < items.Count && keys[l] < keys[m]) m = l;
                if (r < items.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}
