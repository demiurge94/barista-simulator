using UnityEngine;

public interface IInteractable
{
    void Interact();
}

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] float interactRange = 2f;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, interactRange))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
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
