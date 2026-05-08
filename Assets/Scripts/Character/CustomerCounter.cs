using UnityEngine;

public class CustomerCounter : MonoBehaviour
{
    public Chain customers;
    public Customer customer;

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Customer"))
        {
            Debug.Log("Customer Entered");
            customer = col.GetComponent<Customer>();
        }
    }

    public void ServeCustomer()
    {
        customers.UpdateTail();
        customers.MoveRemainingHeadNodeToExit();
    }
}
