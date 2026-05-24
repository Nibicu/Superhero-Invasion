using UnityEngine;

[CreateAssetMenu(
    fileName = "New Hero",
    menuName = "RTS/Hero Data"
)]
public class HeroData : ScriptableObject
{
    [Header("Info")]
    public string heroName;

    [TextArea]
    public string description;

    [Header("Stats")]
    public int maxHealth;

    public int damage;

    public float moveSpeed;

    [Header("Economy")]
    public int cost;

    [Header("Faction")]
    public FactionType faction;

    [Header("Tier")]
    [Range(1, 4)]
    public int tier = 1;

    [Header("Visuals")]
    public Sprite portrait;

    public GameObject prefab;
}