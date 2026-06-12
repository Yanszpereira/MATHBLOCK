using UnityEngine;
using UnityEngine.InputSystem;

public class PencilCursor : MonoBehaviour
{
    [SerializeField] private Vector2 offset;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Mouse.current != null)
        {
            rectTransform.position = Mouse.current.position.ReadValue() + offset;
        }
    }
}