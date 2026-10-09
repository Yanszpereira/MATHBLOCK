using UnityEngine;

/// <summary>
/// Shape-accurate contact queries for a translating piston collider. The capsule
/// is swept in the opposite relative direction against the collider at its current
/// pose, preserving the target collider's mesh shape and orientation.
/// </summary>
internal static class Fase4PistonSweepDetector
{
    public static bool TryGetContact(
        Vector3 capsulePointA,
        Vector3 capsulePointB,
        float capsuleRadius,
        Collider movingCollider,
        Vector3 relativeTranslation,
        Collider[] overlapBuffer,
        RaycastHit[] castBuffer)
    {
        if (movingCollider == null || !movingCollider.enabled || movingCollider.isTrigger ||
            overlapBuffer == null || overlapBuffer.Length == 0 || castBuffer == null || castBuffer.Length == 0 ||
            !IsFinite(capsulePointA) || !IsFinite(capsulePointB) ||
            !IsFinite(relativeTranslation) ||
            float.IsNaN(capsuleRadius) || float.IsInfinity(capsuleRadius) || capsuleRadius <= 0f)
            return false;

        if (OverlapsTarget(capsulePointA, capsulePointB, capsuleRadius, movingCollider, overlapBuffer))
            return true;

        float distance = relativeTranslation.magnitude;
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0.00001f)
            return false;

        // In the target's final-pose frame, the stationary player capsule travels
        // from current + targetDelta back to current. This is the exact relative
        // translation sweep for a collider that did not rotate during the frame.
        Vector3 relativeStartOffset = relativeTranslation;
        Vector3 direction = -relativeTranslation / distance;
        Vector3 startA = capsulePointA + relativeStartOffset;
        Vector3 startB = capsulePointB + relativeStartOffset;

        // CapsuleCast does not report initial overlap, so test the beginning of
        // the relative sweep explicitly against the same oriented target mesh.
        if (OverlapsTarget(startA, startB, capsuleRadius, movingCollider, overlapBuffer))
            return true;

        int hitCount = Physics.CapsuleCastNonAlloc(
            startA,
            startB,
            capsuleRadius,
            direction,
            castBuffer,
            distance,
            1 << movingCollider.gameObject.layer,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hitCount; index++)
            if (castBuffer[index].collider == movingCollider)
                return true;

        if (hitCount == castBuffer.Length)
        {
            RaycastHit[] completeResults = Physics.CapsuleCastAll(
                startA,
                startB,
                capsuleRadius,
                direction,
                distance,
                1 << movingCollider.gameObject.layer,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < completeResults.Length; index++)
                if (completeResults[index].collider == movingCollider)
                    return true;
        }

        return false;
    }

    public static float GetBarDetectionRadius(float playerRadius, float skinWidth, float horizontalScale)
    {
        return Mathf.Max(0f, playerRadius) + Mathf.Max(0f, skinWidth) * Mathf.Abs(horizontalScale);
    }

    public static float GetPistonDetectionRadius(float playerRadiusLocal, float horizontalScale, float paddingMeters)
    {
        return Mathf.Max(0f, playerRadiusLocal) * Mathf.Abs(horizontalScale) + Mathf.Max(0f, paddingMeters);
    }

    private static bool OverlapsTarget(
        Vector3 pointA,
        Vector3 pointB,
        float radius,
        Collider target,
        Collider[] buffer)
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            buffer,
            1 << target.gameObject.layer,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
            if (buffer[index] == target)
                return true;

        // NonAlloc queries can truncate a full buffer. Fall back to the allocating
        // query only in that exceptional case so a crowded layer cannot hide contact.
        if (count == buffer.Length)
        {
            Collider[] completeResults = Physics.OverlapCapsule(
                pointA,
                pointB,
                radius,
                1 << target.gameObject.layer,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < completeResults.Length; index++)
                if (completeResults[index] == target)
                    return true;
        }

        return false;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
