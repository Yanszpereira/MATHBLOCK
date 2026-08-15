using UnityEngine;

/// <summary>
/// Controls the locked/unlocked state, endpoint calls and movement of the
/// platform associated with a Fase 2 elevator totem.
/// </summary>
public sealed class ElevatorTotemController : MonoBehaviour
{
    private enum ElevatorState
    {
        Locked,
        IdleAtInitial,
        IdleAtFinal,
        MovingToInitial,
        MovingToFinal
    }

    [Header("Totem")]
    [SerializeField] private int requiredValue;
    [SerializeField] private PadMathBlockDetector verificationPad;

    [Header("Plataforma")]
    [SerializeField] private Transform platform;
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Destino mundial editavel pelo gizmo PFinal.")]
    [SerializeField] private Vector3 finalDestination;

    [Header("Botoes")]
    [SerializeField] private GameObject platformButtonPrefab;
    [SerializeField] private GameObject initialCallButtonPrefab;
    [SerializeField] private GameObject finalCallButtonPrefab;
    [SerializeField] private Transform platformButtonAnchor;
    [SerializeField] private Vector3 platformButtonLocalPosition = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector3 endpointButtonOffset = new Vector3(0f, 1f, 0f);

    private ElevatorState state = ElevatorState.Locked;
    private Vector3 initialPosition;
    private Vector3 targetPosition;
    private Vector3 pendingPlatformDelta;
    private GameObject platformButtonInstance;
    private GameObject initialCallButtonInstance;
    private GameObject finalCallButtonInstance;
    private bool hasCapturedInitialPosition;
    private bool buttonsUnlocked;

    public bool IsUnlocked => buttonsUnlocked;
    public bool IsMoving => state == ElevatorState.MovingToInitial || state == ElevatorState.MovingToFinal;
    public Vector3 InitialPosition => initialPosition;
    public Vector3 FinalPosition => finalDestination;

    private void Awake()
    {
        if (platform == null)
        {
            Debug.LogError($"Totem {name} nao possui uma plataforma atribuida.");
            return;
        }

        // Capture the exact platform position at the beginning of every play session.
        initialPosition = platform.position;
        targetPosition = initialPosition;
        hasCapturedInitialPosition = true;
        state = ElevatorState.Locked;

        CreateButtonInstances();
    }

    private void OnEnable()
    {
        if (verificationPad != null)
            verificationPad.ValueDetected += OnVerificationValueDetected;
    }

    private void OnDisable()
    {
        if (verificationPad != null)
            verificationPad.ValueDetected -= OnVerificationValueDetected;
    }

    private void Update()
    {
        if (!IsMoving || platform == null)
            return;

        Vector3 oldPosition = platform.position;
        platform.position = Vector3.MoveTowards(oldPosition, targetPosition, moveSpeed * Time.deltaTime);
        pendingPlatformDelta += platform.position - oldPosition;

        if (Vector3.SqrMagnitude(platform.position - targetPosition) < 0.000001f)
        {
            platform.position = targetPosition;
            state = IsAtInitial(targetPosition) ? ElevatorState.IdleAtInitial : ElevatorState.IdleAtFinal;
        }
    }

    private void LateUpdate()
    {
        if (pendingPlatformDelta.sqrMagnitude < 0.0000001f)
            return;

        CarryPlayer(pendingPlatformDelta);
        pendingPlatformDelta = Vector3.zero;
    }

    public void HandleButtonPress(ElevatorButtonRole role)
    {
        if (!buttonsUnlocked || platform == null)
            return;

        Debug.Log($"Totem {name} recebeu o botao {role} em {platform.position}.");

        switch (role)
        {
            case ElevatorButtonRole.Platform:
                TogglePlatformDirection();
                break;
            case ElevatorButtonRole.CallInitial:
                MoveTo(initialPosition);
                break;
            case ElevatorButtonRole.CallFinal:
                MoveTo(finalDestination);
                break;
        }
    }

    private void OnVerificationValueDetected(GameObject padObject, int value, GameObject blockObject)
    {
        if (verificationPad == null || padObject != verificationPad.gameObject || buttonsUnlocked)
            return;

        if (value != requiredValue)
            return;

        buttonsUnlocked = true;
        state = IsAtInitial(platform != null ? platform.position : initialPosition)
            ? ElevatorState.IdleAtInitial
            : ElevatorState.IdleAtFinal;

        SetButtonActive(platformButtonInstance, true);
        SetButtonActive(initialCallButtonInstance, true);
        SetButtonActive(finalCallButtonInstance, true);

        Debug.Log($"Totem {name} liberou os botoes com o valor {value}.");
    }

    private void TogglePlatformDirection()
    {
        if (IsAtInitial(platform.position))
        {
            MoveTo(finalDestination);
            return;
        }

        if (IsAtFinal(platform.position))
        {
            MoveTo(initialPosition);
            return;
        }

        MoveTo(state == ElevatorState.MovingToFinal ? initialPosition : finalDestination);
    }

    private void MoveTo(Vector3 destination)
    {
        if (platform == null)
            return;

        targetPosition = destination;

        if (Vector3.SqrMagnitude(platform.position - destination) < 0.000001f)
        {
            platform.position = destination;
            state = IsAtInitial(destination) ? ElevatorState.IdleAtInitial : ElevatorState.IdleAtFinal;
            return;
        }

        state = IsAtInitial(destination) ? ElevatorState.MovingToInitial : ElevatorState.MovingToFinal;
    }

    private void CreateButtonInstances()
    {
        platformButtonInstance = CreateButton(platformButtonPrefab, ElevatorButtonRole.Platform, GetPlatformButtonPosition());
        if (platformButtonInstance != null)
            platformButtonInstance.transform.SetParent(platform, true);

        initialCallButtonInstance = CreateButton(initialCallButtonPrefab, ElevatorButtonRole.CallInitial, initialPosition + endpointButtonOffset);
        finalCallButtonInstance = CreateButton(finalCallButtonPrefab, ElevatorButtonRole.CallFinal, finalDestination + endpointButtonOffset);

        SetButtonActive(platformButtonInstance, false);
        SetButtonActive(initialCallButtonInstance, false);
        SetButtonActive(finalCallButtonInstance, false);
    }

    private GameObject CreateButton(GameObject prefab, ElevatorButtonRole role, Vector3 position)
    {
        if (prefab == null)
            return null;

        // Scene objects are the three buttons manually positioned by the level
        // designer. Preserve those exact positions. Project prefabs are cloned
        // at the calculated fallback positions instead.
        bool isSceneObject = prefab.scene.IsValid();
        GameObject instance = isSceneObject
            ? prefab
            : Instantiate(prefab, position, prefab.transform.rotation);

        if (!isSceneObject)
            instance.name = $"{name}_{role}Button";

        ElevatorButton button = instance.GetComponent<ElevatorButton>();
        if (button == null)
            button = instance.AddComponent<ElevatorButton>();

        button.Configure(this, role);

        EnsureInteractionCollider(instance);

        Debug.Log($"Botao {role} preparado em {instance.transform.position} (objetoDeCena={isSceneObject}).");

        return instance;
    }

    private void EnsureInteractionCollider(GameObject instance)
    {
        if (instance.GetComponentInChildren<Collider>(true) != null)
            return;

        BoxCollider collider = instance.AddComponent<BoxCollider>();
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            collider.size = Vector3.one;
            Debug.LogWarning($"Botao {instance.name} nao possui Renderer; foi criado um BoxCollider padrao.");
            return;
        }

        Bounds localBounds = new Bounds(
            instance.transform.InverseTransformPoint(renderers[0].bounds.center),
            Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds rendererBounds = renderers[i].bounds;
            localBounds.Encapsulate(instance.transform.InverseTransformPoint(rendererBounds.min));
            localBounds.Encapsulate(instance.transform.InverseTransformPoint(rendererBounds.max));
        }

        collider.center = localBounds.center;
        collider.size = Vector3.Max(localBounds.size, Vector3.one * 0.05f);
    }

    private Vector3 GetPlatformButtonPosition()
    {
        if (platformButtonAnchor != null)
            return platformButtonAnchor.position;

        return platform.TransformPoint(platformButtonLocalPosition);
    }

    private void SetButtonActive(GameObject button, bool active)
    {
        if (button != null)
            button.SetActive(active);
    }

    private bool IsAtInitial(Vector3 position)
    {
        return Vector3.SqrMagnitude(position - initialPosition) < 0.000001f;
    }

    private bool IsAtFinal(Vector3 position)
    {
        return Vector3.SqrMagnitude(position - finalDestination) < 0.000001f;
    }

    private void CarryPlayer(Vector3 delta)
    {
        CharacterController player = FindFirstObjectByType<CharacterController>();
        if (player == null || platform == null)
            return;

        Vector3 rayOrigin = player.transform.position + Vector3.up * 0.2f;
        float rayDistance = Mathf.Max(1f, player.height * 0.5f + 0.5f);

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
            return;

        if (hit.transform != platform && !hit.transform.IsChildOf(platform))
            return;

        player.Move(delta);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying && hasCapturedInitialPosition && platform != null
            ? initialPosition
            : platform != null ? platform.position : transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(start, 0.35f);
        Gizmos.DrawLine(start, finalDestination);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(finalDestination, 0.35f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(start + endpointButtonOffset, Vector3.one * 0.25f);
        Gizmos.DrawWireCube(finalDestination + endpointButtonOffset, Vector3.one * 0.25f);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        endpointButtonOffset.y = Mathf.Max(0f, endpointButtonOffset.y);
    }
}
