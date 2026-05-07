using UnityEngine;

public class Chain : MonoBehaviour
{
    public Link[] links;
    public Transform counterTransform;
    public Transform exitPointTransform;

    public int remaining = 8;

    void Start()
    {
        MoveRemainingHeadToCounter();
        //SetPositions();
        //PrintPositions();

        // First Node Goes To Counter
        // Waits to be served
        // Is Served
        // Pickupts the item
        // Goes to exit position

        // The Rest of Tail Moves + 1 position
        // The Cycle Repeats
    }

    // When customer is served the current customer moves to exit
    // Tail Is updated
    // New Customer is moves to counter

    public void SetBackTrackPositions()
    {
        for(int i = links.Length - 2 - (links.Length - remaining); i >= 0; i--)
        {
            links[i].nextPosition = links[i + 1].currentTransform.position;
            links[i].nextRotation = links[i + 1].currentTransform.rotation;
        }
    }

    public void MoveRemainingHeadNodeToExit()
    {
        links[links.Length - (links.Length - remaining)].MoveToExitPoint(exitPointTransform);
    }

    public void MoveRemainingHeadToCounter()
    {
        SetBackTrackPositions();
        links[links.Length - 1 - (links.Length - remaining)].MoveToCounter(counterTransform);
        remaining = remaining - 1;
    }

    void UpdateTail()
    {
        for(int i = 0; i < links.Length - (links.Length - remaining); i++)
        {
            links[i].MoveToNext();
        }
    }
}
