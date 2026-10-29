using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Рисует временные пиксельные значки характеристик 16×16 (Assets/_Game/Art/Icons/Stats/*.png):
/// опыт (мозг), здоровье (сердце), реген HP (зелёный крест), авто атака (кулак), сила атаки (меч),
/// магия (книга), защита (щит), спец. защита (шлем с синим контуром), энергия (огонёк),
/// реген энергии (капля), скорость (ботинок). Файлы можно заменить своими рисунками с теми же именами.
/// Меню: Tools → Superhero → Нарисовать значки характеристик.
/// </summary>
public static class StatIconsBuilder
{
    public const string Folder = "Assets/_Game/Art/Icons/Stats"; // Куда сохранять значки

    /// <summary>Имена файлов значков по порядку HeroStats.Names (без опыта).</summary>
    public static readonly string[] StatFiles =
        { "Hp", "HpRegen", "AutoAttack", "Attack", "Magic", "Defense", "SpecialDefense", "Energy", "EnergyRegen", "Speed" };

    /// <summary>Файл значка опыта.</summary>
    public const string XpFile = "Xp";

    /// <summary>Нарисовать все значки.</summary>
    [MenuItem("Tools/Superhero/Нарисовать значки характеристик")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        var c = new Dictionary<char, Color32>();

        Save(XpFile, new[] {
            "................",
            "....XXXXXXXX....",
            "...XPPPPXPPPX...",
            "..XPPXPPXPPXPX..",
            "..XPPPPPXPPPPX..",
            ".XPXPPPPXPPXPPX.",
            ".XPPPXPPXPPPPPX.",
            ".XPPPPPPXPXPPPX.",
            ".XPPXPPPXPPPXPX.",
            "..XPPPPPXPPPPX..",
            "..XPPXPPXPPXPX..",
            "...XPPPPXPPPX...",
            "....XXXXXXXX....",
            "......XPPX......",
            "......XXXX......",
            "................" }, Pal('X', "7a2a4a", 'P', "f28fb6"));

        Save("Hp", new[] {
            "................",
            "..XXX......XXX..",
            ".XRRRX....XRRRX.",
            "XRRWRRX..XRRRRRX",
            "XRWRRRRXXRRRRRRX",
            "XRRRRRRRRRRRRRRX",
            "XRRRRRRRRRRRRRRX",
            ".XRRRRRRRRRRRRX.",
            "..XRRRRRRRRRRX..",
            "...XRRRRRRRRX...",
            "....XRRRRRRX....",
            ".....XRRRRX.....",
            "......XRRX......",
            ".......XX.......",
            "................",
            "................" }, Pal('X', "5a0f14", 'R', "e23b3b", 'W', "ffd0d0"));

        Save("HpRegen", new[] {
            "................",
            "................",
            "......XXXX......",
            "......XGGX......",
            "......XGGX......",
            "......XGGX......",
            "..XXXXXGGXXXXX..",
            "..XGGGGGGGGGGX..",
            "..XGGGGGGGGGGX..",
            "..XXXXXGGXXXXX..",
            "......XGGX......",
            "......XGGX......",
            "......XGGX......",
            "......XXXX......",
            "................",
            "................" }, Pal('X', "135a22", 'G', "44d45a"));

        Save("AutoAttack", new[] {
            "................",
            "....XXXXXXXX....",
            "...XSSXSSXSSX...",
            "...XSSXSSXSSXX..",
            "..XXSSXSSXSSXSX.",
            ".XSXSSSSSSSSXSX.",
            ".XSSSSSSSSSSSSX.",
            ".XSSSSSSSSSSSSX.",
            ".XSSSSSSSSSSSX..",
            "..XSSSSSSSSSSX..",
            "...XSSSSSSSSX...",
            "...XSSSSSSSSX...",
            "...XXXXXXXXXX...",
            "...XBBBBBBBBX...",
            "...XXXXXXXXXX...",
            "................" }, Pal('X', "5c3420", 'S', "f0b98a", 'B', "3d6fd1"));

        Save("Attack", new[] {
            ".............XX.",
            "............XWWX",
            "...........XWWX.",
            "..........XWWX..",
            ".........XWWX...",
            "........XWWX....",
            ".......XWWX.....",
            "..X...XWWX......",
            "..XX.XWWX.......",
            "...XXWWX........",
            "....XHX.........",
            "...XHXXX........",
            "..XHX..XX.......",
            ".XHX............",
            ".XX.............",
            "................" }, Pal('X', "2b3340", 'W', "dfe6ee", 'H', "8a5a2b"));

        Save("Magic", new[] {
            "................",
            "..XXXXXXXXXXXX..",
            "..XPPPPPPPPPPX..",
            "..XPPYYYYYYPPX..",
            "..XPPPPPPPPPPX..",
            "..XPPPPYYPPPPX..",
            "..XPPPYYYYPPPX..",
            "..XPPPPYYPPPPX..",
            "..XPPPPPPPPPPX..",
            "..XPPPPPPPPPPX..",
            "..XPPPPPPPPPPX..",
            "..XXXXXXXXXXXX..",
            "..XWWWWWWWWWWX..",
            "..XXXXXXXXXXXX..",
            "................",
            "................" }, Pal('X', "2a1650", 'P', "7b4fd6", 'Y', "ffd84a", 'W', "f2efe6"));

        Save("Defense", new[] {
            "................",
            "..XXXXXXXXXXXX..",
            "..XGGGGGGGGGGX..",
            "..XGLLLLLLLLGX..",
            "..XGLLLLLLLLGX..",
            "..XGLLLLLLLLGX..",
            "..XGLLLLLLLLGX..",
            "..XGLLLLLLLLGX..",
            "...XGLLLLLLGX...",
            "...XGLLLLLLGX...",
            "....XGLLLLGX....",
            ".....XGLLGX.....",
            "......XGGX......",
            ".......XX.......",
            "................",
            "................" }, Pal('X', "2b3340", 'G', "8592a6", 'L', "c9d3df"));

        Save("SpecialDefense", new[] {
            "................",
            ".....BBBBBB.....",
            "...BBMMMMMMBB...",
            "..BMMMMMMMMMMB..",
            "..BMMLMMMMMMMB..",
            ".BMMLMMMMMMMMMB.",
            ".BMMMMMMMMMMMMB.",
            ".BMMMMMMMMMMMMB.",
            ".BMMBBBBBBBBMMB.",
            ".BMMB......BMMB.",
            ".BMMB......BMMB.",
            ".BMMMB....BMMMB.",
            ".BBBBB....BBBBB.",
            "................",
            "................",
            "................" }, Pal('B', "3fa7ff", 'M', "8d97a6", 'L', "dfe6ee"));

        Save("Energy", new[] {
            ".......X........",
            "......XOX.......",
            "......XOOX......",
            ".....XOOOX......",
            "....XOOYOOX.....",
            "...XOOOYYOOX....",
            "...XOOYYYOOX....",
            "..XOOYYYYYOOX...",
            "..XOOYYWYYOOX...",
            "..XOOYYWWYYOX...",
            "..XOOYYWWYYOX...",
            "...XOOYYYYOX....",
            "....XOOOOOX.....",
            ".....XXXXX......",
            "................",
            "................" }, Pal('X', "7a1e05", 'O', "ff7a1a", 'Y', "ffd23a", 'W', "fff4b0"));

        Save("EnergyRegen", new[] {
            ".......XX.......",
            ".......XX.......",
            "......XBBX......",
            "......XBBX......",
            ".....XBBBBX.....",
            ".....XBBBBX.....",
            "....XBBBBBBX....",
            "...XBWBBBBBBX...",
            "...XBWBBBBBBX...",
            "..XBWBBBBBBBBX..",
            "..XBBBBBBBBBBX..",
            "..XBBBBBBBBBBX..",
            "...XBBBBBBBBX...",
            "....XBBBBBBX....",
            ".....XXXXXX.....",
            "................" }, Pal('X', "123a6b", 'B', "3fa7ff", 'W', "e6f4ff"));

        Save("Speed", new[] {
            "................",
            "....XXXXX.......",
            "....XBBBX.......",
            "....XBBBX.......",
            "....XBBBX.......",
            "....XBBBX.......",
            "....XBBBX.......",
            "....XBBBX.......",
            "....XBBBBXXXX...",
            "....XBBBBBBBBX..",
            "...XBBBBBBBBBBX.",
            "...XBBBBBBBBBBX.",
            "...XXXXXXXXXXXX.",
            "...XSSSSSSSSSSX.",
            "...XXXXXXXXXXXX.",
            "................" }, Pal('X', "2a1808", 'B', "8a5a2b", 'S', "4a2c14"));

        AssetDatabase.Refresh();
        foreach (string f in AllFiles())
        {
            string path = $"{Folder}/{f}.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 16;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        Debug.Log("Значки характеристик нарисованы: " + Folder);
    }

    /// <summary>Все файлы значков (опыт + характеристики).</summary>
    public static IEnumerable<string> AllFiles()
    {
        yield return XpFile;
        foreach (string f in StatFiles) yield return f;
    }

    /// <summary>Загрузить значок по имени файла.</summary>
    public static Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{file}.png");

    /// <summary>Палитра: пары (символ, цвет "rrggbb").</summary>
    private static Dictionary<char, Color32> Pal(params object[] pairs)
    {
        var d = new Dictionary<char, Color32>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            string hex = (string)pairs[i + 1];
            d[(char)pairs[i]] = new Color32(
                System.Convert.ToByte(hex.Substring(0, 2), 16),
                System.Convert.ToByte(hex.Substring(2, 2), 16),
                System.Convert.ToByte(hex.Substring(4, 2), 16), 255);
        }
        return d;
    }

    /// <summary>Нарисовать значок по 16 строкам (сверху вниз; '.' — прозрачно) и сохранить PNG.</summary>
    private static void Save(string file, string[] rows, Dictionary<char, Color32> palette)
    {
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char ch = x < rows[y].Length ? rows[y][x] : '.';
                tex.SetPixel(x, 15 - y, palette.TryGetValue(ch, out Color32 col) ? col : new Color32(0, 0, 0, 0));
            }
        tex.Apply();
        File.WriteAllBytes($"{Folder}/{file}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
