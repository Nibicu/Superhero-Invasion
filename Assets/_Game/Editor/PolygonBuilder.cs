using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Сборщик сцены полигона «Битва» (Assets/_Game/Scenes/Polygon.unity).
/// Берёт копию сцены Game (там арена, интерфейс боя и BattleManager), убирает карту, менеджеры
/// и интерфейс карты, и собирает экраны полигона (PolygonUI): выбор арены, состав команд, итог боя, плашка серии.
/// Арена всегда такая же, как в игре: после правок арены в Game просто соберите полигон заново.
/// Меню: Tools → Superhero → Собрать полигон «Битва». Можно запускать повторно.
/// </summary>
public static class PolygonBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";       // Откуда берём арену
    private const string PolygonScenePath = "Assets/_Game/Scenes/Polygon.unity"; // Сцена полигона

    private static readonly Color Blue = new Color(0.184f, 0.482f, 0.878f);   // Синие кнопки, команда A
    private static readonly Color Red = new Color(0.851f, 0.22f, 0.227f);     // Красные кнопки, команда B
    private static readonly Color Green = new Color(0.247f, 0.639f, 0.302f);  // Зелёные кнопки
    private static readonly Color Purple = new Color(0.55f, 0.33f, 0.82f);    // Фиолетовые кнопки
    private static readonly Color Orange = new Color(0.86f, 0.5f, 0.16f);     // Оранжевые кнопки
    private static readonly Color Gray = new Color(0.30f, 0.34f, 0.42f);      // Второстепенные кнопки
    private static readonly Color Back = new Color(0.07f, 0.08f, 0.11f, 1f);  // Фон экранов
    private static readonly Color PanelColor = new Color(0.12f, 0.14f, 0.2f, 0.95f); // Панели команд

    private static Sprite rounded;          // Скруглённый прямоугольник
    private static Sprite square;           // Квадрат
    private static TMP_FontAsset font;      // Шрифт
    private static Material outlineFont;    // Шрифт с обводкой

    /// <summary>Собрать сцену полигона.</summary>
    [MenuItem("Tools/Superhero/Собрать полигон «Битва»")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // 1) Копия сцены Game → Polygon (сама Game не меняется)
        Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(game, PolygonScenePath, true);
        Scene scene = EditorSceneManager.OpenScene(PolygonScenePath, OpenSceneMode.Single);
        LoadAssets();

        // 2) Портрет-образец (из карточки РОСТЕРА) — до удаления интерфейса карты
        var roster = Object.FindAnyObjectByType<RosterWindowUI>(FindObjectsInactive.Include);
        var srcCard = (HeroCardUI)new SerializedObject(roster).FindProperty("cardTemplate").objectReferenceValue;
        var srcPortraitInScene = (HeroPortraitUI)new SerializedObject(srcCard).FindProperty("portrait").objectReferenceValue;
        var holder = new GameObject("PortraitSource", typeof(RectTransform));
        HeroPortraitUI srcPortrait = Object.Instantiate(srcPortraitInScene, holder.transform);
        Vector2 srcSize = ((RectTransform)srcPortraitInScene.transform).rect.size;

        // 3) Убрать всё, что не нужно для боя
        foreach (GameObject top in scene.GetRootGameObjects())
            if (top.name == "Map" || top.name == "Managers" || top.name == "Canvas" || top.name == "ResultCanvas")
                Object.DestroyImmediate(top);
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Back;
        }

        // 4) Холст полигона (поверх интерфейса боя — для плашки серии)
        var canvasGo = new GameObject("PolygonCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; // всегда 1080 по высоте: на широком экране — шире, ничего не обрезается снизу
        RectTransform root = canvasGo.GetComponent<RectTransform>();
        var ui = canvasGo.AddComponent<PolygonUI>();
        var so = new SerializedObject(ui);

        BuildArenaPanel(root, so);
        BuildSetupPanel(root, so, srcPortrait, srcSize);
        BuildResultPanel(root, so);
        BuildSeriesPanel(root, so);

        // Все бойцы: герои, злодеи, потом мобы
        SetArray(so.FindProperty("units"), LoadUnits());
        so.ApplyModifiedPropertiesWithoutUndo();

        Object.DestroyImmediate(holder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuild();
        Debug.Log("Полигон «Битва» собран: " + PolygonScenePath);
    }

    // ---------- Экраны ----------

    /// <summary>Экран 1: выбор арены.</summary>
    private static void BuildArenaPanel(RectTransform root, SerializedObject so)
    {
        var panel = Img("ArenaPanel", root, square, Back);
        Stretch(panel.rectTransform);
        var title = Text("Title", panel.rectTransform, "БИТВА — ПОЛИГОН", 80, Color.white, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1400, 110));
        var sub = Text("Subtitle", panel.rectTransform, "Испытание баланса. Выберите арену", 36, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -240), new Vector2(1200, 56));

        Button normal = Btn("NormalArenaButton", panel.rectTransform,
            "ОБЫЧНАЯ АРЕНА\n<size=55%><color=#DDE6F5>Одна территория.\nБой до последнего бойца</color></size>",
            Blue, new Vector2(-380, -20), new Vector2(680, 300), 54);
        Button flag = Btn("FlagArenaButton", panel.rectTransform,
            "БИТВА ЗА ФЛАГ\n<size=55%><color=#FBE9D6>Длинная арена с флагом в центре.\nУдержите флаг или выбейте всех</color></size>",
            Orange, new Vector2(380, -20), new Vector2(680, 300), 54);
        Button menu = Btn("MenuButton", panel.rectTransform, "В МЕНЮ", Gray, Vector2.zero, Vector2.zero, 34);
        Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(360, 80));

        so.FindProperty("arenaPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("normalArenaButton").objectReferenceValue = normal;
        so.FindProperty("flagArenaButton").objectReferenceValue = flag;
        so.FindProperty("arenaMenuButton").objectReferenceValue = menu;
    }

    /// <summary>Экран 2: состав команд — слева A, справа B, в центре все бойцы.</summary>
    private static void BuildSetupPanel(RectTransform root, SerializedObject so, HeroPortraitUI srcPortrait, Vector2 srcSize)
    {
        var panel = Img("SetupPanel", root, square, Back);
        Stretch(panel.rectTransform);
        RectTransform p = panel.rectTransform;

        var header = Text("Header", p, "СОСТАВ КОМАНД", 54, Color.white, TextAlignmentOptions.Center, true);
        Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(900, 70));
        var arena = Text("Arena", p, "Арена: обычная", 30, Color.white, TextAlignmentOptions.Center, false);
        Place(arena.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -105), new Vector2(900, 44));
        Button change = Btn("ChangeArenaButton", p, "СМЕНИТЬ АРЕНУ", Blue, Vector2.zero, Vector2.zero, 28);
        Place((RectTransform)change.transform, new Vector2(0f, 1f), new Vector2(180, -65), new Vector2(300, 70));
        Button menu = Btn("MenuButton", p, "В МЕНЮ", Gray, Vector2.zero, Vector2.zero, 28);
        Place((RectTransform)menu.transform, new Vector2(1f, 1f), new Vector2(-140, -65), new Vector2(220, 70));

        // Команды
        var (slotsA, powerA, clearA) = BuildTeam(p, "TeamA", "КОМАНДА A", new Color(0.5f, 0.75f, 1f), 0f, 240f, srcPortrait, srcSize);
        var (slotsB, powerB, clearB) = BuildTeam(p, "TeamB", "КОМАНДА B", new Color(1f, 0.55f, 0.5f), 1f, -240f, srcPortrait, srcSize);

        // Все бойцы — сетка с прокруткой
        var scrollBg = Img("Units", p, rounded, new Color(1f, 1f, 1f, 0.04f));
        scrollBg.type = Image.Type.Sliced;
        StretchY(scrollBg.rectTransform, 0.5f, 0f, 960f, 150f, 200f);
        var scroll = scrollBg.gameObject.AddComponent<ScrollRect>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(scrollBg.rectTransform, false);
        Stretch(viewport);
        var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 170);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(6, 6, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        PolygonUnitCardUI card = BuildCard(content, srcPortrait, srcSize);

        // Низ: подсказка и кнопки боя
        var hint = Text("Hint", p, "", 28, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 165), new Vector2(960, 40));
        Button fight = Btn("FightButton", p, "В БОЙ!", Green, Vector2.zero, Vector2.zero, 44);
        Place((RectTransform)fight.transform, new Vector2(0.5f, 0f), new Vector2(-200, 95), new Vector2(370, 90));
        Button series = Btn("SeriesButton", p, "СЕРИЯ ×20", Purple, Vector2.zero, Vector2.zero, 40);
        Place((RectTransform)series.transform, new Vector2(0.5f, 0f), new Vector2(200, 95), new Vector2(370, 90));

        so.FindProperty("setupPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("unitGrid").objectReferenceValue = content;
        so.FindProperty("cardTemplate").objectReferenceValue = card;
        SetArray(so.FindProperty("slotsA"), slotsA.Cast<Object>().ToList());
        SetArray(so.FindProperty("slotsB"), slotsB.Cast<Object>().ToList());
        so.FindProperty("powerAText").objectReferenceValue = powerA;
        so.FindProperty("powerBText").objectReferenceValue = powerB;
        so.FindProperty("arenaText").objectReferenceValue = arena;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("fightButton").objectReferenceValue = fight;
        so.FindProperty("seriesButton").objectReferenceValue = series;
        so.FindProperty("changeArenaButton").objectReferenceValue = change;
        so.FindProperty("setupMenuButton").objectReferenceValue = menu;
        so.FindProperty("clearAButton").objectReferenceValue = clearA;
        so.FindProperty("clearBButton").objectReferenceValue = clearB;
    }

    /// <summary>Колонка команды: заголовок, сила, 4 ячейки, кнопка «ОЧИСТИТЬ». anchorX — 0 (слева) или 1 (справа).</summary>
    private static (List<PolygonSlotUI>, TMP_Text, Button) BuildTeam(RectTransform parent, string name, string title, Color color,
                                                                    float anchorX, float x, HeroPortraitUI srcPortrait, Vector2 srcSize)
    {
        var panel = Img(name, parent, rounded, PanelColor);
        panel.type = Image.Type.Sliced;
        StretchY(panel.rectTransform, anchorX, x, 440f, 150f, 20f);
        RectTransform p = panel.rectTransform;
        var t = Text("Title", p, title, 40, color, TextAlignmentOptions.Center, true);
        Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(400, 56));
        var power = Text("Power", p, "Сила: 0", 26, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center, false);
        Place(power.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -85), new Vector2(400, 40));
        var slots = new List<PolygonSlotUI>();
        for (int i = 0; i < PolygonUI.TeamSize; i++)
            slots.Add(BuildSlot(p, i, srcPortrait, srcSize));
        Button clear = Btn("ClearButton", p, "ОЧИСТИТЬ", Gray, Vector2.zero, Vector2.zero, 26);
        Place((RectTransform)clear.transform, new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(240, 60));
        return (slots, power, clear);
    }

    /// <summary>Ячейка команды: портрет, имя, класс, «‹ Ур. N ›», «X».</summary>
    private static PolygonSlotUI BuildSlot(RectTransform parent, int index, HeroPortraitUI srcPortrait, Vector2 srcSize)
    {
        var bg = Img($"Slot{index + 1}", parent, rounded, new Color(1f, 1f, 1f, 0.07f));
        bg.type = Image.Type.Sliced;
        Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -190 - index * 162), new Vector2(410, 150));
        RectTransform r = bg.rectTransform;

        var empty = Text("Empty", r, "пусто", 28, new Color(0.55f, 0.6f, 0.7f), TextAlignmentOptions.Center, false);
        Stretch(empty.rectTransform);

        var filled = new GameObject("Filled", typeof(RectTransform)).GetComponent<RectTransform>();
        filled.SetParent(r, false);
        Stretch(filled);
        HeroPortraitUI portrait = Portrait(filled, srcPortrait, srcSize, 84f, new Vector2(0f, 0.5f), new Vector2(60, 8));
        var name = Text("Name", filled, "Имя", 26, Color.white, TextAlignmentOptions.Left, false);
        Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(245, 42), new Vector2(260, 34));
        var info = Text("Info", filled, "Класс", 18, new Color(0.78f, 0.82f, 0.9f), TextAlignmentOptions.Left, false);
        Place(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(245, 12), new Vector2(260, 26));
        Button down = Btn("LevelDown", filled, "‹", Blue, Vector2.zero, Vector2.zero, 36);
        Place((RectTransform)down.transform, new Vector2(0f, 0.5f), new Vector2(145, -38), new Vector2(46, 46));
        var level = Text("Level", filled, "Ур. 1", 26, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, false);
        Place(level.rectTransform, new Vector2(0f, 0.5f), new Vector2(220, -38), new Vector2(100, 40));
        Button up = Btn("LevelUp", filled, "›", Blue, Vector2.zero, Vector2.zero, 36);
        Place((RectTransform)up.transform, new Vector2(0f, 0.5f), new Vector2(295, -38), new Vector2(46, 46));
        Button remove = Btn("Remove", filled, "X", Red, Vector2.zero, Vector2.zero, 24);
        Place((RectTransform)remove.transform, new Vector2(1f, 1f), new Vector2(-28, -28), new Vector2(40, 40));

        var slot = bg.gameObject.AddComponent<PolygonSlotUI>();
        var so = new SerializedObject(slot);
        so.FindProperty("filled").objectReferenceValue = filled.gameObject;
        so.FindProperty("emptyText").objectReferenceValue = empty;
        so.FindProperty("portrait").objectReferenceValue = portrait;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("levelText").objectReferenceValue = level;
        so.FindProperty("levelDown").objectReferenceValue = down;
        so.FindProperty("levelUp").objectReferenceValue = up;
        so.FindProperty("removeButton").objectReferenceValue = remove;
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    /// <summary>Карточка бойца в сетке: портрет, имя, класс, «+A» / «+B».</summary>
    private static PolygonUnitCardUI BuildCard(RectTransform parent, HeroPortraitUI srcPortrait, Vector2 srcSize)
    {
        var bg = Img("CardTemplate", parent, rounded, new Color(0.16f, 0.19f, 0.26f, 1f));
        bg.type = Image.Type.Sliced;
        RectTransform r = bg.rectTransform;
        HeroPortraitUI portrait = Portrait(r, srcPortrait, srcSize, 64f, new Vector2(0.5f, 1f), new Vector2(0, -40));
        var name = Text("Name", r, "Имя", 22, Color.white, TextAlignmentOptions.Center, false);
        Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(172, 28));
        name.enableAutoSizing = true;
        name.fontSizeMin = 14;
        name.fontSizeMax = 22;
        var info = Text("Info", r, "Класс", 15, new Color(0.78f, 0.82f, 0.9f), TextAlignmentOptions.Center, false);
        Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(174, 22));
        info.textWrappingMode = TextWrappingModes.NoWrap; // в одну строку, длинная подпись уменьшается
        info.enableAutoSizing = true;
        info.fontSizeMin = 10;
        info.fontSizeMax = 15;
        Button a = Btn("AddA", r, "+A", Blue, Vector2.zero, Vector2.zero, 24);
        Place((RectTransform)a.transform, new Vector2(0f, 0f), new Vector2(47, 22), new Vector2(80, 34));
        Button b = Btn("AddB", r, "+B", Red, Vector2.zero, Vector2.zero, 24);
        Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(-47, 22), new Vector2(80, 34));

        var card = bg.gameObject.AddComponent<PolygonUnitCardUI>();
        var so = new SerializedObject(card);
        so.FindProperty("portrait").objectReferenceValue = portrait;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("addAButton").objectReferenceValue = a;
        so.FindProperty("addBButton").objectReferenceValue = b;
        so.ApplyModifiedPropertiesWithoutUndo();
        return card;
    }

    /// <summary>Экран 3: итог боя или серии — заголовок, счёт, таблица, кнопки.</summary>
    private static void BuildResultPanel(RectTransform root, SerializedObject so)
    {
        var panel = Img("ResultPanel", root, square, new Color(Back.r, Back.g, Back.b, 0.97f));
        Stretch(panel.rectTransform);
        RectTransform p = panel.rectTransform;

        var title = Text("Title", p, "ПОБЕДА КОМАНДЫ A", 64, Color.white, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -75), new Vector2(1600, 90));
        var sub = Text("Subtitle", p, "Бой длился 0 с", 30, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.Center, false);
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(1800, 50));
        var tableHeader = Text("TableHeader", p, "Цифры каждого бойца", 26, new Color(0.6f, 0.65f, 0.75f), TextAlignmentOptions.Center, false);
        Place(tableHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1200, 40));

        var table = new GameObject("Table", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        table.SetParent(p, false);
        table.anchorMin = table.anchorMax = new Vector2(0.5f, 1f);
        table.pivot = new Vector2(0.5f, 1f);
        table.anchoredPosition = new Vector2(0, -230);
        table.sizeDelta = new Vector2(1500, 560);
        var vl = table.GetComponent<VerticalLayoutGroup>();
        vl.spacing = 6;
        vl.childControlWidth = true;
        vl.childControlHeight = false;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;

        // Шаблон строки: 8 столбцов
        var row = Img("RowTemplate", table, rounded, new Color(0.17f, 0.3f, 0.52f, 0.85f));
        row.type = Image.Type.Sliced;
        row.rectTransform.sizeDelta = new Vector2(1500, 52);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
        float[] widths = { 90, 330, 80, 170, 170, 170, 140, 350 };
        var cells = new List<Object>();
        float left = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            var c = Text($"Cell{i}", row.rectTransform, "—", 24, Color.white, i == 1 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, false);
            Place(c.rectTransform, new Vector2(0f, 0.5f), new Vector2(left + widths[i] / 2f + (i == 1 ? 10f : 0f), 0), new Vector2(widths[i] - 10f, 48));
            left += widths[i];
            cells.Add(c);
        }
        var rowUi = row.gameObject.AddComponent<PolygonStatRowUI>();
        var rso = new SerializedObject(rowUi);
        rso.FindProperty("background").objectReferenceValue = row;
        SetArray(rso.FindProperty("cells"), cells);
        rso.ApplyModifiedPropertiesWithoutUndo();

        Button again = Btn("AgainButton", p, "ЗАНОВО", Green, Vector2.zero, Vector2.zero, 34);
        Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-570, 90), new Vector2(340, 84));
        Button newTeams = Btn("NewTeamsButton", p, "НОВЫЕ КОМАНДЫ", Blue, Vector2.zero, Vector2.zero, 32);
        Place((RectTransform)newTeams.transform, new Vector2(0.5f, 0f), new Vector2(-190, 90), new Vector2(340, 84));
        Button series = Btn("SeriesButton", p, "СЕРИЯ ×20", Purple, Vector2.zero, Vector2.zero, 34);
        Place((RectTransform)series.transform, new Vector2(0.5f, 0f), new Vector2(190, 90), new Vector2(340, 84));
        Button menu = Btn("MenuButton", p, "В МЕНЮ", Gray, Vector2.zero, Vector2.zero, 34);
        Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(570, 90), new Vector2(340, 84));

        so.FindProperty("resultPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("resultTitle").objectReferenceValue = title;
        so.FindProperty("resultSubtitle").objectReferenceValue = sub;
        so.FindProperty("tableHeader").objectReferenceValue = tableHeader;
        so.FindProperty("tableRoot").objectReferenceValue = table;
        so.FindProperty("rowTemplate").objectReferenceValue = rowUi;
        so.FindProperty("againButton").objectReferenceValue = again;
        so.FindProperty("newTeamsButton").objectReferenceValue = newTeams;
        so.FindProperty("resultSeriesButton").objectReferenceValue = series;
        so.FindProperty("resultMenuButton").objectReferenceValue = menu;
    }

    /// <summary>Плашка серии внизу экрана во время боёв: счёт и «СТОП».</summary>
    private static void BuildSeriesPanel(RectTransform root, SerializedObject so)
    {
        var panel = Img("SeriesPanel", root, rounded, new Color(0.08f, 0.09f, 0.14f, 0.92f));
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1300, 76));
        var text = Text("Text", panel.rectTransform, "СЕРИЯ", 28, Color.white, TextAlignmentOptions.Center, false);
        Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(560, 0), new Vector2(1100, 66));
        Button stop = Btn("StopButton", panel.rectTransform, "СТОП", Red, Vector2.zero, Vector2.zero, 30);
        Place((RectTransform)stop.transform, new Vector2(1f, 0.5f), new Vector2(-90, 0), new Vector2(150, 58));
        panel.gameObject.SetActive(false);

        so.FindProperty("seriesPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("seriesText").objectReferenceValue = text;
        so.FindProperty("stopSeriesButton").objectReferenceValue = stop;
    }

    // ---------- Данные ----------

    /// <summary>Загрузить спрайты и шрифты интерфейса (после открытия сцены).</summary>
    private static void LoadAssets()
    {
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/RoundedRect.png");
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Square.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        outlineFont = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");
    }

    /// <summary>Все бойцы из Data/Heroes и Data/Villains: сначала герои, потом злодеи, потом мобы; внутри — по звёздам и имени.</summary>
    private static List<Object> LoadUnits()
    {
        var list = new List<HeroData>();
        foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { "Assets/_Game/Data/Heroes", "Assets/_Game/Data/Villains" }))
        {
            var d = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d != null) list.Add(d);
        }
        return list
            .OrderBy(d => !d.hireable ? 2 : d.side == Side.Heroes ? 0 : 1)
            .ThenBy(d => d.stars)
            .ThenBy(d => d.displayName)
            .Cast<Object>()
            .ToList();
    }

    /// <summary>Добавить полигон в список сцен сборки (после меню и карты).</summary>
    private static void AddToBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == PolygonScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(PolygonScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    /// <summary>Записать массив ссылок в поле компонента.</summary>
    private static void SetArray(SerializedProperty prop, IList<Object> values)
    {
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ---------- Элементы интерфейса ----------

    /// <summary>Уменьшенная копия портрета-образца: size — нужный размер, anchor/pos — где стоит.</summary>
    private static HeroPortraitUI Portrait(RectTransform parent, HeroPortraitUI src, Vector2 srcSize, float size, Vector2 anchor, Vector2 pos)
    {
        HeroPortraitUI portrait = Object.Instantiate(src, parent);
        portrait.name = "Portrait";
        var rt = (RectTransform)portrait.transform;
        Place(rt, anchor, pos, srcSize);
        float k = size / Mathf.Max(1f, Mathf.Max(srcSize.x, srcSize.y));
        rt.localScale = new Vector3(k, k, 1f);
        foreach (var g in portrait.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        return portrait;
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
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        b.colors = colors;
        var t = Text("Text", rt, label, fontSize, Color.white, TextAlignmentOptions.Center, false);
        Stretch(t.rectTransform);
        return b;
    }

    /// <summary>Растянуть на весь родитель (с отступом pad).</summary>
    private static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    /// <summary>
    /// Растянуть по высоте родителя: сверху отступ top, снизу bottom; по ширине — width,
    /// центр на anchorX (0 — левый край, 0.5 — середина, 1 — правый) со сдвигом x.
    /// </summary>
    private static void StretchY(RectTransform rt, float anchorX, float x, float width, float top, float bottom)
    {
        rt.anchorMin = new Vector2(anchorX, 0f);
        rt.anchorMax = new Vector2(anchorX, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(x - width / 2f, bottom);
        rt.offsetMax = new Vector2(x + width / 2f, -top);
    }

    /// <summary>Поставить по якорю: anchor — точка родителя, pos — сдвиг центра, size — размер.</summary>
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
