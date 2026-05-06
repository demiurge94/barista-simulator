using UnityEngine;

public class Link : MonoBehaviour
{
    public Transform currentPosition;
    public Vector3 nextPosition;

    void Start()
    {
        currentPosition = this.transform;
    }

    public bool IsLastNode() { return currentPosition == null; }

    public void MoveToNext()
    {
        this.transform.position = nextPosition;
    }

    public void MoveToExitPoint()
    {
        this.transform.position = new Vector3(10.0f, 2.0f, 8.0f);
    }
}
