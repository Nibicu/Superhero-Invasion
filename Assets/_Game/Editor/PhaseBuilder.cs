using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Шаг 4 (сцена Game): фазы игры (PhaseManager), окно тактической паузы (через PauseBuilder), список общих миссий
/// (все файлы Data/GlobalMissions — случайное событие фазы миссий), разовые награды объектов,
/// убирает кнопку РОСТЕР с экрана (героев покупают только в паузе). Меню: Tools → Superhero → Собрать фазы и паузу.
/// Повторный запуск пересоздаёт окно паузы; данные миссий не перезаписываются.
/// </summary>
public static class PhaseBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты

    private static Sprite rounded; // Скруглённый прямоугольник
    private static TMP_FontAsset font;   // Шрифт
    private static Material outlineFont; // Шрифт с обводкой

    /// <summary>Собрать всё для шага 4.</summary>
    [MenuItem("Tools/Superhero/Собрать фазы и паузу")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/RoundedRect.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        outlineFont = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");

        // 1) Менеджер фаз и длительность события
        var managers = GameObject.Find("Managers");
        if (managers.GetComponent<PhaseManager>() == null) managers.AddComponent<PhaseManager>();
        var em = Object.FindAnyObjectByType<EventManager>();
        var eso = new SerializedObject(em);
        eso.FindProperty("eventDuration").floatValue = 120f;
        eso.ApplyModifiedPropertiesWithoutUndo();

        // 2) Общие миссии — все файлы Data/GlobalMissions (появляются случайно в фазе миссий)
        var gmm = Object.FindAnyObjectByType<GlobalMissionManager>();
        var gso = new SerializedObject(gmm);
        var pool = gso.FindProperty("pool");
        string[] guids = AssetDatabase.FindAssets("t:GlobalMissionData", new[] { "Assets/_Game/Data/GlobalMissions" });
        pool.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GlobalMissionData>(AssetDatabase.GUIDToAssetPath(guids[i]));
        gso.ApplyModifiedPropertiesWithoutUndo();

        // 3) Разовые награды объектов (событие «Объект»)
        Reward("MapObject_MilitaryBase", 400, 0);
        Reward("MapObject_Bank", 600, 0);
        Reward("MapObject_Factory", 300, 30);
        Reward("MapObject_Institute", 200, 60);
        AssetDatabase.SaveAssets();

        // 4) Кнопка РОСТЕР с экрана убрана — покупка героев в паузе
        Transform canvas = GameObject.Find("Canvas").transform;
        Transform menu = canvas.Find("HeroMenu");
        if (menu != null)
        {
            Transform rosterBtn = menu.Find("RosterButton");
            if (rosterBtn != null) rosterBtn.gameObject.SetActive(false);
            Transform inv = menu.Find("InventoryButton");
            if (inv != null) ((RectTransform)inv).anchoredPosition = new Vector2(0, 0);
        }

        // 5) Окно тактической паузы (вкладки Фракция / Герои / База) — собирает PauseBuilder
        PauseBuilder.BuildPause(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Шаг 4: фазы и пауза собраны");
    }

    /// <summary>Разовая награда объекта.</summary>
    private static void Reward(string file, int gold, int plutonium)
    {
        var d = AssetDatabase.LoadAssetAtPath<MapObjectData>($"Assets/_Game/Data/MapObjects/{file}.asset");
        if (d == null) return;
        d.eventGold = gold;
        d.eventPlutonium = plutonium;
        EditorUtility.SetDirty(d);
    }

    // ---------- Элементы ----------

    /// <summary>Колонка: фон по высоте экрана и заголовок.</summary>
    private static RectTransform Column(RectTransform parent, string name, float anchorX, float x, float width, Color color, out TMP_Text header)
    {
        var bg = Img(name, parent, rounded, color);
        bg.type = Image.Type.Sliced;
        var rt = bg.rectTransform;
        rt.anchorMin = new Vector2(anchorX, 0f);
        rt.anchorMax = new Vector2(anchorX, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float top = 150f, bottom = 120f;
        rt.sizeDelta = new Vector2(width, -(top + bottom));
        rt.anchoredPosition = new Vector2(x, (bottom - top) / 2f);
        header = Text("Header", rt, "", 32, Color.white, TextAlignmentOptions.Center, true);
        Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(width - 20, 90));
        return rt;
    }

    /// <summary>Вертикальный список внутри колонки (сверху отступ top).</summary>
    private static RectTransform List(RectTransform col, float top)
    {
        var go = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(col, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(12, 12);
        rt.offsetMax = new Vector2(-12, -top);
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = false;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        return rt;
    }

    /// <summary>Кнопка в колонке (y — от верха колонки).</summary>
    private static Button Btn(string name, RectTransform parent, string label, Color color, Vector2 pos, float size)
    {
        var img = Img(name, parent, rounded, color);
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, new Vector2(0.5f, 1f), pos, new Vector2(460, 80));
        var b = img.gameObject.AddComponent<Button>();
        Stretch(Text("Text", img.rectTransform, label, size, Color.white, TextAlignmentOptions.Center, false).rectTransform);
        return b;
    }

    /// <summary>Картинка.</summary>
    private static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    /// <summary>Текст.</summary>
    private static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool outline)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (outline && outlineFont != null) t.fontSharedMaterial = outlineFont;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Растянуть на весь родитель.</summary>
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Поставить по якорю.</summary>
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
