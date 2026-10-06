using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Экран конца игры: "ПОБЕДА!" (база врага разрушена) или "ПОРАЖЕНИЕ" (наша база разрушена).
/// Карта ставится на паузу. Кнопка "ИГРАТЬ ЗАНОВО" перезапускает сцену.
/// Вызов из любого скрипта: GameOverUI.Show(true / false).
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private static GameOverUI instance; // Единственный экземпляр в сцене

    [SerializeField] private GameObject root;       // Всё окно
    [SerializeField] private Image header;          // Полоса заголовка (зелёная/красная)
    [SerializeField] private TMP_Text titleText;    // "ПОБЕДА!" / "ПОРАЖЕНИЕ"
    [SerializeField] private TMP_Text messageText;  // Пояснение
    [SerializeField] private Button restartButton;  // "ИГРАТЬ ЗАНОВО"

    /// <summary>Закончилась ли игра.</summary>
    public static bool IsGameOver { get; private set; }

    private void Awake()
    {
        instance = this;
        IsGameOver = false;
        restartButton.onClick.AddListener(Restart);
        root.SetActive(false);
    }

    /// <summary>Показать итог игры. playerWon — победил ли игрок.</summary>
    public static void Show(bool playerWon)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        WorldTime.Paused = true; // карта замирает
        if (instance == null) { Debug.Log(playerWon ? "ПОБЕДА!" : "ПОРАЖЕНИЕ"); return; }

        instance.header.color = playerWon ? new Color(0.25f, 0.64f, 0.3f) : new Color(0.72f, 0.22f, 0.23f);
        instance.titleText.text = playerWon ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
        instance.messageText.text = playerWon
            ? "База врага разрушена.\nГород под защитой Супергеройского бюро!"
            : "Наша база разрушена.\nЗлодеи захватили город...";
        instance.root.SetActive(true);
        instance.transform.SetAsLastSibling();
    }

    /// <summary>Начать игру заново (перезагрузить сцену).</summary>
    private void Restart()
    {
        WorldTime.Paused = false;
        IsGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
