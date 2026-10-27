using UnityEngine;

[DisallowMultipleComponent]
public sealed class Fase4BlackboardMotion : MonoBehaviour
{
    [SerializeField] private Transform motionPivot;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Renderer visualCenterSource;
    [SerializeField, Min(0f)] private float floatAmplitude = 0.15f;
    [SerializeField, Min(0.01f)] private float floatFrequency = 0.6f;
    [SerializeField, Min(0.1f)] private float rotationSharpness = 6f;
    [SerializeField, Range(0f, 89f)] private float maxVerticalAngle = 35f;
    [SerializeField, Min(0.05f)] private float activationDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float returnDuration = 1f;

    private Transform boardTransform;
    private Vector3 originalPivotPosition;
    private Quaternion originalPivotRotation;
    private Vector3 originalVisualCenter;
    private Vector3 visualCenterInPivotSpace;
    private Vector3 visualFrontInPivotSpace;
    private Quaternion activationStartRotation;
    private Vector3 activationStartCenter;
    private float activationStartYaw;
    private float activationStartPitch;
    private float returnStartYaw;
    private float returnStartPitch;
    private float activationElapsed;
    private float returnElapsed;
    private Vector3 returnStartCenter;
    private float smoothedYaw;
    private float smoothedPitch;
    private bool initialized;
    private bool trackingRequested;
    private bool activating;
    private bool returning;

    private void Awake()
    {
        if (motionPivot == null)
            motionPivot = transform;
        boardTransform = transform;
    }

private void LateUpdate()
    {
        if (!EnsureInitialized())
            return;

        float deltaTime = Time.unscaledDeltaTime;
        if (returning)
        {
            returnElapsed += deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(returnElapsed / Mathf.Max(0.05f, returnDuration)));
            float yaw = Mathf.Lerp(returnStartYaw, 0f, t);
            float pitch = Mathf.Lerp(returnStartPitch, 0f, t);
            ApplyPose(ComposeOrientation(yaw, pitch), Vector3.Lerp(returnStartCenter, originalVisualCenter, t));
            if (t >= 1f)
            {
                returning = false;
                smoothedYaw = 0f;
            }
            return;
        }

        if (!trackingRequested)
            return;

        if (activating)
        {
            activationElapsed += deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(activationElapsed / Mathf.Max(0.05f, activationDuration)));
            float followBlend = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            smoothedYaw = Mathf.LerpAngle(smoothedYaw, GetTargetYaw(), followBlend);
            smoothedPitch = Mathf.Lerp(smoothedPitch, GetTargetPitch(), followBlend);
            float yaw = Mathf.Lerp(activationStartYaw, smoothedYaw, t);
            float pitch = Mathf.Lerp(activationStartPitch, smoothedPitch, t);
            Vector3 targetCenter = GetFloatingCenter(1f);
            Vector3 center = Vector3.Lerp(activationStartCenter, targetCenter, t);
            ApplyPose(ComposeOrientation(yaw, pitch), center);
            if (t >= 1f)
            {
                activating = false;
                smoothedYaw = yaw;
            }
            return;
        }

        float desiredYaw = GetTargetYaw();
        float blend = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
        smoothedYaw = Mathf.LerpAngle(smoothedYaw, desiredYaw, blend);
        smoothedPitch = Mathf.Lerp(smoothedPitch, GetTargetPitch(), blend);
        ApplyPose(ComposeOrientation(smoothedYaw, smoothedPitch), GetFloatingCenter(1f));
    }

    public void SetCameraTarget(Transform target)
    {
        if (target != null)
            cameraTarget = target;
    }

public void SetChallengeActive(bool active)
    {
        if (!EnsureInitialized() || trackingRequested == active && !returning)
            return;

        if (active)
        {
            activationStartRotation = motionPivot.rotation;
            activationStartCenter = GetCurrentVisualCenter();
            Vector3 baseFront = originalPivotRotation * visualFrontInPivotSpace;
            Vector3 currentFront = activationStartRotation * visualFrontInPivotSpace;
            activationStartYaw = Vector3.SignedAngle(
                Vector3.ProjectOnPlane(baseFront, Vector3.up),
                Vector3.ProjectOnPlane(currentFront, Vector3.up),
                Vector3.up);
            activationStartPitch = GetFrontElevation(currentFront);
            smoothedYaw = activationStartYaw;
            smoothedPitch = activationStartPitch;
            activationElapsed = 0f;
            trackingRequested = true;
            activating = true;
            returning = false;
        }
        else
        {
            if (!trackingRequested && returning)
                return;

            trackingRequested = false;
            activating = false;
            Vector3 baseFront = originalPivotRotation * visualFrontInPivotSpace;
            Vector3 currentFront = motionPivot.rotation * visualFrontInPivotSpace;
            returnStartYaw = Vector3.SignedAngle(
                Vector3.ProjectOnPlane(baseFront, Vector3.up),
                Vector3.ProjectOnPlane(currentFront, Vector3.up),
                Vector3.up);
            returnStartPitch = GetFrontElevation(currentFront);
            returnStartCenter = GetCurrentVisualCenter();
            returnElapsed = 0f;
            returning = true;
        }
    }

private bool EnsureInitialized()
    {
        if (initialized)
            return motionPivot != null;

        if (motionPivot == null)
            motionPivot = transform;

        if (boardTransform == null)
            boardTransform = transform;
        if (visualCenterSource == null)
        {
            Transform surface = boardTransform.Find("QuadroNegro_Superficie");
            if (surface != null)
                visualCenterSource = surface.GetComponent<Renderer>();
        }
        if (visualCenterSource == null)
            return false;

        originalPivotPosition = motionPivot.position;
        originalPivotRotation = motionPivot.rotation;
        originalVisualCenter = visualCenterSource.bounds.center;
        visualCenterInPivotSpace = motionPivot.InverseTransformPoint(originalVisualCenter);
        Transform frontSource = boardTransform;
        visualFrontInPivotSpace = motionPivot.InverseTransformDirection(frontSource.forward).normalized;
        if (visualFrontInPivotSpace.sqrMagnitude < 0.0001f)
            visualFrontInPivotSpace = motionPivot.InverseTransformDirection(motionPivot.forward).normalized;

        smoothedYaw = 0f;
        smoothedPitch = 0f;
        initialized = true;
        return true;
    }

    private float GetTargetYaw()
    {
        if (cameraTarget == null)
            return smoothedYaw;

        Vector3 baseFront = originalPivotRotation * visualFrontInPivotSpace;
        Vector3 toCamera = cameraTarget.position - GetCurrentVisualCenter();
        baseFront = Vector3.ProjectOnPlane(baseFront, Vector3.up);
        toCamera = Vector3.ProjectOnPlane(toCamera, Vector3.up);
        if (baseFront.sqrMagnitude < 0.0001f || toCamera.sqrMagnitude < 0.0001f)
            return smoothedYaw;

        return Vector3.SignedAngle(baseFront, toCamera, Vector3.up);
    }

    private float GetTargetPitch()
    {
        if (cameraTarget == null)
            return smoothedPitch;

        Vector3 toCamera = cameraTarget.position - GetCurrentVisualCenter();
        Vector3 horizontal = Vector3.ProjectOnPlane(toCamera, Vector3.up);
        if (toCamera.sqrMagnitude < 0.0001f)
            return smoothedPitch;

        float requestedPitch = Mathf.Atan2(toCamera.y, horizontal.magnitude) * Mathf.Rad2Deg;
        return Mathf.Clamp(requestedPitch, -maxVerticalAngle, maxVerticalAngle);
    }

    private Quaternion ComposeOrientation(float yaw, float pitch)
    {
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up) * originalPivotRotation;
        Vector3 front = Vector3.ProjectOnPlane(yawRotation * visualFrontInPivotSpace, Vector3.up);
        if (front.sqrMagnitude < 0.0001f)
            return yawRotation;

        Vector3 pitchAxis = Vector3.Cross(Vector3.up, front.normalized);
        if (pitchAxis.sqrMagnitude < 0.0001f)
            return yawRotation;

        return Quaternion.AngleAxis(-pitch, pitchAxis.normalized) * yawRotation;
    }

    private static float GetFrontElevation(Vector3 front)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(front, Vector3.up);
        if (front.sqrMagnitude < 0.0001f || horizontal.sqrMagnitude < 0.0001f)
            return 0f;

        return Mathf.Atan2(front.y, horizontal.magnitude) * Mathf.Rad2Deg;
    }

    private Vector3 GetFloatingCenter(float weight)
    {
        float phase = Time.unscaledTime * (Mathf.PI * 2f * floatFrequency);
        return originalVisualCenter + Vector3.up * (Mathf.Sin(phase) * floatAmplitude * weight);
    }

    private Vector3 GetCurrentVisualCenter()
    {
        return motionPivot.TransformPoint(visualCenterInPivotSpace);
    }

    private void ApplyPose(Quaternion rotation, Vector3 visualCenter)
    {
        motionPivot.rotation = rotation;
        motionPivot.position += visualCenter - GetCurrentVisualCenter();
    }

    private void OnDisable()
    {
        if (!initialized || motionPivot == null)
            return;

        motionPivot.SetPositionAndRotation(originalPivotPosition, originalPivotRotation);
        trackingRequested = false;
        activating = false;
        returning = false;
        smoothedYaw = 0f;
        smoothedPitch = 0f;
    }
}
