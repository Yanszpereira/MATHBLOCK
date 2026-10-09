using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class Fase4ArenaRoundCoordinator : MonoBehaviour
{
    private struct SubmittedBlockState
    {
        public MathBlockValue block;
        public int submittedValue;
    }

    private static readonly Color WaitingBlue = new Color(0.22f, 0.73f, 0.91f);
    private static readonly Color CorrectGreen = new Color(0.24f, 0.92f, 0.42f);
    private static readonly Color IncorrectRed = new Color(0.95f, 0.20f, 0.16f);

    [SerializeField] private Fase4ArenaSpawnerController spawner;
    [SerializeField] private PadMathBlockDetector receptacleDetector;
    [SerializeField] private Transform receptacleRoot;
    [SerializeField] private Fase4BlackboardView blackboard;
    [SerializeField] private Renderer receptacleIndicator;
    [SerializeField] private VoidRespawner voidRespawner;
    [SerializeField] private CountdownTimer globalCountdown;
    [SerializeField, Min(0.05f)] private float releaseSettleDelay = 0.18f;
    [SerializeField, Min(0.1f)] private float releaseSettleTimeout = 1.5f;
    [SerializeField, Min(0.5f)] private float receptacleInteractionDistance = 4f;
    [SerializeField, Min(0.1f)] private float wrongBlockReturnDuration = 0.65f;
    [SerializeField, Min(0.1f)] private float correctBlockDisappearDuration = 0.7f;
    [SerializeField, Min(0f)] private float failureRespawnDelay = 2f;

    private Fase4RoundManager rounds;
    private Fase4ArenaObstacleController obstacleController;
    private GravityInteract interaction;
    private PlayerMovement player;
    private InputAction receptacleInteractAction;
    private Collider[] receptacleColliders = Array.Empty<Collider>();
    private bool? lastWaitingPromptInRange;
    private bool originalGlobalTimerPaused;
    private bool ownsGlobalTimerPause;
    private bool isSubscribedToReceptacleAction;
    private bool insideVoidRespawnStart;
    private bool resumeProgressionAfterRespawn;
    private float transitionDisplayRemaining;
    private Coroutine candidateRoutine;
    private Coroutine failureRoutine;
    private MathBlockValue pendingCandidate;
    private readonly Dictionary<int, SubmittedBlockState> submittedBlocks = new Dictionary<int, SubmittedBlockState>();
    private readonly List<Renderer> indicatorRenderers = new List<Renderer>();
    private readonly List<MaterialPropertyBlock> originalIndicatorBlocks = new List<MaterialPropertyBlock>();
    private MaterialPropertyBlock indicatorPropertyBlock;
    private AudioSource feedbackAudio;
    private AudioClip wrongTone;
    private AudioClip correctTone;

    public event Action PistonsActivationRequested;
    public event Action ObstaclesDeactivationRequested;
    public event Action ElevatorUnlockRequested;

    private void Awake()
    {
        ResolveReferences();
        CacheIndicatorState();
        indicatorPropertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        rounds = spawner.RoundManager;
        if (rounds == null)
        {
            Debug.LogError("Fase4ArenaRoundCoordinator não recebeu uma máquina de rodadas válida do spawner.", this);
            enabled = false;
            return;
        }

        interaction = FindFirstObjectByType<GravityInteract>();
        player = FindFirstObjectByType<PlayerMovement>();
        if (interaction == null || player == null)
        {
            Debug.LogError("Fase 4 requer GravityInteract e PlayerMovement ativos para iniciar e recuperar a rodada.", this);
            enabled = false;
            return;
        }

        if (obstacleController == null || !obstacleController.Initialize(rounds, player))
        {
            Debug.LogError("Integração M6 incompleta: configure Fase4ArenaObstacleController em Arena_Obstaculos.", this);
            enabled = false;
            return;
        }

        PlayerInput playerInput = interaction.GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            receptacleInteractAction = FindReceptacleInteractAction(playerInput.actions);
            if (receptacleInteractAction != null)
                SubscribeToReceptacleAction();
        }

        if (receptacleInteractAction == null)
            Debug.LogWarning("Fase 4: a ação Player/Operators (E) não foi encontrada; o início por teclado ficará indisponível.", this);

        receptacleColliders = receptacleRoot.GetComponentsInChildren<Collider>(true);

        rounds.StateChanged += OnRoundStateChanged;
        rounds.ExpressionSelected += OnExpressionSelected;
        rounds.TimeChanged += OnTimeChanged;
        rounds.WrongAnswerRecorded += OnWrongAnswerRecorded;
        rounds.PistonsActivated += OnPistonsActivated;
        rounds.ChallengeCompleted += OnChallengeCompleted;
        spawner.SpawnPreparationCompleted += OnSpawnPreparationCompleted;
        receptacleDetector.CurrentValueChanged += OnReceptacleContentsChanged;
        interaction.InteractionRequested += OnInteractionRequested;
        voidRespawner.PlayerRespawnStarted += OnPlayerRespawnStarted;
        voidRespawner.PlayerRespawnCompleted += OnPlayerRespawnCompleted;

        originalGlobalTimerPaused = globalCountdown.IsPaused;
        globalCountdown.SetPaused(true);
        ownsGlobalTimerPause = true;
        EnsureFeedbackAudio();
        blackboard.SetRound(rounds.CurrentRound, Fase4RoundManager.TotalRounds);
        blackboard.SetExpression(string.Empty);
        blackboard.SetTimer(Fase4RoundManager.InitialRoundSeconds);
        blackboard.SetErrors(0);
        lastWaitingPromptInRange = null;
        RefreshWaitingPrompt();
        SetIndicatorColor(WaitingBlue);
    }

    private void OnDestroy()
    {
        if (rounds != null)
        {
            rounds.StateChanged -= OnRoundStateChanged;
            rounds.ExpressionSelected -= OnExpressionSelected;
            rounds.TimeChanged -= OnTimeChanged;
            rounds.WrongAnswerRecorded -= OnWrongAnswerRecorded;
            rounds.PistonsActivated -= OnPistonsActivated;
            rounds.ChallengeCompleted -= OnChallengeCompleted;
        }

        if (spawner != null)
            spawner.SpawnPreparationCompleted -= OnSpawnPreparationCompleted;
        if (receptacleDetector != null)
            receptacleDetector.CurrentValueChanged -= OnReceptacleContentsChanged;
        if (interaction != null)
            interaction.InteractionRequested -= OnInteractionRequested;
        UnsubscribeFromReceptacleAction();
        if (voidRespawner != null)
        {
            voidRespawner.PlayerRespawnStarted -= OnPlayerRespawnStarted;
            voidRespawner.PlayerRespawnCompleted -= OnPlayerRespawnCompleted;
        }

        if (candidateRoutine != null) StopCoroutine(candidateRoutine);
        if (failureRoutine != null) StopCoroutine(failureRoutine);
        RestoreIndicatorState();
        if (ownsGlobalTimerPause && globalCountdown != null)
            globalCountdown.SetPaused(originalGlobalTimerPaused);
        if (wrongTone != null) Destroy(wrongTone);
        if (correctTone != null) Destroy(correctTone);
        if (obstacleController != null)
            obstacleController.Shutdown();
    }

    private void OnEnable()
    {
        SubscribeToReceptacleAction();
    }

    private void OnDisable()
    {
        UnsubscribeFromReceptacleAction();
    }

    private void SubscribeToReceptacleAction()
    {
        if (!isActiveAndEnabled || isSubscribedToReceptacleAction || receptacleInteractAction == null)
            return;

        receptacleInteractAction.performed += OnReceptacleInteractPerformed;
        isSubscribedToReceptacleAction = true;
    }

    private void UnsubscribeFromReceptacleAction()
    {
        if (!isSubscribedToReceptacleAction || receptacleInteractAction == null)
            return;

        receptacleInteractAction.performed -= OnReceptacleInteractPerformed;
        isSubscribedToReceptacleAction = false;
    }

    private void Update()
    {
        if (rounds == null)
            return;

        if (rounds.State == Fase4RoundManager.RoundState.AwaitingInteraction)
        {
            RefreshWaitingPrompt();
            return;
        }

        if (rounds.State == Fase4RoundManager.RoundState.Active)
            blackboard.SetTimer(rounds.RemainingSeconds);

        if (rounds.State == Fase4RoundManager.RoundState.InterRoundTransition && !resumeProgressionAfterRespawn)
        {
            transitionDisplayRemaining = Mathf.Max(0f, transitionDisplayRemaining - Time.deltaTime);
            blackboard.SetTransitionCountdown(Mathf.Max(1, Mathf.CeilToInt(transitionDisplayRemaining)));
        }
    }

    private void FixedUpdate()
    {
        if (rounds == null || receptacleDetector == null)
            return;

        ReleaseFinishedSubmissionLocks();
        if (rounds.State != Fase4RoundManager.RoundState.Active || resumeProgressionAfterRespawn ||
            receptacleDetector.HasMultipleBlocks || !receptacleDetector.TryGetCurrentBlock(out MathBlockValue block) ||
            !IsEligibleCandidate(block))
        {
            CancelCandidate();
            return;
        }

        if (pendingCandidate == block || submittedBlocks.ContainsKey(block.GetInstanceID()))
            return;

        CancelCandidate();
        pendingCandidate = block;
        candidateRoutine = StartCoroutine(WaitForReleasedBlock(block));
    }

    private void ResolveReferences()
    {
        if (spawner == null)
            spawner = GetComponent<Fase4ArenaSpawnerController>();

        Transform arenaRoot = transform.root.Find("Arena_Root");
        if (arenaRoot == null)
            arenaRoot = transform.root;
        Transform obstacleRoot = arenaRoot.Find("Arena_Obstaculos");
        if (obstacleController == null && obstacleRoot != null)
            obstacleController = obstacleRoot.GetComponent<Fase4ArenaObstacleController>();
        if (receptacleRoot == null)
        {
            Transform candidate = arenaRoot.Find("Arena_Centro/ReceptaculoCentral");
            if (candidate != null) receptacleRoot = candidate;
        }
        if (receptacleDetector == null && receptacleRoot != null)
        {
            Transform surface = receptacleRoot.Find("Receptaculo_AreaValidacao");
            if (surface != null) receptacleDetector = surface.GetComponent<PadMathBlockDetector>();
        }
        if (blackboard == null)
        {
            Transform candidate = arenaRoot.Find("Arena_Centro/QuadroNegro");
            if (candidate != null) blackboard = candidate.GetComponent<Fase4BlackboardView>();
        }
        if (voidRespawner == null)
        {
            Transform candidate = arenaRoot.Find("Arena_Void");
            if (candidate != null) voidRespawner = candidate.GetComponent<VoidRespawner>();
        }
        if (globalCountdown == null)
        {
            CountdownTimer[] timers = FindObjectsByType<CountdownTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (CountdownTimer timer in timers)
                if (timer.gameObject.scene == gameObject.scene)
                {
                    globalCountdown = timer;
                    break;
                }
        }
        if (receptacleIndicator == null && receptacleRoot != null)
        {
            Transform candidate = receptacleRoot.Find("Receptaculo_ContornoLuminoso");
            if (candidate != null) receptacleIndicator = candidate.GetComponent<Renderer>();
        }
    }

    private bool ValidateReferences()
    {
        bool valid = spawner != null && receptacleDetector != null && receptacleRoot != null &&
                     blackboard != null && voidRespawner != null && globalCountdown != null && receptacleIndicator != null;
        if (!valid)
            Debug.LogError("Integração M5 incompleta: verifique spawner, detector do receptáculo, quadro, Void, cronômetro global e contorno luminoso.", this);
        return valid;
    }

    private void OnInteractionRequested()
    {
        TryStartFromInteraction();
    }

    private void OnReceptacleInteractPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryStartFromInteraction();
    }

    internal static InputAction FindReceptacleInteractAction(InputActionAsset actions)
    {
        if (actions == null)
            return null;

        return actions.FindAction("Player/Operators", false)
            ?? actions.FindAction("Operators", false);
    }

    private void OnReceptacleContentsChanged(PadMathBlockDetector detector, int? value)
    {
        // The detector is shared with legacy door pads. Subscribing here makes
        // this receptor an explicit consumer and cancels a pending submission
        // as soon as its contents become empty or ambiguous.
        if (detector == null || detector.HasMultipleBlocks || !detector.TryGetCurrentBlock(out _))
            CancelCandidate();
    }

    /// <summary>Entry point for the existing player interaction and future UI adapters.</summary>
    public bool TryStartFromInteraction()
    {
        if (!isActiveAndEnabled || rounds == null || rounds.State != Fase4RoundManager.RoundState.AwaitingInteraction ||
            interaction == null || interaction.IsHoldingObject || !IsPlayerWithinReceptacleRange())
            return false;

        return spawner.RequestAttempt();
    }

    private bool IsPlayerWithinReceptacleRange()
    {
        if (player == null || receptacleRoot == null)
            return false;

        Vector3 playerPosition = player.transform.position;
        float nearestDistanceSquared = float.PositiveInfinity;
        foreach (Collider collider in receptacleColliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                continue;

            Vector3 closestPoint = collider.ClosestPoint(playerPosition);
            nearestDistanceSquared = Mathf.Min(nearestDistanceSquared, (closestPoint - playerPosition).sqrMagnitude);
        }

        if (float.IsPositiveInfinity(nearestDistanceSquared))
            nearestDistanceSquared = (receptacleRoot.position - playerPosition).sqrMagnitude;

        return nearestDistanceSquared <= receptacleInteractionDistance * receptacleInteractionDistance;
    }

    private void RefreshWaitingPrompt()
    {
        bool inRange = interaction != null && !interaction.IsHoldingObject && IsPlayerWithinReceptacleRange();
        if (lastWaitingPromptInRange == inRange)
            return;

        lastWaitingPromptInRange = inRange;
        blackboard.SetStatus(inRange ? "PRESSIONE E PARA COMEÇAR" : "APROXIME-SE DO RECEPTÁCULO");
    }

    private bool IsEligibleCandidate(MathBlockValue block)
    {
        if (block == null || interaction == null || interaction.IsHoldingObject && interaction.GrabbedObject == block.transform)
            return false;

        Fase4SpawnedMathBlock spawned = block.GetComponent<Fase4SpawnedMathBlock>();
        if (spawned == null || !spawned.InteractionEnabled || !spawner.IsCurrentAttempt(spawned))
            return false;

        Rigidbody body = block.GetComponent<Rigidbody>();
        return body != null && !body.isKinematic;
    }

    private IEnumerator WaitForReleasedBlock(MathBlockValue block)
    {
        yield return new WaitForSeconds(releaseSettleDelay);
        Rigidbody body = block != null ? block.GetComponent<Rigidbody>() : null;
        float elapsed = 0f;
        float settledFor = 0f;
        const float RequiredSettledDuration = 0.12f;
        const float MaximumSettledSpeed = 1.5f;
        const float MaximumSettledAngularSpeed = 1.5f;
        while (body != null && elapsed < releaseSettleTimeout && settledFor < RequiredSettledDuration)
        {
            if (rounds.State != Fase4RoundManager.RoundState.Active || receptacleDetector.HasMultipleBlocks ||
                !receptacleDetector.TryGetCurrentBlock(out MathBlockValue onlyBlock) || onlyBlock != block ||
                !IsEligibleCandidate(block))
                break;

            bool isSettled = body.linearVelocity.sqrMagnitude <= MaximumSettledSpeed * MaximumSettledSpeed &&
                             body.angularVelocity.sqrMagnitude <= MaximumSettledAngularSpeed * MaximumSettledAngularSpeed;
            settledFor = isSettled
                ? settledFor + Time.fixedDeltaTime
                : 0f;
            if (settledFor >= RequiredSettledDuration)
                break;

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        if (block == null || body == null || settledFor < RequiredSettledDuration ||
            rounds.State != Fase4RoundManager.RoundState.Active || receptacleDetector.HasMultipleBlocks ||
            !receptacleDetector.TryGetCurrentBlock(out MathBlockValue settledBlock) || settledBlock != block ||
            !IsEligibleCandidate(block))
        {
            pendingCandidate = null;
            candidateRoutine = null;
            yield break;
        }

        int instanceId = block.GetInstanceID();
        submittedBlocks[instanceId] = new SubmittedBlockState { block = block, submittedValue = block.CurrentValue };
        pendingCandidate = null;
        candidateRoutine = null;

        Fase4ExpressionDefinition expression = rounds.CurrentExpression;
        if (!Fase4ExpressionEvaluator.TryEvaluate(expression, out int expectedValue))
        {
            submittedBlocks.Remove(instanceId);
            Debug.LogError("A expressão ativa não pôde ser avaliada; a resposta do receptáculo foi recusada.", this);
            yield break;
        }

        bool developerBypass = DeveloperModeController.IsDeveloperModeActive;
        bool correct = developerBypass || block.CurrentValue == expectedValue;
        if (correct)
        {
            Fase4SpawnedMathBlock ownership = block.GetComponent<Fase4SpawnedMathBlock>();
            spawner.RetainBlockForPresentation(ownership);
            FreezeBlock(block);
            SetBlockHighlight(block, CorrectGreen, 1f);
            // Keep the normal round-manager success path and bypass only the
            // value comparison for a valid, released current-attempt block.
            int valueForRoundManager = developerBypass ? expectedValue : block.CurrentValue;
            bool submitted = rounds.SubmitAnswer(valueForRoundManager);
            if (!submitted)
            {
                spawner.ForgetPresentedBlock(ownership);
                submittedBlocks.Remove(instanceId);
                yield break;
            }
            PlayFeedbackTone(correctTone);
            StartCoroutine(DisappearCorrectBlock(block, ownership));
        }
        else
        {
            bool submitted = rounds.SubmitAnswer(block.CurrentValue);
            if (submitted)
                StartCoroutine(ReturnRejectedBlock(block));
            else
                submittedBlocks.Remove(instanceId);
        }
    }

    private void OnExpressionSelected(Fase4ExpressionDefinition expression)
    {
        if (expression == null)
            return;

        ClearSubmissionLocks();
        blackboard.SetExpression(expression.BuildDisplayText());
        blackboard.SetRound(rounds.CurrentRound, Fase4RoundManager.TotalRounds);
        blackboard.SetTimer(rounds.RemainingSeconds);
        blackboard.SetErrors(0);
        blackboard.SetStatus(string.Empty);
        blackboard.SetTransitionCountdown(null);
        SetIndicatorColor(WaitingBlue);
    }

    private void OnRoundStateChanged(Fase4RoundManager.RoundState state)
    {
        switch (state)
        {
            case Fase4RoundManager.RoundState.Preparing:
                blackboard.SetRound(rounds.CurrentRound, Fase4RoundManager.TotalRounds);
                blackboard.SetErrors(0);
                blackboard.SetStatus("PREPARANDO...");
                break;
            case Fase4RoundManager.RoundState.Active:
                blackboard.SetTimer(rounds.RemainingSeconds);
                blackboard.SetStatus(string.Empty);
                SetIndicatorColor(WaitingBlue);
                break;
            case Fase4RoundManager.RoundState.InterRoundTransition:
                CancelCandidate();
                transitionDisplayRemaining = Fase4RoundManager.InterRoundTransitionSeconds;
                blackboard.SetExpression(string.Empty);
                blackboard.SetStatus("CORRETO!");
                blackboard.SetTransitionCountdown(5);
                SetIndicatorColor(CorrectGreen);
                blackboard.ShowTimeChange("+35s", CorrectGreen);
                PlayFeedbackTone(correctTone);
                break;
            case Fase4RoundManager.RoundState.FailureAndRespawn:
                CancelCandidate();
                blackboard.SetExpression(string.Empty);
                blackboard.SetTransitionCountdown(null);
                if (insideVoidRespawnStart)
                {
                    blackboard.SetStatus("RETORNANDO AO CHECKPOINT...");
                }
                else if (rounds.ErrorsThisRound >= 3)
                {
                    blackboard.SetStatus("TENTATIVA ENCERRADA!");
                }
                else
                {
                    blackboard.SetStatus("TEMPO ESGOTADO!");
                }
                SetIndicatorColor(IncorrectRed);
                if (!insideVoidRespawnStart && failureRoutine == null)
                    failureRoutine = StartCoroutine(RespawnAfterFailureDelay());
                break;
            case Fase4RoundManager.RoundState.AwaitingInteraction:
                ShowWaitingPrompt();
                break;
            case Fase4RoundManager.RoundState.ChallengeCompleted:
                CancelCandidate();
                blackboard.SetExpression(string.Empty);
                blackboard.SetTransitionCountdown(null);
                blackboard.SetErrors(0);
                blackboard.SetStatus("DESAFIO MATEMÁTICO CONCLUÍDO!");
                SetIndicatorColor(CorrectGreen);
                break;
        }
    }

    private void OnTimeChanged(float seconds)
    {
        blackboard.SetTimer(seconds);
    }

    private void OnWrongAnswerRecorded(int errorCount)
    {
        blackboard.SetErrors(errorCount);
        blackboard.SetStatus("INCORRETO!");
        SetIndicatorColor(IncorrectRed);
        PlayFeedbackTone(wrongTone);
        if (errorCount == 1)
            blackboard.ShowTimeChange("−5s", IncorrectRed);
    }

    private void OnPistonsActivated()
    {
        PistonsActivationRequested?.Invoke();
    }

    private void OnChallengeCompleted()
    {
        spawner.ClearAttemptBlocks();
        ObstaclesDeactivationRequested?.Invoke();
        ElevatorUnlockRequested?.Invoke();
    }

    private void OnSpawnPreparationCompleted()
    {
        if (rounds.State == Fase4RoundManager.RoundState.Active)
            blackboard.SetStatus(string.Empty);
    }

    private void OnPlayerRespawnStarted(PlayerMovement respawningPlayer)
    {
        obstacleController?.NotifyPlayerRespawnStarted();
        if (rounds == null)
            return;

        Fase4RoundManager.RoundState state = rounds.State;
        if (state == Fase4RoundManager.RoundState.Active)
        {
            insideVoidRespawnStart = true;
            rounds.AbortAttemptWithoutPenalty();
            insideVoidRespawnStart = false;
            CancelCandidate();
            spawner.ClearAttemptBlocks();
        }
        else if (state == Fase4RoundManager.RoundState.Preparing || state == Fase4RoundManager.RoundState.InterRoundTransition)
        {
            resumeProgressionAfterRespawn = true;
            spawner.SetProgressionPaused(true);
        }
        else if (state == Fase4RoundManager.RoundState.FailureAndRespawn)
        {
            CancelCandidate();
            spawner.ClearAttemptBlocks();
        }
    }

    private void OnPlayerRespawnCompleted(PlayerMovement respawnedPlayer)
    {
        if (rounds == null)
            return;

        if (rounds.State == Fase4RoundManager.RoundState.FailureAndRespawn)
        {
            if (failureRoutine != null)
            {
                StopCoroutine(failureRoutine);
                failureRoutine = null;
            }
            spawner.NotifyRespawnCompleted();
            ShowWaitingPrompt();
        }

        if (resumeProgressionAfterRespawn)
        {
            resumeProgressionAfterRespawn = false;
            spawner.SetProgressionPaused(false);
        }
    }

    private IEnumerator RespawnAfterFailureDelay()
    {
        yield return new WaitForSecondsRealtime(failureRespawnDelay);
        // Keep retrying while this failed attempt is still active. A temporary
        // transition/cooldown must not strand the round manager in FailureAndRespawn.
        while (rounds != null && rounds.State == Fase4RoundManager.RoundState.FailureAndRespawn)
        {
            if (voidRespawner.RequestPlayerRespawn(player))
                yield break;
            yield return new WaitForSecondsRealtime(0.25f);
        }
        failureRoutine = null;
    }

    private void ShowWaitingPrompt()
    {
        CancelCandidate();
        ClearSubmissionLocks();
        blackboard.SetExpression(string.Empty);
        blackboard.SetRound(rounds.CurrentRound, Fase4RoundManager.TotalRounds);
        blackboard.SetTimer(Fase4RoundManager.InitialRoundSeconds);
        blackboard.SetErrors(0);
        lastWaitingPromptInRange = null;
        RefreshWaitingPrompt();
        blackboard.SetTransitionCountdown(null);
        SetIndicatorColor(WaitingBlue);
    }

    private void CancelCandidate()
    {
        if (candidateRoutine != null)
            StopCoroutine(candidateRoutine);
        candidateRoutine = null;
        pendingCandidate = null;
    }

    private void ReleaseFinishedSubmissionLocks()
    {
        List<int> remove = null;
        foreach (KeyValuePair<int, SubmittedBlockState> entry in submittedBlocks)
        {
            if (entry.Value.block != null && entry.Value.block.CurrentValue == entry.Value.submittedValue &&
                receptacleDetector.IsBlockCurrentlyDetected(entry.Value.block))
                continue;
            remove ??= new List<int>();
            remove.Add(entry.Key);
        }

        if (remove != null)
            foreach (int id in remove)
                submittedBlocks.Remove(id);
    }

    private void ClearSubmissionLocks()
    {
        submittedBlocks.Clear();
        CancelCandidate();
    }

    private IEnumerator ReturnRejectedBlock(MathBlockValue block)
    {
        if (block == null)
            yield break;

        Rigidbody body = block.GetComponent<Rigidbody>();
        if (body == null)
            yield break;

        Transform blockTransform = block.transform;
        Vector3 start = blockTransform.position;
        Transform cameraTransform = interaction.interactionCamera != null
            ? interaction.interactionCamera
            : Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform == null)
            yield break;
        Vector3 target = cameraTransform.position + cameraTransform.forward * 2.6f - cameraTransform.up * 0.2f;
        Vector3 originalScale = blockTransform.localScale;
        Collider[] colliders = block.GetComponentsInChildren<Collider>(true);
        bool[] colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;

        float elapsed = 0f;
        while (elapsed < wrongBlockReturnDuration && block != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / wrongBlockReturnDuration);
            Vector3 position = Vector3.Lerp(start, target, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.55f;
            body.position = position;
            body.rotation = blockTransform.rotation;
            blockTransform.localScale = originalScale * Mathf.Lerp(1f, 0.92f, t);
            yield return null;
        }

        if (block == null)
            yield break;

        blockTransform.localScale = originalScale;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        body.useGravity = true;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    private IEnumerator DisappearCorrectBlock(MathBlockValue block, Fase4SpawnedMathBlock ownership)
    {
        if (block == null)
            yield break;

        Transform blockTransform = block.transform;
        Vector3 originalScale = blockTransform.localScale;
        float elapsed = 0f;
        while (elapsed < correctBlockDisappearDuration && block != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / correctBlockDisappearDuration);
            float smooth = t * t * (3f - 2f * t);
            blockTransform.localScale = originalScale * (1f - smooth);
            SetBlockHighlight(block, Color.Lerp(CorrectGreen, Color.clear, smooth), 1f - smooth);
            yield return null;
        }

        if (block != null)
            Destroy(block.gameObject);
        spawner.ForgetPresentedBlock(ownership);
    }

    private void FreezeBlock(MathBlockValue block)
    {
        Rigidbody body = block.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        foreach (Collider collider in block.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    private static void SetBlockHighlight(MathBlockValue block, Color color, float intensity)
    {
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        foreach (Renderer renderer in block.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            renderer.SetPropertyBlock(properties);
            properties.Clear();
        }
    }

    private void CacheIndicatorState()
    {
        if (receptacleIndicator == null)
            return;
        indicatorRenderers.Add(receptacleIndicator);
        var original = new MaterialPropertyBlock();
        receptacleIndicator.GetPropertyBlock(original);
        originalIndicatorBlocks.Add(original);
    }

    private void SetIndicatorColor(Color color)
    {
        if (receptacleIndicator == null)
            return;
        receptacleIndicator.GetPropertyBlock(indicatorPropertyBlock);
        indicatorPropertyBlock.SetColor("_Color", color);
        indicatorPropertyBlock.SetColor("_BaseColor", color);
        indicatorPropertyBlock.SetColor("_EmissionColor", color * 0.7f);
        receptacleIndicator.SetPropertyBlock(indicatorPropertyBlock);
    }

    private void RestoreIndicatorState()
    {
        for (int i = 0; i < indicatorRenderers.Count; i++)
            if (indicatorRenderers[i] != null)
                indicatorRenderers[i].SetPropertyBlock(originalIndicatorBlocks[i]);
    }

    private void EnsureFeedbackAudio()
    {
        feedbackAudio = GetComponent<AudioSource>();
        if (feedbackAudio == null)
            feedbackAudio = gameObject.AddComponent<AudioSource>();
        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0f;
        wrongTone = CreateTone("Fase4_WrongAnswer", 520f, 330f, 0.22f);
        correctTone = CreateTone("Fase4_CorrectAnswer", 620f, 880f, 0.26f);
    }

    private static AudioClip CreateTone(string clipName, float startFrequency, float endFrequency, float duration)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float blend = t / duration;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, blend);
            float envelope = Mathf.Sin(Mathf.PI * blend) * 0.15f;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
        }
        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlayFeedbackTone(AudioClip clip)
    {
        if (feedbackAudio != null && clip != null)
            feedbackAudio.PlayOneShot(clip);
    }
}
