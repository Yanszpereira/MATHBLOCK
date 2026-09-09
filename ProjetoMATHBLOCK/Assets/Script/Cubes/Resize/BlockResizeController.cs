using System;
using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

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

    [Header("Touch Drag")]
    [SerializeField, Min(24f)] private float touchPixelsPerUnit = 110f;
    [SerializeField, Min(1)] private int maximumTouchStepsPerDrag = 6;

    [Header("Particles")]
    [SerializeField] private Texture2D resizeParticleTexture;
    [SerializeField] private Color resizeParticleColor = new Color(1f, 0.82f, 0.12f, 1f);

    [Header("Resize Sound")]
    [SerializeField] private EventReference resizeStepSound;

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
    private bool lastEvaluationApplied;
    private bool touchDragActive;
    private int activeTouchId = -1;
    private Vector2 touchDragStartScreenPosition;

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
        if (MobileTouchControls.ShouldShowTouchControls()
            && GetComponent<BlockResizeTouchUI>() == null)
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
            {
                BeginHandleTouchDrag(hoveredHandle);
                return;
            }
            if (clickAction != null && clickAction.WasPressedThisFrame() && hoveredHandle != null)
                BeginHandleDrag(hoveredHandle, GetPointerRay());
            return;
        }

        if (state == BlockResizeInteractionState.DraggingHandle)
        {
            if (touchDragActive)
            {
                if (TryGetActiveTouchPosition(out Vector2 touchPosition))
                    UpdateHandleTouchDrag(touchPosition);
                else
                    EndHandleDrag();
                return;
            }

            // Use screen-space quantization for mouse as well as touch. Ray/plane
            // intersections become unstable when a selected face is viewed at a
            // shallow angle and used to turn a few pixels into several units.
            UpdateHandleTouchDrag(GetPointerScreenPosition());
            if (clickAction == null || clickAction.WasReleasedThisFrame())
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
        if (block == null)
        {
            LogInteraction($"objeto atingido sem ResizableBlock: {(hit.collider != null ? hit.collider.name : "null")}");
            return false;
        }

        ResizeFace face = BlockResizeGizmo.SelectFace(
            block,
            hit.normal,
            true,
            playerCamera.transform.position
        );
        LogInteraction($"alvo detectado block={block.name} face={face} dims={block.Dimensions} volume={block.CurrentVolume}/{block.MaximumVolume}");
        return TryBeginResize(block, face);
    }

    public bool TryBeginResize(ResizableBlock block, ResizeFace face)
    {
        if (state != BlockResizeInteractionState.Idle || block == null)
        {
            LogInteraction($"inicio recusado: state={state}, block={(block != null ? block.name : "null")}");
            return false;
        }
        if (!PrepareResizeTarget(block))
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
            LogInteraction($"modo iniciado block={block.name} face={face} dims={block.Dimensions} volume={block.CurrentVolume}/{block.MaximumVolume}");
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
        touchDragStartScreenPosition = playerCamera.WorldToScreenPoint(dragStartPoint);
        lastEvaluatedSteps = int.MinValue;
        lastEvaluationApplied = false;
        hoveredHandle = null;
        resizeGizmo.SetAllHandlesState(ResizeHandleVisualState.Normal);
        draggedHandle.SetVisualState(ResizeHandleVisualState.Selected);
        touchDragActive = false;
        activeTouchId = -1;
        state = BlockResizeInteractionState.DraggingHandle;
        LogInteraction($"drag iniciado handle={handle.Position} direction={draggedDirection} face={selectedFace} dims={dragStartState.Dimensions} startScreen={touchDragStartScreenPosition} axisWorld={dragAxisWorld} unitWorld={dragUnitWorldSize:0.###}");
        return true;
    }

    private bool BeginHandleTouchDrag(BlockResizeHandle handle)
    {
        if (Touchscreen.current == null)
            return false;

        var primaryTouch = Touchscreen.current.primaryTouch;
        Vector2 startPosition = primaryTouch.position.ReadValue();
        if (!BeginHandleDrag(handle, playerCamera.ScreenPointToRay(startPosition)))
            return false;

        touchDragActive = true;
        activeTouchId = primaryTouch.touchId.ReadValue();
        touchDragStartScreenPosition = startPosition;
        LogInteraction($"touch drag vinculado touchId={activeTouchId} start={startPosition}");
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

        return ApplyResizeSteps(steps);
    }

    private bool UpdateHandleTouchDrag(Vector2 screenPosition)
    {
        if (state != BlockResizeInteractionState.DraggingHandle || selectedBlock == null)
            return false;

        Vector3 originScreen = playerCamera.WorldToScreenPoint(dragStartPoint);
        Vector2 axisScreen = (Vector2)(playerCamera.WorldToScreenPoint(
            dragStartPoint + dragAxisWorld * Mathf.Max(dragUnitWorldSize, 0.01f)) - originScreen);
        if (axisScreen.sqrMagnitude < 4f)
        {
            LogInteraction($"drag sem projecao de tela direction={draggedDirection} axisScreen={axisScreen}");
            return false;
        }

        float projectedPixels = Vector2.Dot(
            screenPosition - touchDragStartScreenPosition,
            axisScreen.normalized);
        int steps = CalculateTouchSteps(projectedPixels, touchPixelsPerUnit, maximumTouchStepsPerDrag);
        if (steps != lastEvaluatedSteps)
            LogInteraction($"drag atualizado pointer={(touchDragActive ? "touch" : "mouse")} direction={draggedDirection} pixels={projectedPixels:0.0} steps={steps} base={dragStartState.Dimensions}");

        return ApplyResizeSteps(steps);
    }

    private bool ApplyResizeSteps(int steps)
    {
        if (steps == lastEvaluatedSteps)
        {
            draggedHandle.SetVisualState(lastEvaluationApplied
                ? ResizeHandleVisualState.Allowed
                : ResizeHandleVisualState.Blocked);
            return lastEvaluationApplied;
        }

        // Clicking a handle starts with zero displacement. Do not rebuild the
        // block for that no-op frame: a rebuild used to reset the root scale and
        // remove the inverse scale inherited from a scaled spawner.
        if (steps == 0 && selectedBlock.Dimensions == dragStartState.Dimensions)
        {
            lastEvaluatedSteps = 0;
            lastEvaluationApplied = true;
            draggedHandle.SetVisualState(ResizeHandleVisualState.Allowed);
            LogInteraction($"resize sem deslocamento; estado preservado dims={selectedBlock.Dimensions} localScale={selectedBlock.transform.localScale} lossyScale={selectedBlock.transform.lossyScale}");
            return true;
        }

        Vector3Int previousDimensions = selectedBlock.Dimensions;

        bool applied = selectedBlock.TryApplyResizeFromState(
            dragStartState, selectedFace, draggedDirection, steps, out ResizeValidationFailure failure);

        lastEvaluatedSteps = steps;
        lastEvaluationApplied = applied;
        draggedHandle.SetVisualState(applied ? ResizeHandleVisualState.Allowed : ResizeHandleVisualState.Blocked);

        if (!applied)
        {
            LogInteraction($"resize bloqueado direction={draggedDirection} steps={steps} base={dragStartState.Dimensions} atual={previousDimensions} volumeMax={selectedBlock.MaximumVolume} motivo={failure}");
            return false;
        }

        Vector3Int newDimensions = selectedBlock.Dimensions;
        LogInteraction($"resize aplicado direction={draggedDirection} steps={steps} {previousDimensions}->{newDimensions} volume={selectedBlock.CurrentVolume}/{selectedBlock.MaximumVolume}");

        if (newDimensions != previousDimensions)
            PlayResizeStepSound();

        resizeGizmo.UpdateLayout();
        RefreshSelectedBlockParticles();
        return true;
    }

    private void PlayResizeStepSound()
    {
        if (resizeStepSound.IsNull)
            return;

        Vector3 soundPosition = selectedBlock != null
            ? selectedBlock.WorldCenter
            : transform.position;

        RuntimeManager.PlayOneShot(resizeStepSound, soundPosition);
    }

    public void EndHandleDrag()
    {
        if (state != BlockResizeInteractionState.DraggingHandle)
            return;

        LogInteraction($"drag encerrado direction={draggedDirection} dims={(selectedBlock != null ? selectedBlock.Dimensions.ToString() : "null")}");
        if (draggedHandle != null)
            draggedHandle.SetVisualState(ResizeHandleVisualState.Normal);
        draggedHandle = null;
        touchDragActive = false;
        activeTouchId = -1;
        lastEvaluatedSteps = int.MinValue;
        lastEvaluationApplied = false;
        state = BlockResizeInteractionState.ResizeMode;
        UpdateHoveredHandle(GetActivePointerRay());
    }

    public void ConfirmResizeSession()
    {
        if (state != BlockResizeInteractionState.Idle)
        {
            LogInteraction($"sessao confirmada dims={(selectedBlock != null ? selectedBlock.Dimensions.ToString() : "null")}");
            ExitResizeMode(false);
        }
    }

    public void CancelResizeSession()
    {
        if (state != BlockResizeInteractionState.Idle)
        {
            LogInteraction($"sessao cancelada; restaurando {sessionStartState.Dimensions}");
            ExitResizeMode(true);
        }
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

    public static int CalculateTouchSteps(float projectedPixels, float pixelsPerUnit, int maximumSteps)
    {
        float safePixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        int safeMaximum = Mathf.Max(1, maximumSteps);
        int magnitude = Mathf.FloorToInt(Mathf.Abs(projectedPixels) / safePixelsPerUnit);
        return Mathf.Clamp(magnitude, 0, safeMaximum) * (projectedPixels < 0f ? -1 : 1);
    }

    private void OnEnterResizePerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryHandleResizeKey();
    }

    public bool TryHandleResizeKey()
    {
        LogInteraction($"F pressionado state={state} holding={(gravityInteract != null && gravityInteract.IsHoldingBlock)}");
        if (state != BlockResizeInteractionState.Idle)
            return false;

        if (gravityInteract != null
            && gravityInteract.TryAnchorHeldResizableBlock(resizeParticleTexture, resizeParticleColor))
        {
            LogInteraction($"bloco ancorado pelo F block={(gravityInteract.HeldBlock != null ? gravityInteract.HeldBlock.name : "null")}");
            return true;
        }

        return TryBeginResizeAtCameraCenter();
    }

    public bool TryHandleResizeTouchButton()
    {
        LogInteraction($"botao touch pressionado state={state} holding={(gravityInteract != null && gravityInteract.IsHoldingBlock)}");
        if (state != BlockResizeInteractionState.Idle)
            return false;

        if (gravityInteract != null && gravityInteract.IsHoldingBlock)
        {
            ResizableBlock heldBlock = gravityInteract.HeldBlock != null
                ? gravityInteract.HeldBlock.GetComponent<ResizableBlock>()
                : null;
            if (heldBlock == null || !PrepareResizeTarget(heldBlock))
                return false;

            if (!gravityInteract.TryAnchorHeldResizableBlock(resizeParticleTexture, resizeParticleColor))
                return false;

            LogInteraction($"bloco ancorado pelo touch block={heldBlock.name}");
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

    public bool HasResizeTargetAtCameraCenter()
    {
        if (state != BlockResizeInteractionState.Idle || playerCamera == null)
            return false;
        if (gravityInteract != null && gravityInteract.IsHoldingBlock)
            return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, InteractionDistance, targetingMask, QueryTriggerInteraction.Ignore))
            return false;

        ResizableBlock targetBlock = hit.collider != null
            ? hit.collider.GetComponentInParent<ResizableBlock>()
            : null;
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

        LogInteraction($"hover handle={(hoveredHandle != null ? hoveredHandle.Position.ToString() : "none")}");
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

    private bool TryGetActiveTouchPosition(out Vector2 position)
    {
        position = default;
        if (Touchscreen.current == null || activeTouchId < 0)
            return false;

        foreach (var touch in Touchscreen.current.touches)
        {
            if (touch.touchId.ReadValue() != activeTouchId || !touch.press.isPressed)
                continue;
            position = touch.position.ReadValue();
            return true;
        }
        return false;
    }

    private Ray GetPointerRay()
    {
        Vector2 point = GetPointerScreenPosition();
        return playerCamera.ScreenPointToRay(point);
    }

    private Vector2 GetPointerScreenPosition()
    {
        return pointAction != null
            ? pointAction.ReadValue<Vector2>()
            : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
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
        string blockName = selectedBlock != null ? selectedBlock.name : "null";
        Vector3Int finalDimensions = selectedBlock != null ? selectedBlock.Dimensions : default;
        if (restoreSessionState && selectedBlock != null)
        {
            selectedBlock.RestoreState(sessionStartState);
            RefreshSelectedBlockParticles();
            finalDimensions = selectedBlock.Dimensions;
        }

        if (resizeGizmo != null)
            resizeGizmo.Hide();
        StopResizeParticles();
        hoveredHandle = null;
        draggedHandle = null;
        touchDragActive = false;
        activeTouchId = -1;
        lastEvaluatedSteps = int.MinValue;
        lastEvaluationApplied = false;
        RestoreRigidbody();
        RestorePlayerControls();
        selectedBlock = null;
        state = BlockResizeInteractionState.Idle;
        LogInteraction($"modo encerrado block={blockName} restored={restoreSessionState} dims={finalDimensions}");
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
        touchPixelsPerUnit = Mathf.Max(24f, touchPixelsPerUnit);
        maximumTouchStepsPerDrag = Mathf.Max(1, maximumTouchStepsPerDrag);
    }

    private bool PrepareResizeTarget(ResizableBlock block)
    {
        if (block == null)
            return false;

        if (block.IsDimensionProposalValid(
            block.Width,
            block.Height,
            block.Depth,
            out ResizeValidationFailure failure))
        {
            return true;
        }

        LogInteraction($"alvo invalido block={block.name} dims={block.Dimensions} volume={block.CurrentVolume}/{block.MaximumVolume} motivo={failure}; dimensoes nao serao alteradas ao entrar");
        return false;
    }

    private void LogInteraction(string message)
    {
        Debug.Log($"[BlockResize] {name}: {message}", this);
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
