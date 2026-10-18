using System;
using UnityEngine;

/// <summary>Кадры одного скила: как двигается персонаж и (если есть) картинка снаряда/эффекта.</summary>
[Serializable]
public class SkillSprites
{
    [Tooltip("Кадры персонажа, пока он применяет скил (играют один раз)")]
    public Sprite[] frames = new Sprite[0];

    [Tooltip("Кадры снаряда или эффекта скила (например, волна). Пусто — обычный снаряд")]
    public Sprite[] effect = new Sprite[0];

    [Tooltip("Скорость кадров персонажа (кадров в секунду)")]
    public float fps = 10f;

    /// <summary>Есть ли кадры персонажа для этого скила.</summary>
    public bool HasFrames => frames != null && frames.Length > 0;

    /// <summary>Есть ли картинка снаряда/эффекта.</summary>
    public bool HasEffect => effect != null && effect.Length > 0;
}

/// <summary>
/// Спрайты персонажа для боя (покадровая анимация): покой, ходьба, бег, атака, скилы.
/// Один такой файл — "по умолчанию" (Data/Characters/Sprites_Default): его получают все герои,
/// у которых в файле героя поле "Спрайты" пустое. Когда персонажа нарисуют отдельно —
/// создайте для него свой файл (ПКМ → Create → Супергеройское бюро → Спрайты персонажа)
/// и укажите его в поле "Спрайты" у героя.
/// Кадры смотрят ВПРАВО (влево боец разворачивается сам).
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Спрайты персонажа", fileName = "Sprites_")]
public class CharacterSprites : ScriptableObject
{
    [Header("Кадры (смотрят вправо)")]
    [Tooltip("Покой — стоит на месте")]
    public Sprite[] idle = new Sprite[0];

    [Tooltip("Ходьба")]
    public Sprite[] walk = new Sprite[0];

    [Tooltip("Бег")]
    public Sprite[] run = new Sprite[0];

    [Tooltip("Авто атака (и скилы, у которых нет своих кадров). Можно один кадр")]
    public Sprite[] attack = new Sprite[0];

    [Header("Скилы (1–4 по порядку). Пусто — кадр атаки")]
    public SkillSprites[] skills = new SkillSprites[0];

    [Header("Скорость анимации (кадров в секунду)")]
    public float idleFps = 5f;
    public float walkFps = 10f;
    public float runFps = 14f;

    [Header("Размер и положение")]
    [Tooltip("Масштаб спрайта (1.7 — как дома на арене, PPU 32)")]
    public float scale = 1.7f;

    [Tooltip("Где ноги персонажа внутри кадра: сколько пикселей от центра кадра вниз до ступней (для кадра 64×64 с ногами у нижнего края — 32)")]
    public float feetBelowCenterPx = 32f;

    [Tooltip("Сдвиг по горизонтали в пикселях, если персонаж нарисован не по центру кадра")]
    public float offsetXPx = 0f;

    /// <summary>Кадры скила i (0..3) или null, если их нет.</summary>
    public SkillSprites GetSkill(int i) => skills != null && i >= 0 && i < skills.Length ? skills[i] : null;

    /// <summary>Кадр анимации по времени (по кругу).</summary>
    public static Sprite Frame(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0) return null;
        int i = Mathf.FloorToInt(time * fps) % frames.Length;
        return frames[Mathf.Clamp(i, 0, frames.Length - 1)];
    }

    /// <summary>Кадр анимации по времени (один раз: в конце остаётся последний кадр).</summary>
    public static Sprite FrameOnce(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0) return null;
        int i = Mathf.FloorToInt(time * fps);
        return frames[Mathf.Clamp(i, 0, frames.Length - 1)];
    }
}
