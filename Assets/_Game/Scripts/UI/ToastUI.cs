using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Короткое всплывающее сообщение внизу экрана ("Не хватает золота" и т.п.).
/// Вызов из любого скрипта: ToastUI.Show("Текст");
/// </summary>
public class ToastUI : MonoBehaviour
{
    private static ToastUI instance; // Единственный экземпляр в сцене

    [SerializeField] private CanvasGroup group;      // Для плавного появления/исчезания (на панели сообщения)
    [SerializeField] private TMP_Text messageText;   // Текст сообщения
    [SerializeField] private float showTime = 2f;    // Сколько секунд сообщение висит
    [SerializeField] private float fadeTime = 0.3f;  // Сколько длится появление/исчезание

    private Coroutine routine; // Текущая анимация

    /// <summary>Запоминаем себя и прячем панель.</summary>
    private void Awake()
    {
        instance = this;
        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }

    /// <summary>Показать сообщение. Если уже что-то показано — заменит его.</summary>
    public static void Show(string message)
    {
        if (instance == null) { Debug.Log(message); return; }
        instance.messageText.text = message;
        if (instance.routine != null) instance.StopCoroutine(instance.routine);
        instance.routine = instance.StartCoroutine(instance.Animate());
    }

    /// <summary>Появление → ожидание → исчезание.</summary>
    private IEnumerator Animate()
    {
        group.gameObject.SetActive(true);
        for (float t = group.alpha * fadeTime; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeTime;
            yield return null;
        }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(showTime);
        for (float t = fadeTime; t > 0; t -= Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeTime;
            yield return null;
        }
        group.alpha = 0f;
        group.gameObject.SetActive(false);
        routine = null;
    }
}
