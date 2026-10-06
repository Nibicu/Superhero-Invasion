using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Общая основа для окон (РОСТЕР, МОИ ГЕРОИ, карточка героя...).
/// Умеет: открываться/закрываться, закрываться крестиком, кликом по
/// затемнённому фону и клавишей Esc (Esc закрывает только верхнее окно).
/// Конкретные окна наследуются от этого класса и переопределяют Refresh().
/// </summary>
public class WindowUI : MonoBehaviour
{
    [Header("Основа окна")]
    [Tooltip("Всё окно (фон + панель). Включается при открытии")]
    [SerializeField] protected GameObject root;
    [Tooltip("Крестик")]
    [SerializeField] private Button closeButton;
    [Tooltip("Затемнённый фон — клик по нему закрывает окно")]
    [SerializeField] private Button backdropButton;

    private static readonly List<WindowUI> openWindows = new List<WindowUI>(); // Открытые окна, последнее — верхнее
    private static int lastEscFrame = -1; // Кадр, в котором уже обработали Esc (чтобы закрыть только одно окно)

    /// <summary>Открыто ли окно.</summary>
    public bool IsOpen => root != null && root.activeSelf;

    /// <summary>Подписываем кнопки и прячем окно при старте.</summary>
    protected virtual void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        root.SetActive(false);
    }

    /// <summary>Esc закрывает самое верхнее открытое окно.</summary>
    protected virtual void Update()
    {
        if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (lastEscFrame == Time.frameCount) return;
        if (openWindows.Count > 0 && openWindows[openWindows.Count - 1] != this) return;
        lastEscFrame = Time.frameCount;
        Close();
    }

    /// <summary>Открыть окно (и поднять его поверх остальных).</summary>
    public virtual void Open()
    {
        root.SetActive(true);
        transform.SetAsLastSibling(); // рисуется поверх других окон
        openWindows.Remove(this);
        openWindows.Add(this);
        Refresh();
    }

    /// <summary>Закрыть окно.</summary>
    public virtual void Close()
    {
        root.SetActive(false);
        openWindows.Remove(this);
    }

    /// <summary>Перерисовать содержимое. Каждое окно делает это по-своему.</summary>
    public virtual void Refresh() { }

    /// <summary>Убираем окно из списка при удалении.</summary>
    protected virtual void OnDestroy() => openWindows.Remove(this);
}
