using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Меню "Выберите постройку" — открывается из окна базы при нажатии
/// "Построить" на пустой ячейке. Показывает все постройки, доступные сейчас.
/// </summary>
public class BuildMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject root;            // Всё меню (фон + панель), включается/выключается
    [SerializeField] private Button closeButton;         // Крестик
    [SerializeField] private Button backdropButton;      // Клик по затемнению вокруг — закрыть
    [SerializeField] private Transform listParent;       // Куда складывать строки
    [SerializeField] private BuildOptionUI optionTemplate; // Шаблон строки (выключен в сцене)
    [SerializeField] private TMP_Text emptyText;         // "Нет доступных построек"

    private readonly List<BuildOptionUI> spawned = new List<BuildOptionUI>(); // Созданные строки
    private MainBase mainBase; // Для какой базы строим
    private int slotIndex;     // В какую ячейку

    /// <summary>Открыто ли меню.</summary>
    public bool IsOpen => root.activeSelf;

    /// <summary>Подписываем кнопки и прячем меню.</summary>
    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        backdropButton.onClick.AddListener(Close);
        optionTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    /// <summary>Открыть меню для ячейки index базы b.</summary>
    public void Open(MainBase b, int index)
    {
        mainBase = b;
        slotIndex = index;
        root.SetActive(true);
        Refresh();
    }

    /// <summary>Закрыть меню.</summary>
    public void Close() => root.SetActive(false);

    /// <summary>Пересоздать список построек.</summary>
    public void Refresh()
    {
        if (!IsOpen) return;
        foreach (BuildOptionUI o in spawned) Destroy(o.gameObject);
        spawned.Clear();

        List<BuildingData> list = mainBase.GetBuildableList();
        emptyText.gameObject.SetActive(list.Count == 0);

        foreach (BuildingData data in list)
        {
            BuildOptionUI row = Instantiate(optionTemplate, listParent);
            row.gameObject.SetActive(true);
            BuildingLevel l = data.GetLevel(1);
            bool afford = ResourceManager.Instance.CanAfford(mainBase.Owner, l.goldCost, l.plutoniumCost);
            BuildingData captured = data; // копия для лямбды
            row.Setup(data, afford, () => OnBuildClicked(captured));
            spawned.Add(row);
        }
    }

    /// <summary>Нажали "Построить" у какой-то постройки.</summary>
    private void OnBuildClicked(BuildingData data)
    {
        if (mainBase.TryBuild(slotIndex, data, out string error))
        {
            ToastUI.Show($"Построено: {data.displayName}");
            Close();
        }
        else
        {
            ToastUI.Show(error);
        }
    }
}
