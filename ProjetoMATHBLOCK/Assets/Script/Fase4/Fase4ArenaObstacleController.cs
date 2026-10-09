using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Drives only the Fase 4 arena obstacles, consuming the existing round manager state
/// instead of owning a second round state machine.
/// </summary>
[DisallowMultipleComponent]
public sealed class Fase4ArenaObstacleController : MonoBehaviour
{
    private const float BarStartDelaySeconds = 3f;
    private const float BarImpactImmunitySeconds = 2f;
    private const float BarImpactSpeed = 5.5f;
    private const float BarImpactDuration = 0.32f;
    private const float PistonMotionSubstepSeconds = 0.02f;
    private const int MaxPistonSubstepsPerFrame = 128;
    private const float MaxPistonTimeBacklogSeconds = 60f;

    [Serializable]
    private sealed class PistonAssembly
    {
        public Transform root;
        public Transform rod;
        public Transform impactHead;
        public Renderer[] warningRenderers;
        public Collider[] movingColliders;
        public Vector3 rodRestPosition;
        public Vector3 headRestPosition;
        public float rodStrokeLocalUnits;
        public float headStrokeLocalUnits;
        public float safeStrokeWorldMeters;
    }

    private sealed class WarningRendererState
    {
        public Renderer renderer;
        public MaterialPropertyBlock originalBlock;
    }

    [Header("Audio events (optional; leave empty if the FMOD bank has no matching event)")]
    [SerializeField] private EventReference pistonWarningEvent;
    [SerializeField] private EventReference obstacleImpactEvent;

    [Header("Optional scene references; resolved from this object's children when empty")]
    [SerializeField] private Transform rotatingBar;
    [SerializeField] private Transform[] pistonRoots = Array.Empty<Transform>();

    [Header("Piston reach and impact tuning")]
    [Tooltip("Maximum world-space stroke per piston, calibrated from the current head/rod MeshColliders and arena ring. Recalibrate if those transforms or meshes change.")]
    [SerializeField] private float[] pistonStrokeWorldMeters = { 13.98f, 13.60f, 13.60f };
    [Tooltip("Disable for demonstrations with known piston physics issues. The visual warning and extension cycles continue; player collision response remains suppressed during moving cycles.")]
    [SerializeField] private bool pistonImpactsEnabled = true;
    [SerializeField, Min(0f)] private float pistonImpactSpeed = 4f;
    [SerializeField, Min(0f)] private float pistonImpactDuration = 0.35f;
    [SerializeField, Min(0f)] private float pistonDetectionPaddingMeters;

    private readonly Fase4PistonCycleSchedule pistonSchedule = new Fase4PistonCycleSchedule();
    private readonly List<PistonAssembly> pistons = new List<PistonAssembly>(3);
    private readonly List<WarningRendererState> warningRendererStates = new List<WarningRendererState>(3);
    private readonly Collider[] playerOverlapBuffer = new Collider[64];
    private readonly RaycastHit[] pistonSweepHitBuffer = new RaycastHit[32];
    private readonly Dictionary<Collider, Vector3> previousPistonColliderPositions = new Dictionary<Collider, Vector3>();
    private readonly Fase4PistonPlayerCollisionPolicy pistonPlayerCollisionPolicy = new Fase4PistonPlayerCollisionPolicy();

    private Fase4RoundManager rounds;
    private PlayerMovement playerMovement;
    private CharacterController playerController;
    private Fase4ObstaclePushReceiver pushReceiver;
    private Transform barHaste;
    private Collider[] barImpactColliders = Array.Empty<Collider>();
    private Quaternion barRestRotation;
    private float accumulatedBarAngle;
    private float barStartTime;
    private float nextBarImpactTime;
    private float warningStartedAt;
    private int activeRound;
    private int observedCycleIndex;
    private bool initialized;
    private bool barDelayPending;
    private bool barSpinning;
    private bool pistonHitThisCycle;
    private bool warningFlashBright;
    private bool warningOverridesActive;
    private bool roundEventsSubscribed;
    private float pistonTimeBacklog;
    private Vector3 previousPlayerCapsuleCenter;
    private bool hasPreviousPlayerCapsuleCenter;

    public bool IsBarSpinning => barSpinning;
    public bool ArePistonsRepeating => pistonSchedule.IsRepeating;
    public Fase4PistonCycleSchedule.Phase CurrentPistonPhase => pistonSchedule.CurrentPhase;
    public float PistonExtensionAmount => pistonSchedule.ExtensionAmount;

    /// <summary>Called once by the existing round coordinator after it resolves its live manager and player.</summary>
    public bool Initialize(Fase4RoundManager roundManager, PlayerMovement movement)
    {
        if (roundManager == null || movement == null)
        {
            Debug.LogError("O controlador de obstáculos precisa do gerenciador de rodadas e do jogador da Fase 4.", this);
            return false;
        }

        if (initialized)
        {
            if (rounds == roundManager && playerMovement == movement)
                return true;
            Shutdown();
        }

        rounds = roundManager;
        playerMovement = movement;
        playerController = movement.controller != null ? movement.controller : movement.GetComponent<CharacterController>();
        if (playerController == null)
        {
            Debug.LogError("Fase 4: o jogador não possui CharacterController para receber os impactos.", movement);
            ClearReferences();
            return false;
        }

        ResolveObstacleReferences();
        if (rotatingBar == null || barHaste == null || pistons.Count != 3)
        {
            Debug.LogError("Fase 4 requer a barra giratória e os três conjuntos de pistão em Arena_Obstaculos.", this);
            ClearReferences();
            return false;
        }
        if (barHaste.GetComponentsInChildren<Collider>(true).Length == 0)
            Debug.LogWarning("A haste da barra não possui colliders; impactos da barra não serão detectados.", barHaste);

        pushReceiver = movement.GetComponent<Fase4ObstaclePushReceiver>();
        if (pushReceiver == null)
            pushReceiver = movement.gameObject.AddComponent<Fase4ObstaclePushReceiver>();

        barRestRotation = rotatingBar.rotation;
        barImpactColliders = barHaste.GetComponentsInChildren<Collider>(true);
        if (!ConfigurePistonStrokeOffsets(out string reachError))
        {
            Debug.LogError("Fase 4: configuração de alcance dos pistões inválida. " + reachError, this);
            ClearReferences();
            return false;
        }

        try
        {
            CacheWarningRenderers();
            CachePistonColliderPositions();
            initialized = true;
            if (isActiveAndEnabled)
            {
                SubscribeToRoundEvents();
                OnRoundStateChanged(rounds.State);
                if (rounds.State == Fase4RoundManager.RoundState.Active && rounds.PistonsLogicallyActive)
                    OnPistonsActivated();
            }
            return true;
        }
        catch (Exception exception)
        {
            UnsubscribeFromRoundEvents();
            pistonPlayerCollisionPolicy.Restore();
            RestoreWarningRenderers();
            ClearReferences();
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnDisable()
    {
        if (!initialized)
            return;

        UnsubscribeFromRoundEvents();
        barDelayPending = false;
        barSpinning = false;
        StopAndRestorePistonState();
    }

    private void OnEnable()
    {
        if (!initialized || rounds == null)
            return;

        try
        {
            if (pistonSchedule.IsRepeating || pistonSchedule.ExtensionAmount > 0f)
                ApplyPistonPlayerCollisionPolicy();
            else
                pistonPlayerCollisionPolicy.Restore();
        }
        catch (Exception exception)
        {
            pistonPlayerCollisionPolicy.Restore();
            Debug.LogException(exception, this);
            enabled = false;
            return;
        }
        SubscribeToRoundEvents();
        // Disabling this controller safely stops and homes its moving parts. Reconcile
        // with the single round manager when it is enabled again so an active round
        // does not leave the obstacles permanently inactive.
        OnRoundStateChanged(rounds.State);
        if (rounds.State == Fase4RoundManager.RoundState.Active && rounds.PistonsLogicallyActive)
            OnPistonsActivated();
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (pistonSchedule.IsRepeating || pistonSchedule.ExtensionAmount > 0f)
        {
            try
            {
                if (!pistonPlayerCollisionPolicy.IsApplied)
                    ApplyPistonPlayerCollisionPolicy();
                else
                    pistonPlayerCollisionPolicy.EnsureApplied();
            }
            catch (Exception exception)
            {
                pistonPlayerCollisionPolicy.Restore();
                Debug.LogException(exception, this);
                enabled = false;
                return;
            }
        }
        else if (pistonPlayerCollisionPolicy.IsApplied)
        {
            pistonPlayerCollisionPolicy.Restore();
        }

        bool transformsChanged = UpdateBarRotation(Time.deltaTime);
        if (transformsChanged)
            Physics.SyncTransforms();
        DetectBarImpact();
        AdvancePistonSchedule(Time.deltaTime);
    }

    /// <summary>Called by the existing Void integration before it starts the player's fade.</summary>
    public void NotifyPlayerRespawnStarted()
    {
        if (pushReceiver != null)
            pushReceiver.ClearImpact();
        ResetContactHistory();
    }

    public void Shutdown()
    {
        try
        {
            UnsubscribeFromRoundEvents();
            if (initialized || previousPistonColliderPositions.Count > 0 || pistonPlayerCollisionPolicy.IsApplied)
                StopAndRestorePistonState();
        }
        finally
        {
            ClearReferences();
        }
    }

    private void StopAndRestorePistonState()
    {
        try
        {
            // A disabled MonoBehaviour cannot continue its normal smooth-retraction
            // update. Fail closed and home the movable pieces before leaving it idle.
            pistonSchedule.Reset();
            ApplyPistonExtension(0f);
            Physics.SyncTransforms();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            pistonTimeBacklog = 0f;
            pistonPlayerCollisionPolicy.Restore();
            try
            {
                CachePistonColliderPositions();
                RestoreWarningRenderers();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (pushReceiver != null)
                pushReceiver.ClearImpact();
        }
    }

    private void SubscribeToRoundEvents()
    {
        if (roundEventsSubscribed || rounds == null)
            return;

        rounds.StateChanged += OnRoundStateChanged;
        rounds.PistonsActivated += OnPistonsActivated;
        rounds.ChallengeCompleted += OnChallengeCompleted;
        roundEventsSubscribed = true;
    }

    private void UnsubscribeFromRoundEvents()
    {
        if (!roundEventsSubscribed || rounds == null)
        {
            roundEventsSubscribed = false;
            return;
        }

        rounds.StateChanged -= OnRoundStateChanged;
        rounds.PistonsActivated -= OnPistonsActivated;
        rounds.ChallengeCompleted -= OnChallengeCompleted;
        roundEventsSubscribed = false;
    }

    private void ResolveObstacleReferences()
    {
        if (rotatingBar == null)
            rotatingBar = transform.Find("BarraGiratoria");
        barHaste = rotatingBar != null ? rotatingBar.Find("BarraGiratoria_HasteMetalica") : null;

        if (pistonRoots == null || pistonRoots.Length != 3)
            pistonRoots = new Transform[3];

        pistons.Clear();
        for (int index = 0; index < 3; index++)
        {
            Transform root = pistonRoots[index];
            if (root == null)
                root = transform.Find($"PistaoHorizontal_{index + 1:00}");
            if (root == null)
                continue;

            Transform movable = root.Find("Pistao_ConjuntoMovel");
            Transform rod = movable != null ? movable.Find("Pistao_Haste") : root.Find("Pistao_Haste");
            Transform head = movable != null ? movable.Find("Pistao_CabecaImpacto") : root.Find("Pistao_CabecaImpacto");
            if (rod == null || head == null)
                continue;

            Transform lights = root.Find("Pistao_LuzesAdvertencia");
            pistons.Add(new PistonAssembly
            {
                root = root,
                rod = rod,
                impactHead = head,
                warningRenderers = lights != null ? lights.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>(),
                movingColliders = CombineColliders(rod, head),
                rodRestPosition = rod.localPosition,
                headRestPosition = head.localPosition
            });
        }
    }

    private void CacheWarningRenderers()
    {
        warningRendererStates.Clear();
        foreach (PistonAssembly piston in pistons)
        {
            foreach (Renderer renderer in piston.warningRenderers)
            {
                if (renderer == null)
                    continue;
                var originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock);
                warningRendererStates.Add(new WarningRendererState { renderer = renderer, originalBlock = originalBlock });
            }
        }
    }

    private void ApplyPistonPlayerCollisionPolicy()
    {
        Collider[] playerColliders = playerMovement != null
            ? playerMovement.GetComponents<Collider>()
            : Array.Empty<Collider>();
        var movingPistonColliders = new List<Collider>(12);
        foreach (PistonAssembly piston in pistons)
            if (piston != null && piston.movingColliders != null)
                movingPistonColliders.AddRange(piston.movingColliders);

        pistonPlayerCollisionPolicy.Apply(playerColliders, movingPistonColliders.ToArray());
    }

    private bool ConfigurePistonStrokeOffsets(out string error)
    {
        error = null;
        if (pistonStrokeWorldMeters == null || pistonStrokeWorldMeters.Length != 3)
        {
            error = "Informe exatamente três cursos calibrados em metros, na ordem dos pistões 01, 02 e 03.";
            return false;
        }

        if (pistons.Count != 3)
        {
            error = "Não foram resolvidos os três conjuntos de pistão.";
            return false;
        }

        for (int index = 0; index < pistons.Count; index++)
        {
            PistonAssembly piston = pistons[index];
            float strokeMeters = pistonStrokeWorldMeters[index];
            if (float.IsNaN(strokeMeters) || float.IsInfinity(strokeMeters) || strokeMeters <= 0f)
            {
                error = $"Curso inválido para {piston.root.name}.";
                return false;
            }

            Vector3 inward = Vector3.ProjectOnPlane(piston.root.TransformDirection(Vector3.left), Vector3.up).normalized;
            Vector3 rodAxis = Vector3.ProjectOnPlane(piston.rod.parent.TransformVector(Vector3.left), Vector3.up).normalized;
            Vector3 headAxis = Vector3.ProjectOnPlane(piston.impactHead.parent.TransformVector(Vector3.left), Vector3.up).normalized;
            if (Vector3.Dot(inward, rodAxis) < 0.999f || Vector3.Dot(inward, headAxis) < 0.999f)
            {
                error = $"Eixos de movimento desalinhados em {piston.root.name}; não é seguro aplicar o curso calibrado.";
                return false;
            }

            piston.safeStrokeWorldMeters = strokeMeters;
            piston.rodStrokeLocalUnits = strokeMeters / piston.rod.parent.TransformVector(Vector3.left).magnitude;
            piston.headStrokeLocalUnits = strokeMeters / piston.impactHead.parent.TransformVector(Vector3.left).magnitude;
            Debug.Log($"Fase 4: {piston.root.name} curso configurado em {strokeMeters:F3} m (local haste {piston.rodStrokeLocalUnits:F4}; cabeça {piston.headStrokeLocalUnits:F4}).", piston.root);
        }

        return true;
    }

    private void OnRoundStateChanged(Fase4RoundManager.RoundState state)
    {
        if (state == Fase4RoundManager.RoundState.Active)
        {
            // A new active round always starts at home; the preceding five-second transition
            // gives any interrupted 1.5-second retraction time to finish first.
            pistonSchedule.Reset();
            pistonTimeBacklog = 0f;
            ApplyPistonExtension(0f);
            Physics.SyncTransforms();
            ResetContactHistory();
            RestoreWarningRenderers();
            pistonHitThisCycle = false;
            observedCycleIndex = 0;
            activeRound = rounds.CurrentRound;
            barDelayPending = true;
            barSpinning = false;
            barStartTime = Time.time + BarStartDelaySeconds;
            nextBarImpactTime = Time.time;
            return;
        }

        barDelayPending = false;
        barSpinning = false;
        pistonSchedule.Stop();
        pistonTimeBacklog = 0f;
        RestoreWarningRenderers();
        ResetContactHistory();
        if (state == Fase4RoundManager.RoundState.Preparing ||
            state == Fase4RoundManager.RoundState.AwaitingInteraction ||
            state == Fase4RoundManager.RoundState.InterRoundTransition ||
            state == Fase4RoundManager.RoundState.FailureAndRespawn ||
            state == Fase4RoundManager.RoundState.ChallengeCompleted)
        {
            if (state == Fase4RoundManager.RoundState.FailureAndRespawn ||
                state == Fase4RoundManager.RoundState.ChallengeCompleted)
                pushReceiver?.ClearImpact();
        }
    }

    private void OnPistonsActivated()
    {
        if (!initialized || rounds.State != Fase4RoundManager.RoundState.Active ||
            !rounds.PistonsLogicallyActive || pistonSchedule.IsRepeating)
            return;

        try
        {
            ApplyPistonPlayerCollisionPolicy();
            pistonHitThisCycle = false;
            pistonSchedule.Activate();
        }
        catch (Exception exception)
        {
            pistonPlayerCollisionPolicy.Restore();
            Debug.LogException(exception, this);
            enabled = false;
        }
    }

    private void OnChallengeCompleted()
    {
        barDelayPending = false;
        barSpinning = false;
        pistonSchedule.Stop();
        pushReceiver?.ClearImpact();
    }

    private bool UpdateBarRotation(float deltaTime)
    {
        if (barDelayPending)
        {
            if (rounds.State != Fase4RoundManager.RoundState.Active)
            {
                barDelayPending = false;
                return false;
            }

            if (Time.time >= barStartTime)
            {
                barDelayPending = false;
                barSpinning = true;
            }
        }

        if (!barSpinning || rounds.State != Fase4RoundManager.RoundState.Active || deltaTime <= 0f)
            return false;

        accumulatedBarAngle = Mathf.Repeat(
            accumulatedBarAngle + Fase4ObstacleTiming.GetDegreesPerSecond(activeRound) * deltaTime,
            360f);
        rotatingBar.rotation = Quaternion.AngleAxis(accumulatedBarAngle, Vector3.up) * barRestRotation;
        return true;
    }

    private bool ApplyPistonExtension(float normalizedExtension)
    {
        bool changed = false;
        foreach (PistonAssembly piston in pistons)
        {
            if (piston == null || piston.rod == null || piston.impactHead == null)
                continue;

            Vector3 rodPosition = piston.rodRestPosition + Vector3.left * (piston.rodStrokeLocalUnits * Mathf.Clamp01(normalizedExtension));
            Vector3 headPosition = piston.headRestPosition + Vector3.left * (piston.headStrokeLocalUnits * Mathf.Clamp01(normalizedExtension));
            if ((piston.rod.localPosition - rodPosition).sqrMagnitude > 0.000001f)
            {
                piston.rod.localPosition = rodPosition;
                changed = true;
            }
            if ((piston.impactHead.localPosition - headPosition).sqrMagnitude > 0.000001f)
            {
                piston.impactHead.localPosition = headPosition;
                changed = true;
            }
        }
        return changed;
    }

    private void AdvancePistonSchedule(float deltaTime)
    {
        if (!float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime) && deltaTime > 0f)
            pistonTimeBacklog = Mathf.Min(MaxPistonTimeBacklogSeconds, pistonTimeBacklog + deltaTime);

        int substeps = 0;
        while (pistonTimeBacklog > 0.000001f && substeps < MaxPistonSubstepsPerFrame &&
               pistonSchedule.CurrentPhase != Fase4PistonCycleSchedule.Phase.Stopped)
        {
            Fase4PistonCycleSchedule.Phase phase = pistonSchedule.CurrentPhase;
            if (phase == Fase4PistonCycleSchedule.Phase.Holding && pistonSchedule.IsRepeating)
                DetectPistonImpacts();

            bool movingPhase = phase == Fase4PistonCycleSchedule.Phase.Extending ||
                               phase == Fase4PistonCycleSchedule.Phase.Retracting;
            float stepLimit = movingPhase
                ? PistonMotionSubstepSeconds
                : Mathf.Max(0.000001f, pistonSchedule.SecondsUntilPhaseChange);
            float step = Mathf.Min(pistonTimeBacklog, stepLimit);
            if (step <= 0f)
                break;

            int previousCycle = observedCycleIndex;
            pistonSchedule.Advance(step);
            pistonTimeBacklog = Mathf.Max(0f, pistonTimeBacklog - step);
            if (pistonSchedule.CycleIndex != previousCycle)
                OnPistonCycleStarted();

            bool changed = ApplyPistonExtension(pistonSchedule.ExtensionAmount);
            if (changed)
                Physics.SyncTransforms();

            UpdateWarningFlash();
            if (pistonSchedule.IsRepeating &&
                (pistonSchedule.CurrentPhase == Fase4PistonCycleSchedule.Phase.Extending ||
                 pistonSchedule.CurrentPhase == Fase4PistonCycleSchedule.Phase.Holding))
                DetectPistonImpacts();

            CachePistonColliderPositions();
            CapturePlayerCapsuleCenter();
            substeps++;
        }

        if (pistonSchedule.CurrentPhase == Fase4PistonCycleSchedule.Phase.Stopped)
        {
            pistonTimeBacklog = 0f;
            if (pistonPlayerCollisionPolicy.IsApplied)
                pistonPlayerCollisionPolicy.Restore();
        }
    }

    private void DetectBarImpact()
    {
        if (playerController == null || pushReceiver == null || !playerController.enabled)
            return;

        GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out float radius, out Vector3 center, out _);
        float horizontalScale = Mathf.Max(Mathf.Abs(playerController.transform.lossyScale.x),
                                          Mathf.Abs(playerController.transform.lossyScale.z));
        float barQueryRadius = Fase4PistonSweepDetector.GetBarDetectionRadius(
            radius, playerController.skinWidth, horizontalScale);
        int count = Physics.OverlapCapsuleNonAlloc(
            bottom, top, barQueryRadius, playerOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        Collider barContact = null;
        for (int index = 0; index < count; index++)
        {
            Collider candidate = playerOverlapBuffer[index];
            if (candidate != null && candidate.transform != playerController.transform &&
                !candidate.transform.IsChildOf(playerController.transform) &&
                IsChildOfAny(candidate.transform, barImpactColliders))
            {
                barContact = candidate;
                break;
            }
        }

        if (barContact == null || !barSpinning || Time.time < nextBarImpactTime)
            return;

        Vector3 radial = center - rotatingBar.position;
        radial.y = 0f;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
        pushReceiver.ApplyImpact(tangent, BarImpactSpeed, BarImpactDuration);
        nextBarImpactTime = Time.time + BarImpactImmunitySeconds;
        PlayEvent(obstacleImpactEvent, barContact.bounds.ClosestPoint(center));
    }

    private void OnPistonCycleStarted()
    {
        observedCycleIndex = pistonSchedule.CycleIndex;
        pistonHitThisCycle = false;
        warningStartedAt = Time.time;
        warningFlashBright = true;
        SetWarningFlash(true);
        PlayEvent(pistonWarningEvent, transform.position);
    }

    private void UpdateWarningFlash()
    {
        if (pistonSchedule.CurrentPhase != Fase4PistonCycleSchedule.Phase.Warning)
        {
            if (warningOverridesActive)
                RestoreWarningRenderers();
            return;
        }

        bool bright = Mathf.FloorToInt((Time.time - warningStartedAt) / 0.25f) % 2 == 0;
        if (bright == warningFlashBright)
            return;
        warningFlashBright = bright;
        SetWarningFlash(bright);
    }

    private void SetWarningFlash(bool bright)
    {
        warningOverridesActive = true;
        Color color = bright ? new Color(1f, 0.035f, 0.015f, 1f) : new Color(0.12f, 0.008f, 0.004f, 1f);
        foreach (WarningRendererState state in warningRendererStates)
        {
            if (state.renderer == null)
                continue;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", bright ? color * 2.2f : Color.black);
            state.renderer.SetPropertyBlock(block);
        }
    }

    private void RestoreWarningRenderers()
    {
        foreach (WarningRendererState state in warningRendererStates)
            if (state.renderer != null)
                state.renderer.SetPropertyBlock(state.originalBlock);
        warningFlashBright = false;
        warningOverridesActive = false;
    }

    private void DetectPistonImpacts()
    {
        if (!pistonImpactsEnabled || playerController == null || pushReceiver == null || !playerController.enabled)
            return;

        GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out _, out Vector3 center, out float horizontalScale);
        float pistonQueryRadius = Fase4PistonSweepDetector.GetPistonDetectionRadius(
            playerController.radius, horizontalScale, pistonDetectionPaddingMeters);
        Vector3 currentPlayerCenter = center;
        Vector3 playerDelta = hasPreviousPlayerCapsuleCenter
            ? currentPlayerCenter - previousPlayerCapsuleCenter
            : Vector3.zero;
        if (!IsFinite(playerDelta) || playerDelta.magnitude > 5f)
            playerDelta = Vector3.zero;

        Collider pistonContact = null;
        PistonAssembly hitPiston = null;
        float bestPistonContactDistance = float.PositiveInfinity;

        if (pistonSchedule.IsRepeating &&
            (pistonSchedule.CurrentPhase == Fase4PistonCycleSchedule.Phase.Extending ||
             pistonSchedule.CurrentPhase == Fase4PistonCycleSchedule.Phase.Holding))
        {
            foreach (PistonAssembly piston in pistons)
            {
                foreach (Collider movingCollider in piston.movingColliders)
                {
                    if (movingCollider == null || !movingCollider.enabled || movingCollider.isTrigger)
                        continue;

                    Vector3 previousPosition = previousPistonColliderPositions.TryGetValue(movingCollider, out Vector3 cachedPosition)
                        ? cachedPosition
                        : movingCollider.transform.position;
                    Vector3 relativeTranslation = movingCollider.transform.position - previousPosition - playerDelta;
                    if (!Fase4PistonSweepDetector.TryGetContact(
                            bottom, top, pistonQueryRadius, movingCollider, relativeTranslation,
                            playerOverlapBuffer, pistonSweepHitBuffer))
                        continue;

                    Vector3 closestBoundsPoint = movingCollider.bounds.ClosestPoint(center);
                    float distance = (closestBoundsPoint - center).sqrMagnitude;
                    if (distance < bestPistonContactDistance)
                    {
                        bestPistonContactDistance = distance;
                        pistonContact = movingCollider;
                        hitPiston = piston;
                    }
                }
            }
        }

        if (pistonContact != null && hitPiston != null && !pistonHitThisCycle)
        {
            Vector3 inward = hitPiston.root.TransformDirection(Vector3.left).normalized;
            pushReceiver.ApplyImpact(inward, pistonImpactSpeed, pistonImpactDuration);
            pistonHitThisCycle = true;
            PlayEvent(obstacleImpactEvent, pistonContact.bounds.ClosestPoint(center));
        }

        previousPlayerCapsuleCenter = currentPlayerCenter;
        hasPreviousPlayerCapsuleCenter = true;
    }

    private void GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out float radius, out Vector3 center, out float horizontalScale)
    {
        Vector3 scale = playerController.transform.lossyScale;
        horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        radius = playerController.radius * horizontalScale;
        float halfHeight = Mathf.Max(playerController.height * Mathf.Abs(scale.y) * 0.5f, radius);
        float segment = Mathf.Max(0f, halfHeight - radius);
        center = playerController.transform.TransformPoint(playerController.center);
        Vector3 axis = playerController.transform.up * segment;
        bottom = center - axis;
        top = center + axis;
    }

    private void CachePistonColliderPositions()
    {
        previousPistonColliderPositions.Clear();
        foreach (PistonAssembly piston in pistons)
            foreach (Collider movingCollider in piston.movingColliders)
                if (movingCollider != null)
                    previousPistonColliderPositions[movingCollider] = movingCollider.transform.position;
    }

    private void CapturePlayerCapsuleCenter()
    {
        if (playerController == null)
        {
            hasPreviousPlayerCapsuleCenter = false;
            return;
        }

        previousPlayerCapsuleCenter = playerController.transform.TransformPoint(playerController.center);
        hasPreviousPlayerCapsuleCenter = true;
    }

    private void ResetContactHistory()
    {
        CachePistonColliderPositions();
        CapturePlayerCapsuleCenter();
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void PlayEvent(EventReference eventReference, Vector3 position)
    {
        if (!eventReference.IsNull)
            RuntimeManager.PlayOneShot(eventReference, position);
    }

    private void ClearReferences()
    {
        rounds = null;
        playerMovement = null;
        playerController = null;
        pushReceiver = null;
        initialized = false;
        roundEventsSubscribed = false;
        pistonTimeBacklog = 0f;
        hasPreviousPlayerCapsuleCenter = false;
        previousPistonColliderPositions.Clear();
    }

    private static Collider[] CombineColliders(Transform first, Transform second)
    {
        var colliders = new List<Collider>();
        if (first != null) colliders.AddRange(first.GetComponentsInChildren<Collider>(true));
        if (second != null) colliders.AddRange(second.GetComponentsInChildren<Collider>(true));
        return colliders.ToArray();
    }

    private static bool IsChildOfAny(Transform candidate, Collider[] colliders)
    {
        if (candidate == null || colliders == null)
            return false;
        foreach (Collider collider in colliders)
            if (collider != null && (candidate == collider.transform || candidate.IsChildOf(collider.transform)))
                return true;
        return false;
    }
}
