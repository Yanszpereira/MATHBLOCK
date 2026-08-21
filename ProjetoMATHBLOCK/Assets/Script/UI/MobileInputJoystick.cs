using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

public sealed class MobileInputJoystick : OnScreenControl,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private string inputControlPath = "<Gamepad>/leftStick";
    [SerializeField] private RectTransform handle;
    [SerializeField, Min(1f)] private float movementRange = 76f;

    private RectTransform rectTransform;
    private int activePointerId = int.MinValue;

    protected override string controlPathInternal
    {
        get => inputControlPath;
        set => inputControlPath = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        rectTransform = transform as RectTransform;
        ResetStick();
    }

    protected override void OnDisable()
    {
        ResetStick();
        base.OnDisable();
    }

    public void Configure(RectTransform newHandle, float newRange)
    {
        rectTransform = transform as RectTransform;
        handle = newHandle;
        movementRange = Mathf.Max(1f, newRange);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        UpdateStick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
            UpdateStick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
            ResetStick();
    }

    public void ResetStick()
    {
        activePointerId = int.MinValue;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
        SendValueToControl(Vector2.zero);
    }

    private void UpdateStick(PointerEventData eventData)
    {
        if (rectTransform == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventCamera, out Vector2 localPoint))
            return;

        Vector2 value = Vector2.ClampMagnitude(localPoint / movementRange, 1f);
        if (value.sqrMagnitude < 0.0225f)
            value = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = value * movementRange;
        SendValueToControl(value);
    }
}
