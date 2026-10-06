using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Верхняя панель ресурсов: золото, плутоний, доход за начисление
/// и полоска-таймер до следующего начисления.
/// При получении дохода над золотом всплывает надпись "+300".
/// Все ссылки назначаются в инспекторе (объект Canvas/TopBar).
/// </summary>
public class TopBarUI : MonoBehaviour
{
    [Header("Тексты")]
    [SerializeField] private TMP_Text goldText;      // Сколько золота у игрока
    [SerializeField] private TMP_Text plutoniumText; // Сколько плутония у игрока
    [SerializeField] private TMP_Text incomeText;    // Доход за начисление, например "+300  +10"
    [SerializeField] private TMP_Text timerText;     // Секунды до следующего начисления

    [Header("Таймер дохода")]
    [SerializeField] private Image timerFill; // Полоска, заполняется за 10 секунд (Image Type = Filled)

    [Header("Всплывающий доход")]
    [SerializeField] private TMP_Text popupText;          // Надпись "+300", всплывает при начислении
    [SerializeField] private float popupDuration = 1.2f;  // Сколько секунд видна надпись
    [SerializeField] private float popupRise = 40f;       // На сколько пикселей поднимается вверх

    private Vector2 popupStartPos;  // Исходная позиция всплывающей надписи
    private Coroutine popupRoutine; // Текущая анимация всплывания (чтобы перезапускать)

    /// <summary>Подписываемся на события менеджера ресурсов.</summary>
    private void Start()
    {
        if (popupText != null)
        {
            popupStartPos = popupText.rectTransform.anchoredPosition;
            popupText.gameObject.SetActive(false);
        }

        ResourceManager rm = ResourceManager.Instance;
        if (rm == null)
        {
            Debug.LogError("TopBarUI: в сцене нет ResourceManager!");
            enabled = false;
            return;
        }
        rm.ResourcesChanged += OnResourcesChanged;
        rm.IncomeReceived += OnIncomeReceived;
        RefreshResources();
    }

    /// <summary>Отписываемся, чтобы не было ошибок после удаления панели.</summary>
    private void OnDestroy()
    {
        ResourceManager rm = ResourceManager.Instance;
        if (rm == null) return;
        rm.ResourcesChanged -= OnResourcesChanged;
        rm.IncomeReceived -= OnIncomeReceived;
    }

    /// <summary>Каждый кадр обновляем полоску таймера и доход (источники могут меняться).</summary>
    private void Update()
    {
        ResourceManager rm = ResourceManager.Instance;
        if (timerFill != null) timerFill.fillAmount = rm.IncomeProgress;
        if (timerText != null) timerText.text = Mathf.CeilToInt(rm.TimeToNextIncome) + "с";

        if (incomeText != null)
        {
            rm.GetIncomePerTick(Team.Player, out int gold, out int plutonium);
            incomeText.text = plutonium > 0
                ? $"+{gold} <color=#C58BFF>+{plutonium}</color>"
                : $"+{gold}";
        }
    }

    /// <summary>Ресурсы изменились — если это ресурсы игрока, перерисовываем цифры.</summary>
    private void OnResourcesChanged(Team team)
    {
        if (team == Team.Player) RefreshResources();
    }

    /// <summary>Записать текущие золото и плутоний игрока в тексты.</summary>
    private void RefreshResources()
    {
        ResourceManager rm = ResourceManager.Instance;
        if (goldText != null) goldText.text = rm.GetGold(Team.Player).ToString("N0");
        if (plutoniumText != null) plutoniumText.text = rm.GetPlutonium(Team.Player).ToString("N0");
    }

    /// <summary>Пришёл доход — показываем всплывающую надпись (только для игрока).</summary>
    private void OnIncomeReceived(Team team, int gold, int plutonium)
    {
        if (team != Team.Player || popupText == null) return;
        if (gold == 0 && plutonium == 0) return;

        popupText.text = plutonium > 0
            ? $"+{gold}  <color=#C58BFF>+{plutonium}</color>"
            : $"+{gold}";

        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupAnimation());
    }

    /// <summary>Анимация: надпись поднимается вверх и плавно исчезает.</summary>
    private IEnumerator PopupAnimation()
    {
        popupText.gameObject.SetActive(true);
        float t = 0f;
        while (t < popupDuration)
        {
            t += Time.deltaTime;
            float k = t / popupDuration;
            popupText.rectTransform.anchoredPosition = popupStartPos + Vector2.up * popupRise * k;
            popupText.alpha = 1f - k * k;
            yield return null;
        }
        popupText.gameObject.SetActive(false);
        popupRoutine = null;
    }
}
