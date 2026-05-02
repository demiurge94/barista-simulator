using UnityEngine;

public class Shelf : MonoBehaviour
{
    public GameObject item;
    public Transform itemPoint;

    void Start()
    {
        Instantiate(item, itemPoint.position, itemPoint.rotation);
    }
}
