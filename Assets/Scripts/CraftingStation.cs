using UnityEngine;

/// <summary>
/// Attach to a kiosk (must have a Collider for the raycast).
/// When the player presses E, opens the fabricator menu.
/// </summary>
public class CraftingStation : MonoBehaviour, IInteractable
{
    [Tooltip("Drag the FabricatorMenu component (lives on the Canvas).")]
    public FabricatorMenu fabricatorMenu;

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
            fabricatorMenu.Open();
    }
}
