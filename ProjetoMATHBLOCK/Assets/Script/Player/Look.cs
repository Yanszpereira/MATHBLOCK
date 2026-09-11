using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Look : MonoBehaviour
{
    public float mouseSensitivity = 30f;
    public float touchLookSensitivity = 0.08f;
    public bool enableTouchSplitLook = true;
    public Transform cameraTransform;

    [Header("Camera Walk Bob")]
    [SerializeField] private bool enableWalkBob = true;
    [SerializeField] private float bobFrequency = 10f;
    [SerializeField] private float bobVerticalAmplitude = 0.12f;
    [SerializeField] private float bobHorizontalAmplitude = 0.05f;
    [SerializeField] private float cameraTiltAmplitude = 1.2f;
    [SerializeField] private float bobReturnSpeed = 12f;

    [Header("Camera Jump Shake")]
    [SerializeField, Min(0.01f)] private float jumpShakeDuration = 0.28f;
    [SerializeField, Min(0f)] private float jumpShakeStrength = 0.16f;
    [SerializeField, Min(0f)] private float jumpShakeRotation = 2.2f;
    [SerializeField, Min(1f)] private float jumpShakeFrequency = 30f;

    [Header("Camera Fall Shake")]
    [SerializeField, Min(0f)] private float fallShakeStartSpeed = 3.5f;
    [SerializeField, Min(0f)] private float fallShakeFullSpeed = 16f;
    [SerializeField, Min(0f)] private float fallShakeStrength = 0.075f;
    [SerializeField, Min(0f)] private float fallShakeRotation = 0.8f;
    [SerializeField, Min(1f)] private float fallShakeFrequency = 17f;

    [Header("Pencil Walk Bob")]
    [SerializeField] private Transform pencilTransform;
    [SerializeField] private float pencilVerticalAmplitude = 0.08f;
    [SerializeField] private float pencilHorizontalAmplitude = 0.05f;
    [SerializeField] private float pencilRotationAmplitude = 4f;

    private float xRotation;
    private Vector2 mouseInput;
    private Vector2 touchInput;
    private int lookTouchId = -1;
    private CharacterController controller;
    private PlayerMovement playerMovement;
    private Vector3 defaultCameraLocalPosition;
    private Vector3 defaultPencilLocalPosition;
    private Quaternion defaultPencilLocalRotation;
    private float bobTimer;
    private float currentCameraTilt;
    private float jumpShakeElapsed;
    private bool jumpShakeActive;
    private Vector3 lastJumpShakeOffset;
    private float currentJumpPitch;
    private float currentJumpRoll;
    private Vector3 lastFallShakeOffset;
    private float currentFallPitch;
    private float currentFallRoll;

    private void Start()
    {
        LockCursor();
        controller = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", mouseSensitivity);
        touchLookSensitivity = PlayerPrefs.GetFloat("TouchSensitivity", 40f) * 0.002f;

        if (cameraTransform != null)
        {
            defaultCameraLocalPosition = cameraTransform.localPosition;
        }

        if (pencilTransform == null && cameraTransform != null)
        {
            Transform pencil = cameraTransform.Find("PecilgunCamera/PencilGun");
            pencilTransform = pencil != null ? pencil : cameraTransform.Find("PencilGun");
        }

        if (pencilTransform != null)
        {
            defaultPencilLocalPosition = pencilTransform.localPosition;
            defaultPencilLocalRotation = pencilTransform.localRotation;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            LockCursor();
    }

    private static void LockCursor()
    {
        if (Application.isMobilePlatform)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        if (cameraTransform != null)
            cameraTransform.localPosition -= lastJumpShakeOffset + lastFallShakeOffset;
        lastJumpShakeOffset = Vector3.zero;
        lastFallShakeOffset = Vector3.zero;
        jumpShakeActive = false;
        currentJumpPitch = 0f;
        currentJumpRoll = 0f;
        currentFallPitch = 0f;
        currentFallRoll = 0f;
        mouseInput = Vector2.zero;
        touchInput = Vector2.zero;
        lookTouchId = -1;
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            touchInput = Vector2.zero;
            lookTouchId = -1;
            return;
        }

        RotateMouse();

        if (enableTouchSplitLook)
        {
            HandleTouchLook();
            RotateTouch();
        }
    }

    private void LateUpdate()
    {
        ApplyWalkBob();
    }

    public void OnLookEvent(InputAction.CallbackContext context)
    {
        if (context.control?.device is Mouse)
            mouseInput = context.ReadValue<Vector2>();
    }

    private void RotateMouse()
    {
        xRotation -= mouseInput.y * mouseSensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        ApplyCameraRotation();
        transform.Rotate(Vector3.up * mouseInput.x * mouseSensitivity * Time.deltaTime);
    }

    private void RotateTouch()
    {
        if (touchInput == Vector2.zero)
            return;

        xRotation -= touchInput.y * touchLookSensitivity;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        ApplyCameraRotation();
        transform.Rotate(Vector3.up * touchInput.x * touchLookSensitivity);
    }

    private void ApplyWalkBob()
    {
        if (cameraTransform == null)
        {
            return;
        }

        cameraTransform.localPosition -= lastJumpShakeOffset;
        lastJumpShakeOffset = Vector3.zero;
        cameraTransform.localPosition -= lastFallShakeOffset;
        lastFallShakeOffset = Vector3.zero;

        if (!enableWalkBob || controller == null)
        {
            ApplyJumpShake();
            ApplyFallShake();
            return;
        }

        bool hasMoveInput = playerMovement != null && playerMovement.IsTryingToMove;
        bool isWalking = controller.isGrounded && hasMoveInput;

        if (isWalking)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            float verticalWave = Mathf.Sin(bobTimer);
            float horizontalWave = Mathf.Cos(bobTimer * 0.5f);

            Vector3 bobOffset = new Vector3(
                horizontalWave * bobHorizontalAmplitude,
                verticalWave * bobVerticalAmplitude,
                0f
            );

            cameraTransform.localPosition = defaultCameraLocalPosition + bobOffset;
            ApplyJumpShake();
            ApplyFallShake();
            currentCameraTilt = horizontalWave * cameraTiltAmplitude;
            ApplyCameraRotation();
            ApplyPencilBob(verticalWave, horizontalWave);
            return;
        }

        bobTimer = 0f;
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            defaultCameraLocalPosition,
            bobReturnSpeed * Time.deltaTime
        );
        ApplyJumpShake();
        ApplyFallShake();
        currentCameraTilt = Mathf.Lerp(currentCameraTilt, 0f, bobReturnSpeed * Time.deltaTime);
        ApplyCameraRotation();
        ResetPencilBob();
    }

    public void PlayJumpShake()
    {
        jumpShakeElapsed = 0f;
        jumpShakeActive = true;
    }

    private void ApplyJumpShake()
    {
        if (!jumpShakeActive || cameraTransform == null)
            return;

        float duration = Mathf.Max(0.01f, jumpShakeDuration);
        jumpShakeElapsed += Time.unscaledDeltaTime;
        float normalized = Mathf.Clamp01(jumpShakeElapsed / duration);
        float envelope = 1f - normalized;
        float phase = jumpShakeElapsed * jumpShakeFrequency;

        lastJumpShakeOffset = new Vector3(
            Mathf.Sin(phase * 0.73f) * jumpShakeStrength * 0.35f,
            -Mathf.Sin(phase) * jumpShakeStrength,
            0f) * envelope;
        cameraTransform.localPosition += lastJumpShakeOffset;
        currentJumpPitch = -Mathf.Sin(phase) * jumpShakeRotation * envelope;
        currentJumpRoll = Mathf.Sin(phase * 0.73f) * jumpShakeRotation * 0.65f * envelope;

        if (normalized >= 1f)
        {
            jumpShakeActive = false;
            lastJumpShakeOffset = Vector3.zero;
            currentJumpPitch = 0f;
            currentJumpRoll = 0f;
        }
    }

    private void ApplyFallShake()
    {
        bool falling = playerMovement != null && playerMovement.IsFalling;
        float intensity = falling
            ? Mathf.InverseLerp(fallShakeStartSpeed, Mathf.Max(fallShakeStartSpeed + 0.01f, fallShakeFullSpeed), playerMovement.FallingSpeed)
            : 0f;
        intensity = Mathf.SmoothStep(0f, 1f, intensity);

        if (intensity <= 0.001f || cameraTransform == null)
        {
            currentFallPitch = Mathf.Lerp(currentFallPitch, 0f, 14f * Time.unscaledDeltaTime);
            currentFallRoll = Mathf.Lerp(currentFallRoll, 0f, 14f * Time.unscaledDeltaTime);
            return;
        }

        float phase = Time.unscaledTime * fallShakeFrequency;
        float noiseX = Mathf.PerlinNoise(phase, 3.17f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(8.41f, phase * 1.13f) * 2f - 1f;
        lastFallShakeOffset = new Vector3(noiseX, noiseY, 0f) * fallShakeStrength * intensity;
        cameraTransform.localPosition += lastFallShakeOffset;
        currentFallPitch = noiseY * fallShakeRotation * intensity;
        currentFallRoll = noiseX * fallShakeRotation * intensity;
        ApplyCameraRotation();
    }

    private void ApplyCameraRotation()
    {
        if (cameraTransform == null)
        {
            return;
        }

        cameraTransform.localRotation = Quaternion.Euler(
            xRotation + currentJumpPitch + currentFallPitch,
            0f,
            currentCameraTilt + currentJumpRoll + currentFallRoll);
    }

    private void ApplyPencilBob(float verticalWave, float horizontalWave)
    {
        if (pencilTransform == null)
        {
            return;
        }

        Vector3 pencilOffset = new Vector3(
            horizontalWave * pencilHorizontalAmplitude,
            Mathf.Abs(verticalWave) * pencilVerticalAmplitude,
            0f
        );
        Quaternion pencilRotation = Quaternion.Euler(
            verticalWave * pencilRotationAmplitude,
            horizontalWave * pencilRotationAmplitude,
            -horizontalWave * pencilRotationAmplitude
        );

        pencilTransform.localPosition = defaultPencilLocalPosition + pencilOffset;
        pencilTransform.localRotation = defaultPencilLocalRotation * pencilRotation;
    }

    private void ResetPencilBob()
    {
        if (pencilTransform == null)
        {
            return;
        }

        pencilTransform.localPosition = Vector3.Lerp(
            pencilTransform.localPosition,
            defaultPencilLocalPosition,
            bobReturnSpeed * Time.deltaTime
        );
        pencilTransform.localRotation = Quaternion.Slerp(
            pencilTransform.localRotation,
            defaultPencilLocalRotation,
            bobReturnSpeed * Time.deltaTime
        );
    }

    private void HandleTouchLook()
    {
        touchInput = Vector2.zero;

        foreach (Touch touch in Touch.activeTouches)
        {
            if (touch.touchId != lookTouchId)
                continue;

            if (touch.phase == TouchPhase.Moved)
                touchInput = touch.delta;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                lookTouchId = -1;
            return;
        }

        lookTouchId = -1;
        foreach (Touch touch in Touch.activeTouches)
        {
            if (touch.phase != TouchPhase.Began || touch.screenPosition.x <= Screen.width * 0.5f)
                continue;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId))
                continue;

            lookTouchId = touch.touchId;
            return;
        }
    }

    public void SetMouseSensitivity(float value) => mouseSensitivity = value;

    public void SetTouchSensitivity(float value) =>
        touchLookSensitivity = Mathf.Clamp(value, 1f, 100f) * 0.002f;
}
