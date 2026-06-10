using System.Collections.Generic;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ProceduralMathBlockSpawner : MonoBehaviour
{
    private const int CandidateAttemptCount = 100;

    [Header("Door Range")]
    [SerializeField] private int possibleDoorMinValue = 20;
    [SerializeField] private int possibleDoorMaxValue = 50;

    [Header("Generation")]
    [SerializeField, Range(1, 3)] private int generationMode = 2;
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private DoorValueVerifier doorVerifier;
    [SerializeField] private PadMathBlockDetector padVerifier;
    [SerializeField] private bool generateOnStart = true;

    [Header("Spawn Area")]
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(6f, 0f, 6f);
    [SerializeField] private float spawnHeightOffset = 0.75f;
    [SerializeField] private Color gizmoColor = new Color(0.1f, 0.8f, 1f, 0.25f);

    private readonly List<GameObject> spawnedBlocks = new List<GameObject>();

    private void Reset()
    {
        ConfigureSpawnCollider();
    }

    private void OnValidate()
    {
        possibleDoorMinValue = Mathf.Max(0, possibleDoorMinValue);
        possibleDoorMaxValue = Mathf.Max(possibleDoorMinValue, possibleDoorMaxValue);
        generationMode = Mathf.Clamp(generationMode, 1, 3);
        spawnAreaSize.x = Mathf.Max(0.1f, spawnAreaSize.x);
        spawnAreaSize.y = Mathf.Max(0f, spawnAreaSize.y);
        spawnAreaSize.z = Mathf.Max(0.1f, spawnAreaSize.z);
        spawnHeightOffset = Mathf.Max(0f, spawnHeightOffset);
        ConfigureSpawnCollider();
    }

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Math Blocks")]
    public void Generate()
    {
        if (!ValidateSettings())
            return;

        ClearSpawnedBlocks();

        int finalDoorValue = DrawFinalDoorValue();
        doorVerifier.SetRequiredRange(finalDoorValue, finalDoorValue);

        GenerationSettings settings = GetGenerationSettings(finalDoorValue);
        int solutionTarget = finalDoorValue;
        int solutionBlockCount = DetermineSolutionBlockCount(solutionTarget, settings);
        int solutionQualityScore;
        List<int> solutionValues = GenerateBestSolution(
            solutionTarget,
            solutionBlockCount,
            settings.MinBlockValue,
            settings.MaxBlockValue,
            out solutionQualityScore
        );
        List<int> allValues = new List<int>(solutionValues);
        List<int> extraValues = new List<int>();

        int extraCount = Random.Range(settings.MinExtraBlocks, settings.MaxExtraBlocks + 1);
        for (int i = 0; i < extraCount; i++)
        {
            int extraValue = Random.Range(settings.MinBlockValue, settings.MaxBlockValue + 1);
            extraValues.Add(extraValue);
            allValues.Add(extraValue);
        }

        Shuffle(allValues);
        SpawnBlocks(allValues);
        LogGeneration(finalDoorValue, solutionTarget, solutionValues, solutionQualityScore, extraValues, allValues, settings);
    }

    private bool ValidateSettings()
    {
        if (possibleDoorMinValue > possibleDoorMaxValue)
        {
            Debug.LogError($"{name}: valor minimo possivel da porta nao pode ser maior que o maximo.");
            return false;
        }

        if (generationMode < 1 || generationMode > 3)
        {
            Debug.LogError($"{name}: modo de geracao deve ser 1, 2 ou 3.");
            return false;
        }

        if (blockPrefab == null)
        {
            Debug.LogError($"{name}: prefab do bloco nao foi configurado.");
            return false;
        }

        if (doorVerifier == null)
        {
            Debug.LogError($"{name}: referencia para DoorValueVerifier nao foi configurada.");
            return false;
        }

        if (blockPrefab.GetComponent<MathBlockValue>() == null)
        {
            Debug.LogError($"{name}: prefab do bloco precisa ter MathBlockValue.");
            return false;
        }

        return true;
    }

    private int DrawFinalDoorValue()
    {
        return Random.Range(possibleDoorMinValue, possibleDoorMaxValue + 1);
    }

    private GenerationSettings GetGenerationSettings(int finalDoorValue)
    {
        int targetCeiling = Mathf.Max(1, finalDoorValue);
        GenerationSettings settings = new GenerationSettings();

        switch (generationMode)
        {
            case 1:
                settings.MinSolutionBlocks = 5;
                settings.MaxSolutionBlocks = 8;
                settings.MinExtraBlocks = 3;
                settings.MaxExtraBlocks = 6;
                settings.MinBlockValue = 1;
                settings.MaxBlockValue = Mathf.Clamp(Mathf.CeilToInt(targetCeiling * 0.3f), 5, 20);
                break;

            case 3:
                settings.MinSolutionBlocks = 2;
                settings.MaxSolutionBlocks = 4;
                settings.MinExtraBlocks = 0;
                settings.MaxExtraBlocks = 2;
                settings.MinBlockValue = 1;
                settings.MaxBlockValue = Mathf.Clamp(Mathf.CeilToInt(targetCeiling * 0.6f), 8, 40);
                break;

            default:
                settings.MinSolutionBlocks = 3;
                settings.MaxSolutionBlocks = 5;
                settings.MinExtraBlocks = 1;
                settings.MaxExtraBlocks = 4;
                settings.MinBlockValue = 1;
                settings.MaxBlockValue = Mathf.Clamp(Mathf.CeilToInt(targetCeiling * 0.45f), 6, 30);
                break;
        }

        return settings;
    }

    private int DetermineSolutionBlockCount(int targetValue, GenerationSettings settings)
    {
        if (targetValue <= 0)
            return 1;

        int preferredBlockCount = Random.Range(settings.MinSolutionBlocks, settings.MaxSolutionBlocks + 1);
        int maxUsefulBlockCount = Mathf.Max(1, targetValue / Mathf.Max(1, settings.MinBlockValue));
        int solutionBlockCount = Mathf.Min(preferredBlockCount, maxUsefulBlockCount);
        while (solutionBlockCount * settings.MaxBlockValue < targetValue)
        {
            solutionBlockCount++;
        }

        return solutionBlockCount;
    }

    private List<int> GenerateBestSolution(
        int targetValue,
        int blockCount,
        int minValue,
        int maxValue,
        out int bestScore)
    {
        List<int> bestSolution = null;
        bestScore = int.MinValue;

        for (int attempt = 0; attempt < CandidateAttemptCount; attempt++)
        {
            List<int> candidate = GenerateCandidateSolution(targetValue, blockCount, minValue, maxValue);
            if (!IsValidSolution(candidate, targetValue, minValue, maxValue))
                continue;

            int score = EvaluateSolutionQuality(candidate);
            if (bestSolution == null || score > bestScore)
            {
                bestSolution = candidate;
                bestScore = score;
            }
        }

        if (bestSolution != null)
            return bestSolution;

        bestSolution = GenerateFallbackSolution(targetValue, blockCount, minValue, maxValue);
        bestScore = EvaluateSolutionQuality(bestSolution);
        return bestSolution;
    }

    private List<int> GenerateCandidateSolution(int targetValue, int blockCount, int minValue, int maxValue)
    {
        List<int> values = new List<int>();
        if (targetValue <= 0)
        {
            values.Add(0);
            return values;
        }

        if (blockCount <= 0 || targetValue < blockCount * minValue || targetValue > blockCount * maxValue)
            return values;

        int remaining = targetValue;
        for (int i = 0; i < blockCount; i++)
        {
            int slotsLeft = blockCount - i;
            int minAllowed = Mathf.Max(minValue, remaining - ((slotsLeft - 1) * maxValue));
            int maxAllowed = Mathf.Min(maxValue, remaining - ((slotsLeft - 1) * minValue));
            int nextValue = slotsLeft == 1
                ? remaining
                : DrawCandidateValue(targetValue, i, minAllowed, maxAllowed);

            values.Add(nextValue);
            remaining -= nextValue;
        }

        values.Sort((left, right) => right.CompareTo(left));
        return values;
    }

    private int DrawCandidateValue(int targetValue, int valueIndex, int minAllowed, int maxAllowed)
    {
        if (maxAllowed < minAllowed)
            return minAllowed;

        if (valueIndex == 0)
        {
            int anchorMin = Mathf.Max(minAllowed, Mathf.CeilToInt(targetValue * 0.25f));
            int anchorMax = Mathf.Min(maxAllowed, Mathf.CeilToInt(targetValue * 0.65f));
            if (anchorMin <= anchorMax)
                return Random.Range(anchorMin, anchorMax + 1);
        }

        if (valueIndex == 1)
        {
            int midMin = Mathf.Max(minAllowed, Mathf.CeilToInt(targetValue * 0.12f));
            int midMax = Mathf.Min(maxAllowed, Mathf.CeilToInt(targetValue * 0.35f));
            if (midMin <= midMax)
                return Random.Range(midMin, midMax + 1);
        }

        if (Random.value < 0.65f)
        {
            int smallMax = Mathf.Min(maxAllowed, 5);
            if (minAllowed <= smallMax)
                return Random.Range(minAllowed, smallMax + 1);
        }

        return Random.Range(minAllowed, maxAllowed + 1);
    }

    private int EvaluateSolutionQuality(List<int> values)
    {
        if (values == null || values.Count == 0)
            return int.MinValue;

        int score = 0;
        int min = values[0];
        int max = values[0];
        int sum = 0;
        int smallAdjustmentCount = 0;
        int easyNumberCount = 0;
        Dictionary<int, int> valueCounts = new Dictionary<int, int>();

        for (int i = 0; i < values.Count; i++)
        {
            int value = values[i];
            min = Mathf.Min(min, value);
            max = Mathf.Max(max, value);
            sum += value;

            if (value >= 1 && value <= 5)
                smallAdjustmentCount++;

            if (value % 10 == 0)
            {
                easyNumberCount++;
                score += 10;
            }
            else if (value % 5 == 0)
            {
                easyNumberCount++;
                score += 6;
            }

            if (!valueCounts.ContainsKey(value))
            {
                valueCounts[value] = 0;
            }

            valueCounts[value]++;
        }

        int repeatedExtraCount = 0;
        int maxRepeatCount = 0;
        foreach (KeyValuePair<int, int> valueCount in valueCounts)
        {
            score += 7;
            if (valueCount.Value > 1)
            {
                repeatedExtraCount += valueCount.Value - 1;
            }

            maxRepeatCount = Mathf.Max(maxRepeatCount, valueCount.Value);
        }

        score -= repeatedExtraCount * 10;
        score += valueCounts.Count * 6;

        int spread = max - min;
        score += Mathf.Min(spread * 2, 40);
        if (spread >= Mathf.Max(4, Mathf.CeilToInt(sum * 0.25f)))
        {
            score += 18;
        }
        else if (spread <= 2)
        {
            score -= 25;
        }

        if (smallAdjustmentCount > 0)
        {
            score += 14 + Mathf.Min(smallAdjustmentCount * 3, 9);
        }
        else
        {
            score -= 10;
        }

        if (easyNumberCount > 0)
        {
            score += Mathf.Min(easyNumberCount * 5, 15);
        }

        int mediumLargeCount = 0;
        int mediumLargeThreshold = Mathf.Max(8, Mathf.CeilToInt(sum * 0.25f));
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] >= mediumLargeThreshold)
            {
                mediumLargeCount++;
            }
        }

        if (mediumLargeCount > 0)
        {
            score += 12;
        }

        if (maxRepeatCount >= Mathf.CeilToInt(values.Count * 0.5f))
        {
            score -= 20;
        }

        float average = sum / (float)values.Count;
        int closeToAverageCount = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (Mathf.Abs(values[i] - average) <= 1f)
            {
                closeToAverageCount++;
            }
        }

        if (closeToAverageCount >= values.Count - 1)
        {
            score -= 25;
        }

        if (valueCounts.Count == 1)
        {
            score -= 40;
        }

        return score;
    }

    private List<int> GenerateFallbackSolution(int targetValue, int blockCount, int minValue, int maxValue)
    {
        List<int> values = new List<int>();
        if (targetValue <= 0)
        {
            values.Add(0);
            return values;
        }

        int remaining = targetValue;
        for (int i = 0; i < blockCount; i++)
        {
            int slotsLeft = blockCount - i;
            int minAllowed = Mathf.Max(minValue, remaining - ((slotsLeft - 1) * maxValue));
            int maxAllowed = Mathf.Min(maxValue, remaining - ((slotsLeft - 1) * minValue));
            int nextValue = slotsLeft == 1 ? remaining : Random.Range(minAllowed, maxAllowed + 1);

            values.Add(nextValue);
            remaining -= nextValue;
        }

        values.Sort((left, right) => right.CompareTo(left));
        return values;
    }

    private bool IsValidSolution(List<int> values, int targetValue, int minValue, int maxValue)
    {
        if (values == null || values.Count == 0)
            return false;

        int sum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            int value = values[i];
            if (value < minValue || value > maxValue)
                return false;

            sum += value;
        }

        return sum == targetValue;
    }

    private void SpawnBlocks(List<int> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            GameObject block = Instantiate(blockPrefab, GetRandomSpawnPosition(), Quaternion.identity);
            block.name = $"{blockPrefab.name}_Generated_{i + 1}_{values[i]}";

            MathBlockValue blockValue = block.GetComponent<MathBlockValue>();
            if (blockValue != null)
            {
                blockValue.SetValue(values[i]);
            }

            Rigidbody blockRigidbody = block.GetComponent<Rigidbody>();
            if (blockRigidbody != null)
            {
                blockRigidbody.isKinematic = false;
                blockRigidbody.useGravity = true;
                blockRigidbody.linearVelocity = Vector3.zero;
                blockRigidbody.angularVelocity = Vector3.zero;
            }

            spawnedBlocks.Add(block);
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 halfSize = spawnAreaSize * 0.5f;
        Vector3 localOffset = new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y) + spawnHeightOffset,
            Random.Range(-halfSize.z, halfSize.z)
        );

        return transform.TransformPoint(localOffset);
    }

    private void ClearSpawnedBlocks()
    {
        for (int i = spawnedBlocks.Count - 1; i >= 0; i--)
        {
            GameObject block = spawnedBlocks[i];
            if (block == null)
                continue;

            if (Application.isPlaying)
            {
                Destroy(block);
            }
            else
            {
                DestroyImmediate(block);
            }
        }

        spawnedBlocks.Clear();
    }

    private void ConfigureSpawnCollider()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
            return;

        boxCollider.isTrigger = true;
        boxCollider.size = new Vector3(spawnAreaSize.x, Mathf.Max(0.1f, spawnAreaSize.y), spawnAreaSize.z);
        boxCollider.center = new Vector3(0f, spawnHeightOffset, 0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(new Vector3(0f, spawnHeightOffset, 0f), new Vector3(spawnAreaSize.x, Mathf.Max(0.1f, spawnAreaSize.y), spawnAreaSize.z));
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, spawnHeightOffset, 0f), new Vector3(spawnAreaSize.x, Mathf.Max(0.1f, spawnAreaSize.y), spawnAreaSize.z));
        Gizmos.matrix = previousMatrix;
    }

    private void LogGeneration(
        int finalDoorValue,
        int solutionTarget,
        List<int> solutionValues,
        int solutionQualityScore,
        List<int> extraValues,
        List<int> allValues,
        GenerationSettings settings)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"{name}: geracao procedural concluida.");
        builder.AppendLine($"Intervalo possivel no Inspector: {possibleDoorMinValue} ate {possibleDoorMaxValue}");
        builder.AppendLine($"Valor final da porta: {finalDoorValue}");
        builder.AppendLine($"Solution target: {solutionTarget}");
        builder.AppendLine($"Modo de geracao: {generationMode}");
        builder.AppendLine($"Quantidade de blocos da solucao: {solutionValues.Count}");
        builder.AppendLine($"Limites calculados dos blocos: {settings.MinBlockValue} ate {settings.MaxBlockValue}");
        builder.AppendLine($"Quantidade de blocos gerada: {allValues.Count}");
        builder.AppendLine($"Solucao escolhida: {string.Join(" + ", solutionValues)} = {solutionTarget}");
        builder.AppendLine($"Pontuacao da solucao escolhida: {solutionQualityScore}");
        builder.AppendLine($"Valores extras: {(extraValues.Count > 0 ? string.Join(", ", extraValues) : "nenhum")}");
        builder.AppendLine($"Valores finais embaralhados: {string.Join(", ", allValues)}");

        if (padVerifier != null)
        {
            builder.AppendLine($"Pad/verificador referenciado: {padVerifier.name}");
        }

        Debug.Log(builder.ToString());
    }

    private static void Shuffle<T>(List<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T current = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = current;
        }
    }

    private struct GenerationSettings
    {
        public int MinSolutionBlocks;
        public int MaxSolutionBlocks;
        public int MinExtraBlocks;
        public int MaxExtraBlocks;
        public int MinBlockValue;
        public int MaxBlockValue;
    }
}
