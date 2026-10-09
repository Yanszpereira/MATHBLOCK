using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlockSpawnPop : MonoBehaviour
{
    public static void Play(GameObject block, float duration)
    {
        if (block == null)
            return;

        BlockSpawnPop effect = block.GetComponent<BlockSpawnPop>();
        if (effect == null)
            effect = block.AddComponent<BlockSpawnPop>();
        effect.StartPop(duration);
    }

    private Coroutine animationRoutine;

    private void StartPop(float duration)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(Animate(Mathf.Max(0.05f, duration)));
    }

    private IEnumerator Animate(float duration)
    {
        Transform cachedTransform = transform;
        Vector3 finalScale = cachedTransform.localScale;
        Rigidbody body = GetComponent<Rigidbody>();
        bool originalKinematic = body != null && body.isKinematic;
        bool originalGravity = body != null && body.useGravity;
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool[] colliderStates = new bool[colliders.Length];

        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }

        cachedTransform.localScale = Vector3.zero;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float shifted = t - 1f;
            float scale = 1f + 2.2f * shifted * shifted * shifted + 1.2f * shifted * shifted;
            cachedTransform.localScale = finalScale * Mathf.Max(0f, scale);
            yield return null;
        }

        cachedTransform.localScale = finalScale;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = colliderStates[i];
        if (body != null)
        {
            body.isKinematic = originalKinematic;
            body.useGravity = originalGravity;
        }
        animationRoutine = null;
    }
}
