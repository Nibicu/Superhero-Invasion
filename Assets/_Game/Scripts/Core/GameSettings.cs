using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Технические настройки и звук (раздел "Опции" главного меню).
/// Хранятся на компьютере игрока (PlayerPrefs) и применяются при запуске игры.
/// Громкость музыки и эффектов пока только запоминается — звуков в игре ещё нет.
/// </summary>
public static class GameSettings
{
    private const string KeyResolution = "opt_resolution"; // Ключ: разрешение ("1920x1080")
    private const string KeyFullscreen = "opt_fullscreen"; // Ключ: полный экран
    private const string KeyQuality = "opt_quality";       // Ключ: качество графики
    private const string KeyVSync = "opt_vsync";           // Ключ: вертикальная синхронизация
    private const string KeyMaster = "opt_master";         // Ключ: общая громкость
    private const string KeyMusic = "opt_music";           // Ключ: громкость музыки
    private const string KeyEffects = "opt_effects";       // Ключ: громкость эффектов

    /// <summary>Ширина экрана.</summary>
    public static int Width { get; set; }

    /// <summary>Высота экрана.</summary>
    public static int Height { get; set; }

    /// <summary>Полный экран (иначе — окно).</summary>
    public static bool Fullscreen { get; set; } = true;

    /// <summary>Уровень качества графики (номер из Project Settings → Quality).</summary>
    public static int Quality { get; set; }

    /// <summary>Вертикальная синхронизация (без рывков картинки).</summary>
    public static bool VSync { get; set; } = true;

    /// <summary>Общая громкость 0..1.</summary>
    public static float MasterVolume { get; set; } = 1f;

    /// <summary>Громкость музыки 0..1.</summary>
    public static float MusicVolume { get; set; } = 0.8f;

    /// <summary>Громкость эффектов 0..1.</summary>
    public static float EffectsVolume { get; set; } = 0.8f;

    /// <summary>Загрузить настройки и применить их при запуске игры.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Load();
        Apply();
    }

    /// <summary>Доступные разрешения экрана (без повторов, от меньшего к большему).</summary>
    public static List<Vector2Int> Resolutions()
    {
        var list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    /// <summary>Прочитать настройки (если их ещё нет — текущие значения экрана и игры).</summary>
    public static void Load()
    {
        Width = Screen.currentResolution.width;
        Height = Screen.currentResolution.height;
        string res = PlayerPrefs.GetString(KeyResolution, "");
        string[] parts = res.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h)) { Width = w; Height = h; }
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        Quality = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        VSync = PlayerPrefs.GetInt(KeyVSync, 1) == 1;
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        EffectsVolume = PlayerPrefs.GetFloat(KeyEffects, 0.8f);
    }

    /// <summary>Сохранить настройки на компьютере игрока.</summary>
    public static void Save()
    {
        PlayerPrefs.SetString(KeyResolution, $"{Width}x{Height}");
        PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyQuality, Quality);
        PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
        PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
        PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
        PlayerPrefs.SetFloat(KeyEffects, EffectsVolume);
        PlayerPrefs.Save();
    }

    /// <summary>Применить настройки: экран, качество, синхронизация, громкость.</summary>
    public static void Apply()
    {
        if (!Application.isEditor && Width > 0 && Height > 0) // в редакторе размер окна задаёт сам редактор
            Screen.SetResolution(Width, Height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        if (Quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(Quality, true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        AudioListener.volume = MasterVolume;
    }
}
