using UnityEngine;

public class Chain : MonoBehaviour
{

    public Link[] links;
    public Transform counterTransform;

    public int remaining = 6;

    void Start()
    {
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

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.W))
        {
            SetBackTrackPositions();
            MoveRemainingHeadToCounter();
            remaining = remaining - 1;
        }

        if(Input.GetKeyDown(KeyCode.E))
        {
            MoveRemainingHeadNodeToExit();
        }

        if(Input.GetKeyDown(KeyCode.S))
        {
            UpdateTail();
        }
    }

    public void SetBackTrackPositions()
    {
        for(int i = links.Length - 2 - (links.Length - remaining); i >= 0; i--)
        {
            links[i].nextPosition = links[i + 1].currentPosition.position;
        }
    }

    public void MoveRemainingHeadNodeToExit()
    {
        links[links.Length - (links.Length - remaining)].MoveToExitPoint();
    }

    public void MoveRemainingHeadToCounter()
    {
        links[links.Length - 1 - (links.Length - remaining)].MoveToCounter(counterTransform.position);
    }

    void UpdateTail()
    {
        for(int i = 0; i < links.Length - (links.Length - remaining); i++)
        {
            links[i].MoveToNext();
        }
    }
}
