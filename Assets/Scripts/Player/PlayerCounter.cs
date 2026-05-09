using UnityEngine;

public class PlayerCounter : MonoBehaviour
{
    [Tooltip("OrderManager to notify when the player steps on the mat.")]
    public OrderManager orderManager;

    void OnTriggerEnter(Collider col)
    {
        if (!col.CompareTag("Player")) return;
        if (orderManager == null) return;
        orderManager.OnMatStepped();
    }
}
