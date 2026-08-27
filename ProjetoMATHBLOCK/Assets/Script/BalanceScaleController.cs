using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Reads MathBlockValue from blocks resting on either tray and drives the
/// balance's visual response. It does not open doors or change scenes.
/// </summary>
[DisallowMultipleComponent]
public sealed class BalanceScaleController : MonoBehaviour
{
    [Serializable]
    private struct SideSensorSettings
    {
        public Vector3 localCenter;
        public Vector3 size;
    }

    private sealed class BlockContactInfo
    {
        public MathBlockValue value;
        public bool directLeft;
        public bool directRight;
        public int leftDistance = int.MaxValue;
        public int rightDistance = int.MaxValue;
    }

    private struct QueueEntry
    {
        public MathBlockValue value;
        public int distance;
        public bool left;

        public QueueEntry(MathBlockValue value, int distance, bool left)
        {
            this.value = value;
            this.distance = distance;
            this.left = left;
        }
    }

    private const string SensorRootName = "BalanceSensors";
    private const string LeftSensorName = "LeftBalanceSensor";
    private const string RightSensorName = "RightBalanceSensor";
    private const float ExactTieEpsilon = 0.0001f;

    [Header("Detection")]
    [SerializeField] private SideSensorSettings leftSensor = new SideSensorSettings
    {
        localCenter = new Vector3(-13f, 27f, 0f),
        size = new Vector3(13f, 8f, 15f)
    };
    [SerializeField] private SideSensorSettings rightSensor = new SideSensorSettings
    {
        localCenter = new Vector3(13f, 27f, 0f),
        size = new Vector3(13f, 8f, 15f)
    };
    [SerializeField, Min(0f)] private float contactTolerance = 0.08f;
    [SerializeField] private LayerMask blockLayers = ~0;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.1f;
    [SerializeField] private bool drawSensorGizmos = true;

    [Header("Visual References")]
    [SerializeField] private Transform apoioBalancas;
    [SerializeField] private Transform corpo;
    [SerializeField] private Transform combinedTrays;
    [SerializeField] private Transform leftTrayVisual;
    [SerializeField] private Transform rightTrayVisual;

    [Header("Visual Response")]
    [SerializeField, Range(0f, 45f)] private float maximumTiltAngle = 8f;
    [SerializeField, Min(0f)] private float maximumVerticalOffset = 0.25f;
    [SerializeField, Min(0f)] private float maximumLateralOffset = 0.15f;
    [SerializeField, Min(0.01f)] private float smoothingTime = 1.5f;
    [SerializeField, Min(0f)] private float balanceTolerance = 0f;

    [Header("Events")]
    public UnityEvent OnBalanced = new UnityEvent();

    private readonly Dictionary<MathBlockValue, BlockContactInfo> contacts =
        new Dictionary<MathBlockValue, BlockContactInfo>();
    private readonly HashSet<MathBlockValue> directLeftBlocks = new HashSet<MathBlockValue>();
    private readonly HashSet<MathBlockValue> directRightBlocks = new HashSet<MathBlockValue>();
    private readonly List<BalanceLoadMarker> activeMarkers = new List<BalanceLoadMarker>();
    private readonly Queue<QueueEntry> pending = new Queue<QueueEntry>();
    private readonly Collider[] nearbyColliders = new Collider[128];

    private BoxCollider leftSensorCollider;
    private BoxCollider rightSensorCollider;
    private GravityInteract gravityInteract;
    private Quaternion originalSupportRotation;
    private Quaternion originalLeftTrayRotation;
    private Quaternion originalRightTrayRotation;
    private Quaternion originalCombinedTrayRotation;
    private Vector3 originalLeftTraySupportPosition;
    private Vector3 originalRightTraySupportPosition;
    private Vector3 originalCombinedTraySupportPosition;
    private float targetSupportAngle;
    private float currentSupportAngle;
    private float supportAngleVelocity;
    private float scanTimer;
    private bool hasBalanceState;
    private bool lastBalanced;
    private bool hasWarnedAboutCombinedMesh;

    public int LeftWeight { get; private set; }
    public int RightWeight { get; private set; }
    public int WeightDifference => LeftWeight - RightWeight;
    public bool IsBalanced { get; private set; }
    public Transform ApoioBalancas => apoioBalancas;
    public Transform Corpo => corpo;

    private void Awake()
    {
        ResolveReferences();
        CreateSensors();
        CacheOriginalVisualState();
        gravityInteract = FindFirstObjectByType<GravityInteract>();
        RecalculateNow();
    }

    private void OnValidate()
    {
        leftSensor.size = ClampSensorSize(leftSensor.size);
        rightSensor.size = ClampSensorSize(rightSensor.size);
        contactTolerance = Mathf.Max(0f, contactTolerance);
        scanInterval = Mathf.Max(0.02f, scanInterval);
        smoothingTime = Mathf.Max(0.01f, smoothingTime);
        balanceTolerance = Mathf.Max(0f, balanceTolerance);
    }

    private void FixedUpdate()
    {
        scanTimer -= Time.fixedDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            RecalculateNow();
        }

        ApplyVisualResponse(Time.fixedDeltaTime);
    }

    /// <summary>Forces an immediate physics rescan; useful for tests and future UI.</summary>
    public void RecalculateNow()
    {
        if (leftSensorCollider == null || rightSensorCollider == null)
        {
            ResolveReferences();
            CreateSensors();
        }

        contacts.Clear();
        directLeftBlocks.Clear();
        directRightBlocks.Clear();
        ClearMarkers();

        if (gravityInteract == null)
            gravityInteract = FindFirstObjectByType<GravityInteract>();

        CollectDirectBlocks(leftSensorCollider, directLeftBlocks);
        CollectDirectBlocks(rightSensorCollider, directRightBlocks);

        foreach (MathBlockValue block in directLeftBlocks)
            GetOrCreateContact(block).directLeft = true;
        foreach (MathBlockValue block in directRightBlocks)
            GetOrCreateContact(block).directRight = true;

        TraverseContactChains();
        AssignWeightsAndMarkers();
        UpdateBalanceState();
    }

private void ResolveReferences()
    {
        if (apoioBalancas == null)
            apoioBalancas = FindChildByName("ApoioBalancas");
        if (corpo == null)
            corpo = FindChildByName("Corpo");
        if (combinedTrays == null)
            combinedTrays = FindChildByName("balancas");

        if (leftTrayVisual == null)
            leftTrayVisual = FindChildByName(
                "LeftTray",
                "BandejaEsquerda",
                "Bandeja_Esquerda",
                "BalancaE");
        if (rightTrayVisual == null)
            rightTrayVisual = FindChildByName(
                "RightTray",
                "BandejaDireita",
                "Bandeja_Direita",
                "BalancaD");
    }

    private Transform FindChildByName(params string[] names)
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (string candidateName in names)
        {
            foreach (Transform candidate in allChildren)
            {
                if (candidate != transform && string.Equals(candidate.name, candidateName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        return null;
    }

    private void CreateSensors()
    {
        Transform sensorRoot = transform.Find(SensorRootName);
        if (sensorRoot == null)
        {
            GameObject rootObject = new GameObject(SensorRootName);
            sensorRoot = rootObject.transform;
            sensorRoot.SetParent(transform, false);
        }

        leftSensorCollider = GetOrCreateSensor(sensorRoot, LeftSensorName, leftSensor);
        rightSensorCollider = GetOrCreateSensor(sensorRoot, RightSensorName, rightSensor);
    }

    private BoxCollider GetOrCreateSensor(Transform parent, string sensorName, SideSensorSettings settings)
    {
        Transform sensorTransform = parent.Find(sensorName);
        if (sensorTransform == null)
        {
            GameObject sensorObject = new GameObject(sensorName);
            sensorTransform = sensorObject.transform;
            sensorTransform.SetParent(parent, false);
        }

        sensorTransform.localPosition = settings.localCenter;
        sensorTransform.localRotation = Quaternion.identity;
        sensorTransform.localScale = Vector3.one;

        BoxCollider box = sensorTransform.GetComponent<BoxCollider>();
        if (box == null)
            box = sensorTransform.gameObject.AddComponent<BoxCollider>();

        box.isTrigger = true;
        box.size = ClampSensorSize(settings.size);
        box.center = Vector3.zero;
        return box;
    }

    private void CacheOriginalVisualState()
    {
        if (apoioBalancas != null)
            originalSupportRotation = apoioBalancas.localRotation;

        if (leftTrayVisual != null)
        {
            originalLeftTrayRotation = leftTrayVisual.rotation;
            originalLeftTraySupportPosition = GetSupportLocalPosition(leftTrayVisual);
        }

        if (rightTrayVisual != null)
        {
            originalRightTrayRotation = rightTrayVisual.rotation;
            originalRightTraySupportPosition = GetSupportLocalPosition(rightTrayVisual);
        }

        if (combinedTrays != null)
        {
            originalCombinedTrayRotation = combinedTrays.rotation;
            originalCombinedTraySupportPosition = GetSupportLocalPosition(combinedTrays);
        }
    }

private Vector3 GetSupportLocalPosition(Transform visual)
    {
        Vector3 visualAnchorWorld = GetVisualAnchorWorldPosition(visual);
        return apoioBalancas != null
            ? apoioBalancas.InverseTransformPoint(visualAnchorWorld)
            : transform.InverseTransformPoint(visualAnchorWorld);
    }

private Vector3 GetVisualAnchorWorldPosition(Transform visual)
    {
        if (visual == null)
            return Vector3.zero;

        MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
            return visual.TransformPoint(meshFilter.sharedMesh.bounds.center);

        return visual.position;
    }

    private Vector3 GetVisualMeshCenterOffset(Transform visual)
    {
        if (visual == null)
            return Vector3.zero;

        MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
            return visual.TransformVector(meshFilter.sharedMesh.bounds.center);

        return Vector3.zero;
    }


    private void CollectDirectBlocks(BoxCollider sensor, HashSet<MathBlockValue> destination)
    {
        if (sensor == null || !sensor.enabled)
            return;

        Vector3 halfExtents = Vector3.Scale(sensor.size * 0.5f, Abs(sensor.transform.lossyScale));
        int count = Physics.OverlapBoxNonAlloc(
            sensor.bounds.center,
            halfExtents,
            nearbyColliders,
            sensor.transform.rotation,
            blockLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            MathBlockValue block = GetBlockValue(nearbyColliders[i]);
            if (IsUsableBlock(block) && IsTouchingSensor(sensor, block))
                destination.Add(block);
        }
    }

    private bool IsTouchingSensor(BoxCollider sensor, MathBlockValue block)
    {
        Collider[] blockColliders = block.GetComponentsInChildren<Collider>(true);
        foreach (Collider blockCollider in blockColliders)
        {
            if (blockCollider == null || !blockCollider.enabled || blockCollider.isTrigger)
                continue;

            Vector3 direction;
            float distance;
            if (Physics.ComputePenetration(
                    sensor,
                    sensor.transform.position,
                    sensor.transform.rotation,
                    blockCollider,
                    blockCollider.transform.position,
                    blockCollider.transform.rotation,
                    out direction,
                    out distance))
                return true;
        }

        return false;
    }

    private void TraverseContactChains()
    {
        pending.Clear();
        foreach (MathBlockValue block in directLeftBlocks)
        {
            BlockContactInfo info = GetOrCreateContact(block);
            info.leftDistance = 0;
            pending.Enqueue(new QueueEntry(block, 0, true));
        }
        foreach (MathBlockValue block in directRightBlocks)
        {
            BlockContactInfo info = GetOrCreateContact(block);
            info.rightDistance = 0;
            pending.Enqueue(new QueueEntry(block, 0, false));
        }

        while (pending.Count > 0)
        {
            QueueEntry entry = pending.Dequeue();
            BlockContactInfo currentInfo = GetOrCreateContact(entry.value);
            int currentDistance = entry.left ? currentInfo.leftDistance : currentInfo.rightDistance;
            if (entry.distance != currentDistance)
                continue;

            foreach (MathBlockValue neighbour in FindContactNeighbours(entry.value))
            {
                if (!IsUsableBlock(neighbour))
                    continue;

                BlockContactInfo neighbourInfo = GetOrCreateContact(neighbour);
                int nextDistance = entry.distance + 1;
                if (entry.left)
                {
                    if (nextDistance >= neighbourInfo.leftDistance)
                        continue;
                    neighbourInfo.leftDistance = nextDistance;
                }
                else
                {
                    if (nextDistance >= neighbourInfo.rightDistance)
                        continue;
                    neighbourInfo.rightDistance = nextDistance;
                }

                pending.Enqueue(new QueueEntry(neighbour, nextDistance, entry.left));
            }
        }
    }

    private IEnumerable<MathBlockValue> FindContactNeighbours(MathBlockValue block)
    {
        Collider[] blockColliders = block.GetComponentsInChildren<Collider>(true);
        HashSet<MathBlockValue> neighbours = new HashSet<MathBlockValue>();

        foreach (Collider sourceCollider in blockColliders)
        {
            if (sourceCollider == null || !sourceCollider.enabled || sourceCollider.isTrigger)
                continue;

            Bounds expanded = sourceCollider.bounds;
            expanded.Expand(contactTolerance * 2f);
            int count = Physics.OverlapBoxNonAlloc(
                expanded.center,
                expanded.extents,
                nearbyColliders,
                Quaternion.identity,
                blockLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                MathBlockValue neighbour = GetBlockValue(nearbyColliders[i]);
                if (neighbour == null || neighbour == block || !IsUsableBlock(neighbour) || neighbours.Contains(neighbour))
                    continue;
                if (AreBlocksInContact(block, neighbour))
                    neighbours.Add(neighbour);
            }
        }

        return neighbours;
    }

    private bool AreBlocksInContact(MathBlockValue left, MathBlockValue right)
    {
        Collider[] leftColliders = left.GetComponentsInChildren<Collider>(true);
        Collider[] rightColliders = right.GetComponentsInChildren<Collider>(true);
        foreach (Collider leftCollider in leftColliders)
        {
            if (leftCollider == null || !leftCollider.enabled || leftCollider.isTrigger)
                continue;
            foreach (Collider rightCollider in rightColliders)
            {
                if (rightCollider == null || !rightCollider.enabled || rightCollider.isTrigger)
                    continue;
                if (GetBoundsGap(leftCollider.bounds, rightCollider.bounds) <= contactTolerance)
                    return true;
            }
        }

        return false;
    }

    private static float GetBoundsGap(Bounds left, Bounds right)
    {
        float x = Mathf.Max(0f, Mathf.Max(left.min.x - right.max.x, right.min.x - left.max.x));
        float y = Mathf.Max(0f, Mathf.Max(left.min.y - right.max.y, right.min.y - left.max.y));
        float z = Mathf.Max(0f, Mathf.Max(left.min.z - right.max.z, right.min.z - left.max.z));
        return new Vector3(x, y, z).magnitude;
    }

    private void AssignWeightsAndMarkers()
    {
        foreach (BlockContactInfo info in contacts.Values)
        {
            float leftCenterDistance = GetSensorCenterDistance(leftSensorCollider, info.value);
            float rightCenterDistance = GetSensorCenterDistance(rightSensorCollider, info.value);

            bool assignedLeft;
            if (info.leftDistance < info.rightDistance)
                assignedLeft = true;
            else if (info.rightDistance < info.leftDistance)
                assignedLeft = false;
            else
                assignedLeft = leftCenterDistance <= rightCenterDistance + ExactTieEpsilon;

            int value = Mathf.Max(0, info.value.CurrentValue);
            if (assignedLeft)
                LeftWeight += value;
            else
                RightWeight += value;

            BalanceLoadMarker marker = info.value.GetComponent<BalanceLoadMarker>();
            if (marker == null)
                marker = info.value.gameObject.AddComponent<BalanceLoadMarker>();
            marker.SetState(
                assignedLeft ? BalanceLoadMarker.LoadSide.Left : BalanceLoadMarker.LoadSide.Right,
                assignedLeft ? info.directLeft : info.directRight,
                Mathf.Min(info.leftDistance, info.rightDistance),
                value);
            activeMarkers.Add(marker);
        }
    }

    private float GetSensorCenterDistance(BoxCollider sensor, MathBlockValue block)
    {
        if (sensor == null || block == null)
            return float.PositiveInfinity;
        return (GetBlockBounds(block).center - sensor.bounds.center).sqrMagnitude;
    }

    private static Bounds GetBlockBounds(MathBlockValue block)
    {
        Collider[] colliders = block.GetComponentsInChildren<Collider>(true);
        Bounds result = new Bounds(block.transform.position, Vector3.zero);
        bool initialized = false;
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            if (!initialized)
            {
                result = collider.bounds;
                initialized = true;
            }
            else
                result.Encapsulate(collider.bounds);
        }
        return result;
    }

    private void UpdateBalanceState()
    {
        IsBalanced = Mathf.Abs(WeightDifference) <= balanceTolerance;
        if (hasBalanceState && !lastBalanced && IsBalanced)
            OnBalanced.Invoke();
        lastBalanced = IsBalanced;
        hasBalanceState = true;
    }

    private void ApplyVisualResponse(float deltaTime)
    {
        float total = Mathf.Max(1f, Mathf.Abs(LeftWeight) + Mathf.Abs(RightWeight));
        float intensity = Mathf.Clamp01(Mathf.Abs(WeightDifference) / total);
        float signedIntensity = Mathf.Sign(WeightDifference) * intensity;
        targetSupportAngle = signedIntensity * maximumTiltAngle;

        if (apoioBalancas != null)
        {
            currentSupportAngle = Mathf.SmoothDampAngle(
                currentSupportAngle,
                targetSupportAngle,
                ref supportAngleVelocity,
                smoothingTime,
                Mathf.Infinity,
                deltaTime);
            // The imported FBX rotates this child 90 degrees around X, so its
            // local Z points upward. The parent's local Z is the horizontal
            // tilt axis perpendicular to the two trays; pre-multiplying keeps
            // that parent-space axis and avoids the unwanted yaw on Y.
            apoioBalancas.localRotation = Quaternion.AngleAxis(currentSupportAngle, Vector3.forward) * originalSupportRotation;
        }

        ApplyTrayVisual(leftTrayVisual, originalLeftTraySupportPosition, originalLeftTrayRotation, -signedIntensity);
        ApplyTrayVisual(rightTrayVisual, originalRightTraySupportPosition, originalRightTrayRotation, signedIntensity);

        if (leftTrayVisual == null || rightTrayVisual == null)
        {
            if (!hasWarnedAboutCombinedMesh && combinedTrays != null)
            {
                Debug.LogWarning(
                    $"{name}: 'balancas' ainda e uma unica malha; configure LeftTrayVisual e RightTrayVisual para mover as bandejas separadamente.",
                    this);
                hasWarnedAboutCombinedMesh = true;
            }

            ApplyTrayVisual(combinedTrays, originalCombinedTraySupportPosition, originalCombinedTrayRotation, 0f);
        }
    }

private void ApplyTrayVisual(Transform visual, Vector3 originalSupportPosition, Quaternion originalRotation, float signedOffset)
    {
        if (visual == null)
            return;

        Transform reference = apoioBalancas != null ? apoioBalancas : transform;
        Vector3 localOffset = new Vector3(
            signedOffset * maximumLateralOffset,
            signedOffset * maximumVerticalOffset,
            0f);

        // Follow the actual mesh anchor, not the imported transform origin.
        // The two FBX tray objects share an origin but their mesh vertices are
        // on opposite sides, so each tray needs its own support trajectory.
        Vector3 targetAnchor = reference.TransformPoint(originalSupportPosition);
        Vector3 worldOffset = reference.TransformDirection(localOffset);
        visual.rotation = originalRotation;
        visual.position = targetAnchor + worldOffset - GetVisualMeshCenterOffset(visual);
    }

    private BlockContactInfo GetOrCreateContact(MathBlockValue block)
    {
        if (!contacts.TryGetValue(block, out BlockContactInfo info))
        {
            info = new BlockContactInfo { value = block };
            contacts.Add(block, info);
        }
        return info;
    }

    private MathBlockValue GetBlockValue(Collider collider)
    {
        return collider != null ? collider.GetComponentInParent<MathBlockValue>() : null;
    }

    private bool IsUsableBlock(MathBlockValue block)
    {
        if (block == null || !block.isActiveAndEnabled)
            return false;

        Transform heldBlock = gravityInteract != null ? gravityInteract.HeldBlock : null;
        return heldBlock == null || (block.transform != heldBlock && !block.transform.IsChildOf(heldBlock) && !heldBlock.IsChildOf(block.transform));
    }

    private void ClearMarkers()
    {
        foreach (BalanceLoadMarker marker in activeMarkers)
        {
            if (marker == null)
                continue;
            if (Application.isPlaying)
                Destroy(marker);
            else
                DestroyImmediate(marker);
        }
        activeMarkers.Clear();
        LeftWeight = 0;
        RightWeight = 0;
    }

    private static Vector3 ClampSensorSize(Vector3 size)
    {
        return new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), Mathf.Max(0.1f, size.z));
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSensorGizmos)
            return;

        DrawSensorGizmo(leftSensor, new Color(0.2f, 0.55f, 1f, 0.25f));
        DrawSensorGizmo(rightSensor, new Color(1f, 0.35f, 0.2f, 0.25f));
    }

    private void DrawSensorGizmo(SideSensorSettings settings, Color color)
    {
        Gizmos.color = color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(settings.localCenter, settings.size);
        Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
        Gizmos.DrawWireCube(settings.localCenter, settings.size);
        Gizmos.matrix = previousMatrix;
    }
}
