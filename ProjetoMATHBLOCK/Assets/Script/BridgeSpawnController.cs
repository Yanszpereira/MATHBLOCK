using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Builds a bridge from its individual rendered child pieces. The bridge is kept
/// isolated from gameplay triggers so another system can call BuildBridge later.
/// </summary>
[DisallowMultipleComponent]
public sealed class BridgeSpawnController : MonoBehaviour
{
    private enum ConstructionAxis
    {
        AutoLongestLocalSpan,
        X,
        Y,
        Z
    }

    private sealed class PieceState
    {
        public Transform transform;
        public Renderer renderer;
        public Collider[] colliders;
        public Vector3 targetLocalPosition;
        public Vector3 startLocalPosition;
        public Quaternion targetLocalRotation;
        public Vector3 targetLocalScale;
        public MaterialPropertyBlock propertyBlock;
    }

    [Header("Balances")]
    [SerializeField, Min(1)] private int balanceCount = 1;
    [SerializeField] private GameObject[] balances = new GameObject[1];

    [Header("Animation")]
    [SerializeField] private bool buildOnStart;
    [SerializeField, Min(0.01f)] private float pieceDuration = 0.45f;
    [SerializeField, Min(0f)] private float delayBetweenPieces = 0.08f;
    [SerializeField, Range(0.01f, 1f)] private float initialScale = 0.85f;
    [SerializeField] private AnimationCurve movementCurve = CreateEaseOutCubicCurve();
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Spawn Positions")]
    [Tooltip("Randomizes each hidden piece inside a local upper hemisphere around its final position.")]
    [SerializeField] private bool useRandomSpawnPositions = true;
    [Tooltip("Maximum local distance from the final position used for random spawn points.")]
    [SerializeField, Min(0f)] private float randomSpawnRadius = 3f;
    [Tooltip("Fallback local offset used only when random spawn positions are disabled.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -1.25f, 0f);

    [Header("Build Order")]
    [SerializeField] private ConstructionAxis constructionAxis = ConstructionAxis.AutoLongestLocalSpan;
    [SerializeField] private bool reverseOrder;

    private readonly List<PieceState> pieces = new List<PieceState>();
    private bool isInitialized;
    private bool hasBuilt;
    private bool isBuilding;
    private int activePieceAnimations;
    private int completedSections;
    private int configuredBalanceCount;
    private bool configurationValid;
    private readonly HashSet<BalanceScaleController> resolvedBalances = new HashSet<BalanceScaleController>();
    private readonly Dictionary<BalanceScaleController, UnityAction> subscriptions =
        new Dictionary<BalanceScaleController, UnityAction>();

    public int ResolvedCount => resolvedBalances.Count;
    public int CompletedSections => completedSections;
    public int PendingSections => Mathf.Max(0, ResolvedCount - completedSections - (isBuilding ? 1 : 0));
    public bool ConfigurationValid => configurationValid;

    public bool HasBuilt => hasBuilt;
    public bool IsBuilding => isBuilding;
    public int PieceCount => pieces.Count;

    private void Awake()
    {
        InitializeIfNeeded();
        ApplyInitialState();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            ConnectBalances();
    }

    private void OnDisable()
    {
        DisconnectBalances();
        if (Application.isPlaying && isInitialized)
        {
            StopAllCoroutines();
            ClearProgress();
            ApplyInitialState();
        }
    }

    private void Start()
    {
        if (buildOnStart)
            BuildNextSection();
    }

    private static AnimationCurve CreateEaseOutCubicCurve()
    {
        // f(t) = 1 - (1 - t)^3: fast departure and a zero-velocity landing.
        return new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    private void OnValidate()
    {
        balanceCount = Mathf.Max(1, balanceCount);
        if (balances == null || balances.Length != balanceCount)
            System.Array.Resize(ref balances, balanceCount);
        pieceDuration = Mathf.Max(0.01f, pieceDuration);
        delayBetweenPieces = Mathf.Max(0f, delayBetweenPieces);
        initialScale = Mathf.Clamp(initialScale, 0.01f, 1f);
        randomSpawnRadius = Mathf.Max(0f, randomSpawnRadius);

        if (movementCurve == null || movementCurve.length == 0)
            movementCurve = CreateEaseOutCubicCurve();
        if (fadeCurve == null || fadeCurve.length == 0)
            fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    /// <summary>Compatibility entry point: accepts only newly balanced configured sources.</summary>
    public void BuildBridge()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !configurationValid)
            return;
        foreach (BalanceScaleController balance in subscriptions.Keys)
            if (balance != null && balance.IsBalanced)
                RegisterResolvedBalance(balance);
    }

    [ContextMenu("Build Next Section")]
    public void BuildNextSection()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !configurationValid)
            return;
        foreach (BalanceScaleController balance in subscriptions.Keys)
        {
            if (!resolvedBalances.Contains(balance))
            {
                RegisterResolvedBalance(balance);
                break;
            }
        }
    }

    [ContextMenu("Reset Bridge")]
    public void ResetBridge()
    {
        if (!Application.isPlaying)
            return;
        StopAllCoroutines();
        DisconnectBalances();
        ClearProgress();
        InitializeIfNeeded();
        ApplyInitialState();
        if (isActiveAndEnabled)
            ConnectBalances();
    }

    private void ClearProgress()
    {
        activePieceAnimations = 0;
        completedSections = 0;
        resolvedBalances.Clear();
        isBuilding = false;
        hasBuilt = false;
    }

    public bool TryValidateBalances(out string error)
    {
        error = null;
        if (balanceCount < 1 || balances == null || balances.Length != balanceCount)
        {
            error = "Balance Count must be >= 1 and match the number of slots.";
            return false;
        }
        var unique = new HashSet<BalanceScaleController>();
        for (int i = 0; i < balances.Length; i++)
        {
            if (!TryResolveBalance(balances[i], out BalanceScaleController balance))
            {
                error = $"Balance slot {i}: assign one real BalanceScaleController, not a miniature or an ambiguous group.";
                return false;
            }
            if (!unique.Add(balance))
            {
                error = $"Balance slot {i}: '{balance.name}' is registered more than once.";
                return false;
            }
        }
        return true;
    }

    private static bool TryResolveBalance(GameObject reference, out BalanceScaleController balance)
    {
        balance = null;
        if (reference == null)
            return false;
        // A directly assigned miniature must never resolve through its original.
        BalanceScaleController direct = reference.GetComponent<BalanceScaleController>();
        if (direct != null)
        {
            balance = direct;
            return !direct.IsSmallBalance;
        }
        foreach (BalanceScaleController candidate in reference.GetComponentsInChildren<BalanceScaleController>(true))
        {
            if (candidate.IsSmallBalance)
                continue;
            if (balance != null)
                return false;
            balance = candidate;
        }
        return balance != null;
    }

    private void ConnectBalances()
    {
        DisconnectBalances();
        if (!TryValidateBalances(out string error))
        {
            Debug.LogError($"{name}: bridge configuration invalid. {error}", this);
            return;
        }
        configuredBalanceCount = balanceCount;
        configurationValid = true;
        foreach (GameObject reference in balances)
        {
            TryResolveBalance(reference, out BalanceScaleController balance);
            UnityAction callback = () => RegisterResolvedBalance(balance);
            subscriptions.Add(balance, callback);
            balance.OnBalanced.AddListener(callback);
        }
    }

    private void DisconnectBalances()
    {
        foreach (var pair in subscriptions)
            if (pair.Key != null)
                pair.Key.OnBalanced.RemoveListener(pair.Value);
        subscriptions.Clear();
        configurationValid = false;
    }

    private void RegisterResolvedBalance(BalanceScaleController balance)
    {
        if (!configurationValid || !isActiveAndEnabled || balance == null ||
            balance.IsSmallBalance || !subscriptions.ContainsKey(balance) ||
            !resolvedBalances.Add(balance))
            return;
        if (!isBuilding)
        {
            isBuilding = true;
            StartCoroutine(BuildRoutine());
        }
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized)
            return;

        pieces.Clear();
        foreach (Transform child in transform)
        {
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null)
                continue;

            Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    colliders = new Collider[] { child.gameObject.AddComponent<BoxCollider>() };
            }

            pieces.Add(new PieceState
            {
                transform = child,
                renderer = renderer,
                colliders = colliders,
                targetLocalPosition = child.localPosition,
                targetLocalRotation = child.localRotation,
                targetLocalScale = child.localScale,
                propertyBlock = new MaterialPropertyBlock()
            });
        }

        SortPieces();
        isInitialized = true;

        if (pieces.Count == 0)
            Debug.LogError($"{name}: no rendered direct child pieces were found.", this);
    }

    private void SortPieces()
    {
        ConstructionAxis axis = ResolveAxis();
        pieces.Sort((left, right) =>
        {
            float leftValue = GetAxisValue(left.targetLocalPosition, axis);
            float rightValue = GetAxisValue(right.targetLocalPosition, axis);
            int comparison = leftValue.CompareTo(rightValue);
            return reverseOrder ? -comparison : comparison;
        });
    }

    private ConstructionAxis ResolveAxis()
    {
        if (constructionAxis != ConstructionAxis.AutoLongestLocalSpan || pieces.Count < 2)
            return constructionAxis == ConstructionAxis.AutoLongestLocalSpan ? ConstructionAxis.Z : constructionAxis;

        Vector3 minimum = pieces[0].targetLocalPosition;
        Vector3 maximum = minimum;
        for (int i = 1; i < pieces.Count; i++)
        {
            Vector3 position = pieces[i].targetLocalPosition;
            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        Vector3 span = maximum - minimum;
        if (span.x >= span.y && span.x >= span.z)
            return ConstructionAxis.X;
        return span.y >= span.z ? ConstructionAxis.Y : ConstructionAxis.Z;
    }

    private static float GetAxisValue(Vector3 value, ConstructionAxis axis)
    {
        switch (axis)
        {
            case ConstructionAxis.X:
                return value.x;
            case ConstructionAxis.Y:
                return value.y;
            default:
                return value.z;
        }
    }

    private void ApplyInitialState()
    {
        foreach (PieceState piece in pieces)
        {
            if (piece == null || piece.transform == null)
                continue;

            piece.startLocalPosition = GetSpawnLocalPosition(piece);
            piece.transform.localPosition = piece.startLocalPosition;
            piece.transform.localRotation = piece.targetLocalRotation;
            piece.transform.localScale = piece.targetLocalScale * initialScale;
            SetPieceFade(piece, 0f);
            SetPieceCollidersEnabled(piece, false);
        }
    }

    private Vector3 GetSpawnLocalPosition(PieceState piece)
    {
        if (!useRandomSpawnPositions || randomSpawnRadius <= 0f)
            return piece.targetLocalPosition + spawnOffset;

        Vector3 randomOffset = Random.insideUnitSphere * randomSpawnRadius;
        randomOffset.y = Mathf.Abs(randomOffset.y);
        return piece.targetLocalPosition + randomOffset;
    }

    private IEnumerator BuildRoutine()
    {
        while (completedSections < resolvedBalances.Count)
        {
            int start = (int)((long)completedSections * pieces.Count / configuredBalanceCount);
            int end = (int)((long)(completedSections + 1) * pieces.Count / configuredBalanceCount);
            for (int i = start; i < end; i++)
            {
                activePieceAnimations++;
                StartCoroutine(AnimatePiece(pieces[i]));
                if (delayBetweenPieces > 0f)
                    yield return new WaitForSeconds(delayBetweenPieces);
                else
                    yield return null;
            }
            while (activePieceAnimations > 0)
                yield return null;
            completedSections++;
        }
        isBuilding = false;
        hasBuilt = completedSections == configuredBalanceCount;
    }

    private IEnumerator AnimatePiece(PieceState piece)
    {
        float elapsed = 0f;
        Vector3 startPosition = piece.startLocalPosition;
        Vector3 startScale = piece.targetLocalScale * initialScale;

        while (elapsed < pieceDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / pieceDuration);
            float movementT = movementCurve.Evaluate(normalizedTime);
            float fadeT = fadeCurve.Evaluate(normalizedTime);

            piece.transform.localPosition = Vector3.LerpUnclamped(startPosition, piece.targetLocalPosition, movementT);
            piece.transform.localScale = Vector3.LerpUnclamped(startScale, piece.targetLocalScale, movementT);
            SetPieceFade(piece, fadeT);
            yield return null;
        }

        piece.transform.localPosition = piece.targetLocalPosition;
        piece.transform.localRotation = piece.targetLocalRotation;
        piece.transform.localScale = piece.targetLocalScale;
        SetPieceFade(piece, 1f);
        SetPieceCollidersEnabled(piece, true);
        activePieceAnimations--;
    }

    private static void SetPieceCollidersEnabled(PieceState piece, bool enabled)
    {
        if (piece == null || piece.colliders == null)
            return;

        foreach (Collider collider in piece.colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private static void SetPieceFade(PieceState piece, float fade)
    {
        if (piece == null || piece.renderer == null)
            return;

        piece.renderer.GetPropertyBlock(piece.propertyBlock);
        piece.propertyBlock.SetFloat("_Fade", Mathf.Clamp01(fade));
        piece.renderer.SetPropertyBlock(piece.propertyBlock);
    }
}


