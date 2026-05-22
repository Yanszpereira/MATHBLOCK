using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MathBlockIdController : MonoBehaviour
{
    private readonly Dictionary<int, MathBlockValue> activeBlocksById = new Dictionary<int, MathBlockValue>();
    private int nextBlockId;

    public int RegisterBlock(MathBlockValue block)
    {
        if (block == null)
            return -1;

        int requestedId = block.BlockId;
        if (requestedId >= 0 && TryReserveId(requestedId, block))
            return requestedId;

        int assignedId = GetNextFreeId();
        activeBlocksById[assignedId] = block;
        block.SetBlockIdFromController(assignedId);
        return assignedId;
    }

    public int AssignNewId(MathBlockValue block)
    {
        if (block == null)
            return -1;

        UnregisterBlock(block);

        int assignedId = GetNextFreeId();
        activeBlocksById[assignedId] = block;
        block.SetBlockIdFromController(assignedId);
        return assignedId;
    }

    public void RestoreExistingId(MathBlockValue block, int restoredId)
    {
        if (block == null)
            return;

        UnregisterBlock(block);

        if (restoredId < 0)
        {
            AssignNewId(block);
            return;
        }

        MathBlockValue existingBlock;
        if (activeBlocksById.TryGetValue(restoredId, out existingBlock) && existingBlock != null && existingBlock != block)
        {
            Debug.LogWarning($"ID {restoredId} ja estava ativo em {existingBlock.name}. Um novo ID sera atribuido para {block.name}.");
            AssignNewId(block);
            return;
        }

        activeBlocksById[restoredId] = block;
        block.SetBlockIdFromController(restoredId);

        if (restoredId >= nextBlockId)
        {
            nextBlockId = restoredId + 1;
        }
    }

    public void UnregisterBlock(MathBlockValue block)
    {
        if (block == null)
            return;

        int blockId = block.BlockId;
        if (blockId < 0)
            return;

        MathBlockValue registeredBlock;
        if (activeBlocksById.TryGetValue(blockId, out registeredBlock) && registeredBlock == block)
        {
            activeBlocksById.Remove(blockId);
        }
    }

    private bool TryReserveId(int requestedId, MathBlockValue block)
    {
        MathBlockValue existingBlock;
        if (activeBlocksById.TryGetValue(requestedId, out existingBlock))
        {
            if (existingBlock == null || existingBlock == block)
            {
                activeBlocksById[requestedId] = block;
                return true;
            }

            return false;
        }

        activeBlocksById[requestedId] = block;
        if (requestedId >= nextBlockId)
        {
            nextBlockId = requestedId + 1;
        }

        return true;
    }

    private int GetNextFreeId()
    {
        while (activeBlocksById.ContainsKey(nextBlockId) && activeBlocksById[nextBlockId] != null)
        {
            nextBlockId++;
        }

        int freeId = nextBlockId;
        nextBlockId++;
        return freeId;
    }
}
