using UnityEngine;

public class UnitStats : MonoBehaviour
{
    [Header("Health")]

    public int maxHP = 100;
    public int currentHP;

    [Header("Attack")]

    public int damage = 10;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;

    [Header("Movement")]

    public float walkSpeed = 1.5f;
    public float runSpeed = 3f;

    [Header("Physics")]

    public float weight = 1f;

    [Header("Resources")]

    public int maxMana = 100;
    public int currentMana;

    [Header("Faction")]
    public FactionType faction;
}