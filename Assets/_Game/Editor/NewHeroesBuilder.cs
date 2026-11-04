using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Шесть новых персонажей: Америка, Фризен (герои), Зед Рок, Тень, Шторм, Мозг (злодеи).
/// Меню: Tools → Superhero → Новые герои (6). Можно запускать повторно.
/// Делает:
///  - настройки картинок из Assets/My/Units/(имя): 32 пикселя на единицу, без размытия;
///  - файл кадров Data/Characters/Sprites_(имя): покой 4, ходьба 7, бег 7, атака 1 (как у Фаер Герл);
///  - в файле героя: портрет, кадры, набор навыков (kit), названия, описания и перезарядки скилов;
///  - добавляет героя в список Hero Pool (сцена Game), если его там нет.
/// Числа навыков (урон, длительности) — в файле героя, Skills → Values (по умолчанию — таблица KitParams).
/// </summary>
public static class NewHeroesBuilder
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity"; // Сцена карты (там HeroManager)

    /// <summary>Описание одного персонажа для сборки.</summary>
    private class Entry
    {
        public string data;      // Путь к файлу героя
        public string folder;    // Папка с артом
        public string sheet;     // Лист кадров (448×256)
        public string icon;      // Портрет 32×32
        public string sprites;   // Имя файла кадров
        public HeroKitType kit;  // Набор навыков
        public Skill[] skills;   // 3 скила
    }

    /// <summary>Скил: название, описание, перезарядка, пассивный ли.</summary>
    private class Skill
    {
        public string name, desc;
        public float cooldown;
        public bool passive;
        public Skill(string name, string desc, float cooldown, bool passive = false)
        {
            this.name = name; this.desc = desc; this.cooldown = cooldown; this.passive = passive;
        }
    }

    private static readonly Entry[] Entries =
    {
        new Entry
        {
            data = "Assets/_Game/Data/Heroes/Hero_America.asset", folder = "Assets/My/Units/America",
            sheet = "StandartNewWumen02.png", icon = "America.png", sprites = "Sprites_America", kit = HeroKitType.America,
            skills = new[]
            {
                new Skill("Энергетический бумеранг", "Метает кольцо на среднее расстояние, оно возвращается к Америке. Каждого врага на пути туда и обратно ранит (60% Силы атаки) и вводит в гроги.", 7f),
                new Skill("Энерго-апперкот", "Апперкот всем врагам вплотную: урон 90% Силы атаки и подбрасывает.", 8f),
                new Skill("Контратака", "В ближнем бою пропущенный удар не проходит — Америка сразу бьёт в ответ мощным ударом (100% Силы атаки), враг отлетает.", 6f, true),
            }
        },
        new Entry
        {
            data = "Assets/_Game/Data/Heroes/Hero_Frizen.asset", folder = "Assets/My/Units/Frizen",
            sheet = "StandartNewWumen03.png", icon = "Frizen.png", sprites = "Sprites_Frizen", kit = HeroKitType.Frizen,
            skills = new[]
            {
                new Skill("Ледяной шар", "Шар льда: урон 100% Магии и заморозка (стан) на 3 с.", 8f),
                new Skill("Ледяной смерч", "Голубой смерч движется вперёд и подхватывает врагов (80% Магии). Когда он кончается, замёрзшие враги падают и 2 с не двигаются.", 12f),
                new Skill("Ледяной покров", "Защита +30 и Спец. защита +30.", 0f, true),
            }
        },
        new Entry
        {
            data = "Assets/_Game/Data/Villains/Villain_ZedRock.asset", folder = "Assets/My/Units/ZedRock",
            sheet = "ZedRock.png", icon = "ZedRockicons.png", sprites = "Sprites_ZedRock", kit = HeroKitType.ZedRock,
            skills = new[]
            {
                new Skill("Тяжёлый рок", "Две ударные мелодии с интервалом 0.5 с, каждая бьёт первого, в кого влетит: 80% Магии.", 6f),
                new Skill("Звуковой удар", "Вокруг расходится звуковое кольцо: урон 70% Магии и подбрасывает всех врагов рядом.", 9f),
                new Skill("Энерго темп", "Союзникам рядом и себе +20% к скорости бега и авто атак на 3 с.", 12f),
            }
        },
        new Entry
        {
            data = "Assets/_Game/Data/Villains/Villain_Ten.asset", folder = "Assets/My/Units/Ten",
            sheet = "Ten.png", icon = "Tenicons.png", sprites = "Sprites_Ten", kit = HeroKitType.Ten,
            skills = new[]
            {
                new Skill("Колода", "Метает 3 карты друг за другом, каждая — 45% Силы атаки (после пик — 60%).", 5f),
                new Skill("Карта судьбы", "Над головой 2 с крутится карта: черви — +20% HP, бубны — 3 быстрые авто атаки, крести — +50% энергии, пики — следующая Колода сильнее.", 12f),
                new Skill("Монетка", "При смертельном ранении бросает монетку: решка — оживает с 20% HP, орёл — выбывает. Один раз за бой.", 0f, true),
            }
        },
        new Entry
        {
            data = "Assets/_Game/Data/Villains/Villain_Shtorm.asset", folder = "Assets/My/Units/Shtorm",
            sheet = "StandartNewWumen01.png", icon = "Shtorm.png", sprites = "Sprites_Shtorm", kit = HeroKitType.Shtorm,
            skills = new[]
            {
                new Skill("Удар молнии", "2–5 разрядов молнии по врагам рядом, каждый — 50% Магии и гроги.", 8f),
                new Skill("Шаровая молния", "Самонаводящийся шар: 120% Магии и отбрасывает.", 7f),
                new Skill("Разряд", "Разряд вокруг Шторм: 80% Магии и гроги всем врагам рядом.", 8f),
            }
        },
        new Entry
        {
            data = "Assets/_Game/Data/Villains/Villain_Mozg.asset", folder = "Assets/My/Units/Mozg",
            sheet = "Mozg.png", icon = "Mozgicons.png", sprites = "Sprites_Mozg", kit = HeroKitType.Mozg,
            skills = new[]
            {
                new Skill("Щупальца", "Бьёт длинными щупальцами вперёд на среднее расстояние: отбрасывает всех задетых, урон 100% Силы атаки.", 6f),
                new Skill("Паразит", "Когда враг выбывает, к нему летит паразит и поднимает его зомби на стороне Мозга до конца боя (50% характеристик, скорость — как была).", 0f, true),
                new Skill("Интеллект", "За каждого паразита +40 к Магии до конца боя.", 0f, true),
            }
        },
    };

    /// <summary>Собрать всех шестерых.</summary>
    [MenuItem("Tools/Superhero/Новые герои (6)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Выйдите из Play"); return; }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        var pool = new List<HeroData>();
        foreach (Entry e in Entries)
        {
            HeroData d = AssetDatabase.LoadAssetAtPath<HeroData>(e.data);
            if (d == null) { Debug.LogWarning($"Нет файла {e.data}"); continue; }
            ImportPixelArt($"{e.folder}/{e.sheet}", false);
            ImportPixelArt($"{e.folder}/{e.icon}", true);
            d.portrait = FirstSprite($"{e.folder}/{e.icon}");
            d.sprites = MakeSprites(e);
            d.kit = e.kit;
            if (d.skills == null || d.skills.Length < 3) d.skills = new HeroSkill[3];
            for (int i = 0; i < 3; i++)
            {
                if (d.skills[i] == null) d.skills[i] = new HeroSkill();
                d.skills[i].name = e.skills[i].name;
                d.skills[i].description = e.skills[i].desc;
                d.skills[i].cooldown = e.skills[i].cooldown;
                d.skills[i].passive = e.skills[i].passive;
            }
            KitParams.Sync(d); // числа навыков — значения по умолчанию из таблицы
            EditorUtility.SetDirty(d);
            pool.Add(d);
        }
        AddToPool(pool);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Новые герои собраны: " + pool.Count);
    }

    /// <summary>Пиксель-арт: 32 пикселя на единицу, без размытия и сжатия. single — одна картинка (портрет), иначе — лист кадров.</summary>
    private static void ImportPixelArt(string path, bool single)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning($"Нет картинки {path}"); return; }
        ti.textureType = TextureImporterType.Sprite;
        if (single) ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    /// <summary>Первый спрайт картинки (портрет).</summary>
    private static Sprite FirstSprite(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        return null;
    }

    /// <summary>
    /// Файл кадров персонажа: лист 448×256 нарезан на кадры 64×64 (19 штук).
    /// Строки сверху вниз: покой (4), ходьба (7), бег (7), атака (1).
    /// </summary>
    private static CharacterSprites MakeSprites(Entry e)
    {
        string path = $"Assets/_Game/Data/Characters/{e.sprites}.asset";
        var cs = AssetDatabase.LoadAssetAtPath<CharacterSprites>(path);
        if (cs == null)
        {
            cs = ScriptableObject.CreateInstance<CharacterSprites>();
            AssetDatabase.CreateAsset(cs, path);
        }
        var frames = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"{e.folder}/{e.sheet}"))
            if (o is Sprite s) frames.Add(s);
        // Сверху вниз (у Unity y растёт вверх), слева направо
        frames.Sort((a, b) => Mathf.Approximately(a.rect.y, b.rect.y) ? a.rect.x.CompareTo(b.rect.x) : b.rect.y.CompareTo(a.rect.y));
        cs.idle = Row(frames, 0);
        cs.walk = Row(frames, 1);
        cs.run = Row(frames, 2);
        cs.attack = Row(frames, 3);
        cs.scale = 1.7f;
        cs.feetBelowCenterPx = 32f;
        EditorUtility.SetDirty(cs);
        if (cs.idle.Length != 4 || cs.walk.Length != 7 || cs.run.Length != 7 || cs.attack.Length < 1)
            Debug.LogWarning($"{e.sprites}: кадров покой {cs.idle.Length}, ходьба {cs.walk.Length}, бег {cs.run.Length}, атака {cs.attack.Length} — проверьте нарезку листа");
        return cs;
    }

    /// <summary>Кадры строки row (0 — верхняя) листа 64×64.</summary>
    private static Sprite[] Row(List<Sprite> sorted, int row)
    {
        if (sorted.Count == 0) return new Sprite[0];
        float top = sorted[0].rect.y;
        var list = new List<Sprite>();
        foreach (Sprite s in sorted)
            if (Mathf.Abs(s.rect.y - (top - row * 64f)) < 1f) list.Add(s);
        return list.ToArray();
    }

    /// <summary>Добавить героев в список Hero Pool у HeroManager (сцена Game), кого там нет.</summary>
    private static void AddToPool(List<HeroData> heroes)
    {
        var hm = Object.FindAnyObjectByType<HeroManager>(FindObjectsInactive.Include);
        if (hm == null) { Debug.LogWarning("Нет HeroManager в сцене Game"); return; }
        var so = new SerializedObject(hm);
        var list = so.FindProperty("heroPool");
        foreach (HeroData h in heroes)
        {
            bool found = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == h) found = true;
            if (found) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = h;
            Debug.Log($"В Hero Pool добавлен {h.displayName}");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
