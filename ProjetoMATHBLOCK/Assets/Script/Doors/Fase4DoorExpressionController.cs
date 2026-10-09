using UnityEngine;

[DisallowMultipleComponent]
public sealed class Fase4DoorExpressionController : MonoBehaviour
{
    [Header("Lentes")]
    [SerializeField] private MeshRenderer leftLightRenderer;
    [SerializeField] private MeshRenderer rightLightRenderer;
    [SerializeField, Min(0)] private int lensMaterialIndex = 1;
    [SerializeField] private Color idleLensColor = Color.red;
    [SerializeField] private Color solvedLensColor = new Color(0.1f, 1f, 0.1f, 1f);
    [SerializeField] private Color solvedEmissionColor = new Color(0f, 2f, 0f, 1f);

    [Header("Point Lights")]
    [SerializeField] private Light leftPointLight;
    [SerializeField] private Light rightPointLight;

    [Header("Folhas da porta")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private float leftOpenAngle = 90f;
    [SerializeField] private float rightOpenAngle = -90f;
    [SerializeField, Min(0f)] private float openingDuration = 4f;

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;
    private Quaternion leftOpenRotation;
    private Quaternion rightOpenRotation;
    private bool leftSolvedFeedbackApplied;
    private bool rightSolvedFeedbackApplied;
    private bool openingStarted;
    private bool openingCompleted;
    private float openingElapsed;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        CacheClosedRotations();
        ResetPlayModeState();
    }

    public void SignalLeftSolved()
    {
        SignalSolved(leftLightRenderer, leftPointLight, ref leftSolvedFeedbackApplied);
    }

    public void SignalRightSolved()
    {
        SignalSolved(rightLightRenderer, rightPointLight, ref rightSolvedFeedbackApplied);
    }

    private void Update()
    {
        if (!Application.isPlaying || !openingStarted)
            return;

        AnimateOpening();
    }

    private void CacheClosedRotations()
    {
        if (leftDoor != null)
        {
            leftClosedRotation = leftDoor.localRotation;
            leftOpenRotation =
                Quaternion.Euler(0f, leftOpenAngle, 0f) * leftClosedRotation;
        }

        if (rightDoor != null)
        {
            rightClosedRotation = rightDoor.localRotation;
            rightOpenRotation =
                Quaternion.Euler(0f, rightOpenAngle, 0f) * rightClosedRotation;
        }
    }

    private void ResetPlayModeState()
    {
        leftSolvedFeedbackApplied = false;
        rightSolvedFeedbackApplied = false;
        openingStarted = false;
        openingCompleted = false;
        openingElapsed = 0f;

        if (leftDoor != null)
            leftDoor.localRotation = leftClosedRotation;

        if (rightDoor != null)
            rightDoor.localRotation = rightClosedRotation;

        SetLensAppearance(leftLightRenderer, idleLensColor, false);
        SetLensAppearance(rightLightRenderer, idleLensColor, false);
        SetPointLightState(leftPointLight, false);
        SetPointLightState(rightPointLight, false);
    }

    private void SignalSolved(
        MeshRenderer lensRenderer,
        Light pointLight,
        ref bool feedbackApplied)
    {
        if (!Application.isPlaying || feedbackApplied)
            return;

        feedbackApplied = true;
        SetLensAppearance(lensRenderer, solvedLensColor, true);
        SetPointLightState(pointLight, true);

        if (!openingStarted && !openingCompleted &&
            leftSolvedFeedbackApplied && rightSolvedFeedbackApplied)
        {
            BeginOpening();
        }
    }

    private void SetLensAppearance(MeshRenderer renderer, Color color, bool emissionEnabled)
    {
        if (renderer == null)
            return;

        Material[] materials = renderer.materials;
        Material lensMaterial = FindLensMaterial(materials);

        if (lensMaterial == null)
            return;

        if (lensMaterial.HasProperty("_BaseColor"))
            lensMaterial.SetColor("_BaseColor", color);

        if (lensMaterial.HasProperty("_Color"))
            lensMaterial.SetColor("_Color", color);

        if (lensMaterial.HasProperty("_EmissionColor"))
        {
            lensMaterial.SetColor(
                "_EmissionColor",
                emissionEnabled ? solvedEmissionColor : Color.black);
        }

        if (emissionEnabled)
            lensMaterial.EnableKeyword("_EMISSION");
        else
            lensMaterial.DisableKeyword("_EMISSION");

        renderer.materials = materials;
    }

    private Material FindLensMaterial(Material[] materials)
    {
        if (materials == null || materials.Length == 0)
            return null;

        if (lensMaterialIndex >= 0 && lensMaterialIndex < materials.Length)
            return materials[lensMaterialIndex];

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].name.Contains("Lens"))
                return materials[i];
        }

        return null;
    }

    private static void SetPointLightState(Light pointLight, bool enabled)
    {
        if (pointLight == null)
            return;

        pointLight.color = new Color(0.1f, 1f, 0.1f, 1f);
        pointLight.enabled = enabled;
    }

    private void BeginOpening()
    {
        if (openingStarted)
            return;

        openingStarted = true;
        openingElapsed = 0f;
    }

    private void AnimateOpening()
    {
        if (openingDuration <= 0f)
        {
            ApplyOpenPose();
            return;
        }

        openingElapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(openingElapsed / openingDuration);

        // Ease-out: abre com velocidade alta e desacelera até parar.
        float easedTime = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f);

        if (leftDoor != null)
        {
            leftDoor.localRotation = Quaternion.Slerp(
                leftClosedRotation,
                leftOpenRotation,
                easedTime);
        }

        if (rightDoor != null)
        {
            rightDoor.localRotation = Quaternion.Slerp(
                rightClosedRotation,
                rightOpenRotation,
                easedTime);
        }

        if (normalizedTime >= 1f)
            ApplyOpenPose();
    }

    private void ApplyOpenPose()
    {
        if (leftDoor != null)
            leftDoor.localRotation = leftOpenRotation;

        if (rightDoor != null)
            rightDoor.localRotation = rightOpenRotation;

        openingStarted = false;
        openingCompleted = true;
    }
}

