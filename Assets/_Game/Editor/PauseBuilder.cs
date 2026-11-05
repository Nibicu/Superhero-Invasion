using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Фракции и новое окно тактической паузы (сцена Game). Меню: Tools → Superhero → Собрать фракции и паузу.
///  - FactionManager на объекте Managers (очки фракции и 5 иконок), список фракций;
///  - 3★ герои фракций: Трон — Титан, Стражи — Аврора, Разрушители — Пожиратель, Тайный порядок — Император;
///  - очки фракции — главный ресурс (бывшее золото): цена иконок (2000), значок на верхней панели;
///  - окно паузы: сверху очки фракции и плутоний, слева вкладки (Фракция / Герои), в центре содержимое, справа внизу "КОНЕЦ ХОДА".
/// Повторный запуск пересоздаёт окно паузы; ручные правки цены иконок не трогаются.
/// </summary>
public static class PauseBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты

    // Очки фракции (распределены так, чтобы 5-ю иконку нельзя было открыть раньше 6-й фазы)

    private static Sprite rounded;       // Скруглённый прямоугольник
    private static Sprite circle;        // Круг
    private static TMP_FontAsset font;   // Шрифт
    private static Material outlineFont; // Шрифт с обводкой

    /// <summary>Собрать всё.</summary>
    [MenuItem("Tools/Superhero/Собрать фракции и паузу")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        SetupData();
        BuildPause(GameObject.Find("Canvas").transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Фракции и окно паузы собраны");
    }

    // ---------- Данные ----------

    /// <summary>FactionManager, 3★ герои фракций и очки фракции в файлах.</summary>
    private static void SetupData()
    {
        // 3★ герои фракций
        Hero("Faction_Throne", "Assets/_Game/Data/Heroes/Hero_Titan.asset");
        Hero("Faction_Guardians", "Assets/_Game/Data/Heroes/Hero_Aurora.asset");
        Hero("Faction_Destroyers", "Assets/_Game/Data/Villains/Villain_Devourer.asset");
        Hero("Faction_SecretOrder", "Assets/_Game/Data/Villains/Villain_Emperor.asset");

        // FactionManager на Managers
        var managers = GameObject.Find("Managers");
        var fm = managers.GetComponent<FactionManager>();
        if (fm == null) fm = managers.AddComponent<FactionManager>();
        var so = new SerializedObject(fm);
        var list = so.FindProperty("factions");
        string[] guids = AssetDatabase.FindAssets("t:FactionData", new[] { "Assets/_Game/Data/Factions" });
        list.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<FactionData>(AssetDatabase.GUIDToAssetPath(guids[i]));
        so.ApplyModifiedPropertiesWithoutUndo();

        // Очки фракции — главный ресурс (бывшее золото): цена иконок и верхняя панель

        SetupEconomy(fm);
        SetupRecommendations();
        RemoveLivesAndInfirmary();
    }

    // ---------- Экономика: очки фракции — главный ресурс ----------

    /// <summary>Новая цена иконки фракции: золото стало очками фракции, поэтому 5 иконок = 10 000 (5-я — не раньше 6-й фазы).</summary>
    private const int IconCost = 2000;

    /// <summary>
    /// Очки фракции вместо золота: цена иконки 2000 (меняется, только если стоит старая 1000 — ручные правки не трогаем),
    /// значок монеты на верхней панели — цвета очков фракции.
    /// </summary>
    private static void SetupEconomy(FactionManager fm)
    {
        var so = new SerializedObject(fm);
        var cost = so.FindProperty("iconCost");
        if (cost.intValue == 1000) cost.intValue = IconCost;
        so.ApplyModifiedPropertiesWithoutUndo();

        ShrinkBaseWindow();

        // Подсказка с читами внизу экрана: золото → очки фракции
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            if (t.text.Contains("+1000 золота")) { t.text = t.text.Replace("+1000 золота", "+1000 очков фракции"); EditorUtility.SetDirty(t); }

        // Верхняя панель: число и значок рядом с ним — фиолетовые (очки фракции)
        var top = Object.FindAnyObjectByType<TopBarUI>(FindObjectsInactive.Include);
        if (top == null) return;
        var gold = new SerializedObject(top).FindProperty("goldText").objectReferenceValue as TMP_Text;
        if (gold == null) return;
        Transform icon = gold.transform.parent.Find("GoldIcon");
        if (icon == null) return;
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Circle.png");
        foreach (Image img in icon.GetComponentsInChildren<Image>(true))
        {
            // Сама монета — фиолетовое кольцо, середина — светлее
            img.sprite = circleSprite;
            img.color = img.transform == icon ? new Color(0.72f, 0.3f, 0.78f) : new Color(0.85f, 0.6f, 0.95f);
            EditorUtility.SetDirty(img);
        }
        foreach (TMP_Text t in icon.GetComponentsInChildren<TMP_Text>(true))
        {
            t.text = ""; // знак монеты больше не нужен
            EditorUtility.SetDirty(t);
        }
    }

    /// <summary>
    /// Окно базы на карте: ячеек построек больше нет — оставляем только верхнюю полосу (название, HP, защитники, доход,
    /// кнопка "Атаковать базу" у врага). Ячейки прячутся, панель становится высотой с заголовок. Повторно не сжимает.
    /// </summary>
    private static void ShrinkBaseWindow()
    {
        var bw = Object.FindAnyObjectByType<BaseWindowUI>(FindObjectsInactive.Include);
        if (bw == null) return;
        var panel = bw.transform.Find("Root/Panel") as RectTransform;
        var header = panel != null ? panel.Find("Header") as RectTransform : null;
        if (panel == null || header == null || panel.sizeDelta.y <= header.sizeDelta.y + 1f) return; // уже сжато
        float shift = header.anchoredPosition.y; // всё, что осталось, — на высоте заголовка: опускаем к центру панели
        foreach (Transform c in panel)
        {
            var rt = (RectTransform)c;
            if (c.name.StartsWith("Slot")) { c.gameObject.SetActive(false); continue; }
            rt.anchoredPosition -= new Vector2(0f, shift);
        }
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, header.sizeDelta.y);
        panel.anchoredPosition += new Vector2(0f, shift);
        EditorUtility.SetDirty(panel);
    }

    // ---------- Рекомендации по числу героев (вместо силы) ----------

    /// <summary>Сколько героев рекомендуется на каждую миссию (по бывшей "рекомендуемой силе": 150–200 → 1, 300–400 → 2, 450–600 → 3, 800 → 4).</summary>
    private static readonly Dictionary<string, int> MissionHeroes = new Dictionary<string, int>
    {
        { "Mission_Training", 1 }, { "Mission_Fire", 1 }, { "Mission_V_Catacombs", 1 }, { "Mission_V_Racket", 1 },
        { "Mission_Jewelry", 2 }, { "Mission_Blueprints", 2 }, { "Mission_V_Truck", 2 }, { "Mission_V_Blueprints", 2 },
        { "Mission_Radiation", 3 }, { "Mission_Escaped", 3 }, { "Mission_V_Plutonium", 3 }, { "Mission_V_Scientist", 3 },
        { "Mission_Hostages", 4 }, { "Mission_V_Prison", 4 },
    };

    /// <summary>Записать рекомендации: миссии — по таблице, общие миссии и объекты — 3 и более, портал — 4.</summary>
    private static void SetupRecommendations()
    {
        foreach (MissionData d in All<MissionData>())
        {
            d.recommendedHeroes = MissionHeroes.TryGetValue(d.name, out int n) ? n : 2;
            EditorUtility.SetDirty(d);
        }
        foreach (GlobalMissionData d in All<GlobalMissionData>()) { d.recommendedHeroes = 3; EditorUtility.SetDirty(d); }
        foreach (MapObjectData d in All<MapObjectData>()) { d.recommendedHeroes = 3; EditorUtility.SetDirty(d); }
        foreach (PortalData d in All<PortalData>()) { d.recommendedHeroes = 4; EditorUtility.SetDirty(d); }
    }

    // ---------- Лазарет и жизни убраны ----------

    /// <summary>Скорость лечения героев дома (доля HP в секунду): было 0.02, теперь на 50% медленнее.</summary>
    private const float HealPerSecond = 0.01f;

    /// <summary>
    /// Убрать следы Лазарета и системы жизней (идёт каждый раз, безопасно повторять):
    ///  - пустые ссылки (удалённый файл Лазарета) — из построек обеих баз и из плана стройки ИИ;
    ///  - лечение дома на 50% медленнее (SquadManager.healPerSecond);
    ///  - значок сердца и число жизней на аватарках героев (объекты LifeIcon и Lives).
    /// </summary>
    private static void RemoveLivesAndInfirmary()
    {
        foreach (MainBase b in Object.FindObjectsByType<MainBase>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(b);
            RemoveEmpty(so.FindProperty("availableBuildings"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (EnemyAI ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(ai);
            RemoveEmpty(so.FindProperty("buildOrder"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (SquadManager sm in Object.FindObjectsByType<SquadManager>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(sm);
            so.FindProperty("healPerSecond").floatValue = HealPerSecond;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // Сердечки жизней на аватарках
        var dead = new List<GameObject>();
        foreach (HeroAvatarUI a in Object.FindObjectsByType<HeroAvatarUI>(FindObjectsInactive.Include))
            foreach (string n in new[] { "LifeIcon", "Lives" })
            {
                Transform t = a.transform.Find(n);
                if (t != null) dead.Add(t.gameObject);
            }
        foreach (GameObject g in dead) Object.DestroyImmediate(g);
    }

    /// <summary>Убрать из списка-свойства пустые ссылки (например, на удалённый файл).</summary>
    private static void RemoveEmpty(SerializedProperty list)
    {
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                list.DeleteArrayElementAtIndex(i);
                // У ссылок на объекты первый вызов только обнуляет элемент — удаляем второй раз, если он остался
                if (i < list.arraySize && list.GetArrayElementAtIndex(i).objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);
            }
    }

    /// <summary>Назначить фракции её 3★ героя.</summary>
    private static void Hero(string faction, string heroPath)
    {
        var f = AssetDatabase.LoadAssetAtPath<FactionData>($"Assets/_Game/Data/Factions/{faction}.asset");
        var h = AssetDatabase.LoadAssetAtPath<HeroData>(heroPath);
        if (f == null || h == null) { Debug.LogWarning($"Нет {faction} или {heroPath}"); return; }
        f.factionHero = h;
        EditorUtility.SetDirty(f);
    }

    /// <summary>Все файлы данного типа в проекте.</summary>
    private static List<T> All<T>() where T : Object
    {
        var res = new List<T>();
        foreach (string g in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Game/Data" }))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
            if (a != null) res.Add(a);
        }
        return res;
    }

    // ---------- Окно паузы ----------

    /// <summary>Пересоздать окно тактической паузы в Canvas (вызывает и PhaseBuilder).</summary>
    public static void BuildPause(Transform canvas)
    {
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/RoundedRect.png");
        circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Placeholder/Circle.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        outlineFont = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");

        Transform old = canvas.Find("TacticalPause");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var roster = Object.FindAnyObjectByType<RosterWindowUI>(FindObjectsInactive.Include);
        var heroInfo = (HeroInfoWindowUI)new SerializedObject(roster).FindProperty("heroInfo").objectReferenceValue;
        var baseWindow = Object.FindAnyObjectByType<BaseWindowUI>(FindObjectsInactive.Include);
        var draft = Object.FindAnyObjectByType<DraftUI>(FindObjectsInactive.Include);
        var srcRow = (DraftRowUI)new SerializedObject(draft).FindProperty("rowTemplate").objectReferenceValue;
        var srcPortrait = srcRow.GetComponentInChildren<HeroPortraitUI>(true);

        var win = new GameObject("TacticalPause", typeof(RectTransform)).GetComponent<RectTransform>();
        win.SetParent(canvas, false);
        Stretch(win);
        var rootImg = Img("Root", win, null, new Color(0.04f, 0.06f, 0.10f, 1f));
        Stretch(rootImg.rectTransform);
        RectTransform r = rootImg.rectTransform;

        // Заголовок, таймер, что дальше
        var timer = Text("Timer", r, "2:00", 56, Color.white, TextAlignmentOptions.Center, true);
        Place(timer.rectTransform, new Vector2(1f, 1f), new Vector2(-130, -60), new Vector2(220, 80));
        var next = Text("Next", r, "Дальше: …", 26, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, false);
        Place(next.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -132), new Vector2(1200, 40));

        // Ресурсы сверху: кольцо + название + число
        TMP_Text gold = null; // золота больше нет — главный ресурс теперь очки фракции
        TMP_Text plut = Resource(r, "Plutonium", "Плутоний", new Color(0.25f, 0.3f, 0.85f), 250);
        TMP_Text fpts = Resource(r, "FactionPoints", "Очки фракции", new Color(0.72f, 0.3f, 0.78f), -250);

        // Слева — вкладки (коричневая колонка)
        var tabs = Img("Tabs", r, rounded, new Color(0.71f, 0.48f, 0.33f));
        tabs.type = Image.Type.Sliced;
        Place(tabs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-790, -30), new Vector2(280, 520));
        var title = Text("Title", r, "ТАКТИЧЕСКАЯ\nПАУЗА", 30, new Color(0.8f, 0.85f, 0.95f), TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-790, 285), new Vector2(300, 80));
        Button tabFaction = TabBtn(tabs.rectTransform, "TabFaction", "Фракция", 160);
        Button tabHeroes = TabBtn(tabs.rectTransform, "TabHeroes", "Герои", 0);
        Button tabBase = null; // вкладки "База" больше нет (строительство убрано)

        // В центре — зелёная рамка и тёмное поле
        var frame = Img("Content", r, rounded, new Color(0.08f, 0.8f, 0.3f));
        frame.type = Image.Type.Sliced;
        Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(175, -30), new Vector2(1480, 660));
        var inner = Img("Inner", frame.rectTransform, rounded, new Color(0.09f, 0.11f, 0.15f));
        inner.type = Image.Type.Sliced;
        Stretch(inner.rectTransform, 8);
        RectTransform c = inner.rectTransform;

        // Справа внизу — "КОНЕЦ ХОДА"
        var readyImg = Img("ReadyButton", r, rounded, new Color(0.88f, 0.12f, 0.15f));
        readyImg.type = Image.Type.Sliced;
        Place(readyImg.rectTransform, new Vector2(1f, 0f), new Vector2(-250, 58), new Vector2(420, 90));
        var ready = readyImg.gameObject.AddComponent<Button>();
        Stretch(Text("Text", readyImg.rectTransform, "КОНЕЦ ХОДА", 40, Color.white, TextAlignmentOptions.Center, true).rectTransform);

        // ---- Вкладка "Фракция" ----
        var factionPage = Page(c, "FactionPage");
        var fTitle = Text("FactionTitle", factionPage, "ФРАКЦИЯ", 46, Color.white, TextAlignmentOptions.Center, true);
        Place(fTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -75), new Vector2(1350, 130));
        float[] xs = { -560, -320, -80, 160, 450 };
        const float y = 10f;
        var links = new Image[xs.Length - 1];
        for (int i = 0; i < links.Length; i++) // линии — под кружками
        {
            var l = Img($"Link{i + 1}", factionPage, null, new Color(0.05f, 0.05f, 0.06f));
            Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((xs[i] + xs[i + 1]) / 2f, y), new Vector2(xs[i + 1] - xs[i], 16));
            links[i] = l;
        }
        var icons = new FactionIconUI[xs.Length];
        for (int i = 0; i < xs.Length; i++)
        {
            bool big = i == xs.Length - 1;
            icons[i] = Icon(factionPage, i + 1, new Vector2(xs[i], y), big ? new Vector2(220, 300) : new Vector2(150, 150));
        }
        var fHint = Text("Hint", factionPage, "", 26, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center, false);
        Place(fHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1380, 100));

        // ---- Вкладка "Герои" ----
        var heroesPage = Page(c, "HeroesPage");
        Oval(heroesPage, "FactionHero", new Color(0.55f, 1f, 1f), new Vector2(-575, 95), srcPortrait,
             out Button fhButton, out HeroPortraitUI fhPortrait, out TMP_Text fhText, out CanvasGroup fhFade);
        Oval(heroesPage, "Leader", new Color(0.0f, 0.65f, 1f), new Vector2(-345, 95), srcPortrait,
             out Button leaderButton, out HeroPortraitUI leaderPortrait, out TMP_Text leaderText, out _);
        Button buy = Btn(heroesPage, "BuyButton", "КУПИТЬ ГЕРОЕВ", new Color(0.91f, 0.525f, 0.165f), new Vector2(-460, -160), new Vector2(430, 72), 32);
        Button inventory = Btn(heroesPage, "InventoryButton", "ИНВЕНТАРЬ", new Color(0.55f, 0.33f, 0.82f), new Vector2(-460, -245), new Vector2(430, 62), 28);

        var pink = Img("Others", heroesPage, rounded, new Color(1f, 0.7f, 0.8f));
        pink.type = Image.Type.Sliced;
        Place(pink.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(285, 0), new Vector2(880, 610));
        var pinkInner = Img("Inner", pink.rectTransform, rounded, new Color(0.12f, 0.1f, 0.13f));
        pinkInner.type = Image.Type.Sliced;
        Stretch(pinkInner.rectTransform, 7);
        var heroesHeader = Text("Header", pinkInner.rectTransform, "ВАШИ ГЕРОИ", 30, Color.white, TextAlignmentOptions.Center, true);
        Place(heroesHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(840, 50));
        RectTransform heroList = Scroll(pinkInner.rectTransform);
        var row = Object.Instantiate(srcRow, heroList);
        row.name = "RowTemplate";
        row.gameObject.SetActive(false);

        // ---- Вкладка "База": копия панели окна базы ----
        BaseWindowUI baseTab = null; // встроенной базы больше нет

        // ---- Компонент ----
        var ui = win.gameObject.AddComponent<TacticalPauseUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("root").objectReferenceValue = rootImg.gameObject;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("nextText").objectReferenceValue = next;
        so.FindProperty("readyButton").objectReferenceValue = ready;
        so.FindProperty("goldText").objectReferenceValue = gold;
        so.FindProperty("plutoniumText").objectReferenceValue = plut;
        so.FindProperty("factionText").objectReferenceValue = fpts;
        so.FindProperty("factionTabButton").objectReferenceValue = tabFaction;
        so.FindProperty("heroesTabButton").objectReferenceValue = tabHeroes;
        so.FindProperty("baseTabButton").objectReferenceValue = tabBase;
        so.FindProperty("factionPage").objectReferenceValue = factionPage.gameObject;
        so.FindProperty("heroesPage").objectReferenceValue = heroesPage.gameObject;
        so.FindProperty("factionTitle").objectReferenceValue = fTitle;
        so.FindProperty("factionHint").objectReferenceValue = fHint;
        SetArray(so.FindProperty("icons"), icons);
        SetArray(so.FindProperty("links"), links);
        so.FindProperty("heroesHeader").objectReferenceValue = heroesHeader;
        so.FindProperty("heroList").objectReferenceValue = heroList;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.FindProperty("factionHeroButton").objectReferenceValue = fhButton;
        so.FindProperty("factionHeroPortrait").objectReferenceValue = fhPortrait;
        so.FindProperty("factionHeroText").objectReferenceValue = fhText;
        so.FindProperty("factionHeroFade").objectReferenceValue = fhFade;
        so.FindProperty("leaderButton").objectReferenceValue = leaderButton;
        so.FindProperty("leaderSlot").objectReferenceValue = (RectTransform)leaderButton.transform;
        so.FindProperty("leaderPortrait").objectReferenceValue = leaderPortrait;
        so.FindProperty("leaderText").objectReferenceValue = leaderText;
        so.FindProperty("buyButton").objectReferenceValue = buy;
        so.FindProperty("inventoryButton").objectReferenceValue = inventory;
        so.FindProperty("roster").objectReferenceValue = roster;
        so.FindProperty("heroInfo").objectReferenceValue = heroInfo;
        so.FindProperty("baseTab").objectReferenceValue = baseTab;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Окно паузы — под окнами драфта и выбора защитников, но над остальным интерфейсом
        win.SetSiblingIndex(canvas.Find("DraftWindow") != null ? canvas.Find("DraftWindow").GetSiblingIndex() : canvas.childCount - 1);

        BuildSummary(canvas, srcPortrait);
    }

    // ---------- Итоги фазы ----------

    /// <summary>
    /// Окно итогов фазы по центру экрана: золото, плутоний, очки фракции (+ заработано за фазу)
    /// и ряд портретов героев, повысивших уровень. Показывается поверх паузы, исчезает само.
    /// </summary>
    private static void BuildSummary(Transform canvas, HeroPortraitUI srcPortrait)
    {
        Transform old = canvas.Find("PhaseSummary");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var holder = new GameObject("PhaseSummary", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(canvas, false);
        Stretch(holder);

        // Root: затемнение на весь экран — клик по нему закрывает итоги
        var dim = Img("Root", holder, null, new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);
        var group = dim.gameObject.AddComponent<CanvasGroup>();
        var close = dim.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;

        // Панель по центру: жёлтая рамка, внутри — тёмная непрозрачная панель с содержимым
        var frame = Img("Frame", dim.rectTransform, rounded, new Color(1f, 0.85f, 0.4f));
        frame.type = Image.Type.Sliced;
        Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 560));
        frame.raycastTarget = false;
        var panel = Img("Panel", frame.rectTransform, rounded, new Color(0.08f, 0.1f, 0.16f, 1f));
        panel.type = Image.Type.Sliced;
        Stretch(panel.rectTransform, 6);
        panel.raycastTarget = false;

        var title = Text("Title", panel.rectTransform, "ИТОГИ ФАЗЫ", 44, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(900, 60));

        TMP_Text gold = null; // золота больше нет
        TMP_Text plut = SummaryValue(panel.rectTransform, "Plutonium", "Плутоний", new Color(0.25f, 0.3f, 0.85f), 220);
        TMP_Text fac = SummaryValue(panel.rectTransform, "FactionPoints", "Очки фракции", new Color(0.72f, 0.3f, 0.78f), -220);

        var header = Text("LevelHeader", panel.rectTransform, "Повысили уровень:", 26, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center, false);
        Place(header.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(900, 36));

        // Ряд портретов героев
        var list = new GameObject("Avatars", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        list.SetParent(panel.rectTransform, false);
        Place(list, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1040, 170));
        var h = list.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        // Шаблон: рамка, портрет, подпись
        var tpl = Img("AvatarTemplate", list, rounded, new Color(0.0f, 0.65f, 1f));
        tpl.type = Image.Type.Sliced;
        tpl.rectTransform.sizeDelta = new Vector2(130, 165);
        tpl.raycastTarget = false;
        var tplFill = Img("Fill", tpl.rectTransform, rounded, new Color(0.1f, 0.12f, 0.17f));
        tplFill.type = Image.Type.Sliced;
        Stretch(tplFill.rectTransform, 5);
        tplFill.raycastTarget = false;
        var portrait = Object.Instantiate(srcPortrait, tplFill.rectTransform);
        portrait.name = "Portrait";
        var prt = (RectTransform)portrait.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, -50);
        float s = 90f / Mathf.Max(1f, prt.rect.width);
        prt.localScale = new Vector3(s, s, 1f);
        foreach (Graphic g in portrait.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        var label = Text("Label", tplFill.rectTransform, "Герой\nУр. 1 → 2", 18, Color.white, TextAlignmentOptions.Center, true);
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(124, 56));

        var hint = Text("Hint", panel.rectTransform, "клик — закрыть", 18, new Color(0.55f, 0.6f, 0.7f), TextAlignmentOptions.Center, false);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(400, 26));

        var ui = holder.gameObject.AddComponent<PhaseSummaryUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("root").objectReferenceValue = dim.gameObject;
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("goldText").objectReferenceValue = gold;
        so.FindProperty("plutoniumText").objectReferenceValue = plut;
        so.FindProperty("factionText").objectReferenceValue = fac;
        so.FindProperty("levelHeader").objectReferenceValue = header;
        so.FindProperty("avatarList").objectReferenceValue = list;
        so.FindProperty("avatarTemplate").objectReferenceValue = tpl.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        dim.gameObject.SetActive(false);
        holder.SetAsLastSibling();
    }

    /// <summary>Ресурс в итогах: кольцо, название и число (вернёт текст числа).</summary>
    private static TMP_Text SummaryValue(RectTransform parent, string name, string title, Color color, float x)
    {
        var go = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        go.SetParent(parent, false);
        Place(go, new Vector2(0.5f, 0.5f), new Vector2(x, 90), new Vector2(320, 150));
        var ring = Img("Ring", go, circle, color);
        Place(ring.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(52, 52));
        ring.raycastTarget = false;
        var hole = Img("Hole", ring.rectTransform, circle, new Color(0.08f, 0.1f, 0.16f));
        Stretch(hole.rectTransform, 10);
        hole.raycastTarget = false;
        var t = Text("Title", go, title, 22, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Center, false);
        Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(300, 30));
        var v = Text("Value", go, "0", 44, Color.white, TextAlignmentOptions.Center, true);
        Place(v.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(320, 80));
        return v;
    }

    // ---------- Части окна ----------

    /// <summary>Ресурс сверху: цветное кольцо, название и число (вернёт текст числа).</summary>
    private static TMP_Text Resource(RectTransform parent, string name, string title, Color color, float x)
    {
        var go = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        go.SetParent(parent, false);
        Place(go, new Vector2(0.5f, 1f), new Vector2(x, -62), new Vector2(400, 80));
        var ring = Img("Ring", go, circle, color);
        Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(36, 0), new Vector2(64, 64));
        var hole = Img("Hole", ring.rectTransform, circle, new Color(0.04f, 0.06f, 0.10f));
        Stretch(hole.rectTransform, 12);
        var t = Text("Title", go, title, 22, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Left, false);
        Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(240, 18), new Vector2(300, 30));
        var v = Text("Value", go, "0", 38, Color.white, TextAlignmentOptions.Left, true);
        Place(v.rectTransform, new Vector2(0f, 0.5f), new Vector2(240, -16), new Vector2(300, 46));
        return v;
    }

    /// <summary>Кнопка вкладки (в чёрной рамке).</summary>
    private static Button TabBtn(RectTransform parent, string name, string label, float y)
    {
        var border = Img(name, parent, rounded, Color.black);
        border.type = Image.Type.Sliced;
        Place(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(240, 120));
        var face = Img("Face", border.rectTransform, rounded, Color.white);
        face.type = Image.Type.Sliced;
        Stretch(face.rectTransform, 7);
        var b = face.gameObject.AddComponent<Button>();
        Stretch(Text("Text", face.rectTransform, label, 34, Color.black, TextAlignmentOptions.Center, false).rectTransform);
        return b;
    }

    /// <summary>Пустая страница вкладки на всё поле.</summary>
    private static RectTransform Page(RectTransform parent, string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

    /// <summary>Иконка фракции: кольцо, заливка, галочка, текст и подпись.</summary>
    private static FactionIconUI Icon(RectTransform parent, int number, Vector2 pos, Vector2 size)
    {
        var ring = Img($"Icon{number}", parent, circle, Color.gray);
        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
        var button = ring.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.disabledColor = Color.white; // цвет задаёт сама иконка
        button.colors = colors;
        var fill = Img("Fill", ring.rectTransform, circle, Color.black);
        Stretch(fill.rectTransform, 12);
        fill.raycastTarget = false;

        // Галочка из двух полосок
        float k = size.x / 150f;
        var check = new GameObject("Check", typeof(RectTransform)).GetComponent<RectTransform>();
        check.SetParent(ring.rectTransform, false);
        Place(check, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100) * k);
        var a = Img("Short", check, null, Color.white);
        Place(a.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-20, -8) * k, new Vector2(46, 16) * k);
        a.rectTransform.localEulerAngles = new Vector3(0, 0, -45);
        var b = Img("Long", check, null, Color.white);
        Place(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(14, 6) * k, new Vector2(84, 16) * k);
        b.rectTransform.localEulerAngles = new Vector3(0, 0, 50);
        a.raycastTarget = b.raycastTarget = false;

        var label = Text("Label", ring.rectTransform, number.ToString(), 34 * Mathf.Min(k, 1.3f), Color.white, TextAlignmentOptions.Center, true);
        Stretch(label.rectTransform);
        var caption = Text("Caption", ring.rectTransform, "", 24, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center, false);
        Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -32), new Vector2(260, 44));

        var icon = ring.gameObject.AddComponent<FactionIconUI>();
        var so = new SerializedObject(icon);
        so.FindProperty("ring").objectReferenceValue = ring;
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("check").objectReferenceValue = check.gameObject;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("caption").objectReferenceValue = caption;
        so.FindProperty("button").objectReferenceValue = button;
        so.ApplyModifiedPropertiesWithoutUndo();
        return icon;
    }

    /// <summary>Овал героя (3★ фракции или лидер): цветная рамка, портрет, текст.</summary>
    private static void Oval(RectTransform parent, string name, Color color, Vector2 pos, HeroPortraitUI srcPortrait,
                             out Button button, out HeroPortraitUI portrait, out TMP_Text text, out CanvasGroup fade)
    {
        var ring = Img(name, parent, rounded, color);
        ring.type = Image.Type.Sliced;
        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(200, 400));
        button = ring.gameObject.AddComponent<Button>();
        var fill = Img("Fill", ring.rectTransform, rounded, new Color(0.1f, 0.12f, 0.17f));
        fill.type = Image.Type.Sliced;
        Stretch(fill.rectTransform, 9);
        fade = fill.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
        portrait = Object.Instantiate(srcPortrait, fill.rectTransform);
        portrait.name = "Portrait";
        var prt = (RectTransform)portrait.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, 75);
        float s = 150f / Mathf.Max(1f, prt.rect.width);
        prt.localScale = new Vector3(s, s, 1f);
        text = Text("Text", fill.rectTransform, "", 26, Color.white, TextAlignmentOptions.Center, true);
        Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -105), new Vector2(190, 150));
    }

    /// <summary>Прокручиваемый список героев (вернёт контейнер строк).</summary>
    private static RectTransform Scroll(RectTransform parent)
    {
        var view = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
        view.SetParent(parent, false);
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(14, 14);
        view.offsetMax = new Vector2(-14, -64);
        view.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f); // ловит колёсико мыши
        var content = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(view, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var v = content.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = false;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.GetComponent<ScrollRect>();
        sr.content = content;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30;
        return content;
    }

    /// <summary>
    /// Вкладка "База": копия панели обычного окна базы (5 ячеек, улучшение базы) без крестика,
    /// со своим BaseWindowUI в режиме "встроено".
    /// </summary>
    private static BaseWindowUI BaseTab(RectTransform parent, BaseWindowUI src)
    {
        Transform srcPanel = src.transform.Find("Root/Panel");
        var page = Object.Instantiate(srcPanel.gameObject, parent).GetComponent<RectTransform>();
        page.name = "BasePage";
        page.anchorMin = page.anchorMax = new Vector2(0.5f, 0.5f);
        page.pivot = new Vector2(0.5f, 0.5f);
        page.anchoredPosition = Vector2.zero;
        float s = Mathf.Min(1440f / page.rect.width, 630f / page.rect.height, 1f);
        page.localScale = new Vector3(s, s, 1f);
        Transform close = page.Find("CloseButton");
        if (close != null) Object.DestroyImmediate(close.gameObject);

        var tab = page.gameObject.AddComponent<BaseWindowUI>();
        var from = new SerializedObject(src);
        var to = new SerializedObject(tab);
        to.FindProperty("embedded").boolValue = true;
        to.FindProperty("root").objectReferenceValue = page.gameObject;
        foreach (string p in new[] { "header", "titleText", "subtitleText", "upgradeButton", "upgradeText" })
            to.FindProperty(p).objectReferenceValue = Twin(from.FindProperty(p).objectReferenceValue, srcPanel, page);
        var srcSlots = from.FindProperty("slotViews");
        var slots = to.FindProperty("slotViews");
        slots.arraySize = srcSlots.arraySize;
        for (int i = 0; i < srcSlots.arraySize; i++)
            slots.GetArrayElementAtIndex(i).objectReferenceValue = Twin(srcSlots.GetArrayElementAtIndex(i).objectReferenceValue, srcPanel, page);
        to.FindProperty("buildMenu").objectReferenceValue = from.FindProperty("buildMenu").objectReferenceValue;
        to.FindProperty("picker").objectReferenceValue = from.FindProperty("picker").objectReferenceValue;
        to.ApplyModifiedPropertiesWithoutUndo();
        page.gameObject.SetActive(false);
        return tab;
    }

    /// <summary>Тот же объект (компонент) в копии панели: ищем по пути от панели.</summary>
    private static Object Twin(Object original, Transform srcRoot, Transform copyRoot)
    {
        if (original == null) return null;
        Transform t = original is Component comp ? comp.transform : ((GameObject)original).transform;
        string path = "";
        for (Transform x = t; x != srcRoot; x = x.parent)
        {
            if (x == null) { Debug.LogWarning($"{original} не внутри панели базы"); return null; }
            path = path == "" ? x.name : x.name + "/" + path;
        }
        Transform twin = path == "" ? copyRoot : copyRoot.Find(path);
        if (twin == null) return null;
        return original is Component c2 ? (Object)twin.GetComponent(c2.GetType()) : twin.gameObject;
    }

    // ---------- Элементы ----------

    /// <summary>Записать массив ссылок.</summary>
    private static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    /// <summary>Кнопка с текстом.</summary>
    private static Button Btn(RectTransform parent, string name, string label, Color color, Vector2 pos, Vector2 size, float fontSize)
    {
        var img = Img(name, parent, rounded, color);
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
        var b = img.gameObject.AddComponent<Button>();
        Stretch(Text("Text", img.rectTransform, label, fontSize, Color.white, TextAlignmentOptions.Center, false).rectTransform);
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

    /// <summary>Растянуть на весь родитель (с отступом pad со всех сторон).</summary>
    private static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
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
