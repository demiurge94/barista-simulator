using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -9.81f;

    CharacterController _controller;
    Vector3 _velocity;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Reset vertical velocity when grounded
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
        Vector3 move = transform.right * x + transform.forward * z;
        _controller.Move(move * speed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}
