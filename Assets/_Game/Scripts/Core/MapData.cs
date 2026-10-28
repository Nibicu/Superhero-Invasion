using UnityEngine;

/// <summary>
/// Карта для Песочницы (ScriptableObject): название, картинка для выбора и сцена, которую загрузить.
/// Создать: ПКМ → Create → Супергеройское бюро → Карта.
/// </summary>
[CreateAssetMenu(menuName = "Супергеройское бюро/Карта", fileName = "Map_")]
public class MapData : ScriptableObject
{
    [Tooltip("Название карты")]
    public string displayName = "Карта";

    [Tooltip("Картинка карты на экране выбора")]
    public Sprite preview;

    [Tooltip("Имя сцены карты (сцена должна быть в Build Settings)")]
    public string sceneName = "Game";

    [Tooltip("Коротко о карте")]
    [TextArea(2, 4)] public string description = "";
}
