using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Шаг 4 (сцена Game): фазы игры (PhaseManager), окно тактической паузы, задания фракции
/// (Data/GlobalMissions/Faction_*), разовые награды объектов, убирает кнопку РОСТЕР с экрана
/// (героев покупают только в паузе). Меню: Tools → Superhero → Собрать фазы и паузу.
/// Повторный запуск пересоздаёт окно паузы; данные заданий не перезаписываются.
/// </summary>
public static class PhaseBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты
    private const float PPU = 38f;                                          // Пикселей картинки города на единицу мира

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

        // 2) Задания фракции
        GlobalMissionData plutonium = FactionMission("Faction_Plutonium", "GlobalMission_Reactor",
            "Плутоний института", "Добыть плутоний в институте физики", "Захватить институт физики и украсть плутоний",
            "Запасы плутония в Институте ядерной физики под угрозой.", new Color(0.55f, 0.85f, 0.3f), W(1110, 410), 200, 60, false);
        GlobalMissionData capsule = FactionMission("Faction_Capsule", "GlobalMission_Robots",
            "Капсула энергии", "Защита капсулы энергии", "Захватить капсулу энергии",
            "В парке нашли капсулу энергии — внутри спит герой. Кто победит, тому он и достанется.", new Color(0.3f, 0.85f, 0.95f), W(350, 395), 0, 0, true);
        var gmm = Object.FindAnyObjectByType<GlobalMissionManager>();
        var gso = new SerializedObject(gmm);
        var pool = gso.FindProperty("pool");
        pool.arraySize = 2;
        pool.GetArrayElementAtIndex(0).objectReferenceValue = plutonium;
        pool.GetArrayElementAtIndex(1).objectReferenceValue = capsule;
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

        // 5) Окно тактической паузы
        Transform old = canvas.Find("TacticalPause");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var roster = Object.FindAnyObjectByType<RosterWindowUI>(FindObjectsInactive.Include);
        var heroInfo = (HeroInfoWindowUI)new SerializedObject(roster).FindProperty("heroInfo").objectReferenceValue;
        var baseWindow = Object.FindAnyObjectByType<BaseWindowUI>(FindObjectsInactive.Include);
        var draft = Object.FindAnyObjectByType<DraftUI>(FindObjectsInactive.Include);
        var srcRow = (DraftRowUI)new SerializedObject(draft).FindProperty("rowTemplate").objectReferenceValue;

        var win = new GameObject("TacticalPause", typeof(RectTransform)).GetComponent<RectTransform>();
        win.SetParent(canvas, false);
        Stretch(win);
        var root = Img("Root", win, null, new Color(0.04f, 0.06f, 0.10f, 0.97f));
        Stretch(root.rectTransform);
        RectTransform r = root.rectTransform;

        Place(Text("Title", r, "ТАКТИЧЕСКАЯ ПАУЗА", 60, Color.white, TextAlignmentOptions.Center, true).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(900, 76));
        var next = Text("Next", r, "Дальше: …", 28, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, false);
        Place(next.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -105), new Vector2(1200, 40));
        var timer = Text("Timer", r, "2:00", 56, Color.white, TextAlignmentOptions.Center, true);
        Place(timer.rectTransform, new Vector2(1f, 1f), new Vector2(-150, -60), new Vector2(220, 80));

        // Слева — герои
        var heroCol = Column(r, "Heroes", 0f, 225f, 420f, new Color(0.12f, 0.2f, 0.34f, 0.95f), out TMP_Text heroesHeader);
        var heroList = List(heroCol, 105f);
        var row = Object.Instantiate(srcRow, heroList);
        row.name = "RowTemplate";
        row.gameObject.SetActive(false);

        // В центре — база
        var baseCol = Column(r, "Base", 0.5f, -150f, 560f, new Color(0.12f, 0.14f, 0.19f, 0.95f), out TMP_Text baseHeader);
        baseHeader.text = "БАЗА";
        var baseText = Text("BaseText", baseCol, "", 26, Color.white, TextAlignmentOptions.Top, false);
        Place(baseText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(520, 130));
        Button buy = Btn("BuyButton", baseCol, "КУПИТЬ ГЕРОЕВ", new Color(0.91f, 0.525f, 0.165f), new Vector2(0, -290), 40);
        Button inventory = Btn("InventoryButton", baseCol, "ИНВЕНТАРЬ", new Color(0.55f, 0.33f, 0.82f), new Vector2(0, -385), 34);
        Button baseBtn = Btn("BaseButton", baseCol, "БАЗА И ПОСТРОЙКИ", new Color(0.184f, 0.482f, 0.878f), new Vector2(0, -480), 34);

        // Справа — задания фракции
        var missionCol = Column(r, "Missions", 1f, -320f, 560f, new Color(0.3f, 0.16f, 0.36f, 0.95f), out TMP_Text missionsHeader);
        var missionsStatus = Text("Status", missionCol, "", 22, Color.white, TextAlignmentOptions.Center, false);
        Place(missionsStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(520, 90));
        var missionList = List(missionCol, 205f);
        missionList.GetComponent<VerticalLayoutGroup>().spacing = 12;
        FactionMissionCardUI card = MakeCard(missionList);

        // Внизу — "ГОТОВ"
        var readyImg = Img("ReadyButton", r, rounded, new Color(0.247f, 0.639f, 0.302f));
        readyImg.type = Image.Type.Sliced;
        Place(readyImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 55), new Vector2(380, 84));
        var ready = readyImg.gameObject.AddComponent<Button>();
        Stretch(Text("Text", readyImg.rectTransform, "ГОТОВ", 42, Color.white, TextAlignmentOptions.Center, false).rectTransform);

        var ui = win.gameObject.AddComponent<TacticalPauseUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("root").objectReferenceValue = root.gameObject;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("nextText").objectReferenceValue = next;
        so.FindProperty("readyButton").objectReferenceValue = ready;
        so.FindProperty("heroesHeader").objectReferenceValue = heroesHeader;
        so.FindProperty("heroList").objectReferenceValue = heroList;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.FindProperty("baseText").objectReferenceValue = baseText;
        so.FindProperty("buyButton").objectReferenceValue = buy;
        so.FindProperty("inventoryButton").objectReferenceValue = inventory;
        so.FindProperty("baseButton").objectReferenceValue = baseBtn;
        so.FindProperty("missionsHeader").objectReferenceValue = missionsHeader;
        so.FindProperty("missionsStatus").objectReferenceValue = missionsStatus;
        so.FindProperty("missionList").objectReferenceValue = missionList;
        so.FindProperty("cardTemplate").objectReferenceValue = card;
        so.FindProperty("roster").objectReferenceValue = roster;
        so.FindProperty("heroInfo").objectReferenceValue = heroInfo;
        so.FindProperty("baseWindow").objectReferenceValue = baseWindow;
        so.ApplyModifiedPropertiesWithoutUndo();
        // Окно паузы — под окнами драфта и выбора защитников, но над остальным интерфейсом
        win.SetSiblingIndex(canvas.Find("DraftWindow") != null ? canvas.Find("DraftWindow").GetSiblingIndex() : canvas.childCount - 1);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Шаг 4: фазы и пауза собраны");
    }

    /// <summary>Пиксель картинки города → точка мира.</summary>
    private static Vector2 W(float px, float py) => new Vector2((px - 806.5f) / PPU, (361.5f - py) / PPU);

    /// <summary>Создать задание фракции (если его ещё нет): охрана — из готовой общей миссии.</summary>
    private static GlobalMissionData FactionMission(string file, string guardsFrom, string title, string heroes, string villains,
        string description, Color color, Vector2 pos, int gold, int plutonium, bool hero)
    {
        string path = $"Assets/_Game/Data/GlobalMissions/{file}.asset";
        var d = AssetDatabase.LoadAssetAtPath<GlobalMissionData>(path);
        if (d != null) return d;
        var src = AssetDatabase.LoadAssetAtPath<GlobalMissionData>($"Assets/_Game/Data/GlobalMissions/{guardsFrom}.asset");
        d = ScriptableObject.CreateInstance<GlobalMissionData>();
        d.title = title;
        d.heroesTitle = heroes;
        d.villainsTitle = villains;
        d.description = description;
        d.color = color;
        d.iconLetter = "!";
        d.mapPosition = pos;
        d.waves = src != null ? src.waves : new GuardWave[0];
        d.rewardGold = gold;
        d.rewardPlutonium = plutonium;
        d.rewardArtifacts = 0;
        d.rewardHero = hero;
        d.rewardHeroMaxStars = 2;
        AssetDatabase.CreateAsset(d, path);
        return d;
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

    /// <summary>Шаблон карточки задания фракции.</summary>
    private static FactionMissionCardUI MakeCard(Transform list)
    {
        var bg = Img("CardTemplate", list, rounded, new Color(0.2f, 0.2f, 0.25f, 0.95f));
        bg.type = Image.Type.Sliced;
        bg.rectTransform.sizeDelta = new Vector2(530, 190);
        bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 190;
        var title = Text("Title", bg.rectTransform, "Задание", 28, Color.white, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(510, 44));
        var info = Text("Info", bg.rectTransform, "", 20, new Color(0.9f, 0.92f, 0.96f), TextAlignmentOptions.Center, false);
        Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -85), new Vector2(510, 64));
        var btnImg = Img("Button", bg.rectTransform, rounded, new Color(0.247f, 0.639f, 0.302f));
        btnImg.type = Image.Type.Sliced;
        Place(btnImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 34), new Vector2(300, 52));
        var button = btnImg.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.8f);
        button.colors = colors;
        var btnText = Text("Text", btnImg.rectTransform, "НАЧАТЬ", 26, Color.white, TextAlignmentOptions.Center, false);
        Stretch(btnText.rectTransform);
        var card = bg.gameObject.AddComponent<FactionMissionCardUI>();
        var so = new SerializedObject(card);
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("buttonText").objectReferenceValue = btnText;
        so.ApplyModifiedPropertiesWithoutUndo();
        bg.gameObject.SetActive(false);
        return card;
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
