using UnityEngine;

/// <summary>
/// Простой источник дохода, который можно повесить на любой объект в сцене
/// и задать цифры прямо в инспекторе. Сейчас висит на главных базах
/// (базовый доход). Позже постройки и объекты карты будут давать доход
/// через свои скрипты, тоже реализующие IIncomeSource.
/// </summary>
public class IncomeSource : MonoBehaviour, IIncomeSource
{
    [Tooltip("Чей это источник дохода")]
    [SerializeField] private Team owner = Team.Player;

    [Tooltip("Название — видно в логах")]
    [SerializeField] private string sourceName = "Главная база";

    [Tooltip("Золото за одно начисление (раз в 10 секунд)")]
    [SerializeField] private int goldIncome = 100;

    [Tooltip("Плутоний за одно начисление (раз в 10 секунд)")]
    [SerializeField] private int plutoniumIncome = 0;

    public Team Owner => owner;
    public int GoldIncome => goldIncome;
    public int PlutoniumIncome => plutoniumIncome;
    public string SourceName => sourceName;

    /// <summary>Когда объект включается — сообщаем менеджеру ресурсов, что мы приносим доход.</summary>
    private void OnEnable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.RegisterSource(this);
    }

    /// <summary>Когда объект выключается или удаляется — перестаём приносить доход.</summary>
    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.UnregisterSource(this);
    }
}
