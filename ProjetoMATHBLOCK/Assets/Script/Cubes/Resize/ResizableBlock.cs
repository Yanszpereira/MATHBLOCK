using UnityEngine;

public enum ResizePlane
{
    XY,
    XZ,
    YZ
}

public enum ResizeDirection
{
    PositiveHorizontal,
    NegativeHorizontal,
    PositiveVertical,
    NegativeVertical,
    Center
}

public enum ResizeValidationFailure
{
    None,
    InvalidValue,
    DimensionBelowMinimum,
    AreaLimitExceeded,
    FixedDimensionChanged,
    SpaceBlocked,
    MissingReference
}

public readonly struct BlockResizeProposal
{
    public Vector3Int Dimensions { get; }
    public Vector3 RootPosition { get; }
    public Vector3 WorldCenter { get; }
    public Quaternion Rotation { get; }
    public Vector3 ColliderSize { get; }
    public Vector3 VisualScale { get; }

    public BlockResizeProposal(
        Vector3Int dimensions,
        Vector3 rootPosition,
        Vector3 worldCenter,
        Quaternion rotation,
        Vector3 colliderSize,
        Vector3 visualScale)
    {
        Dimensions = dimensions;
        RootPosition = rootPosition;
        WorldCenter = worldCenter;
        Rotation = rotation;
        ColliderSize = colliderSize;
        VisualScale = visualScale;
    }
}

public readonly struct ResizableBlockState
{
    public Vector3Int Dimensions { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }

    public ResizableBlockState(Vector3Int dimensions, Vector3 position, Quaternion rotation)
    {
        Dimensions = dimensions;
        Position = position;
        Rotation = rotation;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(MathBlockValue), typeof(BoxCollider), typeof(Rigidbody))]
public class ResizableBlock : MonoBehaviour
{
    private const int MinimumDimension = 1;
    private const float MinimumUnitSize = 0.0001f;

    [Header("References")]
    [SerializeField] private MathBlockValue mathBlockValue;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private BoxCollider blockCollider;
    [SerializeField] private Rigidbody blockRigidbody;

    [Header("Logical Dimensions")]
    [SerializeField] private ResizePlane resizePlane = ResizePlane.XY;
    [SerializeField, Min(MinimumDimension)] private int width = MinimumDimension;
    [SerializeField, Min(MinimumDimension)] private int height = MinimumDimension;
    [SerializeField, Min(MinimumDimension)] private int depth = MinimumDimension;
    [SerializeField] private Vector3 unitSize = Vector3.one;

    [Header("Collision Validation")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField, Min(0f)] private float collisionSkin = 0.01f;

    public int Width => width;
    public int Height => height;
    public int Depth => depth;
    public Vector3Int Dimensions => new Vector3Int(width, height, depth);
    public ResizePlane Plane => resizePlane;
    public int MaximumArea => mathBlockValue != null ? mathBlockValue.CurrentValue : 0;
    public int CurrentArea => GetPlaneArea(Dimensions);
    public Vector3 UnitSize => unitSize;
    public Vector3 WorldCenter => blockCollider != null ? blockCollider.bounds.center : transform.position;
    public Bounds WorldBounds => blockCollider != null ? blockCollider.bounds : new Bounds(transform.position, Vector3.zero);
    public Vector3 HorizontalAxisWorld => GetWorldAxis(GetPlaneAxes().HorizontalAxis);
    public Vector3 VerticalAxisWorld => GetWorldAxis(GetPlaneAxes().VerticalAxis);
    public Vector3 FaceNormalWorld => GetWorldAxis(GetPlaneAxes().FixedAxis);
    public float HorizontalUnitWorldSize => GetWorldUnitSize(GetPlaneAxes().HorizontalAxis);
    public float VerticalUnitWorldSize => GetWorldUnitSize(GetPlaneAxes().VerticalAxis);
    public float FixedUnitWorldSize => GetWorldUnitSize(GetPlaneAxes().FixedAxis);

    private void Reset()
    {
        ResolveReferences();
        NormalizeSerializedValues();
    }

    private void Awake()
    {
        ResolveReferences();
        NormalizeSerializedValues();
        ApplyCurrentDimensions();
    }

    private void OnValidate()
    {
        ResolveReferences();
        NormalizeSerializedValues();

        if (!Application.isPlaying)
        {
            ApplyCurrentDimensions();
        }
    }

    public void GetActivePlaneDimensions(out int horizontal, out int vertical, out int fixedDimension)
    {
        PlaneAxes axes = GetPlaneAxes();
        Vector3Int dimensions = Dimensions;
        horizontal = GetAxis(dimensions, axes.HorizontalAxis);
        vertical = GetAxis(dimensions, axes.VerticalAxis);
        fixedDimension = GetAxis(dimensions, axes.FixedAxis);
    }

    public void GetValidFaceNormals(out Vector3 positiveNormal, out Vector3 negativeNormal)
    {
        positiveNormal = FaceNormalWorld;
        negativeNormal = -positiveNormal;
    }

    public bool CanResize()
    {
        return IsDimensionProposalValid(width, height, depth, out _);
    }

    public bool IsDimensionProposalValid(
        int proposedWidth,
        int proposedHeight,
        int proposedDepth,
        out ResizeValidationFailure failure)
    {
        ResolveReferences();

        if (mathBlockValue == null)
        {
            failure = ResizeValidationFailure.MissingReference;
            return false;
        }

        if (mathBlockValue.CurrentValue <= 0)
        {
            failure = ResizeValidationFailure.InvalidValue;
            return false;
        }

        Vector3Int proposedDimensions = new Vector3Int(proposedWidth, proposedHeight, proposedDepth);
        if (proposedWidth < MinimumDimension || proposedHeight < MinimumDimension || proposedDepth < MinimumDimension)
        {
            failure = ResizeValidationFailure.DimensionBelowMinimum;
            return false;
        }

        PlaneAxes axes = GetPlaneAxes();
        if (GetAxis(proposedDimensions, axes.FixedAxis) != GetAxis(Dimensions, axes.FixedAxis))
        {
            failure = ResizeValidationFailure.FixedDimensionChanged;
            return false;
        }

        long proposedArea = (long)GetAxis(proposedDimensions, axes.HorizontalAxis)
            * GetAxis(proposedDimensions, axes.VerticalAxis);
        if (proposedArea > mathBlockValue.CurrentValue)
        {
            failure = ResizeValidationFailure.AreaLimitExceeded;
            return false;
        }

        failure = ResizeValidationFailure.None;
        return true;
    }

    public bool TryCreateResizeProposal(
        ResizeDirection direction,
        int deltaUnits,
        out BlockResizeProposal proposal,
        out ResizeValidationFailure failure)
    {
        return TryCreateResizeProposal(CaptureState(), direction, deltaUnits, out proposal, out failure);
    }

    public bool TryCreateResizeProposal(
        ResizableBlockState baseState,
        ResizeDirection direction,
        int deltaUnits,
        out BlockResizeProposal proposal,
        out ResizeValidationFailure failure)
    {
        ResolveReferences();
        proposal = default;

        if (!HasRequiredReferences())
        {
            failure = ResizeValidationFailure.MissingReference;
            return false;
        }

        PlaneAxes axes = GetPlaneAxes();
        Vector3Int proposedDimensions = baseState.Dimensions;
        Vector3 centerOffset = Vector3.zero;

        switch (direction)
        {
            case ResizeDirection.PositiveHorizontal:
                AddToAxis(ref proposedDimensions, axes.HorizontalAxis, deltaUnits);
                centerOffset = GetAxisOffset(axes.HorizontalAxis, deltaUnits, 1f);
                break;

            case ResizeDirection.NegativeHorizontal:
                AddToAxis(ref proposedDimensions, axes.HorizontalAxis, deltaUnits);
                centerOffset = GetAxisOffset(axes.HorizontalAxis, deltaUnits, -1f);
                break;

            case ResizeDirection.PositiveVertical:
                AddToAxis(ref proposedDimensions, axes.VerticalAxis, deltaUnits);
                centerOffset = GetAxisOffset(axes.VerticalAxis, deltaUnits, 1f);
                break;

            case ResizeDirection.NegativeVertical:
                AddToAxis(ref proposedDimensions, axes.VerticalAxis, deltaUnits);
                centerOffset = GetAxisOffset(axes.VerticalAxis, deltaUnits, -1f);
                break;

            case ResizeDirection.Center:
                AddToAxis(ref proposedDimensions, axes.HorizontalAxis, deltaUnits);
                AddToAxis(ref proposedDimensions, axes.VerticalAxis, deltaUnits);
                break;

            default:
                failure = ResizeValidationFailure.MissingReference;
                return false;
        }

        if (!IsDimensionProposalValid(
            proposedDimensions.x,
            proposedDimensions.y,
            proposedDimensions.z,
            out failure))
        {
            return false;
        }

        Vector3 rootPosition = baseState.Position + centerOffset;
        Vector3 localSize = CalculateLocalSize(proposedDimensions);
        Vector3 colliderCenterOffset = baseState.Rotation * Vector3.Scale(blockCollider.center, Abs(transform.lossyScale));
        Vector3 worldCenter = rootPosition + colliderCenterOffset;

        proposal = new BlockResizeProposal(
            proposedDimensions,
            rootPosition,
            worldCenter,
            baseState.Rotation,
            localSize,
            localSize
        );
        failure = ResizeValidationFailure.None;
        return true;
    }

    public bool IsProposalValid(BlockResizeProposal proposal, out ResizeValidationFailure failure)
    {
        if (!IsDimensionProposalValid(
            proposal.Dimensions.x,
            proposal.Dimensions.y,
            proposal.Dimensions.z,
            out failure))
        {
            return false;
        }

        return IsSpaceAvailable(proposal, out failure);
    }

    public bool CanResize(ResizeDirection direction, int deltaUnits, out ResizeValidationFailure failure)
    {
        if (!TryCreateResizeProposal(direction, deltaUnits, out BlockResizeProposal proposal, out failure))
            return false;

        return IsProposalValid(proposal, out failure);
    }

    public bool TryApplyResize(
        ResizeDirection direction,
        int deltaUnits,
        out ResizeValidationFailure failure)
    {
        if (!TryCreateResizeProposal(direction, deltaUnits, out BlockResizeProposal proposal, out failure))
            return false;

        if (!IsProposalValid(proposal, out failure))
            return false;

        ApplyProposal(proposal);
        failure = ResizeValidationFailure.None;
        return true;
    }

    public bool TryApplyResizeFromState(
        ResizableBlockState baseState,
        ResizeDirection direction,
        int deltaUnits,
        out ResizeValidationFailure failure)
    {
        if (!TryCreateResizeProposal(baseState, direction, deltaUnits, out BlockResizeProposal proposal, out failure))
            return false;

        if (!IsProposalValid(proposal, out failure))
            return false;

        ApplyProposal(proposal);
        failure = ResizeValidationFailure.None;
        return true;
    }

    public ResizableBlockState CaptureState()
    {
        return new ResizableBlockState(Dimensions, transform.position, transform.rotation);
    }

    public void RestoreState(ResizableBlockState state)
    {
        Vector3Int stateDimensions = state.Dimensions;
        if (stateDimensions.x < MinimumDimension
            || stateDimensions.y < MinimumDimension
            || stateDimensions.z < MinimumDimension)
        {
            Debug.LogWarning($"{name}: estado de redimensionamento ignorado porque possui dimensoes menores que 1.", this);
            return;
        }

        ResolveReferences();
        if (!HasRequiredReferences())
        {
            Debug.LogError($"{name}: nao foi possivel restaurar o estado porque existem referencias ausentes.", this);
            return;
        }

        BlockResizeProposal proposal = CreateDirectProposal(stateDimensions, state.Position, state.Rotation);
        ApplyProposal(proposal);
    }

    private bool IsSpaceAvailable(BlockResizeProposal proposal, out ResizeValidationFailure failure)
    {
        if (blockCollider == null)
        {
            failure = ResizeValidationFailure.MissingReference;
            return false;
        }

        if (!IsGrowth(proposal.Dimensions) || obstacleMask.value == 0)
        {
            failure = ResizeValidationFailure.None;
            return true;
        }

        Vector3 lossyScale = Abs(transform.lossyScale);
        Vector3 worldSize = Vector3.Scale(proposal.ColliderSize, lossyScale);
        Vector3 halfExtents = worldSize * 0.5f;
        halfExtents = new Vector3(
            Mathf.Max(MinimumUnitSize, halfExtents.x - collisionSkin),
            Mathf.Max(MinimumUnitSize, halfExtents.y - collisionSkin),
            Mathf.Max(MinimumUnitSize, halfExtents.z - collisionSkin)
        );

        Collider[] overlaps = Physics.OverlapBox(
            proposal.WorldCenter,
            halfExtents,
            proposal.Rotation,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null || IsOwnCollider(overlap) || overlap.gameObject.layer == LayerMask.NameToLayer("ResizeHandle"))
                continue;

            failure = ResizeValidationFailure.SpaceBlocked;
            return false;
        }

        failure = ResizeValidationFailure.None;
        return true;
    }

    private void ApplyProposal(BlockResizeProposal proposal)
    {
        bool previousIsKinematic = false;
        bool previousUseGravity = false;

        if (blockRigidbody != null)
        {
            previousIsKinematic = blockRigidbody.isKinematic;
            previousUseGravity = blockRigidbody.useGravity;
            blockRigidbody.useGravity = false;
            blockRigidbody.isKinematic = true;
        }

        try
        {
            width = proposal.Dimensions.x;
            height = proposal.Dimensions.y;
            depth = proposal.Dimensions.z;

            transform.localScale = Vector3.one;
            transform.SetPositionAndRotation(proposal.RootPosition, proposal.Rotation);
            blockCollider.size = proposal.ColliderSize;
            visualRoot.localScale = proposal.VisualScale;
            Physics.SyncTransforms();
            mathBlockValue.RefreshVisualLayout();
        }
        finally
        {
            if (blockRigidbody != null)
            {
                blockRigidbody.isKinematic = previousIsKinematic;
                blockRigidbody.useGravity = previousUseGravity;
            }
        }
    }

    private void ApplyCurrentDimensions()
    {
        if (!HasRequiredReferences())
            return;

        transform.localScale = Vector3.one;
        Vector3 localSize = CalculateLocalSize(Dimensions);
        blockCollider.size = localSize;
        visualRoot.localScale = localSize;
        mathBlockValue.RefreshVisualLayout();
    }

    private BlockResizeProposal CreateDirectProposal(
        Vector3Int dimensions,
        Vector3 rootPosition,
        Quaternion rotation)
    {
        Vector3 localSize = CalculateLocalSize(dimensions);
        Vector3 colliderCenterOffset = rotation * Vector3.Scale(blockCollider.center, Abs(transform.lossyScale));
        return new BlockResizeProposal(
            dimensions,
            rootPosition,
            rootPosition + colliderCenterOffset,
            rotation,
            localSize,
            localSize
        );
    }

    private int GetPlaneArea(Vector3Int dimensions)
    {
        PlaneAxes axes = GetPlaneAxes();
        return GetAxis(dimensions, axes.HorizontalAxis) * GetAxis(dimensions, axes.VerticalAxis);
    }

    private Vector3 CalculateLocalSize(Vector3Int dimensions)
    {
        return Vector3.Scale(unitSize, new Vector3(dimensions.x, dimensions.y, dimensions.z));
    }

    private Vector3 GetAxisOffset(int axis, int deltaUnits, float directionSign)
    {
        Vector3 localAxis = GetLocalAxis(axis);
        float localUnitLength = GetAxis(unitSize, axis);
        Vector3 worldUnit = transform.TransformVector(localAxis * localUnitLength);
        return worldUnit * (deltaUnits * directionSign * 0.5f);
    }

    private Vector3 GetWorldAxis(int axis)
    {
        Vector3 worldAxis = transform.TransformDirection(GetLocalAxis(axis));
        return worldAxis.sqrMagnitude > 0f ? worldAxis.normalized : Vector3.zero;
    }

    private float GetWorldUnitSize(int axis)
    {
        Vector3 worldUnit = transform.TransformVector(GetLocalAxis(axis) * GetAxis(unitSize, axis));
        return worldUnit.magnitude;
    }

    private bool IsGrowth(Vector3Int proposedDimensions)
    {
        return proposedDimensions.x > width
            || proposedDimensions.y > height
            || proposedDimensions.z > depth;
    }

    private bool IsOwnCollider(Collider targetCollider)
    {
        Transform targetTransform = targetCollider.transform;
        return targetTransform == transform || targetTransform.IsChildOf(transform);
    }

    private bool HasRequiredReferences()
    {
        return mathBlockValue != null
            && visualRoot != null
            && blockCollider != null;
    }

    private void ResolveReferences()
    {
        if (mathBlockValue == null)
            mathBlockValue = GetComponent<MathBlockValue>();

        if (blockCollider == null)
            blockCollider = GetComponent<BoxCollider>();

        if (blockRigidbody == null)
            blockRigidbody = GetComponent<Rigidbody>();

        if (visualRoot == null)
        {
            Transform blockVisual = transform.Find("BlockVisual");
            if (blockVisual != null)
                visualRoot = blockVisual;
        }
    }

    private void NormalizeSerializedValues()
    {
        width = Mathf.Max(MinimumDimension, width);
        height = Mathf.Max(MinimumDimension, height);
        depth = Mathf.Max(MinimumDimension, depth);
        unitSize = new Vector3(
            Mathf.Max(MinimumUnitSize, Mathf.Abs(unitSize.x)),
            Mathf.Max(MinimumUnitSize, Mathf.Abs(unitSize.y)),
            Mathf.Max(MinimumUnitSize, Mathf.Abs(unitSize.z))
        );
        collisionSkin = Mathf.Max(0f, collisionSkin);
    }

    private PlaneAxes GetPlaneAxes()
    {
        switch (resizePlane)
        {
            case ResizePlane.XZ:
                return new PlaneAxes(0, 2, 1);

            case ResizePlane.YZ:
                return new PlaneAxes(1, 2, 0);

            default:
                return new PlaneAxes(0, 1, 2);
        }
    }

    private static int GetAxis(Vector3Int vector, int axis)
    {
        switch (axis)
        {
            case 0: return vector.x;
            case 1: return vector.y;
            default: return vector.z;
        }
    }

    private static float GetAxis(Vector3 vector, int axis)
    {
        switch (axis)
        {
            case 0: return vector.x;
            case 1: return vector.y;
            default: return vector.z;
        }
    }

    private static void AddToAxis(ref Vector3Int vector, int axis, int amount)
    {
        switch (axis)
        {
            case 0:
                vector.x += amount;
                break;

            case 1:
                vector.y += amount;
                break;

            default:
                vector.z += amount;
                break;
        }
    }

    private static Vector3 GetLocalAxis(int axis)
    {
        switch (axis)
        {
            case 0: return Vector3.right;
            case 1: return Vector3.up;
            default: return Vector3.forward;
        }
    }

    private static Vector3 Abs(Vector3 vector)
    {
        return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
    }

    private readonly struct PlaneAxes
    {
        public int HorizontalAxis { get; }
        public int VerticalAxis { get; }
        public int FixedAxis { get; }

        public PlaneAxes(int horizontalAxis, int verticalAxis, int fixedAxis)
        {
            HorizontalAxis = horizontalAxis;
            VerticalAxis = verticalAxis;
            FixedAxis = fixedAxis;
        }
    }
}
