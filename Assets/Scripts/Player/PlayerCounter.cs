using UnityEngine;

public class PlayerCounter : MonoBehaviour
{
    public Chain customers;

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            if (customers.remaining > 0)
            {
                Debug.Log("Making the Customer Queue to move");
                customers.SetBackTrackPositions();
                customers.MoveRemainingHeadToCounter();
                customers.remaining = customers.remaining - 1;
            }
        }
    }

}
