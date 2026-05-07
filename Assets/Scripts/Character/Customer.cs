using UnityEngine;

public class Customer : MonoBehaviour
{
    public int customerTexture = 1;
    public GameObject model;
    public string customer_name;

    public Material[] customerMaterials = new Material[4];

    void Start()
    {
        int number = Random.Range(1, 5);
        Renderer modelRenderer = model.GetComponent<Renderer>();

        modelRenderer.material = customerMaterials[number - 1];

        switch (number)
        {
            case 1:
                customer_name = "Jessica";
                break;
            case 2:
                customer_name = "Mark";
                break;
            case 3:
                customer_name = "Morning Zombie";
                break;
            case 4:
                customer_name = "Morning Zombie";
                break;
        }
    }
}
