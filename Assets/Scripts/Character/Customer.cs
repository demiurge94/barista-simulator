using UnityEngine;

public class Customer : MonoBehaviour
{
    public int customerTexture = 1;
    public GameObject model;
    public string customerName;

    public Transform itemSpawnTransform;

    public GameObject test_item;
    public bool testItemSpawning = false;

    public Material[] customerMaterials = new Material[4];

    void Start()
    {
        if(testItemSpawning == true)
        {
            GiveItem(test_item);
        }

        int number = Random.Range(1, 5);
        Renderer modelRenderer = model.GetComponent<Renderer>();

        modelRenderer.material = customerMaterials[number - 1];

        switch (number)
        {
            case 1:
                customerName = "Jessica";
                break;
            case 2:
                customerName = "Mark";
                break;
            case 3:
                customerName = "Morning Zombie";
                break;
            case 4:
                customerName = "Morning Zombie";
                break;
        }
    }

    public void GiveItem(GameObject item)
    {
        GameObject temp = Instantiate(item, itemSpawnTransform);
        temp.transform.SetParent(itemSpawnTransform);
        temp.transform.localPosition = Vector3.zero;
    }
}
