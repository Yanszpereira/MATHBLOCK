using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BlockResizeInteractionState
{
    Idle,
    ResizeMode,
    DraggingHandle
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class BlockResizeController : MonoBehaviour
{
    private const string PlayerMapName = "Player";
    private const string ResizeMapName = "Resize";
    private const float GrabDistanceFraction = 2f / 3f;

    [Header("Player References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Look playerLook;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private GravityInteract gravityInteract;

    [Header("Targeting")]
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private LayerMask targetingMask = ~0;

    [Header("Gizmo")]
    [SerializeField] private BlockResizeGizmo resizeGizmoPrefab;
    [SerializeField] private LayerMask resizeHandleMask;
    [SerializeField] private float dragDeadZone = 0.5f;
    [SerializeField] private bool restoreCapturedVelocity;

    [Header("Particles")]
    [SerializeField] private Texture2D resizeParticleTexture;
    [SerializeField] private Color resizeParticleColor = new Color(1f, 0.82f, 0.12f, 1f);

    private BlockResizeGizmo resizeGizmo;
    private BlockResizeParticleEffect resizeParticleEffect;
    private BlockResizeInteractionState state;
    private ResizableBlock selectedBlock;
    private ResizeFace selectedFace;
    private ResizableBlockState sessionStartState;
    private ResizableBlockState dragStartState;
    private Rigidbody selectedRigidbody;
    private RigidbodySessionState rigidbodyState;

    private BlockResizeHandle hoveredHandle;
    private BlockResizeHandle draggedHandle;
    private ResizeDirection draggedDirection;
    private Plane dragPlane;
    private Vector3 dragStartPoint;
    private Vector3 dragAxisWorld;
    private float dragUnitWorldSize;
    private int lastEvaluatedSteps = int.MinValue;

    private InputAction enterResizeAction;
    private InputAction pointAction;
    private InputAction clickAction;
    private InputAction cancelAction;
    private InputAction exitResizeAction;
    private string previousActionMapName;
    private bool previousPlayerMapEnabled;
    private bool controlsCaptured;
    private bool lookWasEnabled;
    private bool movementWasEnabled;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    public BlockResizeInteractionState State => state;
    public ResizableBlock SelectedBlock => selectedBlock;
    public ResizeFace SelectedFace => selectedFace;
    public BlockResizeGizmo ActiveGizmo => resizeGizmo;
    public BlockResizeParticleEffect ActiveParticleEffect => resizeParticleEffect;
    public float InteractionDistance => gravityInteract != null
        ? gravityInteract.GrabDistance * GrabDistanceFraction
        : interactionDistance;
    public event Action<bool> ResizeModeChanged;

    private void Awake()
    {
        ResolveReferences();
        if (GetComponent<BlockResizeTouchUI>() == null)
            gameObject.AddComponent<BlockResizeTouchUI>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindInputActions();
    }

    private void OnDisable()
    {
        if (state != BlockResizeInteractionState.Idle || controlsCaptured)
            ExitResizeMode(true);

        UnbindInputActions();
    }

    private void OnDestroy()
    {
        StopResizeParticles();
        if (resizeGizmo != null)
            Destroy(resizeGizmo.gameObject);
    }

    private void Update()
    {
        if (state == BlockResizeInteractionState.Idle)
            return;

        if (selectedBlock == null)
        {
            ExitResizeMode(false);
            return;
        }

        if (state == BlockResizeInteractionState.ResizeMode)
        {
            UpdateHoveredHandle(GetActivePointerRay());
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame && hoveredHandle != null)
                BeginHandleDrag(hoveredHandle, GetTouchRay());
            if (clickAction != null && clickAction.WasPressedThisFrame() && hoveredHandle != null)
                BeginHandleDrag(hoveredHandle, GetPointerRay());
            return;
        }

        if (state == BlockResizeInteractionState.DraggingHandle)
        {
            Ray pointerRay = GetActivePointerRay();
            UpdateHandleDrag(pointerRay);
            bool touchReleased = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            if (touchReleased || (Touchscreen.current == null && (clickAction == null || clickAction.WasReleasedThisFrame())))
                EndHandleDrag();
        }
    }

    public bool TryBeginResizeAtCameraCenter()
    {
        if (playerCamera == null)
            return false;

        return TryBeginResizeFromRay(playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));
    }

    public bool TryBeginResizeFromRay(Ray ray)
    {
        if (state != BlockResizeInteractionState.Idle || playerCamera == null)
            return false;

        if (gravityInteract != null && gravityInteract.IsHoldingBlock)
            return false;

        if (!Physics.Raycast(ray, out RaycastHit hit, InteractionDistance, targetingMask, QueryTriggerInteraction.Ignore))
            return false;

        ResizableBlock block = hit.collider != null ? hit.collider.GetComponentInParent<ResizableBlock>() : null;
        if (block == null || !block.CanResize())
            return false;

        ResizeFace face = BlockResizeGizmo.SelectFace(
            block,
            hit.normal,
            true,
            playerCamera.transform.position
        );
        return TryBeginResize(block, face);
    }

    public bool TryBeginResize(ResizableBlock block, ResizeFace face)
    {
        if (state != BlockResizeInteractionState.Idle || block == null || !block.CanResize())
            return false;
        if (gravityInteract != null && gravityInteract.IsHoldingBlock)
            return false;
        if (!EnsureGizmo() || !CaptureAndDisablePlayerControls())
            return false;

        selectedBlock = block;
        selectedFace = face;
        sessionStartState = block.CaptureState();
        CaptureAndFreezeRigidbody(block.GetComponent<Rigidbody>());

        try
        {
            resizeGizmo.Show(block, playerCamera, face);
            if (!IsAirAnchored(block))
                StartResizeParticles(block);
            state = BlockResizeInteractionState.ResizeMode;
            ResizeModeChanged?.Invoke(true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"{name}: falha ao iniciar o modo de redimensionamento. {exception.Message}", this);
            ExitResizeMode(false);
            return false;
        }
    }

    public bool BeginHandleDrag(BlockResizeHandle handle, Ray pointerRay)
    {
        if (state != BlockResizeInteractionState.ResizeMode || resizeGizmo == null || !resizeGizmo.ContainsHandle(handle))
            return false;

        Plane candidatePlane = new Plane(resizeGizmo.FaceNormalWorld, resizeGizmo.FacePlanePoint);
        if (!candidatePlane.Raycast(pointerRay, out float enter))
            return false;

        draggedHandle = handle;
        draggedDirection = resizeGizmo.GetResizeDirection(handle.Position);
        dragStartState = selectedBlock.CaptureState();
        dragStartPoint = pointerRay.GetPoint(enter);
        dragPlane = candidatePlane;
        dragAxisWorld = resizeGizmo.GetDragAxisWorld(handle.Position);
        dragUnitWorldSize = GetDragUnitSize(handle.Position);
        lastEvaluatedSteps = int.MinValue;
        hoveredHandle = null;
        resizeGizmo.SetAllHandlesState(ResizeHandleVisualState.Normal);
        draggedHandle.SetVisualState(ResizeHandleVisualState.Selected);
        state = BlockResizeInteractionState.DraggingHandle;
        return true;
    }

    public bool UpdateHandleDrag(Ray pointerRay)
    {
        if (state != BlockResizeInteractionState.DraggingHandle || draggedHandle == null || selectedBlock == null)
            return false;
        if (!dragPlane.Raycast(pointerRay, out float enter))
            return false;

        Vector3 currentPoint = pointerRay.GetPoint(enter);
        Vector3 deltaWorld = currentPoint - dragStartPoint;
        int steps = CalculateLinearSteps(
            Vector3.Dot(deltaWorld, dragAxisWorld),
            dragUnitWorldSize,
            dragDeadZone
        );

        if (steps == lastEvaluatedSteps)
            return true;

        lastEvaluatedSteps = steps;
        bool applied = selectedBlock.TryApplyResizeFromState(
            dragStartState,
            selectedFace,
            draggedDirection,
            steps,
            out _
        );
        draggedHandle.SetVisualState(applied ? ResizeHandleVisualState.Allowed : ResizeHandleVisualState.Blocked);
        if (applied)
        {
            resizeGizmo.UpdateLayout();
            RefreshSelectedBlockParticles();
        }
        return applied;
    }

    public void EndHandleDrag()
    {
        if (state != BlockResizeInteractionState.DraggingHandle)
            return;

        if (draggedHandle != null)
            draggedHandle.SetVisualState(ResizeHandleVisualState.Normal);
        draggedHandle = null;
        lastEvaluatedSteps = int.MinValue;
        state = BlockResizeInteractionState.ResizeMode;
        UpdateHoveredHandle(GetActivePointerRay());
    }

    public void ConfirmResizeSession()
    {
        if (state != BlockResizeInteractionState.Idle)
            ExitResizeMode(false);
    }

    public void CancelResizeSession()
    {
        if (state != BlockResizeInteractionState.Idle)
            ExitResizeMode(true);
    }

    public static int CalculateLinearSteps(float signedDistance, float unitWorldSize, float deadZone = 0.5f)
    {
        if (unitWorldSize <= 0.0001f)
            return 0;

        float units = signedDistance / unitWorldSize;
        if (Mathf.Abs(units) < Mathf.Clamp01(deadZone))
            return 0;
        return Mathf.RoundToInt(units);
    }

    private void OnEnterResizePerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryHandleResizeKey();
    }

    public bool TryHandleResizeKey()
    {
        if (state != BlockResizeInteractionState.Idle)
            return false;

        if (gravityInteract != null
            && gravityInteract.TryAnchorHeldResizableBlock(resizeParticleTexture, resizeParticleColor))
        {
            return true;
        }

        return TryBeginResizeAtCameraCenter();
    }

    public bool TryHandleResizeTouchButton()
    {
        if (state != BlockResizeInteractionState.Idle)
            return false;

        if (gravityInteract != null && gravityInteract.IsHoldingBlock)
        {
            ResizableBlock heldBlock = gravityInteract.HeldBlock != null
                ? gravityInteract.HeldBlock.GetComponent<ResizableBlock>()
                : null;
            if (heldBlock == null || !heldBlock.CanResize())
                return false;

            if (!gravityInteract.TryAnchorHeldResizableBlock(resizeParticleTexture, resizeParticleColor))
                return false;
        }

        return TryBeginResizeAtCameraCenter();
    }

    public bool HasAvailableResizeTarget()
    {
        if (state != BlockResizeInteractionState.Idle)
            return false;

        if (gravityInteract != null && gravityInteract.IsHoldingBlock)
        {
            ResizableBlock heldBlock = gravityInteract.HeldBlock != null
                ? gravityInteract.HeldBlock.GetComponent<ResizableBlock>()
                : null;
            return heldBlock != null && heldBlock.CanResize();
        }

        if (playerCamera == null)
            return false;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, InteractionDistance, targetingMask, QueryTriggerInteraction.Ignore))
            return false;
        ResizableBlock targetBlock = hit.collider != null ? hit.collider.GetComponentInParent<ResizableBlock>() : null;
        return targetBlock != null && targetBlock.CanResize();
    }

    private void OnExitResizePerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
            ConfirmResizeSession();
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
            CancelResizeSession();
    }

    private void UpdateHoveredHandle(Ray ray)
    {
        BlockResizeHandle nextHandle = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, resizeHandleMask, QueryTriggerInteraction.Collide))
            nextHandle = hit.collider != null ? hit.collider.GetComponentInParent<BlockResizeHandle>() : null;
        if (resizeGizmo != null && !resizeGizmo.ContainsHandle(nextHandle))
            nextHandle = null;
        if (nextHandle == hoveredHandle)
            return;

        if (hoveredHandle != null)
            hoveredHandle.SetVisualState(ResizeHandleVisualState.Normal);
        hoveredHandle = nextHandle;
        if (hoveredHandle != null)
            hoveredHandle.SetVisualState(ResizeHandleVisualState.Hover);
    }

    private Ray GetActivePointerRay()
    {
        return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed
            ? GetTouchRay()
            : GetPointerRay();
    }

    private Ray GetTouchRay()
    {
        Vector2 point = Touchscreen.current != null
            ? Touchscreen.current.primaryTouch.position.ReadValue()
            : Vector2.zero;
        return playerCamera.ScreenPointToRay(point);
    }

    private Ray GetPointerRay()
    {
        Vector2 point = pointAction != null
            ? pointAction.ReadValue<Vector2>()
            : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        return playerCamera.ScreenPointToRay(point);
    }

    private float GetDragUnitSize(ResizeHandlePosition position)
    {
        return selectedBlock.GetWorldUnitSize(resizeGizmo.GetResizeDirection(position));
    }

    private bool EnsureGizmo()
    {
        if (resizeGizmo != null)
            return true;
        if (resizeGizmoPrefab == null)
        {
            Debug.LogError($"{name}: prefab BlockResizeGizmo nao foi configurado.", this);
            return false;
        }

        resizeGizmo = Instantiate(resizeGizmoPrefab);
        resizeGizmo.name = resizeGizmoPrefab.name;
        resizeGizmo.Hide();
        return true;
    }

    private void StartResizeParticles(ResizableBlock block)
    {
        StopResizeParticles();
        if (resizeParticleTexture == null)
            return;

        resizeParticleEffect = BlockResizeParticleEffect.Create(
            block,
            resizeParticleTexture,
            resizeParticleColor
        );
    }

    private void StopResizeParticles()
    {
        if (resizeParticleEffect == null)
            return;

        resizeParticleEffect.StopAndFadeOut();
        resizeParticleEffect = null;
    }

    private void RefreshSelectedBlockParticles()
    {
        resizeParticleEffect?.RefreshBounds();
        if (selectedBlock == null)
            return;

        ResizableBlockAirAnchor airAnchor = selectedBlock.GetComponent<ResizableBlockAirAnchor>();
        airAnchor?.ActiveParticleEffect?.RefreshBounds();
    }

    private bool CaptureAndDisablePlayerControls()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError($"{name}: PlayerInput ou InputActionAsset ausente.", this);
            return false;
        }

        InputActionMap resizeMap = playerInput.actions.FindActionMap(ResizeMapName, false);
        if (resizeMap == null)
        {
            Debug.LogError($"{name}: action map Resize nao encontrado.", this);
            return false;
        }

        previousActionMapName = playerInput.currentActionMap != null
            ? playerInput.currentActionMap.name
            : enterResizeAction != null ? enterResizeAction.actionMap.name : PlayerMapName;
        InputActionMap playerMap = playerInput.actions.FindActionMap(PlayerMapName, false);
        previousPlayerMapEnabled = playerMap != null && playerMap.enabled;
        lookWasEnabled = playerLook != null && playerLook.enabled;
        movementWasEnabled = playerMovement != null && playerMovement.enabled;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        controlsCaptured = true;

        try
        {
            playerInput.SwitchCurrentActionMap(ResizeMapName);
            if (playerLook != null)
                playerLook.enabled = false;
            if (playerMovement != null)
                playerMovement.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"{name}: nao foi possivel ativar o action map Resize. {exception.Message}", this);
            RestorePlayerControls();
            return false;
        }
    }

    private void RestorePlayerControls()
    {
        if (!controlsCaptured)
            return;

        try
        {
            if (playerInput != null && playerInput.actions != null)
            {
                if (playerInput.enabled && playerInput.inputIsActive
                    && !string.IsNullOrEmpty(previousActionMapName)
                    && playerInput.actions.FindActionMap(previousActionMapName, false) != null)
                {
                    playerInput.SwitchCurrentActionMap(previousActionMapName);
                }
                else
                {
                    playerInput.actions.FindActionMap(ResizeMapName, false)?.Disable();
                    if (previousPlayerMapEnabled)
                        playerInput.actions.FindActionMap(PlayerMapName, false)?.Enable();
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"{name}: falha ao restaurar o action map anterior. {exception.Message}", this);
            playerInput?.actions?.FindActionMap(ResizeMapName, false)?.Disable();
            if (previousPlayerMapEnabled)
                playerInput?.actions?.FindActionMap(PlayerMapName, false)?.Enable();
        }

        if (playerLook != null)
            playerLook.enabled = lookWasEnabled;
        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        controlsCaptured = false;
    }

    private void CaptureAndFreezeRigidbody(Rigidbody targetRigidbody)
    {
        selectedRigidbody = targetRigidbody;
        if (selectedRigidbody == null)
            return;

        rigidbodyState = new RigidbodySessionState(
            selectedRigidbody.isKinematic,
            selectedRigidbody.useGravity,
            selectedRigidbody.linearVelocity,
            selectedRigidbody.angularVelocity
        );
        if (!selectedRigidbody.isKinematic)
        {
            selectedRigidbody.linearVelocity = Vector3.zero;
            selectedRigidbody.angularVelocity = Vector3.zero;
        }
        selectedRigidbody.useGravity = false;
        selectedRigidbody.isKinematic = true;
    }

    private void RestoreRigidbody()
    {
        if (selectedRigidbody == null)
            return;

        selectedRigidbody.isKinematic = rigidbodyState.IsKinematic;
        selectedRigidbody.useGravity = rigidbodyState.UseGravity;
        if (!selectedRigidbody.isKinematic)
        {
            selectedRigidbody.linearVelocity = restoreCapturedVelocity ? rigidbodyState.LinearVelocity : Vector3.zero;
            selectedRigidbody.angularVelocity = restoreCapturedVelocity ? rigidbodyState.AngularVelocity : Vector3.zero;
        }
        selectedRigidbody = null;
    }

    private void ExitResizeMode(bool restoreSessionState)
    {
        if (restoreSessionState && selectedBlock != null)
        {
            selectedBlock.RestoreState(sessionStartState);
            RefreshSelectedBlockParticles();
        }

        if (resizeGizmo != null)
            resizeGizmo.Hide();
        StopResizeParticles();
        hoveredHandle = null;
        draggedHandle = null;
        RestoreRigidbody();
        RestorePlayerControls();
        selectedBlock = null;
        state = BlockResizeInteractionState.Idle;
        ResizeModeChanged?.Invoke(false);
    }

    private void BindInputActions()
    {
        UnbindInputActions();
        if (playerInput == null || playerInput.actions == null)
            return;

        enterResizeAction = playerInput.actions.FindAction("Player/EnterResize", false);
        pointAction = playerInput.actions.FindAction("Resize/Point", false);
        clickAction = playerInput.actions.FindAction("Resize/Click", false);
        cancelAction = playerInput.actions.FindAction("Resize/Cancel", false);
        exitResizeAction = playerInput.actions.FindAction("Resize/ExitResize", false);

        if (enterResizeAction != null)
            enterResizeAction.performed += OnEnterResizePerformed;
        if (cancelAction != null)
            cancelAction.performed += OnCancelPerformed;
        if (exitResizeAction != null)
            exitResizeAction.performed += OnExitResizePerformed;
    }

    private void UnbindInputActions()
    {
        if (enterResizeAction != null)
            enterResizeAction.performed -= OnEnterResizePerformed;
        if (cancelAction != null)
            cancelAction.performed -= OnCancelPerformed;
        if (exitResizeAction != null)
            exitResizeAction.performed -= OnExitResizePerformed;
        enterResizeAction = null;
        pointAction = null;
        clickAction = null;
        cancelAction = null;
        exitResizeAction = null;
    }

    private void ResolveReferences()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
        if (playerLook == null)
            playerLook = GetComponent<Look>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (gravityInteract == null)
            gravityInteract = GetComponentInChildren<GravityInteract>(true);
        if (resizeHandleMask.value == 0)
        {
            int handleLayer = LayerMask.NameToLayer("ResizeHandle");
            if (handleLayer >= 0)
                resizeHandleMask = 1 << handleLayer;
        }
        int resizeLayer = LayerMask.NameToLayer("ResizeHandle");
        if (resizeLayer >= 0)
            targetingMask &= ~(1 << resizeLayer);
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        dragDeadZone = Mathf.Clamp01(dragDeadZone);
    }

    private static bool IsAirAnchored(ResizableBlock block)
    {
        if (block == null)
            return false;

        ResizableBlockAirAnchor airAnchor = block.GetComponent<ResizableBlockAirAnchor>();
        return airAnchor != null && airAnchor.IsAnchored;
    }

    private readonly struct RigidbodySessionState
    {
        public bool IsKinematic { get; }
        public bool UseGravity { get; }
        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }

        public RigidbodySessionState(bool isKinematic, bool useGravity, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            IsKinematic = isKinematic;
            UseGravity = useGravity;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }
    }
}
