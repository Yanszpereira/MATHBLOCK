using UnityEngine;

/// <summary>
/// Applies short horizontal impulses through the existing CharacterController without
/// changing PlayerMovement, input state, or the player's currently held MathBlock.
/// </summary>
[DisallowMultipleComponent]
public sealed class Fase4ObstaclePushReceiver : MonoBehaviour
{
    private CharacterController characterController;
    private Vector3 impactVelocity;
    private float remainingDuration;
    private float deceleration;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (characterController == null || !characterController.enabled || remainingDuration <= 0f)
            return;

        AdvanceImpact(Time.deltaTime);
    }

    private void AdvanceImpact(float deltaTime)
    {
        if (characterController == null || !characterController.enabled ||
            float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f || remainingDuration <= 0f)
            return;

        // Integrate the decelerating impulse only for the time it remains active.
        // Applying the initial velocity for an entire long frame made displacement
        // depend on frame rate and could continue beyond the configured duration.
        float step = Mathf.Min(deltaTime, remainingDuration);
        float speed = impactVelocity.magnitude;
        float distance = Mathf.Max(0f, speed * step - 0.5f * deceleration * step * step);
        if (distance > 0f && speed > 0f)
            characterController.Move(impactVelocity / speed * distance);

        impactVelocity = Vector3.MoveTowards(impactVelocity, Vector3.zero, deceleration * step);
        remainingDuration = Mathf.Max(0f, remainingDuration - step);
        if (remainingDuration <= 0f)
            impactVelocity = Vector3.zero;
    }

    /// <summary>Queues an additive, decelerating impulse; it never asks GravityInteract to release a block.</summary>
    public void ApplyImpact(Vector3 direction, float startingSpeed, float duration)
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (characterController == null || float.IsNaN(startingSpeed) || float.IsInfinity(startingSpeed) ||
            startingSpeed <= 0f || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
            return;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        impactVelocity += direction.normalized * startingSpeed;
        remainingDuration = Mathf.Max(remainingDuration, duration);
        deceleration = impactVelocity.magnitude / remainingDuration;
    }

    /// <summary>Clears residual motion when the existing Void fade begins.</summary>
    public void ClearImpact()
    {
        impactVelocity = Vector3.zero;
        remainingDuration = 0f;
        deceleration = 0f;
    }
}
