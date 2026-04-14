using UnityEngine;

public interface IInteractable
{
    void Interact();
}

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] float interactRange = 3f;
    [SerializeField] Transform cameraTransform;   // drag your Camera here

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        // Raycast from the camera so it matches where the player is looking
        Transform origin = cameraTransform != null ? cameraTransform : transform;

        if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, interactRange))
        {
            // Check the object we hit, then walk up parents to find IInteractable
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
                interactable.Interact();
            else
                Debug.Log($"Hit '{hit.collider.name}' — not interactable");
        }
        else
        {
            Debug.Log("Nothing to interact with");
        }
    }
}
