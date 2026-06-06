using UnityEngine;
using UnityEngine.EventSystems;

public class MapObject : MonoBehaviour, ISelectable
{
    [Header("Object Info")]
    public string objectName;

    [Header("Stats")]
    public int maxHealth = 1000;
    protected int currentHealth;

    [Header("Ownership")]
    public FactionType ownerFaction;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    private void OnMouseDown()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        SelectionManager.Instance.Select(this);
    }

    // SELECT
    public virtual void Select()
    {
        Debug.Log(objectName + " selected");

        UIManager.Instance.OpenMapObjectPanel(this);
    }

    // DESELECT
    public virtual void Deselect()
    {
        Debug.Log(objectName + " deselected");

        UIManager.Instance.CloseMapObjectPanel();
    }

    // DAMAGE
    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;

        Debug.Log(objectName + " took damage");

        if (currentHealth <= 0)
        {
            DestroyObject();
        }
    }

    // DESTROY
    protected virtual void DestroyObject()
    {
        Debug.Log(objectName + " destroyed");

        Destroy(gameObject);
    }

    // GETTERS
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
}