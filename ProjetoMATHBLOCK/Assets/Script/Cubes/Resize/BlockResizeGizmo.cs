using UnityEngine;

public readonly struct BlockResizeGizmoLayout
{
    public Vector3 Top { get; }
    public Vector3 Bottom { get; }
    public Vector3 Left { get; }
    public Vector3 Right { get; }
    public Vector3 Center { get; }

    public BlockResizeGizmoLayout(Vector3 top, Vector3 bottom, Vector3 left, Vector3 right, Vector3 center)
    {
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
        Center = center;
    }
}

[DisallowMultipleComponent]
public sealed class BlockResizeGizmo : MonoBehaviour
{
    [SerializeField] private BlockResizeHandle topHandle;
    [SerializeField] private BlockResizeHandle bottomHandle;
    [SerializeField] private BlockResizeHandle leftHandle;
    [SerializeField] private BlockResizeHandle rightHandle;
    [SerializeField] private BlockResizeHandle centerHandle;
    [SerializeField] private float surfaceOffset = 0.03f;

    private ResizableBlock target;
    private Camera playerCamera;
    private Vector3 faceNormalWorld;
    private Vector3 horizontalScreenAxisWorld;
    private Vector3 verticalScreenAxisWorld;
    private Vector3 facePlanePoint;

    public Vector3 FaceNormalWorld => faceNormalWorld;
    public Vector3 HorizontalScreenAxisWorld => horizontalScreenAxisWorld;
    public Vector3 VerticalScreenAxisWorld => verticalScreenAxisWorld;
    public Vector3 FacePlanePoint => facePlanePoint;

    private void Awake()
    {
        ResolveHandles();
        SetAllHandlesState(ResizeHandleVisualState.Normal);
    }

    private void Reset()
    {
        ResolveHandles();
    }

    private void OnValidate()
    {
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        ResolveHandles();
    }

    public void Show(ResizableBlock block, Camera camera, Vector3 hitNormal, bool hasHitNormal)
    {
        target = block;
        playerCamera = camera;
        gameObject.SetActive(true);
        UpdateLayout(hitNormal, hasHitNormal);
    }

    public void Hide()
    {
        SetAllHandlesState(ResizeHandleVisualState.Normal);
        target = null;
        playerCamera = null;
        gameObject.SetActive(false);
    }

    public void UpdateLayout()
    {
        UpdateLayout(faceNormalWorld, true);
    }

    public ResizeDirection GetResizeDirection(ResizeHandlePosition position)
    {
        if (target == null)
            return ResizeDirection.Center;

        switch (position)
        {
            case ResizeHandlePosition.Right:
                return ResolveHorizontalDirection(playerCamera.transform.right, target.HorizontalAxisWorld, true);
            case ResizeHandlePosition.Left:
                return ResolveHorizontalDirection(playerCamera.transform.right, target.HorizontalAxisWorld, false);
            case ResizeHandlePosition.Top:
                return ResolveVerticalDirection(playerCamera.transform.up, target.VerticalAxisWorld, true);
            case ResizeHandlePosition.Bottom:
                return ResolveVerticalDirection(playerCamera.transform.up, target.VerticalAxisWorld, false);
            default:
                return ResizeDirection.Center;
        }
    }

    public Vector3 GetDragAxisWorld(ResizeHandlePosition position)
    {
        switch (position)
        {
            case ResizeHandlePosition.Right: return horizontalScreenAxisWorld;
            case ResizeHandlePosition.Left: return -horizontalScreenAxisWorld;
            case ResizeHandlePosition.Top: return verticalScreenAxisWorld;
            case ResizeHandlePosition.Bottom: return -verticalScreenAxisWorld;
            default: return Vector3.zero;
        }
    }

    public bool ContainsHandle(BlockResizeHandle handle)
    {
        return handle != null && (handle == topHandle || handle == bottomHandle || handle == leftHandle
            || handle == rightHandle || handle == centerHandle);
    }

    public void SetAllHandlesState(ResizeHandleVisualState state)
    {
        SetState(topHandle, state);
        SetState(bottomHandle, state);
        SetState(leftHandle, state);
        SetState(rightHandle, state);
        SetState(centerHandle, state);
    }

    public static ResizeDirection ResolveHorizontalDirection(Vector3 cameraRight, Vector3 positiveHorizontalAxis, bool rightHandle)
    {
        bool positivePointsRight = Vector3.Dot(cameraRight, positiveHorizontalAxis) >= 0f;
        bool usePositive = rightHandle ? positivePointsRight : !positivePointsRight;
        return usePositive ? ResizeDirection.PositiveHorizontal : ResizeDirection.NegativeHorizontal;
    }

    public static ResizeDirection ResolveVerticalDirection(Vector3 cameraUp, Vector3 positiveVerticalAxis, bool topHandle)
    {
        bool positivePointsUp = Vector3.Dot(cameraUp, positiveVerticalAxis) >= 0f;
        bool usePositive = topHandle ? positivePointsUp : !positivePointsUp;
        return usePositive ? ResizeDirection.PositiveVertical : ResizeDirection.NegativeVertical;
    }

    public static Vector3 ChooseFaceNormal(
        Vector3 positiveNormal,
        Vector3 blockCenter,
        Vector3 cameraPosition,
        Vector3 hitNormal,
        bool hasHitNormal)
    {
        positiveNormal = positiveNormal.normalized;
        if (hasHitNormal && hitNormal.sqrMagnitude > 0.0001f)
        {
            Vector3 normalizedHit = hitNormal.normalized;
            float positiveAlignment = Vector3.Dot(normalizedHit, positiveNormal);
            float negativeAlignment = Vector3.Dot(normalizedHit, -positiveNormal);
            if (Mathf.Max(positiveAlignment, negativeAlignment) >= 0.75f)
                return positiveAlignment >= negativeAlignment ? positiveNormal : -positiveNormal;
        }

        Vector3 cameraDirection = cameraPosition - blockCenter;
        return Vector3.Dot(cameraDirection, positiveNormal) >= 0f ? positiveNormal : -positiveNormal;
    }

    public static BlockResizeGizmoLayout CalculateLayout(
        Vector3 faceCenter,
        Vector3 screenRightAxis,
        Vector3 screenUpAxis,
        float horizontalSize,
        float verticalSize)
    {
        Vector3 horizontalOffset = screenRightAxis.normalized * horizontalSize * 0.5f;
        Vector3 verticalOffset = screenUpAxis.normalized * verticalSize * 0.5f;
        return new BlockResizeGizmoLayout(
            faceCenter + verticalOffset,
            faceCenter - verticalOffset,
            faceCenter - horizontalOffset,
            faceCenter + horizontalOffset,
            faceCenter
        );
    }

    private void UpdateLayout(Vector3 hitNormal, bool hasHitNormal)
    {
        if (target == null || playerCamera == null)
            return;

        target.GetActivePlaneDimensions(out int horizontal, out int vertical, out int fixedDimension);
        faceNormalWorld = ChooseFaceNormal(
            target.FaceNormalWorld,
            target.WorldCenter,
            playerCamera.transform.position,
            hitNormal,
            hasHitNormal
        );

        Vector3 positiveHorizontal = target.HorizontalAxisWorld;
        Vector3 positiveVertical = target.VerticalAxisWorld;
        horizontalScreenAxisWorld = Vector3.Dot(playerCamera.transform.right, positiveHorizontal) >= 0f
            ? positiveHorizontal
            : -positiveHorizontal;
        verticalScreenAxisWorld = Vector3.Dot(playerCamera.transform.up, positiveVertical) >= 0f
            ? positiveVertical
            : -positiveVertical;

        float horizontalSize = horizontal * target.HorizontalUnitWorldSize;
        float verticalSize = vertical * target.VerticalUnitWorldSize;
        float fixedSize = fixedDimension * target.FixedUnitWorldSize;
        facePlanePoint = target.WorldCenter + faceNormalWorld * fixedSize * 0.5f;
        Vector3 gizmoCenter = facePlanePoint + faceNormalWorld * surfaceOffset;

        transform.SetPositionAndRotation(gizmoCenter, Quaternion.identity);
        transform.localScale = Vector3.one;
        BlockResizeGizmoLayout layout = CalculateLayout(
            gizmoCenter,
            horizontalScreenAxisWorld,
            verticalScreenAxisWorld,
            horizontalSize,
            verticalSize
        );

        PositionHandle(topHandle, layout.Top, verticalScreenAxisWorld);
        PositionHandle(bottomHandle, layout.Bottom, -verticalScreenAxisWorld);
        PositionHandle(leftHandle, layout.Left, -horizontalScreenAxisWorld);
        PositionHandle(rightHandle, layout.Right, horizontalScreenAxisWorld);
        PositionHandle(centerHandle, layout.Center, verticalScreenAxisWorld);
    }

    private void PositionHandle(BlockResizeHandle handle, Vector3 position, Vector3 visualDirection)
    {
        if (handle == null)
            return;

        handle.transform.SetPositionAndRotation(position, Quaternion.LookRotation(faceNormalWorld, visualDirection));
        handle.transform.localScale = Vector3.one;
    }

    private void ResolveHandles()
    {
        BlockResizeHandle[] handles = GetComponentsInChildren<BlockResizeHandle>(true);
        for (int i = 0; i < handles.Length; i++)
        {
            BlockResizeHandle handle = handles[i];
            switch (handle.Position)
            {
                case ResizeHandlePosition.Top: topHandle = handle; break;
                case ResizeHandlePosition.Bottom: bottomHandle = handle; break;
                case ResizeHandlePosition.Left: leftHandle = handle; break;
                case ResizeHandlePosition.Right: rightHandle = handle; break;
                case ResizeHandlePosition.Center: centerHandle = handle; break;
            }
        }
    }

    private static void SetState(BlockResizeHandle handle, ResizeHandleVisualState state)
    {
        if (handle != null)
            handle.SetVisualState(state);
    }
}
