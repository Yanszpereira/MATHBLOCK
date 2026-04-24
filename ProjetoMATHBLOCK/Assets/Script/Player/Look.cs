using UnityEngine;
using UnityEngine.InputSystem;

public class Look : MonoBehaviour
{
    public float mouseSensitivity = 30f;
    public Transform cameraTransform;

    private float xRotation = 0f;
    private Vector2 mouseInput;
    

    void Update()
    {
        RotateMouse();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OnLookEvent(InputAction.CallbackContext context)
    {
        mouseInput = context.ReadValue<Vector2>();
    }
    public void RotateMouse()
    {
        // Aplica a rotação vertical (clamp para não virar de cabeça para baixo)
        xRotation -= mouseInput.y * mouseSensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Rotaciona a câmera para cima/baixo
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotaciona o corpo do player para os lados
        transform.Rotate(Vector3.up * mouseInput.x * mouseSensitivity * Time.deltaTime);
    }
}