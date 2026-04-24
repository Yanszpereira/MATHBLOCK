using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;

    private float horizontalInput;
    private float verticalInput;
    private float verticalVelocity;
    private Vector3 velocity;

    void Update()
    {
        // Movimento no plano horizontal
        Vector3 move = transform.right * horizontalInput + transform.forward * verticalInput;
        controller.Move(move * speed * Time.deltaTime);

        // Aplica gravidade
        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f; // pequena força para manter no chão
        else
            verticalVelocity += gravity * Time.deltaTime;

        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    public void OnMoveEvent(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        horizontalInput = input.x;
        verticalInput = input.y;

    }
}