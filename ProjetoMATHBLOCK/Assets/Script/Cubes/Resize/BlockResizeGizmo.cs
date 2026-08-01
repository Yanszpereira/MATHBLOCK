using UnityEngine;

public readonly struct BlockResizeGizmoLayout
{
    public Vector3 Top { get; }
    public Vector3 Bottom { get; }
    public Vector3 Left { get; }
    public Vector3 Right { get; }

    public BlockResizeGizmoLayout(Vector3 top, Vector3 bottom, Vector3 left, Vector3 right)
    {
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
    }
}

public readonly struct BlockResizeGizmoBasis
{
    public Vector3 Normal { get; }
    public Vector3 Horizontal { get; }
    public Vector3 Vertical { get; }
    public int HorizontalAxis { get; }
    public int VerticalAxis { get; }

    public BlockResizeGizmoBasis(
        Vector3 normal,
        Vector3 horizontal,
        Vector3 vertical,
        int horizontalAxis,
        int verticalAxis)
    {
        Normal = normal;
        Horizontal = horizontal;
        Vertical = vertical;
        HorizontalAxis = horizontalAxis;
        VerticalAxis = verticalAxis;
    }
}

[DisallowMultipleComponent]
public sealed class BlockResizeGizmo : MonoBehaviour
{
    private const float DirectionEpsilon = 0.0001f;
    private const float ScoreEpsilon = 0.0001f;

    [SerializeField] private BlockResizeHandle topHandle;
    [SerializeField] private BlockResizeHandle bottomHandle;
    [SerializeField] private BlockResizeHandle leftHandle;
    [SerializeField] private BlockResizeHandle rightHandle;
    [SerializeField] private float surfaceOffset = 0.03f;

    private ResizableBlock target;
    private ResizeFace selectedFace;
    private Vector3 faceNormalWorld;
    private Vector3 horizontalScreenAxisWorld;
    private Vector3 verticalScreenAxisWorld;
    private Vector3 facePlanePoint;
    private int horizontalDimensionAxis;
    private int verticalDimensionAxis;

    public ResizeFace SelectedFace => selectedFace;
    public Vector3 FaceNormalWorld => faceNormalWorld;
    public Vector3 HorizontalScreenAxisWorld => horizontalScreenAxisWorld;
    public Vector3 VerticalScreenAxisWorld => verticalScreenAxisWorld;
    public Vector3 FacePlanePoint => facePlanePoint;
    public int HandleCount => GetComponentsInChildren<BlockResizeHandle>(true).Length;

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

    public void Show(ResizableBlock block, Camera camera, ResizeFace face)
    {
        target = block;
        selectedFace = face;
        gameObject.SetActive(true);

        BlockResizeGizmoBasis basis = CalculateFaceBasis(block, face, camera);
        faceNormalWorld = basis.Normal;
        horizontalScreenAxisWorld = basis.Horizontal;
        verticalScreenAxisWorld = basis.Vertical;
        horizontalDimensionAxis = basis.HorizontalAxis;
        verticalDimensionAxis = basis.VerticalAxis;
        UpdateLayout();
    }

    public void Hide()
    {
        SetAllHandlesState(ResizeHandleVisualState.Normal);
        target = null;
        gameObject.SetActive(false);
    }

    public void UpdateLayout()
    {
        if (target == null)
            return;

        facePlanePoint = target.GetFaceCenterWorld(selectedFace);
        Vector3 gizmoCenter = facePlanePoint + faceNormalWorld * surfaceOffset;
        float horizontalSize = target.GetWorldSize(horizontalDimensionAxis);
        float verticalSize = target.GetWorldSize(verticalDimensionAxis);

        transform.SetPositionAndRotation(gizmoCenter, Quaternion.LookRotation(faceNormalWorld, verticalScreenAxisWorld));
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
    }

    public ResizeDirection GetResizeDirection(ResizeHandlePosition position)
    {
        if (target == null)
            return ResizeDirection.PositiveX;

        return target.GetResizeDirectionForWorldAxis(GetDragAxisWorld(position));
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
        return handle != null
            && (handle == topHandle || handle == bottomHandle || handle == leftHandle || handle == rightHandle);
    }

    public void SetAllHandlesState(ResizeHandleVisualState state)
    {
        SetState(topHandle, state);
        SetState(bottomHandle, state);
        SetState(leftHandle, state);
        SetState(rightHandle, state);
    }

    public static ResizeFace SelectFace(
        ResizableBlock block,
        Vector3 hitNormal,
        bool hasHitNormal,
        Vector3 cameraPosition)
    {
        if (block == null)
            return ResizeFace.PositiveZ;

        if (hasHitNormal)
        {
            Vector3 localNormal = block.transform.InverseTransformDirection(hitNormal);
            if (ResizableBlock.TryGetFaceFromLocalNormal(localNormal, out ResizeFace hitFace))
                return hitFace;
        }

        return SelectFaceByCamera(block, cameraPosition);
    }

    public static ResizeFace SelectFaceByCamera(ResizableBlock block, Vector3 cameraPosition)
    {
        ResizeFace bestFace = ResizeFace.PositiveZ;
        float bestFacingScore = float.NegativeInfinity;
        float bestDistance = float.PositiveInfinity;

        foreach (ResizeFace face in System.Enum.GetValues(typeof(ResizeFace)))
        {
            Vector3 center = block.GetFaceCenterWorld(face);
            Vector3 toCamera = cameraPosition - center;
            float distance = toCamera.magnitude;
            Vector3 direction = distance > DirectionEpsilon ? toCamera / distance : block.GetFaceNormalWorld(face);
            float facingScore = Vector3.Dot(block.GetFaceNormalWorld(face), direction);
            if (facingScore < 0f)
                continue;

            if (!IsBetterFaceCandidate(facingScore, distance, bestFacingScore, bestDistance))
                continue;

            bestFace = face;
            bestFacingScore = facingScore;
            bestDistance = distance;
        }

        return bestFace;
    }

    public static bool IsBetterFaceCandidate(
        float facingScore,
        float distance,
        float bestFacingScore,
        float bestDistance)
    {
        return facingScore > bestFacingScore + ScoreEpsilon
            || (Mathf.Abs(facingScore - bestFacingScore) <= ScoreEpsilon && distance < bestDistance);
    }

    public static BlockResizeGizmoBasis CalculateFaceBasis(
        ResizableBlock block,
        ResizeFace face,
        Camera camera)
    {
        block.GetFacePlaneAxes(face, out int firstAxis, out int secondAxis, out _);
        Vector3 normal = block.GetFaceNormalWorld(face);
        Vector3 first = block.GetWorldAxis(firstAxis);
        Vector3 second = block.GetWorldAxis(secondAxis);

        Vector3 cameraRight = camera != null ? camera.transform.right : first;
        Vector3 cameraUp = camera != null ? camera.transform.up : second;
        Vector3 projectedCameraRight = Vector3.ProjectOnPlane(cameraRight, normal);
        if (projectedCameraRight.sqrMagnitude <= DirectionEpsilon)
            projectedCameraRight = Vector3.ProjectOnPlane(cameraUp, normal);
        if (projectedCameraRight.sqrMagnitude <= DirectionEpsilon)
            projectedCameraRight = first;
        projectedCameraRight.Normalize();

        float firstAlignment = Mathf.Abs(Vector3.Dot(first, projectedCameraRight));
        float secondAlignment = Mathf.Abs(Vector3.Dot(second, projectedCameraRight));
        int horizontalAxis = firstAlignment >= secondAlignment ? firstAxis : secondAxis;
        int verticalAxis = horizontalAxis == firstAxis ? secondAxis : firstAxis;
        Vector3 horizontal = horizontalAxis == firstAxis ? first : second;
        if (Vector3.Dot(horizontal, projectedCameraRight) < 0f)
            horizontal = -horizontal;

        Vector3 vertical = Vector3.Cross(normal, horizontal).normalized;
        Vector3 projectedCameraUp = Vector3.ProjectOnPlane(cameraUp, normal);
        if (projectedCameraUp.sqrMagnitude > DirectionEpsilon
            && Vector3.Dot(vertical, projectedCameraUp.normalized) < 0f)
        {
            vertical = -vertical;
        }

        return new BlockResizeGizmoBasis(normal, horizontal, vertical, horizontalAxis, verticalAxis);
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
            faceCenter + horizontalOffset
        );
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
        topHandle = null;
        bottomHandle = null;
        leftHandle = null;
        rightHandle = null;

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
            }
        }
    }

    private static void SetState(BlockResizeHandle handle, ResizeHandleVisualState state)
    {
        if (handle != null)
            handle.SetVisualState(state);
    }
}
