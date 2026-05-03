using UnityEngine;

public class Shelf : MonoBehaviour
{
    public GameObject item;
    public Transform itemPoint;

    void Start()
    {
        GameObject temp = Instantiate(item, itemPoint.position, itemPoint.rotation);
        temp.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        temp.transform.SetParent(itemPoint);
    }
}
