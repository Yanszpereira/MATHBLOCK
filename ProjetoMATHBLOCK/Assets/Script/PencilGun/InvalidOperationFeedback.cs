using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class InvalidOperationFeedback : MonoBehaviour
{
    [Header("Camera shake")]
    [SerializeField] private float shakeDuration = 0.28f;
    [SerializeField] private float shakeStrength = 0.075f;
    [SerializeField] private float shakeFrequency = 35f;

    [Header("Invalid block color")]
    [SerializeField] private Color invalidColor = new Color(1f, 0.03f, 0.03f, 1f);
    [SerializeField] private float transitionDuration = 0.18f;
    [SerializeField] private float holdDuration = 0.3f;

    private Coroutine feedbackRoutine;

    public void Play(Transform carriedBlock, Transform cameraTransform)
    {
        if (carriedBlock == null)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(PlayRoutine(carriedBlock, cameraTransform));
    }

    private IEnumerator PlayRoutine(Transform carriedBlock, Transform cameraTransform)
    {
        List<ColorState> colorStates = CaptureColors(carriedBlock);
        Vector3 originalCameraPosition = cameraTransform != null
            ? cameraTransform.localPosition
            : Vector3.zero;

        float safeTransition = Mathf.Max(0.01f, transitionDuration);
        float totalDuration = safeTransition + holdDuration + safeTransition;
        float elapsed = 0f;

        while (elapsed < totalDuration && carriedBlock != null)
        {
            float redAmount;
            if (elapsed < safeTransition)
                redAmount = Mathf.SmoothStep(0f, 1f, elapsed / safeTransition);
            else if (elapsed < safeTransition + holdDuration)
                redAmount = 1f;
            else
                redAmount = Mathf.SmoothStep(1f, 0f, (elapsed - safeTransition - holdDuration) / safeTransition);

            ApplyColor(colorStates, redAmount);

            if (cameraTransform != null && elapsed < shakeDuration)
            {
                float fade = 1f - (elapsed / Mathf.Max(0.01f, shakeDuration));
                float sample = elapsed * shakeFrequency;
                Vector3 offset = new Vector3(
                    Mathf.PerlinNoise(sample, 0.17f) - 0.5f,
                    Mathf.PerlinNoise(0.37f, sample) - 0.5f,
                    0f);
                cameraTransform.localPosition = originalCameraPosition + offset * shakeStrength * fade;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestoreColors(colorStates);
        if (cameraTransform != null)
            cameraTransform.localPosition = originalCameraPosition;

        feedbackRoutine = null;
    }

    private static List<ColorState> CaptureColors(Transform block)
    {
        List<ColorState> states = new List<ColorState>();
        Renderer[] renderers = block.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            foreach (Material material in targetRenderer.materials)
            {
                if (material == null)
                    continue;

                string propertyName = material.HasProperty("_BaseColor")
                    ? "_BaseColor"
                    : material.HasProperty("_Color") ? "_Color" : null;

                if (propertyName != null)
                    states.Add(new ColorState(material, propertyName, material.GetColor(propertyName)));
            }
        }

        return states;
    }

    private void ApplyColor(List<ColorState> states, float amount)
    {
        foreach (ColorState state in states)
        {
            if (state.Material == null)
                continue;

            Color target = invalidColor;
            target.a = state.OriginalColor.a;
            state.Material.SetColor(state.PropertyName, Color.Lerp(state.OriginalColor, target, amount));
        }
    }

    private static void RestoreColors(List<ColorState> states)
    {
        foreach (ColorState state in states)
        {
            if (state.Material != null)
                state.Material.SetColor(state.PropertyName, state.OriginalColor);
        }
    }

    private static bool IsLabelRenderer(Renderer targetRenderer)
    {
        Transform current = targetRenderer.transform;
        while (current != null)
        {
            if (current.name == "ValueLabels" || current.GetComponent<TextMesh>() != null)
                return true;
            current = current.parent;
        }
        return false;
    }

    private readonly struct ColorState
    {
        public readonly Material Material;
        public readonly string PropertyName;
        public readonly Color OriginalColor;

        public ColorState(Material material, string propertyName, Color originalColor)
        {
            Material = material;
            PropertyName = propertyName;
            OriginalColor = originalColor;
        }
    }
}
