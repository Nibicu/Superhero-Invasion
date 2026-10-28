using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Сборщик сцены главного меню (Assets/_Game/Scenes/MainMenu.unity) и данных к ней:
/// фракции (Data/Factions) и карта (Data/Maps). Меню: Tools → Superhero → Собрать главное меню.
/// Можно запускать повторно — сцена собирается заново, данные не перезаписываются.
/// </summary>
public static class MainMenuBuilder
{
    private const string ScenePath = "Assets/_Game/Scenes/MainMenu.unity"; // Где лежит сцена меню
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты "Город"

    private static readonly Color Blue = new Color(0.184f, 0.482f, 0.878f);   // Синие кнопки
    private static readonly Color Green = new Color(0.247f, 0.639f, 0.302f);  // Зелёные кнопки
    private static readonly Color Red = new Color(0.851f, 0.22f, 0.227f);     // Красные кнопки
    private static readonly Color Gray = new Color(0.30f, 0.34f, 0.42f);      // Неактивные / заглушки
    private static readonly Color Purple = new Color(0.55f, 0.33f, 0.82f);    // Фиолетовые кнопки
    private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.17f, 0.94f); // Тёмная панель
    private static readonly Color Idle = new Color(0.22f, 0.25f, 0.32f);      // Невыбранная кнопка выбора

    private static Sprite rounded; // Скруглённый прямоугольник (кнопки, панели)
    private static Sprite square;  // Квадрат (фон)
    private static Sprite circle;  // Круг (ручка ползунка)
    private static TMP_FontAsset font;     // Шрифт
    private static Material outlineFont;   // Шрифт с обводкой (заголовки)

    /// <summary>Собрать сцену главного меню.</summary>
    [MenuItem("Tools/Superhero/Собрать главное меню")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // Сначала сцена, потом загрузка ассетов (иначе ссылки сохранятся пустыми)
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadAssets();
        FactionData[] factions = EnsureFactions();
        MapData[] maps = EnsureMaps();

        // Камера и EventSystem
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.transform.position = new Vector3(0, 0, -10);
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // Холст
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        // Фон: город, затемнённый
        var bg = Img("Background", root, square, Color.white);
        Stretch(bg.rectTransform);
        bg.sprite = maps.Length > 0 ? maps[0].preview : square;
        bg.color = new Color(0.55f, 0.55f, 0.6f);
        bg.preserveAspect = false;
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = bg.sprite != null ? bg.sprite.rect.width / bg.sprite.rect.height : 16f / 9f;
        var shade = Img("Shade", root, square, new Color(0.03f, 0.04f, 0.07f, 0.55f));
        Stretch(shade.rectTransform);

        // ---------- Главное меню (с заголовком — он прячется вместе с меню) ----------
        var mainPanel = new GameObject("MainPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        mainPanel.SetParent(root, false);
        Stretch(mainPanel);
        var title = Text("Title", mainPanel, "SUPERHERO INVASION", 96, Color.white, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1400, 130));
        var sub = Text("Subtitle", mainPanel, "Герои против злодеев", 40, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -235), new Vector2(1000, 60));
        float y = 90f;
        Button campaign = Btn("CampaignButton", mainPanel, "КАМПАНИЯ", Gray, new Vector2(0, y), new Vector2(560, 92), 40);
        Badge(campaign.transform, "скоро");
        Button sandbox = Btn("SandboxButton", mainPanel, "ПЕСОЧНИЦА", Green, new Vector2(0, y - 115), new Vector2(560, 92), 40);
        Button encyclopedia = Btn("EncyclopediaButton", mainPanel, "ЭНЦИКЛОПЕДИЯ", Gray, new Vector2(0, y - 230), new Vector2(560, 92), 40);
        Badge(encyclopedia.transform, "скоро");
        Button options = Btn("OptionsButton", mainPanel, "ОПЦИИ", Blue, new Vector2(0, y - 345), new Vector2(560, 92), 40);
        Button quit = Btn("QuitButton", mainPanel, "ВЫХОД", Red, new Vector2(0, y - 460), new Vector2(560, 80), 34);

        // ---------- Песочница ----------
        var sandboxPanel = Img("SandboxPanel", root, rounded, PanelColor);
        sandboxPanel.type = Image.Type.Sliced;
        Place(sandboxPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1640, 800));
        RectTransform sp = sandboxPanel.rectTransform;
        Text("Header", sp, "ПЕСОЧНИЦА", 52, Color.white, TextAlignmentOptions.Center, true).rectTransform.Also(r => Place(r, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(800, 70)));

        // Карта (слева)
        Text("MapLabel", sp, "КАРТА", 32, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Center, false).rectTransform.Also(r => Place(r, new Vector2(0f, 1f), new Vector2(340, -125), new Vector2(560, 44)));
        var previewFrame = Img("MapFrame", sp, rounded, new Color(0.05f, 0.06f, 0.09f, 1f));
        previewFrame.type = Image.Type.Sliced;
        Place(previewFrame.rectTransform, new Vector2(0f, 1f), new Vector2(340, -320), new Vector2(580, 320));
        var preview = Img("MapPreview", previewFrame.rectTransform, square, Color.white);
        Stretch(preview.rectTransform, 10f);
        preview.preserveAspect = true;
        var mapName = Text("MapName", sp, "Город", 38, Color.white, TextAlignmentOptions.Center, true);
        Place(mapName.rectTransform, new Vector2(0f, 1f), new Vector2(340, -515), new Vector2(560, 54));
        var mapInfo = Text("MapInfo", sp, "", 24, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Top, false);
        Place(mapInfo.rectTransform, new Vector2(0f, 1f), new Vector2(340, -590), new Vector2(560, 90));
        Button mapPrev = Btn("MapPrev", sp, "‹", Idle, Vector2.zero, new Vector2(60, 90), 54);
        Place((RectTransform)mapPrev.transform, new Vector2(0f, 1f), new Vector2(80, -320), new Vector2(60, 90));
        Button mapNext = Btn("MapNext", sp, "›", Idle, Vector2.zero, new Vector2(60, 90), 54);
        Place((RectTransform)mapNext.transform, new Vector2(0f, 1f), new Vector2(600, -320), new Vector2(60, 90));

        // Сторона (по центру)
        Text("SideLabel", sp, "СТОРОНА", 32, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Center, false).rectTransform.Also(r => Place(r, new Vector2(0.5f, 1f), new Vector2(40, -125), new Vector2(400, 44)));
        Button heroes = Btn("HeroesButton", sp, "ГЕРОИ", Blue, Vector2.zero, Vector2.zero, 44);
        Place((RectTransform)heroes.transform, new Vector2(0.5f, 1f), new Vector2(40, -235), new Vector2(380, 130));
        Button villains = Btn("VillainsButton", sp, "ЗЛОДЕИ", Idle, Vector2.zero, Vector2.zero, 44);
        Place((RectTransform)villains.transform, new Vector2(0.5f, 1f), new Vector2(40, -390), new Vector2(380, 130));

        // Фракция (справа)
        Text("FactionLabel", sp, "ФРАКЦИЯ", 32, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Center, false).rectTransform.Also(r => Place(r, new Vector2(1f, 1f), new Vector2(-300, -125), new Vector2(460, 44)));
        Button f1 = Btn("Faction1", sp, "ФРАКЦИЯ 1", Idle, Vector2.zero, Vector2.zero, 36);
        Place((RectTransform)f1.transform, new Vector2(1f, 1f), new Vector2(-300, -215), new Vector2(440, 92));
        Button f2 = Btn("Faction2", sp, "ФРАКЦИЯ 2", Idle, Vector2.zero, Vector2.zero, 36);
        Place((RectTransform)f2.transform, new Vector2(1f, 1f), new Vector2(-300, -325), new Vector2(440, 92));
        var factionInfo = Text("FactionInfo", sp, "", 24, new Color(0.85f, 0.87f, 0.92f), TextAlignmentOptions.Top, false);
        Place(factionInfo.rectTransform, new Vector2(1f, 1f), new Vector2(-300, -480), new Vector2(440, 170));

        // Итог и кнопки
        var summary = Text("Summary", sp, "", 28, new Color(1f, 0.88f, 0.5f), TextAlignmentOptions.Center, false);
        Place(summary.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1500, 46));
        Button sandboxBack = Btn("BackButton", sp, "НАЗАД", Gray, Vector2.zero, Vector2.zero, 34);
        Place((RectTransform)sandboxBack.transform, new Vector2(0f, 0f), new Vector2(190, 70), new Vector2(280, 84));
        Button start = Btn("StartButton", sp, "НАЧАТЬ", Green, Vector2.zero, Vector2.zero, 44);
        Place((RectTransform)start.transform, new Vector2(1f, 0f), new Vector2(-230, 70), new Vector2(360, 96));

        var sandboxUI = sandboxPanel.gameObject.AddComponent<SandboxSetupUI>();
        var sso = new SerializedObject(sandboxUI);
        SetArray(sso.FindProperty("maps"), maps);
        SetArray(sso.FindProperty("factions"), factions);
        sso.FindProperty("mapPreview").objectReferenceValue = preview;
        sso.FindProperty("mapName").objectReferenceValue = mapName;
        sso.FindProperty("mapInfo").objectReferenceValue = mapInfo;
        sso.FindProperty("mapPrev").objectReferenceValue = mapPrev;
        sso.FindProperty("mapNext").objectReferenceValue = mapNext;
        sso.FindProperty("heroesButton").objectReferenceValue = heroes;
        sso.FindProperty("villainsButton").objectReferenceValue = villains;
        SetArray(sso.FindProperty("factionButtons"), new Object[] { f1, f2 });
        sso.FindProperty("factionInfo").objectReferenceValue = factionInfo;
        sso.FindProperty("summary").objectReferenceValue = summary;
        sso.FindProperty("startButton").objectReferenceValue = start;
        sso.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Опции ----------
        var optionsPanel = Img("OptionsPanel", root, rounded, PanelColor);
        optionsPanel.type = Image.Type.Sliced;
        Place(optionsPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1060, 800));
        RectTransform op = optionsPanel.rectTransform;
        Text("Header", op, "ОПЦИИ", 52, Color.white, TextAlignmentOptions.Center, true).rectTransform.Also(r => Place(r, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(800, 70)));
        Text("GraphicsLabel", op, "ГРАФИКА", 30, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Left, false).rectTransform.Also(r => Place(r, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(900, 44)));
        OptionSelectorUI resolution = Selector(op, "Разрешение", -175);
        OptionSelectorUI screenMode = Selector(op, "Режим экрана", -245);
        OptionSelectorUI quality = Selector(op, "Качество графики", -315);
        OptionSelectorUI vsync = Selector(op, "Верт. синхронизация", -385);
        Text("SoundLabel", op, "ЗВУК", 30, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Left, false).rectTransform.Also(r => Place(r, new Vector2(0.5f, 1f), new Vector2(0, -455), new Vector2(900, 44)));
        (Slider master, TMP_Text masterVal) = VolumeRow(op, "Общая громкость", -510);
        (Slider music, TMP_Text musicVal) = VolumeRow(op, "Музыка", -575);
        (Slider effects, TMP_Text effectsVal) = VolumeRow(op, "Эффекты", -640);
        Button optionsBack = Btn("BackButton", op, "НАЗАД", Gray, Vector2.zero, Vector2.zero, 34);
        Place((RectTransform)optionsBack.transform, new Vector2(0.5f, 0f), new Vector2(0, 65), new Vector2(300, 84));

        var optionsUI = optionsPanel.gameObject.AddComponent<OptionsUI>();
        var oso = new SerializedObject(optionsUI);
        oso.FindProperty("resolution").objectReferenceValue = resolution;
        oso.FindProperty("screenMode").objectReferenceValue = screenMode;
        oso.FindProperty("quality").objectReferenceValue = quality;
        oso.FindProperty("vsync").objectReferenceValue = vsync;
        oso.FindProperty("masterVolume").objectReferenceValue = master;
        oso.FindProperty("musicVolume").objectReferenceValue = music;
        oso.FindProperty("effectsVolume").objectReferenceValue = effects;
        oso.FindProperty("masterValue").objectReferenceValue = masterVal;
        oso.FindProperty("musicValue").objectReferenceValue = musicVal;
        oso.FindProperty("effectsValue").objectReferenceValue = effectsVal;
        oso.ApplyModifiedPropertiesWithoutUndo();

        // Подсказка внизу
        var toast = Text("Toast", root, "", 34, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1000, 60));

        // Главный скрипт меню
        var menu = canvasGo.AddComponent<MainMenuUI>();
        var mso = new SerializedObject(menu);
        mso.FindProperty("mainPanel").objectReferenceValue = mainPanel.gameObject;
        mso.FindProperty("sandboxPanel").objectReferenceValue = sandboxPanel.gameObject;
        mso.FindProperty("optionsPanel").objectReferenceValue = optionsPanel.gameObject;
        mso.FindProperty("campaignButton").objectReferenceValue = campaign;
        mso.FindProperty("sandboxButton").objectReferenceValue = sandbox;
        mso.FindProperty("encyclopediaButton").objectReferenceValue = encyclopedia;
        mso.FindProperty("optionsButton").objectReferenceValue = options;
        mso.FindProperty("quitButton").objectReferenceValue = quit;
        mso.FindProperty("sandboxBack").objectReferenceValue = sandboxBack;
        mso.FindProperty("optionsBack").objectReferenceValue = optionsBack;
        mso.FindProperty("toast").objectReferenceValue = toast;
        mso.ApplyModifiedPropertiesWithoutUndo();
        sandboxPanel.gameObject.SetActive(false);
        optionsPanel.gameObject.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildScenes();
        Debug.Log("Главное меню собрано: " + ScenePath);
    }

    // ---------- Данные ----------

    /// <summary>Загрузить спрайты и шрифты интерфейса.</summary>
    private static void LoadAssets()
    {
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/RoundedRect.png");
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Square.png");
        circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Circle.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        outlineFont = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");
    }

    /// <summary>Создать 4 фракции, если их ещё нет (Data/Factions).</summary>
    private static FactionData[] EnsureFactions()
    {
        EnsureFolder("Assets/_Game/Data", "Factions");
        return new[]
        {
            Faction("Faction_Throne", "Трон", Side.Heroes, new Color(0.85f, 0.66f, 0.22f),
                "Герои на службе государства: порядок, закон и сила власти.\n<size=85%><color=#9AA4B8>Задачи, бонусы и здания фракции — скоро.</color></size>"),
            Faction("Faction_Guardians", "Стражи", Side.Heroes, new Color(0.16f, 0.66f, 0.80f),
                "Независимые защитники города: действуют быстро и без лишних приказов.\n<size=85%><color=#9AA4B8>Задачи, бонусы и здания фракции — скоро.</color></size>"),
            Faction("Faction_Destroyers", "Разрушители", Side.Villains, new Color(0.86f, 0.36f, 0.18f),
                "Банда, что держит город в страхе грубой силой и разрушениями.\n<size=85%><color=#9AA4B8>Задачи, бонусы и здания фракции — скоро.</color></size>"),
            Faction("Faction_SecretOrder", "Тайный порядок", Side.Villains, new Color(0.55f, 0.30f, 0.80f),
                "Скрытая сеть злодеев: заговоры, шпионы и тайные планы.\n<size=85%><color=#9AA4B8>Задачи, бонусы и здания фракции — скоро.</color></size>"),
        };
    }

    /// <summary>Создать карту "Город", если её ещё нет (Data/Maps).</summary>
    private static MapData[] EnsureMaps()
    {
        EnsureFolder("Assets/_Game/Data", "Maps");
        string path = "Assets/_Game/Data/Maps/Map_City.asset";
        var m = AssetDatabase.LoadAssetAtPath<MapData>(path);
        if (m == null)
        {
            m = ScriptableObject.CreateInstance<MapData>();
            m.displayName = "Город";
            m.preview = AssetDatabase.LoadAllAssetsAtPath("Assets/My/City.png").OfType<Sprite>().FirstOrDefault();
            m.sceneName = "Game";
            m.description = "Изометрический город: 4 объекта, портал в центре, базы на краях города.";
            AssetDatabase.CreateAsset(m, path);
        }
        return new[] { m };
    }

    /// <summary>Фракция из файла или новая.</summary>
    private static FactionData Faction(string file, string name, Side side, Color color, string description)
    {
        string path = $"Assets/_Game/Data/Factions/{file}.asset";
        var f = AssetDatabase.LoadAssetAtPath<FactionData>(path);
        if (f != null) return f;
        f = ScriptableObject.CreateInstance<FactionData>();
        f.displayName = name;
        f.side = side;
        f.color = color;
        f.description = description;
        AssetDatabase.CreateAsset(f, path);
        return f;
    }

    /// <summary>Создать папку, если её нет.</summary>
    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    /// <summary>Сцены в сборке: сначала меню, потом карта.</summary>
    private static void SetBuildScenes()
    {
        var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(GameScenePath, true) };
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != ScenePath && s.path != GameScenePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>Записать массив ссылок в поле компонента.</summary>
    private static void SetArray(SerializedProperty prop, IList<Object> values)
    {
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ---------- Элементы интерфейса ----------

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

    /// <summary>Кнопка с надписью (pos — от центра родителя).</summary>
    private static Button Btn(string name, Transform parent, string label, Color color, Vector2 pos, Vector2 size, float fontSize)
    {
        var img = Img(name, parent, rounded, color);
        img.type = Image.Type.Sliced;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var b = img.gameObject.AddComponent<Button>();
        var colors = b.colors;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        b.colors = colors;
        var t = Text("Text", rt, label, fontSize, Color.white, TextAlignmentOptions.Center, false);
        Stretch(t.rectTransform);
        return b;
    }

    /// <summary>Маленькая плашка в углу кнопки ("скоро").</summary>
    private static void Badge(Transform button, string text)
    {
        var img = Img("Badge", button, rounded, new Color(1f, 0.75f, 0.2f));
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, new Vector2(1f, 1f), new Vector2(-50, -2), new Vector2(110, 36));
        var t = Text("Text", img.rectTransform, text, 22, new Color(0.15f, 0.12f, 0.05f), TextAlignmentOptions.Center, false);
        Stretch(t.rectTransform);
    }

    /// <summary>Строка настройки "Название  ‹ значение ›".</summary>
    private static OptionSelectorUI Selector(RectTransform parent, string label, float y)
    {
        var row = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        Place(row, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(900, 60));
        var name = Text("Label", row, label, 30, Color.white, TextAlignmentOptions.Left, false);
        Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(230, 0), new Vector2(460, 56));
        var box = Img("Value", row, rounded, new Color(0.16f, 0.19f, 0.26f));
        box.type = Image.Type.Sliced;
        Place(box.rectTransform, new Vector2(1f, 0.5f), new Vector2(-210, 0), new Vector2(420, 56));
        var value = Text("Text", box.rectTransform, "—", 28, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center, false);
        Stretch(value.rectTransform);
        Button prev = Btn("Prev", box.rectTransform, "‹", Blue, Vector2.zero, Vector2.zero, 40);
        Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(28, 0), new Vector2(52, 52));
        Button next = Btn("Next", box.rectTransform, "›", Blue, Vector2.zero, Vector2.zero, 40);
        Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(-28, 0), new Vector2(52, 52));
        var sel = row.gameObject.AddComponent<OptionSelectorUI>();
        var so = new SerializedObject(sel);
        so.FindProperty("valueText").objectReferenceValue = value;
        so.FindProperty("prevButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();
        return sel;
    }

    /// <summary>Строка громкости: название, ползунок, проценты.</summary>
    private static (Slider, TMP_Text) VolumeRow(RectTransform parent, string label, float y)
    {
        var row = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        Place(row, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(900, 56));
        var name = Text("Label", row, label, 30, Color.white, TextAlignmentOptions.Left, false);
        Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(230, 0), new Vector2(460, 56));

        // Ползунок: фон, заполнение, ручка
        var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var srt = sliderGo.GetComponent<RectTransform>();
        srt.SetParent(row, false);
        Place(srt, new Vector2(1f, 0.5f), new Vector2(-270, 0), new Vector2(320, 28));
        var back = Img("Background", srt, rounded, new Color(0.16f, 0.19f, 0.26f));
        back.type = Image.Type.Sliced;
        Stretch(back.rectTransform);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(srt, false);
        Stretch(fillArea, 4f);
        var fill = Img("Fill", fillArea, rounded, Green);
        fill.type = Image.Type.Sliced;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(srt, false);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(14, 0);
        handleArea.offsetMax = new Vector2(-14, 0);
        var handle = Img("Handle", handleArea, circle, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(40, 12); // высота = область (28) + 12 — ровный круг
        var slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;

        var val = Text("Value", row, "100%", 28, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Right, false);
        Place(val.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40, 0), new Vector2(80, 56));
        return (slider, val);
    }

    /// <summary>Растянуть на весь родитель (с отступом).</summary>
    private static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    /// <summary>Поставить по якорю: anchor — точка родителя, pos — сдвиг центра, size — размер.</summary>
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>Сделать что-то с объектом и вернуть его (для коротких записей).</summary>
    private static T Also<T>(this T obj, System.Action<T> action)
    {
        action(obj);
        return obj;
    }
}
