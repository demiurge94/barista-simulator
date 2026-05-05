using UnityEngine;

public class Shelf : MonoBehaviour, IInteractable
{
    [Tooltip("The ingredient this shelf produces. Press E to add restockAmount of it to player inventory.")]
    public IngredientData ingredient;

    [Tooltip("How many of this ingredient are added to inventory per E-press. Default 5.")]
    public int restockAmount = 5;

    [Tooltip("Visual model spawned at startup. May be a prefab or a scene GameObject.")]
    public GameObject item;

    [Tooltip("Transform the spawned visual is parented under (rotates via ItemHover).")]
    public Transform itemPoint;

    void Start()
    {
        if (item == null || itemPoint == null) return;

        GameObject temp = Instantiate(item, itemPoint.position, itemPoint.rotation);
        temp.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        temp.transform.SetParent(itemPoint);
    }

    public void Interact()
    {
        if (ingredient == null)
        {
            Debug.LogWarning($"Shelf '{name}': ingredient is null, nothing to give.");
            return;
        }

        var inv = PlayerInventory.Instance;
        if (inv == null)
        {
            Debug.LogWarning("Shelf: PlayerInventory.Instance is null.");
            return;
        }

        inv.Add(ingredient, restockAmount);
        Debug.Log($"[Shelf] +{restockAmount} {ingredient.ingredientName} (now {inv.Get(ingredient)})");
    }
}
