using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Suppresses only physical response between the player's own colliders and moving
/// piston colliders. Other scene collision pairs remain unchanged; contact is still
/// detected by the arena controller's capsule overlap query.
/// </summary>
internal sealed class Fase4PistonPlayerCollisionPolicy
{
    private struct PairState
    {
        public Collider playerCollider;
        public Collider pistonCollider;
        public bool wasIgnored;
    }

    private readonly List<PairState> pairs = new List<PairState>();

    public bool IsApplied => pairs.Count > 0;

    public void Apply(Collider[] playerColliders, Collider[] pistonColliders)
    {
        try
        {
            // Keep any pairs whose restoration previously failed, but still track
            // newly supplied colliders after a controller reinitialization.
            if (playerColliders != null && pistonColliders != null)
            {
                foreach (Collider playerCollider in playerColliders)
                {
                    if (playerCollider == null)
                        continue;

                    foreach (Collider pistonCollider in pistonColliders)
                        AddPair(playerCollider, pistonCollider);
                }
            }

            EnsureApplied();
        }
        catch
        {
            Restore();
            throw;
        }
    }

    public void EnsureApplied()
    {
        for (int index = 0; index < pairs.Count; index++)
        {
            PairState pair = pairs[index];
            if (pair.playerCollider != null && pair.pistonCollider != null &&
                !Physics.GetIgnoreCollision(pair.playerCollider, pair.pistonCollider))
            {
                Physics.IgnoreCollision(pair.playerCollider, pair.pistonCollider, true);
            }
        }
    }

    public void Restore()
    {
        for (int index = pairs.Count - 1; index >= 0; index--)
        {
            PairState pair = pairs[index];
            try
            {
                if (pair.playerCollider == null || pair.pistonCollider == null)
                {
                    pairs.RemoveAt(index);
                    continue;
                }

                if (Physics.GetIgnoreCollision(pair.playerCollider, pair.pistonCollider) != pair.wasIgnored)
                    Physics.IgnoreCollision(pair.playerCollider, pair.pistonCollider, pair.wasIgnored);

                // Retain a failed pair so a later lifecycle/update pass can retry;
                // never forget a collision state that this policy still owns.
                if (Physics.GetIgnoreCollision(pair.playerCollider, pair.pistonCollider) == pair.wasIgnored)
                    pairs.RemoveAt(index);
            }
            catch (System.Exception exception)
            {
                // Continue restoring remaining pairs even if Unity invalidated one
                // collider during teardown; keep this pair for a future retry.
                Debug.LogException(exception);
            }
        }
    }

    private void AddPair(Collider playerCollider, Collider pistonCollider)
    {
        if (playerCollider == null || pistonCollider == null || playerCollider == pistonCollider ||
            playerCollider.transform.IsChildOf(pistonCollider.transform))
            return;

        for (int index = 0; index < pairs.Count; index++)
            if (pairs[index].playerCollider == playerCollider && pairs[index].pistonCollider == pistonCollider)
                return;

        pairs.Add(new PairState
        {
            playerCollider = playerCollider,
            pistonCollider = pistonCollider,
            wasIgnored = Physics.GetIgnoreCollision(playerCollider, pistonCollider)
        });
    }
}
