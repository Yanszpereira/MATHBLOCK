using UnityEngine;

/// <summary>Runtime ownership and origin metadata for a block spawned by M4.</summary>
[DisallowMultipleComponent]
public sealed class Fase4SpawnedMathBlock : MonoBehaviour
{
    [SerializeField] private Fase4ArenaSpawnerController owner;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int attemptId;
    [SerializeField] private Quaternion originRotation = Quaternion.identity;
    [SerializeField] private int sourceBlockId = -1;
    [SerializeField] private bool interactionEnabled;

    private Vector3 originPosition;
    private Vector3 duplicateCreationPosition;
    private Quaternion duplicateCreationRotation;
    private bool useStoredOrigin;
    private bool hasDuplicateCandidate;

    public Fase4ArenaSpawnerController Owner => owner;
    public Transform SpawnPoint => spawnPoint;
    public int AttemptId => attemptId;
    public bool InteractionEnabled => interactionEnabled;

    private void OnEnable()
    {
        // GravityInteract clones the whole MathBlock prefab. Capture that
        // clone's actual creation pose before the global duplicator lets it
        // fall; LateUpdate detects its new identity without changing that API.
        if (owner != null)
        {
            duplicateCreationPosition = transform.position;
            duplicateCreationRotation = transform.rotation;
            hasDuplicateCandidate = true;
        }
    }

    private void LateUpdate()
    {
        if (!hasDuplicateCandidate || owner == null)
            return;

        MathBlockValue value = GetComponent<MathBlockValue>();
        if (value == null || value.BlockId < 0 || value.BlockId == sourceBlockId)
            return;

        originPosition = duplicateCreationPosition;
        originRotation = duplicateCreationRotation;
        useStoredOrigin = true;
        hasDuplicateCandidate = false;
    }

    public void Initialize(Fase4ArenaSpawnerController source, Transform origin, int sourceAttemptId)
    {
        owner = source;
        spawnPoint = origin;
        attemptId = sourceAttemptId;
        originRotation = transform.rotation;
        originPosition = origin != null ? origin.position : transform.position;
        sourceBlockId = GetComponent<MathBlockValue>() != null
            ? GetComponent<MathBlockValue>().BlockId
            : -1;
        useStoredOrigin = false;
        interactionEnabled = false;
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
    }

    public bool TryGetReturnPose(out Vector3 position, out Quaternion rotation)
    {
        if (owner == null || (!useStoredOrigin && spawnPoint == null) || !owner.IsCurrentAttempt(this))
        {
            position = default;
            rotation = default;
            return false;
        }

        position = useStoredOrigin ? originPosition : spawnPoint.position;
        rotation = originRotation;
        return true;
    }
}
