using UnityEngine;

/// <summary>
/// Minimal presentation-only elevator bridge. Put this on the elevator boarding
/// trigger and assign the independently animated platform and its top-position marker.
/// </summary>
[DisallowMultipleComponent]
public sealed class Fase4EmergencyElevator : MonoBehaviour
{
    [SerializeField] private Fase4ArenaRoundCoordinator coordinator;
    [SerializeField] private Transform movingPlatform;
    [SerializeField] private Transform topPosition;
    [SerializeField] private VoidRespawner voidRespawner;
    [SerializeField, Min(0.1f)] private float riseSpeed = 2f;

    private Collider boardingTrigger;
    private Vector3 startPosition;
    private Vector3 destination;
    private bool unlocked;
    private bool rising;
    private bool completedRide;
    private Transform carriedPlayer;
    private Transform originalPlayerParent;

    private void Awake()
    {
        boardingTrigger = GetComponent<Collider>();
        if (boardingTrigger == null || !boardingTrigger.isTrigger)
            Debug.LogError("Fase4EmergencyElevator precisa estar em um Collider marcado como Trigger.", this);
        if (coordinator == null)
            coordinator = FindFirstObjectByType<Fase4ArenaRoundCoordinator>();
        if (voidRespawner == null)
            voidRespawner = FindFirstObjectByType<VoidRespawner>();
        if (movingPlatform == null || topPosition == null || coordinator == null)
            Debug.LogError("Elevador emergencial: atribua plataforma móvel, posição superior e coordenador M5.", this);
    }

    private void OnEnable()
    {
        if (coordinator != null)
            coordinator.ElevatorUnlockRequested += Unlock;
        if (voidRespawner != null)
            voidRespawner.PlayerRespawnStarted += OnPlayerRespawnStarted;
    }

    private void Start()
    {
        if (movingPlatform != null)
            startPosition = movingPlatform.position;
        if (topPosition != null)
            destination = topPosition.position;
    }

    private void OnDisable()
    {
        if (coordinator != null)
            coordinator.ElevatorUnlockRequested -= Unlock;
        if (voidRespawner != null)
            voidRespawner.PlayerRespawnStarted -= OnPlayerRespawnStarted;
        DetachPlayer();
    }

    private void OnTriggerEnter(Collider other) => TryBoard(other);
    private void OnTriggerStay(Collider other) => TryBoard(other);

    private void Update()
    {
        if (!rising || movingPlatform == null)
            return;

        Vector3 previousPosition = movingPlatform.position;
        movingPlatform.position = Vector3.MoveTowards(previousPosition, destination, riseSpeed * Time.deltaTime);
        if ((movingPlatform.position - destination).sqrMagnitude <= 0.0001f)
        {
            movingPlatform.position = destination;
            rising = false;
            completedRide = true;
            DetachPlayer();
            Debug.Log("Fase 4: elevador chegou à plataforma superior.", this);
        }
    }

    private void Unlock()
    {
        unlocked = true;
        Debug.Log("Fase 4: elevador liberado após as três rodadas.", this);
    }

    private void TryBoard(Collider other)
    {
        if (!unlocked || rising || completedRide || movingPlatform == null || topPosition == null)
            return;

        PlayerMovement player = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
        if (player == null)
            return;

        carriedPlayer = player.transform;
        originalPlayerParent = carriedPlayer.parent;
        carriedPlayer.SetParent(movingPlatform, true);
        rising = true;
        Debug.Log("Fase 4: elevador iniciou a subida.", this);
    }

    private void OnPlayerRespawnStarted(PlayerMovement player)
    {
        if (player != null && carriedPlayer == player.transform)
        {
            rising = false;
            if (movingPlatform != null)
                movingPlatform.position = startPosition;
            DetachPlayer();
        }
    }

    private void DetachPlayer()
    {
        if (carriedPlayer != null)
            carriedPlayer.SetParent(originalPlayerParent, true);
        carriedPlayer = null;
        originalPlayerParent = null;
    }
}
