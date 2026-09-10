using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

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
    private const string SmallBalanceTextRootName = "SmallBalanceValueTexts";
    private const string LeftValueTextName = "LeftBalanceValueText";
    private const string RightValueTextName = "RightBalanceValueText";
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
    [SerializeField, Min(0.1f)] private float traySensorHeight = 8f;
    [SerializeField] private bool drawSensorGizmos = true;

    [Header("Visual References")]
    [SerializeField] private Transform apoioBalancas;
    [SerializeField] private Transform corpo;
    [SerializeField] private Transform combinedTrays;
    [SerializeField] private Transform leftTrayVisual;
    [SerializeField] private Transform rightTrayVisual;
    [SerializeField] private Transform leftLateralPoint;
    [SerializeField] private Transform rightLateralPoint;

    [Header("Balança Menor")]
    [SerializeField, InspectorName("É Balança Menor")] private bool isSmallBalance;
    [SerializeField, InspectorName("Balança Original")] private BalanceScaleController originalBalance;
    [SerializeField, Min(0f), InspectorName("Altura dos Textos")] private float smallBalanceTextHeight = 0.75f;
    [SerializeField, Min(1), InspectorName("Tamanho da Fonte")] private int smallBalanceTextFontSize = 64;
    [SerializeField, Min(0.001f), InspectorName("Escala dos Textos")] private float smallBalanceTextCharacterSize = 0.1f;
    [SerializeField, InspectorName("Cor quando Igual")] private Color smallBalanceEqualColor = Color.green;
    [SerializeField, InspectorName("Cor quando Diferente")] private Color smallBalanceDifferentColor = Color.red;

    [Header("Visual Response")]
    [SerializeField, Range(0f, 45f)] private float maximumTiltAngle = 20f;
    [SerializeField, Min(0f)] private float maximumVerticalOffset = 0.25f;
    [SerializeField, Min(0f)] private float maximumLateralOffset = 0.15f;
    [SerializeField, Min(0.01f)] private float smoothingTime = 1.5f;
    [SerializeField, Min(0f)] private float balanceTolerance = 0f;
    [SerializeField, Min(0f)] private float visualStateStabilityTime = 0.3f;

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
    private BoxCollider leftTrayCollider;
    private BoxCollider rightTrayCollider;
    private GravityInteract gravityInteract;
    private Quaternion originalSupportRotation;
    private Quaternion originalLeftTrayRotation;
    private Quaternion originalRightTrayRotation;
    private Quaternion originalCombinedTrayRotation;
    private Vector3 originalLeftTraySupportPosition;
    private Vector3 originalRightTraySupportPosition;
    private Vector3 originalCombinedTraySupportPosition;
    private Vector3 originalLeftTrayWorldPosition;
    private Vector3 originalRightTrayWorldPosition;
    private Vector3 originalLeftLateralPointPosition;
    private Vector3 originalRightLateralPointPosition;
    private float targetSupportAngle;
    private float currentSupportAngle;
    private float supportAngleVelocity;
    private float scanTimer;
    private int visualLeftWeight;
    private int visualRightWeight;
    private int pendingVisualLeftWeight;
    private int pendingVisualRightWeight;
    private float pendingVisualSince;
    private bool hasPendingVisualState;
    private TextMeshPro leftSmallBalanceText;
    private TextMeshPro rightSmallBalanceText;
    private bool hasBalanceState;
    private bool lastBalanced;
    private bool hasWarnedAboutCombinedMesh;

    public bool IsSmallBalance => isSmallBalance;

    public int LeftWeight { get; private set; }
    public int RightWeight { get; private set; }
    public int WeightDifference => LeftWeight - RightWeight;
    public bool IsBalanced { get; private set; }
    public Transform ApoioBalancas => apoioBalancas;
    public Transform Corpo => corpo;

    private void Awake()
    {
        ResolveReferences();
        CacheOriginalVisualState();

        if (isSmallBalance)
        {
            DisableSmallBalanceColliders();
            CreateSmallBalanceValueTexts();
            UpdateSmallBalanceTexts();
            return;
        }

        CreateSensors();
        gravityInteract = FindFirstObjectByType<GravityInteract>();
        RecalculateNow();
    }

    private void OnValidate()
    {
        leftSensor.size = ClampSensorSize(leftSensor.size);
        rightSensor.size = ClampSensorSize(rightSensor.size);
        contactTolerance = Mathf.Max(0f, contactTolerance);
        scanInterval = Mathf.Max(0.02f, scanInterval);
        traySensorHeight = Mathf.Max(0.1f, traySensorHeight);
        smoothingTime = Mathf.Max(0.01f, smoothingTime);
        balanceTolerance = Mathf.Max(0f, balanceTolerance);
        visualStateStabilityTime = Mathf.Max(0f, visualStateStabilityTime);

        if (isSmallBalance)
        {
            ResolveReferences();
            DisableSmallBalanceColliders();
        }
    }

    private void FixedUpdate()
    {
        if (isSmallBalance)
        {
            ApplySmallBalanceMirror();
            return;
        }

        scanTimer -= Time.fixedDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            RecalculateNow();
        }

        ApplyVisualResponse(Time.fixedDeltaTime);
    }

    private void CreateSmallBalanceValueTexts()
    {
        Transform textRoot = transform.Find(SmallBalanceTextRootName);
        if (textRoot == null)
        {
            GameObject rootObject = new GameObject(SmallBalanceTextRootName);
            textRoot = rootObject.transform;
            textRoot.SetParent(transform, false);
        }

        leftSmallBalanceText = GetOrCreateSmallBalanceText(textRoot, LeftValueTextName);
        rightSmallBalanceText = GetOrCreateSmallBalanceText(textRoot, RightValueTextName);
    }

private TextMeshPro GetOrCreateSmallBalanceText(Transform parent, string textName)
    {
        Transform textTransform = parent.Find(textName);
        if (textTransform == null)
        {
            GameObject textObject = new GameObject(textName);
            textTransform = textObject.transform;
            textTransform.SetParent(parent, false);
        }

        TextMesh legacyText = textTransform.GetComponent<TextMesh>();
        if (legacyText != null)
        {
            if (Application.isPlaying)
                Destroy(legacyText);
            else
                DestroyImmediate(legacyText);
        }

        TextMeshPro textMesh = textTransform.GetComponent<TextMeshPro>();
        if (textMesh == null)
            textMesh = textTransform.gameObject.AddComponent<TextMeshPro>();

        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.enableWordWrapping = false;
        textMesh.fontSize = Mathf.Max(1, smallBalanceTextFontSize);
        textMesh.color = smallBalanceEqualColor;
        ApplySmallBalanceTextOutline(textMesh);
        CompensateSmallBalanceTextScale(textMesh);
        return textMesh;
    }

private static void ApplySmallBalanceTextOutline(TextMeshPro textMesh)
    {
        if (textMesh == null)
            return;

        const float outlineWidth = 0.5f;
        textMesh.outlineColor = Color.white;
        textMesh.outlineWidth = outlineWidth;

        Material material = textMesh.fontMaterial;
        if (material != null)
        {
            if (material.HasProperty("_OutlineColor"))
                material.SetColor("_OutlineColor", Color.white);
            if (material.HasProperty("_OutlineWidth"))
                material.SetFloat("_OutlineWidth", outlineWidth);
        }

        textMesh.UpdateMeshPadding();
    }




private void UpdateSmallBalanceTexts()
    {
        if (leftSmallBalanceText == null || rightSmallBalanceText == null)
            CreateSmallBalanceValueTexts();

        int leftValue = originalBalance != null ? originalBalance.LeftWeight : 0;
        int rightValue = originalBalance != null ? originalBalance.RightWeight : 0;
        Color stateColor = leftValue == rightValue
            ? smallBalanceEqualColor
            : smallBalanceDifferentColor;

        UpdateSmallBalanceText(leftSmallBalanceText, leftLateralPoint, leftValue, stateColor);
        UpdateSmallBalanceText(rightSmallBalanceText, rightLateralPoint, rightValue, stateColor);
    }

private void UpdateSmallBalanceText(TextMeshPro textMesh, Transform point, int value, Color color)
    {
        if (textMesh == null || point == null)
            return;

        textMesh.transform.position = point.position + Vector3.up * smallBalanceTextHeight;
        FaceSmallBalanceTextTowardPlayer(textMesh);
        CompensateSmallBalanceTextScale(textMesh);
        textMesh.color = color;
        ApplySmallBalanceTextOutline(textMesh);
        textMesh.text = value.ToString();
    }

private static void FaceSmallBalanceTextTowardPlayer(TextMeshPro textMesh)
    {
        Camera playerCamera = Camera.main;
        if (textMesh == null || playerCamera == null)
            return;

        Vector3 toPlayer = playerCamera.transform.position - textMesh.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return;

        textMesh.transform.rotation =
            Quaternion.LookRotation(toPlayer.normalized, Vector3.up) *
            Quaternion.Euler(0f, 180f, 0f);
    }

private void CompensateSmallBalanceTextScale(TextMeshPro textMesh)
    {
        if (textMesh == null || textMesh.transform.parent == null)
            return;

        Vector3 parentScale = textMesh.transform.parent.lossyScale;
        float visualScale = Mathf.Max(0.001f, smallBalanceTextCharacterSize);
        textMesh.transform.localScale = new Vector3(
            SafeInverseScale(parentScale.x) * visualScale,
            SafeInverseScale(parentScale.y) * visualScale,
            SafeInverseScale(parentScale.z) * visualScale);
    }

    private static float SafeInverseScale(float value)
    {
        return 1f / Mathf.Max(0.0001f, Mathf.Abs(value));
    }

    private void DisableSmallBalanceColliders()
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
                collider.enabled = false;
        }
    }

    private void ApplySmallBalanceMirror()
    {
        if (originalBalance == null || originalBalance == this)
            return;

        CopySupportMotion(
            originalBalance.apoioBalancas,
            originalBalance.originalSupportRotation,
            apoioBalancas,
            originalSupportRotation);
        CopyTrayMotion(
            originalBalance.leftTrayVisual,
            originalBalance.transform,
            originalBalance.originalLeftTrayWorldPosition,
            originalBalance.originalLeftTrayRotation,
            leftTrayVisual,
            transform,
            originalLeftTrayWorldPosition,
            originalLeftTrayRotation);
        CopyTrayMotion(
            originalBalance.rightTrayVisual,
            originalBalance.transform,
            originalBalance.originalRightTrayWorldPosition,
            originalBalance.originalRightTrayRotation,
            rightTrayVisual,
            transform,
            originalRightTrayWorldPosition,
            originalRightTrayRotation);

        UpdateSmallBalanceTexts();
    }

    private static void CopySupportMotion(
        Transform source,
        Quaternion sourceOriginalRotation,
        Transform destination,
        Quaternion destinationOriginalRotation)
    {
        if (source == null || destination == null)
            return;

        Quaternion delta = source.localRotation * Quaternion.Inverse(sourceOriginalRotation);
        destination.localRotation = delta * destinationOriginalRotation;
    }

    private static void CopyTrayMotion(
        Transform source,
        Transform sourceBalance,
        Vector3 sourceOriginalPosition,
        Quaternion sourceOriginalRotation,
        Transform destination,
        Transform destinationBalance,
        Vector3 destinationOriginalPosition,
        Quaternion destinationOriginalRotation)
    {
        if (source == null || sourceBalance == null ||
            destination == null || destinationBalance == null)
            return;

        // Convert the original tray displacement through each balance root.
        // TransformVector applies the destination scale, making the miniature
        // reproduce the same relative trajectory at its own size.
        Vector3 sourceWorldMotion = source.position - sourceOriginalPosition;
        Vector3 sourceLocalMotion = sourceBalance.InverseTransformVector(sourceWorldMotion);
        Vector3 destinationWorldMotion = destinationBalance.TransformVector(sourceLocalMotion);
        destination.position = destinationOriginalPosition + destinationWorldMotion;

        Quaternion rotationDelta = source.rotation * Quaternion.Inverse(sourceOriginalRotation);
        destination.rotation = rotationDelta * destinationOriginalRotation;
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
        UpdateVisualWeights();
    }

private void ResolveReferences()
    {
        if (apoioBalancas == null)
            apoioBalancas = FindChildByName("ApoioBalancas");
        if (leftLateralPoint == null && apoioBalancas != null)
            leftLateralPoint = apoioBalancas.Find("PontoBE");
        if (rightLateralPoint == null && apoioBalancas != null)
            rightLateralPoint = apoioBalancas.Find("PontoBD");
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
        leftTrayCollider = FindTrayCollider(leftTrayVisual, leftTrayCollider);
        rightTrayCollider = FindTrayCollider(rightTrayVisual, rightTrayCollider);
        leftSensorCollider = CreateTraySensor(leftTrayCollider, LeftSensorName, leftSensor, leftSensorCollider);
        rightSensorCollider = CreateTraySensor(rightTrayCollider, RightSensorName, rightSensor, rightSensorCollider);
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

    private BoxCollider FindTrayCollider(Transform trayVisual, BoxCollider cachedCollider)
    {
        if (cachedCollider != null)
            return cachedCollider;
        if (trayVisual == null)
            return null;
        foreach (BoxCollider candidate in trayVisual.GetComponentsInChildren<BoxCollider>(true))
        {
            if (candidate != null && candidate.enabled && !candidate.isTrigger)
                return candidate;
        }
        return null;
    }

    private BoxCollider CreateTraySensor(BoxCollider trayCollider, string sensorName, SideSensorSettings fallbackSettings, BoxCollider existingSensor)
    {
        if (trayCollider == null)
        {
            Transform sensorRoot = transform.Find(SensorRootName);
            if (sensorRoot == null)
            {
                GameObject rootObject = new GameObject(SensorRootName);
                sensorRoot = rootObject.transform;
                sensorRoot.SetParent(transform, false);
            }
            return GetOrCreateSensor(sensorRoot, sensorName, fallbackSettings);
        }

        Rigidbody trayBody = trayCollider.attachedRigidbody;
        if (trayBody == null)
            trayBody = trayCollider.gameObject.AddComponent<Rigidbody>();
        trayBody.isKinematic = true;
        trayBody.useGravity = false;
        trayBody.interpolation = RigidbodyInterpolation.Interpolate;
        trayBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Transform sensorTransform = trayCollider.transform.Find(sensorName);
        if (sensorTransform == null && existingSensor != null)
            sensorTransform = existingSensor.transform;
        if (sensorTransform == null)
            sensorTransform = new GameObject(sensorName).transform;

        sensorTransform.SetParent(trayCollider.transform, false);
        sensorTransform.localRotation = Quaternion.identity;
        sensorTransform.localScale = Vector3.one;
        BoxCollider sensor = sensorTransform.GetComponent<BoxCollider>();
        if (sensor == null)
            sensor = sensorTransform.gameObject.AddComponent<BoxCollider>();

        float height = Mathf.Max(0.1f, traySensorHeight);
        Vector3 traySize = ClampSensorSize(trayCollider.size);
        sensorTransform.localPosition = trayCollider.center + new Vector3(0f, traySize.y * 0.5f + height * 0.5f, 0f);
        sensor.isTrigger = true;
        sensor.center = Vector3.zero;
        sensor.size = new Vector3(Mathf.Max(0.1f, traySize.x * 0.92f), height, Mathf.Max(0.1f, traySize.z * 0.92f));
        return sensor;
    }

    private void CacheOriginalVisualState()
    {
        if (apoioBalancas != null)
            originalSupportRotation = apoioBalancas.localRotation;

        if (leftTrayVisual != null)
        {
            originalLeftTrayRotation = leftTrayVisual.rotation;
            originalLeftTraySupportPosition = GetSupportLocalPosition(leftTrayVisual);
            originalLeftTrayWorldPosition = leftTrayVisual.position;
        }

        if (rightTrayVisual != null)
        {
            originalRightTrayRotation = rightTrayVisual.rotation;
            originalRightTraySupportPosition = GetSupportLocalPosition(rightTrayVisual);
            originalRightTrayWorldPosition = rightTrayVisual.position;
        }

        if (combinedTrays != null)
        {
            originalCombinedTrayRotation = combinedTrays.rotation;
            originalCombinedTraySupportPosition = GetSupportLocalPosition(combinedTrays);
        }

        if (leftLateralPoint != null)
            originalLeftLateralPointPosition = leftLateralPoint.position;
        if (rightLateralPoint != null)
            originalRightLateralPointPosition = rightLateralPoint.position;
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

    private void UpdateVisualWeights()
    {
        if (!hasPendingVisualState || pendingVisualLeftWeight != LeftWeight || pendingVisualRightWeight != RightWeight)
        {
            pendingVisualLeftWeight = LeftWeight;
            pendingVisualRightWeight = RightWeight;
            pendingVisualSince = Time.fixedTime;
            hasPendingVisualState = true;
        }
        if (visualLeftWeight == pendingVisualLeftWeight && visualRightWeight == pendingVisualRightWeight)
            return;
        if (Time.fixedTime - pendingVisualSince < visualStateStabilityTime)
            return;
        visualLeftWeight = pendingVisualLeftWeight;
        visualRightWeight = pendingVisualRightWeight;
    }

    private void ApplyVisualResponse(float deltaTime)
    {
        int visualDifference = visualLeftWeight - visualRightWeight;
        float total = Mathf.Max(1f, Mathf.Abs(visualLeftWeight) + Mathf.Abs(visualRightWeight));
        float intensity = Mathf.Clamp01(Mathf.Abs(visualDifference) / total);
        // Imported support axes are mirrored relative to the logical tray sides.
        // Invert the visual sign so the loaded physical tray moves downward.
        float signedIntensity = -Mathf.Sign(visualDifference) * intensity;
        targetSupportAngle = signedIntensity * maximumTiltAngle;
        bool moved = false;

        if (apoioBalancas != null)
        {
            currentSupportAngle = Mathf.SmoothDampAngle(currentSupportAngle, targetSupportAngle, ref supportAngleVelocity, smoothingTime, Mathf.Infinity, deltaTime);
            Quaternion targetRotation = Quaternion.AngleAxis(currentSupportAngle, Vector3.forward) * originalSupportRotation;
            if (Quaternion.Angle(apoioBalancas.localRotation, targetRotation) > 0.0001f)
            {
                apoioBalancas.localRotation = targetRotation;
                moved = true;
            }
        }

        moved |= ApplyTrayVisual(
            leftTrayVisual,
            originalLeftTraySupportPosition,
            originalLeftTrayRotation,
            -signedIntensity,
            leftLateralPoint,
            originalLeftTrayWorldPosition,
            originalLeftLateralPointPosition);
        moved |= ApplyTrayVisual(
            rightTrayVisual,
            originalRightTraySupportPosition,
            originalRightTrayRotation,
            signedIntensity,
            rightLateralPoint,
            originalRightTrayWorldPosition,
            originalRightLateralPointPosition);
        if (leftTrayVisual == null || rightTrayVisual == null)
        {
            if (!hasWarnedAboutCombinedMesh && combinedTrays != null)
            {
                Debug.LogWarning($"{name}: 'balancas' ainda e uma unica malha; configure LeftTrayVisual e RightTrayVisual para mover as bandejas separadamente.", this);
                hasWarnedAboutCombinedMesh = true;
            }
            moved |= ApplyTrayVisual(combinedTrays, originalCombinedTraySupportPosition, originalCombinedTrayRotation, 0f);
        }
        if (moved)
            Physics.SyncTransforms();
    }

private bool ApplyTrayVisual(
        Transform visual,
        Vector3 originalSupportPosition,
        Quaternion originalRotation,
        float signedOffset,
        Transform lateralPoint = null,
        Vector3 originalVisualPosition = default,
        Vector3 originalLateralPointPosition = default)
    {
        if (visual == null)
            return false;
        Transform reference = apoioBalancas != null ? apoioBalancas : transform;
        Vector3 localOffset = new Vector3(signedOffset * maximumLateralOffset, signedOffset * maximumVerticalOffset, 0f);
        Vector3 worldOffset = reference.TransformDirection(localOffset);
        Vector3 targetPosition = reference.TransformPoint(originalSupportPosition) +
            worldOffset - GetVisualMeshCenterOffset(visual);

        if (lateralPoint != null)
        {
            // The point controls only the horizontal trajectory. Vertical motion
            // remains governed by the scale's tilt and the visual load offset.
            Vector3 lateralDelta = lateralPoint.position - originalLateralPointPosition;
            targetPosition.x = originalVisualPosition.x + lateralDelta.x + worldOffset.x;
            targetPosition.z = originalVisualPosition.z + lateralDelta.z + worldOffset.z;
        }

        bool moved = false;
        if (Quaternion.Angle(visual.rotation, originalRotation) > 0.0001f)
        {
            visual.rotation = originalRotation;
            moved = true;
        }
        if ((visual.position - targetPosition).sqrMagnitude > 0.00000001f)
        {
            visual.position = targetPosition;
            moved = true;
        }
        return moved;
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
