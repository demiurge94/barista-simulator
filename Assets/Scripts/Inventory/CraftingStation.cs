using UnityEngine;

/// <summary>
/// Attach to a kiosk/machine (must have a Collider for the raycast).
/// When the player presses E, opens the fabricator menu populated with
/// this station's recipes and title.
/// </summary>
public class CraftingStation : MonoBehaviour, IInteractable
{
    [Tooltip("Drag the FabricatorMenu component (lives on the Canvas).")]
    public FabricatorMenu fabricatorMenu;

    [Tooltip("Recipes this station can craft. Coffee machine: assign coffee recipes. Toaster: assign food recipes.")]
    public DrinkRecipe[] recipes;

    [Tooltip("Title shown at the top of the fabricator menu when this station is opened.")]
    public string stationTitle = "Crafting Station";

    public void Interact()
    {
        if (fabricatorMenu == null)
        {
            Debug.LogWarning("CraftingStation: No FabricatorMenu assigned!");
            return;
        }

        if (fabricatorMenu.IsOpen)
            fabricatorMenu.Close();
        else
            fabricatorMenu.Open(recipes, stationTitle);
    }
}
