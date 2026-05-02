using UnityEngine;
using UnityEngine.InputSystem;

public class CameraLook : MonoBehaviour
{
    [SerializeField] float mouseSensitivity = 100f;
    [SerializeField] Transform playerBody;

    float _xRotation;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Mouse.current.delta.ReadValue().x * mouseSensitivity * Time.deltaTime;
        float mouseY = Mouse.current.delta.ReadValue().y * mouseSensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);

        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
