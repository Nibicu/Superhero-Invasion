using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка героя (сцена Game, окно HeroInfoWindow): значки у характеристик, строка опыта сверху,
/// кнопка усиления становится кнопкой "ПОВЫСИТЬ ЗВЕЗДУ". Повторный запуск безопасен.
/// Меню: Tools → Superhero → Обновить карточку героя.
/// </summary>
public static class HeroCardBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты

    /// <summary>Обновить карточку героя.</summary>
    [MenuItem("Tools/Superhero/Обновить карточку героя")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        var window = Object.FindAnyObjectByType<HeroInfoWindowUI>(FindObjectsInactive.Include);
        var so = new SerializedObject(window);
        var rowsProp = so.FindProperty("statRows");
        Transform panel = window.transform.Find("Root/Panel");

        // Ряды характеристик: шаг 28, сверху освобождаем место под опыт
        const float top = 198f, step = 28f;
        StatRowUI first = null;
        for (int i = 0; i < rowsProp.arraySize; i++)
        {
            var row = (StatRowUI)rowsProp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (i == 0) first = row;
            ((RectTransform)row.transform).anchoredPosition = new Vector2(200f, top - i * step);
            AddIcon(row);
        }

        // Строка опыта — копия первой строки, над характеристиками
        Transform oldXp = panel.Find("XpRow");
        if (oldXp != null) Object.DestroyImmediate(oldXp.gameObject);
        var xp = Object.Instantiate(first, panel);
        xp.name = "XpRow";
        ((RectTransform)xp.transform).anchoredPosition = new Vector2(200f, top + step + 4f);
        var xpFill = xp.transform.Find("BarFill");
        if (xpFill != null) xpFill.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.75f);
        var label = (RectTransform)panel.Find("StatsLabel");
        if (label != null) label.anchoredPosition = new Vector2(200f, top + 2 * step + 12f);

        // Значки
        so.FindProperty("xpRow").objectReferenceValue = xp;
        so.FindProperty("xpIcon").objectReferenceValue = StatIconsBuilder.Load(StatIconsBuilder.XpFile);
        var icons = so.FindProperty("statIcons");
        icons.arraySize = StatIconsBuilder.StatFiles.Length;
        for (int i = 0; i < StatIconsBuilder.StatFiles.Length; i++)
            icons.GetArrayElementAtIndex(i).objectReferenceValue = StatIconsBuilder.Load(StatIconsBuilder.StatFiles[i]);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Кнопка "ПОВЫСИТЬ ЗВЕЗДУ" (бывшая "УСИЛИТЬ") — золотая
        var boost = panel.Find("BoostButton");
        if (boost != null) boost.GetComponent<Image>().color = new Color(0.85f, 0.62f, 0.12f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Карточка героя обновлена");
    }

    /// <summary>Добавить значок слева от названия характеристики (если его ещё нет) и подвинуть название.</summary>
    private static void AddIcon(StatRowUI row)
    {
        Transform t = row.transform.Find("Icon");
        Image icon;
        if (t == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row.transform, false);
            icon = go.GetComponent<Image>();
        }
        else icon = t.GetComponent<Image>();
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(-365f, 0f);
        irt.sizeDelta = new Vector2(24f, 24f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var label = (RectTransform)row.transform.Find("Label");
        if (label != null)
        {
            label.anchoredPosition = new Vector2(-232f, 0f);
            label.sizeDelta = new Vector2(230f, 32f);
            var txt = label.GetComponent<TMP_Text>();
            if (txt != null) txt.alignment = TextAlignmentOptions.Left;
        }
        var so = new SerializedObject(row);
        so.FindProperty("icon").objectReferenceValue = icon;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
