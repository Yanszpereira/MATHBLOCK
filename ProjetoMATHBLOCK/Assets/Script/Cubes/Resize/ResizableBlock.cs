using UnityEngine;

public enum ResizeFace
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

public enum ResizeDirection
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

public enum ResizeValidationFailure
{
    None,
    InvalidValue,
    DimensionBelowMinimum,
    VolumeLimitExceeded,
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
    public int MaximumVolume => mathBlockValue != null ? mathBlockValue.CurrentValue : 0;
    public long CurrentVolume => CalculateVolume(Dimensions);
    public Vector3 UnitSize => unitSize;
    public Vector3 WorldCenter => blockCollider != null ? blockCollider.transform.TransformPoint(blockCollider.center) : transform.position;
    public Bounds WorldBounds => blockCollider != null ? blockCollider.bounds : new Bounds(transform.position, Vector3.zero);

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
            ApplyCurrentDimensions();
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

        if (proposedWidth < MinimumDimension || proposedHeight < MinimumDimension || proposedDepth < MinimumDimension)
        {
            failure = ResizeValidationFailure.DimensionBelowMinimum;
            return false;
        }

        long proposedVolume = (long)proposedWidth * proposedHeight * proposedDepth;
        if (proposedVolume > mathBlockValue.CurrentValue)
        {
            failure = ResizeValidationFailure.VolumeLimitExceeded;
            return false;
        }

        failure = ResizeValidationFailure.None;
        return true;
    }

    public bool TryCreateResizeProposal(
        ResizeFace face,
        ResizeDirection direction,
        int deltaUnits,
        out BlockResizeProposal proposal,
        out ResizeValidationFailure failure)
    {
        return TryCreateResizeProposal(CaptureState(), face, direction, deltaUnits, out proposal, out failure);
    }

    public bool TryCreateResizeProposal(
        ResizableBlockState baseState,
        ResizeFace face,
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

        int resizedAxis = GetDirectionAxis(direction);
        if (resizedAxis == GetFaceNormalAxis(face))
        {
            failure = ResizeValidationFailure.FixedDimensionChanged;
            return false;
        }

        Vector3Int proposedDimensions = baseState.Dimensions;
        AddToAxis(ref proposedDimensions, resizedAxis, deltaUnits);

        if (!IsDimensionProposalValid(
            proposedDimensions.x,
            proposedDimensions.y,
            proposedDimensions.z,
            out failure))
        {
            return false;
        }

        float directionSign = IsPositiveDirection(direction) ? 1f : -1f;
        Vector3 centerOffset = GetAxisOffset(resizedAxis, deltaUnits, directionSign);
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

    public bool CanResize(
        ResizeFace face,
        ResizeDirection direction,
        int deltaUnits,
        out ResizeValidationFailure failure)
    {
        if (!TryCreateResizeProposal(face, direction, deltaUnits, out BlockResizeProposal proposal, out failure))
            return false;

        return IsProposalValid(proposal, out failure);
    }

    public bool TryApplyResize(
        ResizeFace face,
        ResizeDirection direction,
        int deltaUnits,
        out ResizeValidationFailure failure)
    {
        if (!TryCreateResizeProposal(face, direction, deltaUnits, out BlockResizeProposal proposal, out failure))
            return false;

        if (!IsProposalValid(proposal, out failure))
            return false;

        ApplyProposal(proposal);
        failure = ResizeValidationFailure.None;
        return true;
    }

    public bool TryApplyResizeFromState(
        ResizableBlockState baseState,
        ResizeFace face,
        ResizeDirection direction,
        int deltaUnits,
        out ResizeValidationFailure failure)
    {
        if (!TryCreateResizeProposal(baseState, face, direction, deltaUnits, out BlockResizeProposal proposal, out failure))
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

        ApplyProposal(CreateDirectProposal(stateDimensions, state.Position, state.Rotation));
    }

    public Vector3 GetFaceNormalWorld(ResizeFace face)
    {
        int axis = GetFaceNormalAxis(face);
        float sign = IsPositiveFace(face) ? 1f : -1f;
        return GetWorldAxis(axis) * sign;
    }

    public Vector3 GetFaceCenterWorld(ResizeFace face)
    {
        int axis = GetFaceNormalAxis(face);
        float halfSize = GetWorldSize(axis) * 0.5f;
        return WorldCenter + GetFaceNormalWorld(face) * halfSize;
    }

    public void GetFacePlaneAxes(ResizeFace face, out int firstAxis, out int secondAxis, out int normalAxis)
    {
        normalAxis = GetFaceNormalAxis(face);
        switch (normalAxis)
        {
            case 0:
                firstAxis = 1;
                secondAxis = 2;
                break;
            case 1:
                firstAxis = 0;
                secondAxis = 2;
                break;
            default:
                firstAxis = 0;
                secondAxis = 1;
                break;
        }
    }

    public Vector3 GetWorldAxis(int axis)
    {
        Vector3 worldAxis = transform.TransformDirection(GetLocalAxis(axis));
        return worldAxis.sqrMagnitude > 0f ? worldAxis.normalized : Vector3.zero;
    }

    public float GetWorldUnitSize(int axis)
    {
        Vector3 worldUnit = transform.TransformVector(GetLocalAxis(axis) * GetAxis(unitSize, axis));
        return worldUnit.magnitude;
    }

    public float GetWorldSize(int axis)
    {
        return GetAxis(Dimensions, axis) * GetWorldUnitSize(axis);
    }

    public float GetWorldUnitSize(ResizeDirection direction)
    {
        return GetWorldUnitSize(GetDirectionAxis(direction));
    }

    public ResizeDirection GetResizeDirectionForWorldAxis(Vector3 worldAxis)
    {
        Vector3 normalized = worldAxis.sqrMagnitude > MinimumUnitSize ? worldAxis.normalized : GetWorldAxis(0);
        int bestAxis = 0;
        float bestDot = Vector3.Dot(normalized, GetWorldAxis(0));
        float bestAlignment = Mathf.Abs(bestDot);

        for (int axis = 1; axis < 3; axis++)
        {
            float dot = Vector3.Dot(normalized, GetWorldAxis(axis));
            if (Mathf.Abs(dot) > bestAlignment)
            {
                bestAxis = axis;
                bestDot = dot;
                bestAlignment = Mathf.Abs(dot);
            }
        }

        return GetDirection(bestAxis, bestDot >= 0f);
    }

    public static bool TryGetFaceFromLocalNormal(
        Vector3 localNormal,
        out ResizeFace face,
        float minimumAxisAlignment = 0.75f)
    {
        face = ResizeFace.PositiveZ;
        if (localNormal.sqrMagnitude <= MinimumUnitSize)
            return false;

        Vector3 normalized = localNormal.normalized;
        float absX = Mathf.Abs(normalized.x);
        float absY = Mathf.Abs(normalized.y);
        float absZ = Mathf.Abs(normalized.z);
        float dominant = Mathf.Max(absX, Mathf.Max(absY, absZ));
        if (dominant < Mathf.Clamp01(minimumAxisAlignment))
            return false;

        if (absX >= absY && absX >= absZ)
            face = normalized.x >= 0f ? ResizeFace.PositiveX : ResizeFace.NegativeX;
        else if (absY >= absZ)
            face = normalized.y >= 0f ? ResizeFace.PositiveY : ResizeFace.NegativeY;
        else
            face = normalized.z >= 0f ? ResizeFace.PositiveZ : ResizeFace.NegativeZ;

        return true;
    }

    public static int GetFaceNormalAxis(ResizeFace face)
    {
        switch (face)
        {
            case ResizeFace.PositiveX:
            case ResizeFace.NegativeX:
                return 0;
            case ResizeFace.PositiveY:
            case ResizeFace.NegativeY:
                return 1;
            default:
                return 2;
        }
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

    private BlockResizeProposal CreateDirectProposal(Vector3Int dimensions, Vector3 rootPosition, Quaternion rotation)
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

    private Vector3 CalculateLocalSize(Vector3Int dimensions)
    {
        return Vector3.Scale(unitSize, new Vector3(dimensions.x, dimensions.y, dimensions.z));
    }

    private Vector3 GetAxisOffset(int axis, int deltaUnits, float directionSign)
    {
        Vector3 worldUnit = transform.TransformVector(GetLocalAxis(axis) * GetAxis(unitSize, axis));
        return worldUnit * (deltaUnits * directionSign * 0.5f);
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
        return mathBlockValue != null && visualRoot != null && blockCollider != null;
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

    private static long CalculateVolume(Vector3Int dimensions)
    {
        return (long)dimensions.x * dimensions.y * dimensions.z;
    }

    private static int GetDirectionAxis(ResizeDirection direction)
    {
        switch (direction)
        {
            case ResizeDirection.PositiveX:
            case ResizeDirection.NegativeX:
                return 0;
            case ResizeDirection.PositiveY:
            case ResizeDirection.NegativeY:
                return 1;
            default:
                return 2;
        }
    }

    private static bool IsPositiveDirection(ResizeDirection direction)
    {
        return direction == ResizeDirection.PositiveX
            || direction == ResizeDirection.PositiveY
            || direction == ResizeDirection.PositiveZ;
    }

    private static bool IsPositiveFace(ResizeFace face)
    {
        return face == ResizeFace.PositiveX || face == ResizeFace.PositiveY || face == ResizeFace.PositiveZ;
    }

    private static ResizeDirection GetDirection(int axis, bool positive)
    {
        switch (axis)
        {
            case 0: return positive ? ResizeDirection.PositiveX : ResizeDirection.NegativeX;
            case 1: return positive ? ResizeDirection.PositiveY : ResizeDirection.NegativeY;
            default: return positive ? ResizeDirection.PositiveZ : ResizeDirection.NegativeZ;
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
            case 0: vector.x += amount; break;
            case 1: vector.y += amount; break;
            default: vector.z += amount; break;
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
}
