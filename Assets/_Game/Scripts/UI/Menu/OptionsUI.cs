using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран "Опции": разрешение, режим экрана, качество графики, вертикальная синхронизация
/// и громкость (общая, музыка, эффекты). Изменения применяются и сохраняются сразу (GameSettings).
/// </summary>
public class OptionsUI : MonoBehaviour
{
    [Header("Графика")]
    [SerializeField] private OptionSelectorUI resolution; // Разрешение
    [SerializeField] private OptionSelectorUI screenMode; // Полный экран / Окно
    [SerializeField] private OptionSelectorUI quality;    // Качество графики
    [SerializeField] private OptionSelectorUI vsync;      // Вертикальная синхронизация

    [Header("Звук")]
    [SerializeField] private Slider masterVolume;   // Общая громкость
    [SerializeField] private Slider musicVolume;    // Музыка
    [SerializeField] private Slider effectsVolume;  // Эффекты
    [SerializeField] private TMP_Text masterValue;  // Подпись "80%" у общей громкости
    [SerializeField] private TMP_Text musicValue;   // Подпись у музыки
    [SerializeField] private TMP_Text effectsValue; // Подпись у эффектов

    private List<Vector2Int> resolutions; // Доступные разрешения

    /// <summary>Каждый раз при открытии — показать текущие настройки.</summary>
    private void OnEnable() => Fill();

    /// <summary>Заполнить строки и ползунки текущими значениями.</summary>
    private void Fill()
    {
        GameSettings.Load();

        resolutions = GameSettings.Resolutions();
        var names = new string[resolutions.Count];
        int current = resolutions.Count - 1;
        for (int i = 0; i < resolutions.Count; i++)
        {
            names[i] = $"{resolutions[i].x} × {resolutions[i].y}";
            if (resolutions[i].x == GameSettings.Width && resolutions[i].y == GameSettings.Height) current = i;
        }
        resolution.Setup(names, current, i =>
        {
            GameSettings.Width = resolutions[i].x;
            GameSettings.Height = resolutions[i].y;
            ApplyAndSave();
        });

        screenMode.Setup(new[] { "Полный экран", "Окно" }, GameSettings.Fullscreen ? 0 : 1, i =>
        {
            GameSettings.Fullscreen = i == 0;
            ApplyAndSave();
        });

        quality.Setup(QualityNames(), GameSettings.Quality, i =>
        {
            GameSettings.Quality = i;
            ApplyAndSave();
        });

        vsync.Setup(new[] { "Вкл", "Выкл" }, GameSettings.VSync ? 0 : 1, i =>
        {
            GameSettings.VSync = i == 0;
            ApplyAndSave();
        });

        SetupSlider(masterVolume, masterValue, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        SetupSlider(musicVolume, musicValue, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        SetupSlider(effectsVolume, effectsValue, GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v);
    }

    /// <summary>Названия уровней качества по-русски (по порядку из Project Settings → Quality).</summary>
    private static string[] QualityNames()
    {
        string[] src = QualitySettings.names;
        var result = new string[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            switch (src[i])
            {
                case "Very Low": result[i] = "Очень низкое"; break;
                case "Low": result[i] = "Низкое"; break;
                case "Medium": result[i] = "Среднее"; break;
                case "High": result[i] = "Высокое"; break;
                case "Very High": result[i] = "Очень высокое"; break;
                case "Ultra": result[i] = "Ультра"; break;
                default: result[i] = src[i]; break;
            }
        }
        return result;
    }

    /// <summary>Настроить ползунок громкости: значение, подпись в процентах, сохранение при движении.</summary>
    private void SetupSlider(Slider slider, TMP_Text label, float value, System.Action<float> set)
    {
        slider.onValueChanged.RemoveAllListeners();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        slider.onValueChanged.AddListener(v =>
        {
            set(v);
            label.text = $"{Mathf.RoundToInt(v * 100f)}%";
            ApplyAndSave();
        });
    }

    /// <summary>Применить и запомнить.</summary>
    private static void ApplyAndSave()
    {
        GameSettings.Apply();
        GameSettings.Save();
    }
}
