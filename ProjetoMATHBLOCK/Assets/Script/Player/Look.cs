using UnityEngine;
using UnityEngine.InputSystem;

public class Look : MonoBehaviour
{
    public float mouseSensitivity = 30f;
    public Transform cameraTransform;

    [Header("Camera Walk Bob")]
    [SerializeField] private bool enableWalkBob = true;
    [SerializeField] private float bobFrequency = 10f;
    [SerializeField] private float bobVerticalAmplitude = 0.12f;
    [SerializeField] private float bobHorizontalAmplitude = 0.05f;
    [SerializeField] private float cameraTiltAmplitude = 1.2f;
    [SerializeField] private float bobReturnSpeed = 12f;

    [Header("Pencil Walk Bob")]
    [SerializeField] private Transform pencilTransform;
    [SerializeField] private float pencilVerticalAmplitude = 0.08f;
    [SerializeField] private float pencilHorizontalAmplitude = 0.05f;
    [SerializeField] private float pencilRotationAmplitude = 4f;

    private float xRotation;
    private Vector2 mouseInput;
    private CharacterController controller;
    private PlayerMovement playerMovement;
    private Vector3 defaultCameraLocalPosition;
    private Vector3 defaultPencilLocalPosition;
    private Quaternion defaultPencilLocalRotation;
    private float bobTimer;
    private float currentCameraTilt;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        controller = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();

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

    private void Update()
    {
        RotateMouse();
    }

    private void LateUpdate()
    {
        ApplyWalkBob();
    }

    public void OnLookEvent(InputAction.CallbackContext context)
    {
        mouseInput = context.ReadValue<Vector2>();
    }

    private void RotateMouse()
    {
        xRotation -= mouseInput.y * mouseSensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        ApplyCameraRotation();
        transform.Rotate(Vector3.up * mouseInput.x * mouseSensitivity * Time.deltaTime);
    }

    private void ApplyWalkBob()
    {
        if (!enableWalkBob || cameraTransform == null || controller == null)
        {
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
        currentCameraTilt = Mathf.Lerp(currentCameraTilt, 0f, bobReturnSpeed * Time.deltaTime);
        ApplyCameraRotation();
        ResetPencilBob();
    }

    private void ApplyCameraRotation()
    {
        if (cameraTransform == null)
        {
            return;
        }

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, currentCameraTilt);
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
}
