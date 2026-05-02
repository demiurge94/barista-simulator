using UnityEngine;

public class ItemHover : MonoBehaviour
{
    public float rotationSpeed = 50.0f;
    private float rotationAngle = 0.0f;

    void Update()
    {
        rotationAngle = (rotationAngle + rotationSpeed * Time.deltaTime) % 360.0f;
        transform.localRotation = Quaternion.Euler(-90.0f, rotationAngle, 0.0f);
    }
}
