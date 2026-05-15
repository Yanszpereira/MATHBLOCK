using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.6f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckRadius = 0.38f;
    [SerializeField] private float groundCheckDistance = 0.18f;

    [Header("Block Duplication")]
    [SerializeField] private int availableBlockDuplications = 5;

    private InputAction jumpAction;
    private float horizontalInput;
    private float verticalInput;
    private float verticalVelocity;
    private bool jumpWasPressed;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private Vector3 velocity;

    public bool IsTryingToMove => new Vector2(horizontalInput, verticalInput).sqrMagnitude > 0.01f;
    public int AvailableBlockDuplications => availableBlockDuplications;

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            jumpAction = playerInput.actions.FindAction("Jump", throwIfNotFound: false);
        }
    }

    void Update()
    {
        bool isGrounded = IsGrounded();

        // Movimento no plano horizontal
        Vector3 move = transform.right * horizontalInput + transform.forward * verticalInput;
        controller.Move(move * speed * Time.deltaTime);

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // pequena força para manter no chão
        }

        bool jumpPressed = IsJumpPressed();
        if (isGrounded && jumpPressed && !jumpWasPressed)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false;
        }

        jumpWasPressed = jumpPressed;

        // Aplica gravidade
        if (!isGrounded || verticalVelocity > 0)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity = Mathf.Min(verticalVelocity, -2f);
        }

        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private bool IsJumpPressed()
    {
        return jumpAction != null && jumpAction.ReadValue<float>() > 0.5f;
    }

    private bool IsGrounded()
    {
        if (controller == null)
            return false;

        Vector3 capsuleCenter = transform.TransformPoint(controller.center);
        float halfHeight = Mathf.Max(controller.height * 0.5f, controller.radius);
        Vector3 bottomSphereCenter = capsuleCenter + Vector3.down * (halfHeight - controller.radius);
        float castOffset = 0.03f;
        float minGroundNormalY = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);

        int hitCount = Physics.SphereCastNonAlloc(
            bottomSphereCenter + Vector3.up * castOffset,
            groundCheckRadius,
            Vector3.down,
            groundHits,
            groundCheckDistance + castOffset,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = groundHits[hitIndex];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider == controller || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y >= minGroundNormalY)
                return true;
        }

        return controller.isGrounded;
    }

    public void OnMoveEvent(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        horizontalInput = input.x;
        verticalInput = input.y;

    }

    public bool TryConsumeBlockDuplication()
    {
        if (availableBlockDuplications <= 0)
            return false;

        availableBlockDuplications--;
        return true;
    }
}
