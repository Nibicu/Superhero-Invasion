using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главное меню (сцена MainMenu): Кампания (пока заглушка), Песочница, Энциклопедия (пока заглушка),
/// Опции и Выход. Экраны — панели внутри одного холста, показывается одна за раз.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Экраны")]
    [SerializeField] private GameObject mainPanel;    // Кнопки главного меню
    [SerializeField] private GameObject sandboxPanel; // Выбор карты, стороны и фракции
    [SerializeField] private GameObject optionsPanel; // Опции

    [Header("Кнопки главного меню")]
    [SerializeField] private Button campaignButton;     // "КАМПАНИЯ" (скоро)
    [SerializeField] private Button sandboxButton;      // "ПЕСОЧНИЦА"
    [SerializeField] private Button encyclopediaButton; // "ЭНЦИКЛОПЕДИЯ" (скоро)
    [SerializeField] private Button optionsButton;      // "ОПЦИИ"
    [SerializeField] private Button quitButton;         // "ВЫХОД"

    [Header("Кнопки «Назад»")]
    [SerializeField] private Button sandboxBack; // Из Песочницы
    [SerializeField] private Button optionsBack; // Из Опций

    [Header("Подсказка")]
    [SerializeField] private TMP_Text toast;     // Короткое сообщение внизу ("Скоро!")

    private Coroutine toastRoutine; // Таймер скрытия подсказки

    /// <summary>Подписать кнопки и открыть главный экран.</summary>
    private void Awake()
    {
        campaignButton.onClick.AddListener(() => ShowToast("Кампания — скоро!"));
        encyclopediaButton.onClick.AddListener(() => ShowToast("Энциклопедия — скоро!"));
        sandboxButton.onClick.AddListener(() => Open(sandboxPanel));
        optionsButton.onClick.AddListener(() => Open(optionsPanel));
        quitButton.onClick.AddListener(Application.Quit);
        sandboxBack.onClick.AddListener(() => Open(mainPanel));
        optionsBack.onClick.AddListener(() => Open(mainPanel));
        toast.gameObject.SetActive(false);
        Time.timeScale = 1f;
        Open(mainPanel);
    }

    /// <summary>Esc — назад в главное меню.</summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !mainPanel.activeSelf) Open(mainPanel);
    }

    /// <summary>Показать один экран, остальные спрятать.</summary>
    private void Open(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        sandboxPanel.SetActive(panel == sandboxPanel);
        optionsPanel.SetActive(panel == optionsPanel);
    }

    /// <summary>Короткое сообщение внизу экрана на 2 секунды.</summary>
    private void ShowToast(string text)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(text));
    }

    /// <summary>Показать сообщение и спрятать через 2 секунды.</summary>
    private IEnumerator ToastRoutine(string text)
    {
        toast.text = text;
        toast.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(2f);
        toast.gameObject.SetActive(false);
    }
}
