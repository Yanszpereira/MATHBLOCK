using UnityEngine;
using UnityEngine.InputSystem;

public class PencilCursor : MonoBehaviour
{
    [SerializeField] private Vector2 offset;

    private RectTransform rectTransform;
    private bool customCursorEnabled;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        customCursorEnabled = !Application.isMobilePlatform;
        if (!customCursorEnabled)
        {
            gameObject.SetActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && customCursorEnabled)
            Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (Mouse.current != null)
        {
            rectTransform.position = Mouse.current.position.ReadValue() + offset;
        }
    }
}
