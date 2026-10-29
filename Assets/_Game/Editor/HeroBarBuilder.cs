using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Шаг 3 (сцена Game): ряд героев внизу вместо панели команд, окно выбора защитников базы,
/// кнопка "ОТСТУПИТЬ" в окне состава команды, убирает кнопку "МОИ ГЕРОИ",
/// окно отправки ("Кого отправить?") теперь собирает команду под цель.
/// Меню: Tools → Superhero → Собрать ряд героев. Повторный запуск пересоздаёт новые части.
/// </summary>
public static class HeroBarBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты

    private static Sprite rounded; // Скруглённый прямоугольник
    private static Sprite square;  // Квадрат
    private static Sprite circle;  // Круг
    private static TMP_FontAsset font;   // Шрифт
    private static Material outlineFont; // Шрифт с обводкой

    /// <summary>Собрать всё для шага 3.</summary>
    [MenuItem("Tools/Superhero/Собрать ряд героев")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/RoundedRect.png");
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Square.png");
        circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Circle.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        outlineFont = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");

        Transform canvas = GameObject.Find("Canvas").transform;
        foreach (string n in new[] { "HeroBar", "DefenderPicker" })
        {
            Transform old = canvas.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var roster = Object.FindAnyObjectByType<RosterWindowUI>(FindObjectsInactive.Include);
        var rso = new SerializedObject(roster);
        var srcCard = (HeroCardUI)rso.FindProperty("cardTemplate").objectReferenceValue;
        var heroInfo = (HeroInfoWindowUI)rso.FindProperty("heroInfo").objectReferenceValue;
        var srcPortrait = (HeroPortraitUI)new SerializedObject(srcCard).FindProperty("portrait").objectReferenceValue;

        // 1) Старая панель команд и кнопка "МОИ ГЕРОИ" — убрать
        Transform squadBar = canvas.Find("SquadBar");
        if (squadBar != null) squadBar.gameObject.SetActive(false);
        Transform menu = canvas.Find("HeroMenu");
        if (menu != null)
        {
            Transform my = menu.Find("MyHeroesButton");
            if (my != null) my.gameObject.SetActive(false);
            Transform rosterBtn = menu.Find("RosterButton");
            Transform inv = menu.Find("InventoryButton");
            if (rosterBtn != null) ((RectTransform)rosterBtn).anchoredPosition = new Vector2(0, 0);
            if (inv != null) ((RectTransform)inv).anchoredPosition = new Vector2(0, 100);
        }

        // 2) Ряд героев внизу по центру
        var bar = new GameObject("HeroBar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(canvas, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(40, 14);
        bar.sizeDelta = new Vector2(1180, 150);
        bar.SetSiblingIndex(squadBar != null ? squadBar.GetSiblingIndex() : 0);
        var barBg = Img("Background", bar, rounded, new Color(0.05f, 0.07f, 0.11f, 0.95f));
        barBg.type = Image.Type.Sliced;
        Stretch(barBg.rectTransform);
        barBg.raycastTarget = false;
        var counter = Text("Counter", bar, "ГЕРОИ  0 / 6", 24, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Left, true);
        Place(counter.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(280, 30));
        var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(bar, false);
        row.anchorMin = new Vector2(0, 0);
        row.anchorMax = new Vector2(1, 1);
        row.offsetMin = new Vector2(16, 8);
        row.offsetMax = new Vector2(-16, -34);
        HeroAvatarUI avatar = MakeAvatar(row, srcPortrait, "AvatarTemplate");
        var heroBar = bar.gameObject.AddComponent<HeroBarUI>();
        var hb = new SerializedObject(heroBar);
        hb.FindProperty("row").objectReferenceValue = row;
        hb.FindProperty("template").objectReferenceValue = avatar;
        hb.FindProperty("counterText").objectReferenceValue = counter;
        hb.FindProperty("heroInfo").objectReferenceValue = heroInfo;
        hb.FindProperty("frame").objectReferenceValue = bar;
        hb.ApplyModifiedPropertiesWithoutUndo();

        // 3) Окно выбора защитников базы
        var picker = new GameObject("DefenderPicker", typeof(RectTransform)).GetComponent<RectTransform>();
        picker.SetParent(canvas, false);
        Stretch(picker);
        var root = Img("Root", picker, null, new Color(0f, 0f, 0f, 0.65f));
        Stretch(root.rectTransform);
        var panel = Img("Panel", root.rectTransform, rounded, new Color(0.12f, 0.14f, 0.19f, 0.98f));
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1000, 470)); // выше всплывающих сообщений
        var header = Img("Header", panel.rectTransform, rounded, new Color(0.72f, 0.22f, 0.23f));
        header.type = Image.Type.Sliced;
        Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -45), new Vector2(1000, 90));
        Stretch(Text("Title", header.rectTransform, "НАПАДЕНИЕ НА БАЗУ!", 44, Color.white, TextAlignmentOptions.Center, true).rectTransform);
        Place(Text("Hint", panel.rectTransform, "Выберите до 4 героев, которые будут защищать базу. Игра на паузе.", 24, new Color(0.85f, 0.88f, 0.94f), TextAlignmentOptions.Center, false).rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0, -125), new Vector2(940, 40));
        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        grid.SetParent(panel.rectTransform, false);
        Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(940, 220));
        var gl = grid.GetComponent<GridLayoutGroup>();
        gl.cellSize = ((RectTransform)avatar.transform).sizeDelta;
        gl.spacing = new Vector2(14, 14);
        gl.childAlignment = TextAnchor.MiddleCenter;
        HeroAvatarUI pickAvatar = MakeAvatar(grid, srcPortrait, "AvatarTemplate");
        var count = Text("Count", panel.rectTransform, "Выбрано: 0 / 4", 30, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(-220, 55), new Vector2(380, 50));
        var confirmImg = Img("ConfirmButton", panel.rectTransform, rounded, new Color(0.247f, 0.639f, 0.302f));
        confirmImg.type = Image.Type.Sliced;
        Place(confirmImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(220, 55), new Vector2(340, 76));
        var confirm = confirmImg.gameObject.AddComponent<Button>();
        Stretch(Text("Text", confirmImg.rectTransform, "К БОЮ!", 38, Color.white, TextAlignmentOptions.Center, false).rectTransform);
        var dp = picker.gameObject.AddComponent<DefenderPickerUI>();
        var dso = new SerializedObject(dp);
        dso.FindProperty("root").objectReferenceValue = root.gameObject;
        dso.FindProperty("countText").objectReferenceValue = count;
        dso.FindProperty("listParent").objectReferenceValue = grid;
        dso.FindProperty("template").objectReferenceValue = pickAvatar;
        dso.FindProperty("confirmButton").objectReferenceValue = confirm;
        dso.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);

        // 4) Окно состава команды: кнопка "ОТСТУПИТЬ"
        var info = Object.FindAnyObjectByType<SquadInfoWindowUI>(FindObjectsInactive.Include);
        Transform infoPanel = info.transform.Find("Root/Panel");
        Transform oldBtn = infoPanel.Find("RetreatButton");
        if (oldBtn != null) Object.DestroyImmediate(oldBtn.gameObject);
        ((RectTransform)infoPanel).sizeDelta = new Vector2(640, 700);
        foreach (string n in new[] { "Header", "Title" })
        {
            var t = (RectTransform)infoPanel.Find(n);
            if (t != null) t.anchoredPosition = new Vector2(0, 305);
        }
        var list = (RectTransform)infoPanel.Find("List");
        if (list != null) list.anchoredPosition = new Vector2(0, 10);
        var retreatImg = Img("RetreatButton", infoPanel, rounded, new Color(0.72f, 0.22f, 0.23f));
        retreatImg.type = Image.Type.Sliced;
        Place(retreatImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(360, 76));
        var retreat = retreatImg.gameObject.AddComponent<Button>();
        var retreatText = Text("Text", retreatImg.rectTransform, "ОТСТУПИТЬ", 32, Color.white, TextAlignmentOptions.Center, false);
        Stretch(retreatText.rectTransform);
        var iso = new SerializedObject(info);
        iso.FindProperty("retreatButton").objectReferenceValue = retreat;
        iso.FindProperty("retreatText").objectReferenceValue = retreatText;
        iso.ApplyModifiedPropertiesWithoutUndo();

        // 5) "Кого отправить?" → собрать команду под цель
        var squadPicker = Object.FindAnyObjectByType<SquadPickerUI>(FindObjectsInactive.Include);
        var squadWindow = Object.FindAnyObjectByType<SquadWindowUI>(FindObjectsInactive.Include);
        var spo = new SerializedObject(squadPicker);
        spo.FindProperty("teamBuilder").objectReferenceValue = squadWindow;
        spo.ApplyModifiedPropertiesWithoutUndo();

        // 6) Команды: капитан + 3, команд может быть много (они временные)
        var sm = Object.FindAnyObjectByType<SquadManager>();
        var smo = new SerializedObject(sm);
        smo.FindProperty("maxMembers").intValue = 3;
        smo.FindProperty("maxSquads").intValue = 20;
        smo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Шаг 3: ряд героев собран");
    }

    /// <summary>Шаблон аватарки: фон-кнопка, рамка выбора, портрет, затемнение, подпись, полоска здоровья.</summary>
    private static HeroAvatarUI MakeAvatar(Transform parent, HeroPortraitUI srcPortrait, string name)
    {
        var bg = Img(name, parent, rounded, new Color(1f, 1f, 1f, 0.08f));
        bg.type = Image.Type.Sliced;
        bg.rectTransform.sizeDelta = new Vector2(88, 104);
        var button = bg.gameObject.AddComponent<Button>();

        var sel = Img("Selection", bg.rectTransform, rounded, new Color(1f, 0.82f, 0.25f, 1f));
        sel.type = Image.Type.Sliced;
        Stretch(sel.rectTransform);
        sel.rectTransform.offsetMin = new Vector2(-4, -4);
        sel.rectTransform.offsetMax = new Vector2(4, 4);
        sel.raycastTarget = false;
        sel.transform.SetAsFirstSibling();

        var portrait = Object.Instantiate(srcPortrait, bg.rectTransform);
        portrait.name = "Portrait";
        var prt = (RectTransform)portrait.transform;
        Vector2 srcSize = ((RectTransform)srcPortrait.transform).rect.size;
        Place(prt, new Vector2(0.5f, 1f), new Vector2(0, -42), srcSize);
        float k = 76f / Mathf.Max(1f, Mathf.Max(srcSize.x, srcSize.y));
        prt.localScale = new Vector3(k, k, 1f);
        foreach (var g in portrait.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        var dim = Img("Dim", bg.rectTransform, circle, new Color(0f, 0f, 0f, 0.6f));
        Place(dim.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(76, 76));
        dim.raycastTarget = false;
        var status = Text("Status", bg.rectTransform, "В ПУТИ", 17, new Color(1f, 0.75f, 0.3f), TextAlignmentOptions.Center, true);
        Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(84, 24));

        var hpBg = Img("HpBg", bg.rectTransform, square, new Color(0.05f, 0.05f, 0.07f, 0.9f));
        Place(hpBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(72, 10));
        hpBg.raycastTarget = false;
        var hp = Img("HpFill", hpBg.rectTransform, square, new Color(0.35f, 0.85f, 0.4f));
        Stretch(hp.rectTransform);
        hp.type = Image.Type.Filled;
        hp.fillMethod = Image.FillMethod.Horizontal;
        hp.fillOrigin = 0;
        hp.raycastTarget = false;

        var a = bg.gameObject.AddComponent<HeroAvatarUI>();
        var so = new SerializedObject(a);
        so.FindProperty("portrait").objectReferenceValue = portrait;
        so.FindProperty("hpFill").objectReferenceValue = hp;
        so.FindProperty("dim").objectReferenceValue = dim;
        so.FindProperty("selection").objectReferenceValue = sel;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("button").objectReferenceValue = button;

        // Жизни: значок сердца и число в правом верхнем углу
        var heart = Img("LifeIcon", bg.rectTransform, StatIconsBuilder.Load("Hp"), Color.white);
        Place(heart.rectTransform, new Vector2(1f, 1f), new Vector2(-14, -12), new Vector2(18, 18));
        heart.raycastTarget = false;
        var lives = Text("Lives", bg.rectTransform, "3", 18, Color.white, TextAlignmentOptions.Center, true);
        Place(lives.rectTransform, new Vector2(1f, 1f), new Vector2(-14, -12), new Vector2(20, 20));
        so.FindProperty("livesText").objectReferenceValue = lives;
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
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

    /// <summary>Поставить по якорю.</summary>
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
