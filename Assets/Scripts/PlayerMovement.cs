using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    public Transform cameraTransform;

    CharacterController controller;
    Vector3 velocity;
    Vector3 movement;
    Vector2 movementInput;

    float speed = 4.0f;


    Vector3 cameraForward;
    Vector3 cameraRight;
    Vector3 direction;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        cameraForward = cameraTransform.forward;
        cameraForward.y = 0.0f;
        cameraForward.Normalize();

        cameraRight = cameraTransform.right;
        cameraRight.y = 0.0f;
        cameraRight.Normalize();

        direction = (cameraForward * movementInput.y + cameraRight * movementInput.x).normalized;

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(((direction * speed) + (Vector3.up * velocity.y)) * Time.deltaTime);


        if(Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(0);
        }
    }

    public void OnMovement(InputAction.CallbackContext context)
    {
        movementInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if(context.performed && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            speed = sprintSpeed;
        }

        if(context.canceled)
        {
            speed = walkSpeed;
        }
    }
}
