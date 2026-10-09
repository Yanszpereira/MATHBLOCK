using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connects the M3 round manager and expression bank to the six authored M4
/// spawn regions. The M5 receptacle can call RequestAttempt when the player
/// interacts; no scene-wide spawner or gameplay system is replaced here.
/// </summary>
[DisallowMultipleComponent]
public sealed class Fase4ArenaSpawnerController : MonoBehaviour
{
    [SerializeField] private Fase4ExpressionBank expressionBank;
    [SerializeField] private GameObject mathBlockPrefab;
    [SerializeField] private Fase4SpawnRegion[] regions = Array.Empty<Fase4SpawnRegion>();
    [SerializeField, Min(0.05f)] private float popDuration = 0.42f;
    [SerializeField, Min(0f)] private float spawnHeight = 1.4f;
    [SerializeField, Min(0.1f)] private float settleTimeout = 5f;
    [SerializeField] private int randomSeed;

    private Fase4RoundManager roundManager;
    private System.Random random;
    private int previousRegionIndex = -1;
    private int currentRegionIndex = -1;
    private int currentAttemptId;
    private bool spawnInProgress;
    private bool progressionPaused;
    private Coroutine settleRoutine;
    private readonly HashSet<Fase4SpawnedMathBlock> presentationBlocks = new HashSet<Fase4SpawnedMathBlock>();

    public Fase4RoundManager RoundManager => roundManager;
    public int ActiveRegionIndex => currentRegionIndex;
    public Fase4SpawnRegion ActiveRegion => currentRegionIndex >= 0 && regions != null && currentRegionIndex < regions.Length
        ? regions[currentRegionIndex]
        : null;
    public event Action<Fase4SpawnRegion> ActiveRegionChanged;
    public event Action SpawnPreparationCompleted;

    private void Awake()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        random = randomSeed == 0
            ? new System.Random(unchecked(Environment.TickCount ^ GetInstanceID()))
            : new System.Random(randomSeed);
        roundManager = new Fase4RoundManager(new Fase4ExpressionDeck(expressionBank.Expressions, random));
        roundManager.ExpressionSelected += OnExpressionSelected;
        roundManager.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.ExpressionSelected -= OnExpressionSelected;
            roundManager.StateChanged -= OnStateChanged;
        }

        if (settleRoutine != null)
            StopCoroutine(settleRoutine);

        ClearAttemptBlocks(true);
    }

    private void OnEnable()
    {
        if (roundManager != null && roundManager.State == Fase4RoundManager.RoundState.Preparing &&
            roundManager.CurrentExpression != null && !spawnInProgress)
            OnExpressionSelected(roundManager.CurrentExpression);
    }

    private void OnDisable()
    {
        if (roundManager == null || roundManager.State != Fase4RoundManager.RoundState.Preparing)
            return;

        if (settleRoutine != null)
        {
            StopCoroutine(settleRoutine);
            settleRoutine = null;
        }

        spawnInProgress = false;
        ClearAttemptBlocks();
    }

    private void Update()
    {
        if (!progressionPaused && roundManager != null)
            roundManager.Tick(Time.deltaTime);
    }

    /// <summary>Entry point for the future receptacle interaction.</summary>
    public bool RequestAttempt()
    {
        return isActiveAndEnabled && roundManager != null && roundManager.RequestAttempt();
    }

    /// <summary>Called by the future recovery adapter after the player's respawn completes.</summary>
    public bool NotifyRespawnCompleted()
    {
        return roundManager != null && roundManager.NotifyRespawnCompleted();
    }

    public void SetProgressionPaused(bool paused)
    {
        progressionPaused = paused;
        Fase4SpawnedMathBlock[] blocks = FindObjectsByType<Fase4SpawnedMathBlock>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Fase4SpawnedMathBlock block in blocks)
        {
            if (block == null || block.Owner != this)
                continue;
            BlockSpawnPop pop = block.GetComponent<BlockSpawnPop>();
            if (pop != null)
                pop.SetPaused(paused);
        }
    }

    public void RetainBlockForPresentation(Fase4SpawnedMathBlock block)
    {
        if (block != null && block.Owner == this)
            presentationBlocks.Add(block);
    }

    public void ForgetPresentedBlock(Fase4SpawnedMathBlock block)
    {
        if (block != null)
            presentationBlocks.Remove(block);
    }

    public void ClearAttemptBlocks()
    {
        ClearAttemptBlocks(false);
    }

    public bool IsCurrentAttempt(Fase4SpawnedMathBlock block)
    {
        return block != null && block.Owner == this && block.AttemptId == currentAttemptId &&
               roundManager != null && roundManager.State != Fase4RoundManager.RoundState.InterRoundTransition &&
               roundManager.State != Fase4RoundManager.RoundState.FailureAndRespawn &&
               roundManager.State != Fase4RoundManager.RoundState.ChallengeCompleted;
    }

    public void Configure(
        Fase4ExpressionBank bank,
        GameObject prefab,
        Fase4SpawnRegion[] configuredRegions)
    {
        expressionBank = bank;
        mathBlockPrefab = prefab;
        regions = configuredRegions != null ? (Fase4SpawnRegion[])configuredRegions.Clone() : Array.Empty<Fase4SpawnRegion>();
    }

    public void SetRandomSeed(int seed)
    {
        randomSeed = seed;
    }

    private bool ValidateConfiguration()
    {
        if (expressionBank == null || expressionBank.Expressions == null || expressionBank.Expressions.Count != 20)
        {
            Debug.LogError("Fase4ArenaSpawnerController precisa do banco único com as 20 expressões.", this);
            return false;
        }

        if (mathBlockPrefab == null || mathBlockPrefab.GetComponent<MathBlockValue>() == null ||
            mathBlockPrefab.GetComponent<Rigidbody>() == null || mathBlockPrefab.GetComponent<Collider>() == null)
        {
            Debug.LogError("O prefab dos MathBlocks precisa conter MathBlockValue, Rigidbody e Collider.", this);
            return false;
        }

        if (regions == null || regions.Length != 6)
        {
            Debug.LogError("A arena requer exatamente seis regiões de spawn.", this);
            return false;
        }

        var seen = new HashSet<Fase4SpawnRegion>();
        foreach (Fase4SpawnRegion region in regions)
        {
            if (region == null || !region.IsValid || !seen.Add(region))
            {
                Debug.LogError("Cada uma das seis regiões deve ser única e possuir exatamente oito pontos configurados.", this);
                return false;
            }

            foreach (Transform point in region.SpawnPoints)
            {
                if (point == null || point.gameObject.scene != gameObject.scene)
                {
                    Debug.LogError($"Região {region.name} contém ponto inválido ou de outra cena.", region);
                    return false;
                }
            }
        }

        return true;
    }

    private void OnExpressionSelected(Fase4ExpressionDefinition expression)
    {
        if (expression == null || spawnInProgress)
        {
            Debug.LogError("Spawn ignorado: expressão nula ou preparação já em andamento.", this);
            return;
        }

        ClearAttemptBlocks();
        currentRegionIndex = Fase4SpawnSelection.ChooseRegionIndex(regions.Length, previousRegionIndex, random);
        previousRegionIndex = currentRegionIndex;
        currentAttemptId++;
        spawnInProgress = true;
        Fase4SpawnRegion region = regions[currentRegionIndex];
        ActiveRegionChanged?.Invoke(region);

        int[] values = Fase4SpawnSelection.BuildValues(expression, random, out int[] distractors);
        Transform[] points = region.SpawnPoints;
        Shuffle(points, random);
        if (values.Length > points.Length)
        {
            Debug.LogError($"A expressão exige {values.Length} blocos, mas {region.name} possui {points.Length} pontos.", region);
            spawnInProgress = false;
            return;
        }

        var spawned = new List<GameObject>(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            Transform point = points[i];
            GameObject block = Instantiate(
                mathBlockPrefab,
                point.position + Vector3.up * spawnHeight,
                point.rotation);
            block.name = $"Fase4_MathBlock_{currentAttemptId}_{i + 1}_{values[i]}";

            MathBlockValue value = block.GetComponent<MathBlockValue>();
            value.SetValue(values[i]);
            Fase4SpawnedMathBlock ownership = block.AddComponent<Fase4SpawnedMathBlock>();
            ownership.Initialize(this, point, currentAttemptId);

            Rigidbody body = block.GetComponent<Rigidbody>();
            body.detectCollisions = true;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            spawned.Add(block);
            BlockSpawnPop.Play(block, popDuration);
        }

        if (spawned.Count == 0)
        {
            spawnInProgress = false;
            Debug.LogError("Nenhum MathBlock foi instanciado para a expressão selecionada.", this);
            return;
        }

        settleRoutine = StartCoroutine(WaitUntilSpawnSettled(spawned, currentAttemptId));
        Debug.Log($"Spawn da tentativa {currentAttemptId}: {values.Length} blocos ({distractors.Length} distratores) na região {region.name}.", this);
    }

    private IEnumerator WaitUntilSpawnSettled(List<GameObject> blocks, int attemptId)
    {
        float elapsed = 0f;
        float stableFor = 0f;
        while (elapsed < popDuration || HasActiveSpawnPop(blocks))
        {
            if (!progressionPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        while (elapsed < settleTimeout && stableFor < 0.2f)
        {
            while (progressionPaused)
                yield return null;

            bool settled = true;
            foreach (GameObject block in blocks)
            {
                if (block == null) continue;
                Rigidbody body = block.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.01f)
                {
                    settled = false;
                    break;
                }
            }

            stableFor = settled ? stableFor + Time.fixedDeltaTime : 0f;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (attemptId != currentAttemptId || roundManager == null ||
            roundManager.State != Fase4RoundManager.RoundState.Preparing)
            yield break;

        while (progressionPaused)
            yield return null;

        if (stableFor < 0.2f)
            Debug.LogWarning("Tempo limite de assentamento atingido; liberando interação com os MathBlocks.", this);

        spawnInProgress = false;
        SetAttemptBlocksInteraction(attemptId, true);
        bool notified = roundManager.NotifySpawnCompleted();
        if (notified)
        {
            SpawnPreparationCompleted?.Invoke();
        }
        else
            SetAttemptBlocksInteraction(attemptId, false);
        settleRoutine = null;
    }

    private void OnStateChanged(Fase4RoundManager.RoundState state)
    {
        if (state == Fase4RoundManager.RoundState.InterRoundTransition ||
            state == Fase4RoundManager.RoundState.FailureAndRespawn ||
            state == Fase4RoundManager.RoundState.ChallengeCompleted)
        {
            spawnInProgress = false;
            if (settleRoutine != null)
            {
                StopCoroutine(settleRoutine);
                settleRoutine = null;
            }
            if (state != Fase4RoundManager.RoundState.FailureAndRespawn)
                ClearAttemptBlocks();
            currentRegionIndex = -1;
            ActiveRegionChanged?.Invoke(null);
        }
    }

    private void ClearAttemptBlocks(bool destroyRetained = false)
    {
        GravityInteract interaction = FindFirstObjectByType<GravityInteract>();
        if (interaction != null && interaction.IsHoldingObject && interaction.GrabbedObject != null)
        {
            Fase4SpawnedMathBlock held = interaction.GrabbedObject.GetComponent<Fase4SpawnedMathBlock>();
            if (held != null && held.Owner == this)
                interaction.Soltar();
        }

        Fase4SpawnedMathBlock[] owned = FindObjectsByType<Fase4SpawnedMathBlock>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Fase4SpawnedMathBlock block in owned)
        {
            if (block != null && block.Owner == this &&
                (destroyRetained || !presentationBlocks.Contains(block)))
                Destroy(block.gameObject);
        }

        if (destroyRetained)
            presentationBlocks.Clear();
        else
            presentationBlocks.RemoveWhere(block => block == null);
    }

    private void SetAttemptBlocksInteraction(int attemptId, bool available)
    {
        Fase4SpawnedMathBlock[] owned = FindObjectsByType<Fase4SpawnedMathBlock>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Fase4SpawnedMathBlock block in owned)
            if (block != null && block.Owner == this && block.AttemptId == attemptId)
                block.SetInteractionEnabled(available);
    }

    private static bool HasActiveSpawnPop(List<GameObject> blocks)
    {
        foreach (GameObject block in blocks)
        {
            if (block == null)
                continue;
            BlockSpawnPop effect = block.GetComponent<BlockSpawnPop>();
            if (effect != null && effect.IsAnimating)
                return true;
        }
        return false;
    }

    private static void Shuffle(Transform[] values, System.Random source)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = source.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
