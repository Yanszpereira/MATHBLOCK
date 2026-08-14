using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

[DisallowMultipleComponent]
[RequireComponent(typeof(MathBlockIdController))]
public class DesfazerManager : MonoBehaviour
{
    private const string ManagerObjectName = "DesfazerManager";
    private const string SnapshotRootName = "UndoSnapshots";

    private static DesfazerManager instance;

    private readonly Dictionary<int, Stack<Acao>> operationStacksByBlockId = new Dictionary<int, Stack<Acao>>();
    private MathBlockIdController idController;
    private Transform snapshotRoot;

    [Header("Sons FMOD")]
    [SerializeField] private bool playSounds = true;
    [SerializeField] private EventReference operationSound;
    [SerializeField] private EventReference undoSound;

    [Header("Controle de repetição")]
    [SerializeField] private float operationSoundCooldown = 0.05f;
    [SerializeField] private float undoSoundCooldown = 0.15f;

    private float lastOperationSoundTime = -999f;
    private float lastUndoSoundTime = -999f;

    public MathBlockIdController IdController => GetIdController();

    public class Acao
    {
        public int targetBlockId;
        public int consumedBlockId;
        public int previousTargetValue;
        public string consumedBlockName;
        public GravityInteract.PencilOperator operatorType;
        public GameObject consumedBlockSnapshot;
        public MathBlockValue.RendererColorSnapshot[] consumedBlockRendererSnapshot;
        public Stack<Acao> consumedBlockStackSnapshot;
    }

    public static DesfazerManager Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<DesfazerManager>();

            if (instance != null)
                return instance;

            GameObject managerObject = GameObject.Find(ManagerObjectName);

            if (managerObject == null)
            {
                managerObject = new GameObject(ManagerObjectName);
            }

            instance = managerObject.GetComponent<DesfazerManager>();

            if (instance == null)
            {
                instance = managerObject.AddComponent<DesfazerManager>();
            }

            return instance;
        }
    }

    public static bool TryGetExistingInstance(out DesfazerManager existingInstance)
    {
        existingInstance = instance;

        if (existingInstance != null)
            return true;

        existingInstance = FindFirstObjectByType<DesfazerManager>();

        if (existingInstance != null)
        {
            instance = existingInstance;
            return true;
        }

        return false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        GetIdController();
        GetSnapshotRoot();
    }

    public void RegisterBlock(MathBlockValue block)
    {
        if (block == null)
            return;

        GetIdController().RegisterBlock(block);
        operationStacksByBlockId[block.BlockId] = block.OperationStack;
    }

    public void AssignNewBlockId(MathBlockValue block)
    {
        if (block == null)
            return;

        GetIdController().AssignNewId(block);
        operationStacksByBlockId[block.BlockId] = block.OperationStack;
    }

    public void RestoreBlockId(MathBlockValue block, int restoredId)
    {
        if (block == null)
            return;

        GetIdController().RestoreExistingId(block, restoredId);
        operationStacksByBlockId[block.BlockId] = block.OperationStack;
    }

    public void UnregisterBlock(MathBlockValue block)
    {
        if (block == null)
            return;

        GetIdController().UnregisterBlock(block);
    }

    public bool TryRecordOperation(
        MathBlockValue targetBlock,
        MathBlockValue consumedBlock,
        GravityInteract.PencilOperator operatorType,
        int previousTargetValue)
    {
        if (targetBlock == null || consumedBlock == null)
            return false;

        RegisterBlock(targetBlock);
        RegisterBlock(consumedBlock);

        Acao action = new Acao
        {
            targetBlockId = targetBlock.BlockId,
            consumedBlockId = consumedBlock.BlockId,
            previousTargetValue = previousTargetValue,
            consumedBlockName = consumedBlock.name,
            operatorType = operatorType,
            consumedBlockSnapshot = CreateConsumedBlockSnapshot(consumedBlock),
            consumedBlockRendererSnapshot = consumedBlock.CaptureRendererColors(),
            consumedBlockStackSnapshot = CloneStack(consumedBlock.OperationStack)
        };

        targetBlock.OperationStack.Push(action);
        operationStacksByBlockId[targetBlock.BlockId] = targetBlock.OperationStack;

        PlayOperationSound(targetBlock, consumedBlock);

        return true;
    }

    public bool TryUndoLastOperation(MathBlockValue targetBlock, float spawnHeight)
    {
        if (targetBlock == null)
            return false;

        RegisterBlock(targetBlock);

        Stack<Acao> targetStack;

        if (!operationStacksByBlockId.TryGetValue(targetBlock.BlockId, out targetStack) || targetStack == null || targetStack.Count == 0)
            return false;

        Acao action = targetStack.Peek();
        ResizableBlock resizableBlock = targetBlock.GetComponent<ResizableBlock>();
        ResizableBlockState previousResizeState = default;
        bool requiresAutomaticFit =
            resizableBlock != null
            && resizableBlock.CurrentVolume > action.previousTargetValue;

        if (requiresAutomaticFit)
        {
            if (!resizableBlock.TryCalculateFittedDimensions(
                action.previousTargetValue,
                out _,
                out ResizeValidationFailure fitFailure))
            {
                Debug.LogWarning(
                    $"Bloco {targetBlock.name} nao pode desfazer {action.operatorType}: "
                    + $"o valor {action.previousTargetValue} nao comporta suas dimensoes atuais "
                    + $"({resizableBlock.Width}x{resizableBlock.Height}x{resizableBlock.Depth}). "
                    + $"Motivo: {fitFailure}.",
                    targetBlock
                );
                return false;
            }

            previousResizeState = resizableBlock.CaptureState();
        }

        if (action.consumedBlockSnapshot == null)
        {
            Debug.LogWarning(
                $"Nao foi possivel desfazer {action.operatorType} em {targetBlock.name}: "
                + $"snapshot de {action.consumedBlockName} ausente.",
                targetBlock
            );
            return false;
        }

        int valueBeforeUndo = targetBlock.CurrentValue;

        if (requiresAutomaticFit
            && !resizableBlock.TryFitToValue(
                action.previousTargetValue,
                out ResizeValidationFailure applyFailure))
        {
            Debug.LogWarning(
                $"Nao foi possivel ajustar {targetBlock.name} antes do undo. Motivo: {applyFailure}.",
                targetBlock
            );
            return false;
        }

        targetBlock.SetValue(action.previousTargetValue);

        if (!TryRestoreConsumedBlock(targetBlock, action, spawnHeight))
        {
            targetBlock.SetValue(valueBeforeUndo);

            if (requiresAutomaticFit)
                resizableBlock.RestoreState(previousResizeState);

            return false;
        }

        targetStack.Pop();
        PlayUndoSound(targetBlock);

        Debug.Log($"Bloco {targetBlock.name} desfez {action.operatorType} e voltou para {targetBlock.CurrentValue}.");

        return true;
    }

    private void PlayOperationSound(MathBlockValue targetBlock, MathBlockValue consumedBlock)
    {
        if (!playSounds)
            return;

        if (operationSound.IsNull)
        {
            Debug.LogWarning($"{name}: som de operacao FMOD nao configurado.");
            return;
        }

        if (Time.time < lastOperationSoundTime + operationSoundCooldown)
            return;

        lastOperationSoundTime = Time.time;

        Vector3 soundPosition = transform.position;

        if (targetBlock != null && consumedBlock != null)
        {
            soundPosition = (targetBlock.transform.position + consumedBlock.transform.position) * 0.5f;
        }
        else if (targetBlock != null)
        {
            soundPosition = targetBlock.transform.position;
        }
        else if (consumedBlock != null)
        {
            soundPosition = consumedBlock.transform.position;
        }

        RuntimeManager.PlayOneShot(operationSound, soundPosition);
    }

    private void PlayUndoSound(MathBlockValue targetBlock)
    {
        if (!playSounds)
            return;

        if (undoSound.IsNull)
        {
            Debug.LogWarning($"{name}: som de desfazer FMOD nao configurado.");
            return;
        }

        if (Time.time < lastUndoSoundTime + undoSoundCooldown)
            return;

        lastUndoSoundTime = Time.time;

        Vector3 soundPosition = targetBlock != null ? targetBlock.transform.position : transform.position;

        RuntimeManager.PlayOneShot(undoSound, soundPosition);
    }

    public Stack<Acao> CloneStack(Stack<Acao> sourceStack)
    {
        if (sourceStack == null || sourceStack.Count == 0)
            return new Stack<Acao>();

        Acao[] actions = sourceStack.ToArray();
        System.Array.Reverse(actions);

        return new Stack<Acao>(actions);
    }

    private GameObject CreateConsumedBlockSnapshot(MathBlockValue consumedBlock)
    {
        Transform root = GetSnapshotRoot();

        MathBlockValue.RendererColorSnapshot[] originalColors = consumedBlock.CaptureRendererColors();

        GameObject snapshot = Instantiate(consumedBlock.gameObject, root);
        snapshot.name = $"{consumedBlock.name}_UndoSnapshot";

        MathBlockValue snapshotValue = snapshot.GetComponent<MathBlockValue>();

        if (snapshotValue != null)
        {
            snapshotValue.ApplyRendererColors(originalColors);
            snapshotValue.DetachFromUndoRuntime();
        }

        snapshot.SetActive(false);

        return snapshot;
    }

    private bool TryRestoreConsumedBlock(MathBlockValue targetBlock, Acao action, float spawnHeight)
    {
        if (action.consumedBlockSnapshot == null)
        {
            Debug.LogWarning($"Nao foi possivel restaurar {action.consumedBlockName}: snapshot ausente.");
            return false;
        }

        Vector3 spawnPosition = targetBlock.transform.position + Vector3.up * spawnHeight;
        GameObject restoredBlock = null;

        try
        {
            restoredBlock = Instantiate(
                action.consumedBlockSnapshot,
                spawnPosition,
                targetBlock.transform.rotation
            );
            restoredBlock.name = $"{action.consumedBlockName}_Restored";

            MathBlockValue restoredValue = restoredBlock.GetComponent<MathBlockValue>();
            if (restoredValue == null)
            {
                Debug.LogError(
                    $"Nao foi possivel restaurar {action.consumedBlockName}: MathBlockValue ausente no snapshot."
                );
                DestroyUndoObject(restoredBlock);
                return false;
            }

            restoredValue.InitializeRestoredFromUndo(
                action.consumedBlockId,
                CloneStack(action.consumedBlockStackSnapshot)
            );

            Rigidbody restoredRigidbody = restoredBlock.GetComponent<Rigidbody>();

            if (restoredRigidbody != null)
            {
                restoredRigidbody.isKinematic = false;
                restoredRigidbody.useGravity = true;
                restoredRigidbody.linearVelocity = Vector3.zero;
                restoredRigidbody.angularVelocity = Vector3.zero;
            }

            restoredBlock.SetActive(true);
            restoredValue.ApplyRendererColors(action.consumedBlockRendererSnapshot);

            DestroyUndoObject(action.consumedBlockSnapshot);
            action.consumedBlockSnapshot = null;
            return true;
        }
        catch (System.Exception exception)
        {
            if (restoredBlock != null)
                DestroyUndoObject(restoredBlock);

            Debug.LogError(
                $"Falha ao restaurar {action.consumedBlockName} durante o undo: {exception.Message}",
                targetBlock
            );
            return false;
        }
    }

    private static void DestroyUndoObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private MathBlockIdController GetIdController()
    {
        if (idController != null)
            return idController;

        idController = GetComponent<MathBlockIdController>();

        if (idController == null)
        {
            idController = gameObject.AddComponent<MathBlockIdController>();
        }

        return idController;
    }

    private Transform GetSnapshotRoot()
    {
        if (snapshotRoot != null)
            return snapshotRoot;

        Transform existingRoot = transform.Find(SnapshotRootName);

        if (existingRoot != null)
        {
            snapshotRoot = existingRoot;
            return snapshotRoot;
        }

        GameObject rootObject = new GameObject(SnapshotRootName);
        rootObject.transform.SetParent(transform, false);

        snapshotRoot = rootObject.transform;

        return snapshotRoot;
    }
}
