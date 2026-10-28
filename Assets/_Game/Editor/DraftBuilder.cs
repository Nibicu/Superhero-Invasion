using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Сборщик окна драфта в сцене Game (Canvas/DraftWindow): колонки героев игрока и врага,
/// сетка покупки (карточка — копия карточки окна РОСТЕР), таймер, золото, кнопка "ГОТОВ".
/// Меню: Tools → Superhero → Собрать окно драфта. Повторный запуск пересоздаёт окно.
/// Заодно ставит стартовый капитал 1500 и лимит героев 6.
/// </summary>
public static class DraftBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты

    private static Sprite rounded; // Скруглённый прямоугольник
    private static Sprite square;  // Квадрат
    private static TMP_FontAsset font;   // Шрифт
    private static Material outlineFont; // Шрифт с обводкой

    /// <summary>Собрать окно драфта.</summary>
    [MenuItem("Tools/Superhero/Собрать окно драфта")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/RoundedRect.png");
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Square.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        outlineFont = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");

        Transform canvas = GameObject.Find("Canvas").transform;
        Transform old = canvas.Find("DraftWindow");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Шаблон карточки и подробная карточка героя — из окна РОСТЕР
        var roster = Object.FindAnyObjectByType<RosterWindowUI>(FindObjectsInactive.Include);
        var rso = new SerializedObject(roster);
        var srcCard = (HeroCardUI)rso.FindProperty("cardTemplate").objectReferenceValue;
        var heroInfo = (HeroInfoWindowUI)rso.FindProperty("heroInfo").objectReferenceValue;

        // Окно на весь экран
        var win = new GameObject("DraftWindow", typeof(RectTransform)).GetComponent<RectTransform>();
        win.SetParent(canvas, false);
        Stretch(win);
        var root = Img("Root", win, null, new Color(0.06f, 0.07f, 0.10f, 1f)); // сплошной фон — карту не видно
        Stretch(root.rectTransform);
        RectTransform r = root.rectTransform;

        // Верх: заголовок, таймер, золото
        Place(Text("Title", r, "ДРАФТ", 64, Color.white, TextAlignmentOptions.Center, true).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -55), new Vector2(600, 80));
        Place(Text("Subtitle", r, "Купите героев перед началом игры. Порядок слева — ваша иерархия (перетаскивайте).", 26, new Color(0.78f, 0.82f, 0.9f), TextAlignmentOptions.Center, false).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1200, 40));
        var timer = Text("Timer", r, "2:00", 56, Color.white, TextAlignmentOptions.Center, true);
        Place(timer.rectTransform, new Vector2(1f, 1f), new Vector2(-150, -60), new Vector2(220, 80));
        var gold = Text("Gold", r, "1 500 золота", 40, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center, true);
        Place(gold.rectTransform, new Vector2(0f, 1f), new Vector2(210, -60), new Vector2(380, 70));

        // Колонки игрока (слева) и врага (справа)
        RectTransform myCol = Column(r, "MyColumn", new Vector2(0f, 0.5f), new Vector2(225, -40), new Color(0.12f, 0.2f, 0.34f, 0.95f), out TMP_Text myHeader, out Transform myList);
        RectTransform enemyCol = Column(r, "EnemyColumn", new Vector2(1f, 0.5f), new Vector2(-225, -40), new Color(0.32f, 0.12f, 0.14f, 0.95f), out TMP_Text enemyHeader, out Transform enemyList);

        // Шаблон строки героя
        DraftRowUI row = MakeRow(myList, srcCard);

        // Центр: сетка покупки
        var center = Img("Shop", r, rounded, new Color(0.12f, 0.14f, 0.19f, 0.95f));
        center.type = Image.Type.Sliced;
        StretchY(center.rectTransform, 0.5f, 1000f, 150f, 120f);
        Place(Text("ShopHeader", center.rectTransform, "КОГО МОЖНО КУПИТЬ", 30, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, false).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(900, 44));
        // Прокрутка
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(center.rectTransform, false);
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(20, 20);
        scrollRt.offsetMax = new Vector2(-20, -64);
        var content = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(scrollRt, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        Vector2 cardSize = ((RectTransform)srcCard.transform).sizeDelta;
        grid.cellSize = cardSize;
        grid.spacing = new Vector2(14, 14);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(4, 4, 4, 4);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var card = Object.Instantiate(srcCard, content);
        card.name = "CardTemplate";
        card.gameObject.SetActive(false);

        // Кнопка "ГОТОВ"
        var readyImg = Img("ReadyButton", r, rounded, new Color(0.247f, 0.639f, 0.302f));
        readyImg.type = Image.Type.Sliced;
        Place(readyImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 55), new Vector2(380, 84));
        var ready = readyImg.gameObject.AddComponent<Button>();
        Stretch(Text("Text", readyImg.rectTransform, "ГОТОВ", 42, Color.white, TextAlignmentOptions.Center, false).rectTransform);

        // Скрипт
        var ui = win.gameObject.AddComponent<DraftUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("root").objectReferenceValue = root.gameObject;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("goldText").objectReferenceValue = gold;
        so.FindProperty("myHeader").objectReferenceValue = myHeader;
        so.FindProperty("enemyHeader").objectReferenceValue = enemyHeader;
        so.FindProperty("myList").objectReferenceValue = myList;
        so.FindProperty("enemyList").objectReferenceValue = enemyList;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.FindProperty("grid").objectReferenceValue = content;
        so.FindProperty("cardTemplate").objectReferenceValue = card;
        so.FindProperty("readyButton").objectReferenceValue = ready;
        so.FindProperty("heroInfo").objectReferenceValue = heroInfo;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Стартовый капитал и лимит героев
        var res = Object.FindAnyObjectByType<ResourceManager>();
        var rs = new SerializedObject(res);
        rs.FindProperty("startGold").intValue = 1500;
        rs.ApplyModifiedPropertiesWithoutUndo();
        var hm = Object.FindAnyObjectByType<HeroManager>();
        var hs = new SerializedObject(hm);
        hs.FindProperty("baseHeroLimit").intValue = 6;
        hs.FindProperty("maxHeroLimit").intValue = 20;
        hs.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Окно драфта собрано");
    }

    /// <summary>Колонка героев: фон, заголовок, вертикальный список.</summary>
    private static RectTransform Column(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Color color, out TMP_Text header, out Transform list)
    {
        var bg = Img(name, parent, rounded, color);
        bg.type = Image.Type.Sliced;
        StretchY(bg.rectTransform, anchor.x, 400f, 150f, 120f);
        bg.rectTransform.anchoredPosition = new Vector2(pos.x, bg.rectTransform.anchoredPosition.y);
        header = Text("Header", bg.rectTransform, "", 32, Color.white, TextAlignmentOptions.Center, true);
        Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(380, 90));
        var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var lrt = listGo.GetComponent<RectTransform>();
        lrt.SetParent(bg.rectTransform, false);
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(12, 12);
        lrt.offsetMax = new Vector2(-12, -105);
        var v = listGo.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = false;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        list = lrt;
        return bg.rectTransform;
    }

    /// <summary>Шаблон строки героя: номер, портрет (копия из карточки), имя, класс.</summary>
    private static DraftRowUI MakeRow(Transform list, HeroCardUI srcCard)
    {
        var bg = Img("RowTemplate", list, rounded, new Color(1f, 1f, 1f, 0.10f));
        bg.type = Image.Type.Sliced;
        bg.rectTransform.sizeDelta = new Vector2(370, 64);
        bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
        var index = Text("Index", bg.rectTransform, "1", 26, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(index.rectTransform, new Vector2(0f, 0.5f), new Vector2(22, 0), new Vector2(36, 50));

        var cso = new SerializedObject(srcCard);
        var srcPortrait = (HeroPortraitUI)cso.FindProperty("portrait").objectReferenceValue;
        var portrait = Object.Instantiate(srcPortrait, bg.rectTransform);
        portrait.name = "Portrait";
        var prt = (RectTransform)portrait.transform;
        Vector2 srcSize = ((RectTransform)srcPortrait.transform).rect.size;
        Place(prt, new Vector2(0f, 0.5f), new Vector2(76, 0), srcSize);
        float k = 54f / Mathf.Max(1f, Mathf.Max(srcSize.x, srcSize.y)); // уменьшенная копия портрета карточки
        prt.localScale = new Vector3(k, k, 1f);
        foreach (var g in portrait.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        var name = Text("Name", bg.rectTransform, "Имя", 26, Color.white, TextAlignmentOptions.Left, false);
        Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(232, 12), new Vector2(250, 32));
        var info = Text("Info", bg.rectTransform, "Класс", 20, new Color(0.78f, 0.82f, 0.9f), TextAlignmentOptions.Left, false);
        Place(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(232, -14), new Vector2(250, 28));

        var row = bg.gameObject.AddComponent<DraftRowUI>();
        var so = new SerializedObject(row);
        so.FindProperty("indexText").objectReferenceValue = index;
        so.FindProperty("portrait").objectReferenceValue = portrait;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("background").objectReferenceValue = bg;
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    // ---------- Элементы ----------

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

    /// <summary>Растянуть по высоте экрана (отступы сверху и снизу), ширина фиксированная, X — якорь по горизонтали.</summary>
    private static void StretchY(RectTransform rt, float anchorX, float width, float top, float bottom)
    {
        rt.anchorMin = new Vector2(anchorX, 0f);
        rt.anchorMax = new Vector2(anchorX, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, -(top + bottom));
        rt.anchoredPosition = new Vector2(0f, (bottom - top) / 2f);
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
