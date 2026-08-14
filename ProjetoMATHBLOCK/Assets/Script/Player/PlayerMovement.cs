using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using FMODUnity;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.6f;
    public float touchMoveSensitivity = 0.015f;
    public bool enableTouchSplitMove = true;

    [Header("Ground Check")]

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckRadius = 0.38f;
    [SerializeField] private float groundCheckDistance = 0.18f;

    [Header("Block Duplication")]
    [SerializeField] private int availableBlockDuplications = 5;

    [Header("Sons FMOD")]
    [SerializeField] private bool playMovementSounds = true;
    [SerializeField] private EventReference footstepSound;
    [SerializeField] private EventReference jumpSound;
    [SerializeField] private EventReference voidFallSound;

    [Header("Som de passos")]
    [SerializeField] private float footstepInterval = 0.42f;
    [SerializeField] private float minMoveInputForFootsteps = 0.1f;

    [Header("Som de queda no void")]
    [SerializeField] private float voidFallSoundDelay = 0.2f;
    [SerializeField] private float voidFallSoundY = -5f;
    [SerializeField] private bool requireVoidHeightToPlayFallSound = true;

    private InputAction jumpAction;
    private float horizontalInput;
    private float verticalInput;
    private float verticalVelocity;
    private bool jumpWasPressed;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private Vector3 velocity;

    private float footstepTimer;
    private float voidFallTimer;
    private bool hasPlayedVoidFallSound;
    private int maximumBlockDuplications;

    public bool IsTryingToMove => new Vector2(horizontalInput, verticalInput).sqrMagnitude > 0.01f;
    public int AvailableBlockDuplications => availableBlockDuplications;
    public int MaximumBlockDuplications => maximumBlockDuplications;
    public event Action<int, int> BlockDuplicationsChanged;
    public event Action BlockDuplicationRequested;

    private void Awake()
    {
        availableBlockDuplications = Mathf.Max(0, availableBlockDuplications);
        maximumBlockDuplications = availableBlockDuplications;

        if (GetComponent<BlockDuplicationCounter>() == null)
            gameObject.AddComponent<BlockDuplicationCounter>();

        EnhancedTouchSupport.Enable();

        PlayerInput playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            jumpAction = playerInput.actions.FindAction("Jump", throwIfNotFound: false);
        }
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        if (enableTouchSplitMove)
            HandleTouchMovement();

        bool isGrounded = IsGrounded();

        Vector3 move = transform.right * horizontalInput + transform.forward * verticalInput;
        controller.Move(move * speed * Time.deltaTime);

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        bool jumpPressed = IsJumpPressed();

        if (isGrounded && jumpPressed && !jumpWasPressed)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false;

            PlayJumpSound();
            ResetFootstepTimer();
        }

        jumpWasPressed = jumpPressed;

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

        HandleFootstepSound(isGrounded);
        HandleVoidFallSound(isGrounded);
    }

    private void HandleFootstepSound(bool isGrounded)
    {
        if (!playMovementSounds)
            return;

        if (!isGrounded)
        {
            ResetFootstepTimer();
            return;
        }

        Vector2 moveInput = new Vector2(horizontalInput, verticalInput);

        if (moveInput.sqrMagnitude < minMoveInputForFootsteps * minMoveInputForFootsteps)
        {
            ResetFootstepTimer();
            return;
        }

        if (footstepSound.IsNull)
            return;

        footstepTimer -= Time.deltaTime;

        if (footstepTimer > 0f)
            return;

        RuntimeManager.PlayOneShot(footstepSound, transform.position);
        footstepTimer = footstepInterval;
    }

    private void PlayJumpSound()
    {
        if (!playMovementSounds)
            return;

        if (jumpSound.IsNull)
            return;

        RuntimeManager.PlayOneShot(jumpSound, transform.position);
    }

    private void HandleVoidFallSound(bool isGrounded)
    {
        if (!playMovementSounds)
            return;

        if (isGrounded)
        {
            ResetVoidFallSoundState();
            return;
        }

        if (hasPlayedVoidFallSound)
            return;

        bool isFalling = verticalVelocity < 0f;
        bool isBelowVoidHeight = !requireVoidHeightToPlayFallSound || transform.position.y <= voidFallSoundY;

        if (!isFalling || !isBelowVoidHeight)
        {
            voidFallTimer = 0f;
            return;
        }

        voidFallTimer += Time.deltaTime;

        if (voidFallTimer < voidFallSoundDelay)
            return;

        if (voidFallSound.IsNull)
            return;

        hasPlayedVoidFallSound = true;
        RuntimeManager.PlayOneShot(voidFallSound, transform.position);
    }

    private void ResetFootstepTimer()
    {
        footstepTimer = 0f;
    }

    private void ResetVoidFallSoundState()
    {
        voidFallTimer = 0f;
        hasPlayedVoidFallSound = false;
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

    private void HandleTouchMovement()
    {
        if (TryGetTouchOffset(false, out Vector2 touchDelta))
        {
            horizontalInput = touchDelta.x * touchMoveSensitivity;
            verticalInput = touchDelta.y * touchMoveSensitivity;
        }
    }

    private bool TryGetTouchOffset(bool leftHalf, out Vector2 offset)
    {
        offset = Vector2.zero;

        foreach (var touch in Touch.activeTouches)
        {
            if (touch.phase != TouchPhase.Moved)
                continue;

            bool isLeftSide = touch.screenPosition.x <= Screen.width * 0.5f;
            if (isLeftSide != leftHalf)
                continue;

            offset = touch.delta;
            return true;
        }

        return false;
    }

    public bool TryConsumeBlockDuplication()
    {
        if (availableBlockDuplications <= 0)
            return false;

        availableBlockDuplications--;
        BlockDuplicationsChanged?.Invoke(availableBlockDuplications, maximumBlockDuplications);
        return true;
    }

    public void NotifyBlockDuplicationRequested()
    {
        BlockDuplicationRequested?.Invoke();
    }

    public void ResetVerticalMovement()
    {
        verticalVelocity = 0f;
        velocity = Vector3.zero;
        jumpWasPressed = false;
        ResetFootstepTimer();
        ResetVoidFallSoundState();
    }
}
