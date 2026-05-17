using UnityEngine;

public class opItem : MonoBehaviour
{
    public GravityInteract.PencilOperator operatorType;

    [Header("Reactivation Animation")]
    [SerializeField] private AnimationClip reactivationAnimation;

    [Header("Absorb Effect")]
    [SerializeField] private OperatorAbsorbEffect absorbEffectPrefab;
    [SerializeField] private OperatorAbsorbWaveEffect absorbWaveEffectPrefab;

    [Header("Visual Wobble")]
    [SerializeField] private bool useWobble = true;
    [SerializeField] private float wobbleAmplitude = 0.15f;
    [SerializeField] private float wobbleSpeed = 1.5f;

    [Header("Point Light Fade")]
    [SerializeField] private bool usePointLightFade;
    [SerializeField] private float lightFadeInDuration = 0.45f;
    [SerializeField] private float lightFadeOutDuration = 0.35f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private float wobbleTimeOffset;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private Light[] cachedLights;
    private float[] originalLightIntensities;
    private bool configuredUseWobble;
    private Coroutine reactivationRoutine;
    private Coroutine lightFadeRoutine;

    private void Awake()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        configuredUseWobble = useWobble;
        wobbleTimeOffset = Random.Range(0f, Mathf.PI * 2f);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        CacheLights();
    }

    private void Update()
    {
        if (!useWobble)
            return;

        float verticalOffset = Mathf.Sin((Time.time * wobbleSpeed) + wobbleTimeOffset) * wobbleAmplitude;
        transform.position = originalPosition + Vector3.up * verticalOffset;
    }

    public void ConsumeFromScene(Transform absorbTarget)
    {
        StopReactivationAnimation();
        transform.localScale = originalScale;
        FadeLightsOut();
        SpawnAbsorbEffect(absorbTarget);
        SetScenePresence(false);
    }

    public void RestoreToScene()
    {
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        SetScenePresence(true);
        FadeLightsIn();
        PlayReactivationAnimation();
    }

    private void SpawnAbsorbEffect(Transform absorbTarget)
    {
        Color effectColor = GetAbsorbEffectColor();
        SpawnWaveEffect(effectColor);

        OperatorAbsorbEffect effect = null;
        if (absorbEffectPrefab != null)
        {
            effect = Instantiate(absorbEffectPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            GameObject effectObject = new GameObject("OperatorAbsorbEffect");
            effectObject.transform.position = transform.position;
            effectObject.AddComponent<ParticleSystem>();
            effect = effectObject.AddComponent<OperatorAbsorbEffect>();
        }

        effect.Init(absorbTarget, effectColor);
    }

    private void SpawnWaveEffect(Color effectColor)
    {
        OperatorAbsorbWaveEffect waveEffect = null;
        if (absorbWaveEffectPrefab != null)
        {
            waveEffect = Instantiate(absorbWaveEffectPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            GameObject waveObject = new GameObject("OperatorAbsorbWaveEffect");
            waveObject.transform.position = transform.position;
            waveObject.AddComponent<ParticleSystem>();
            waveEffect = waveObject.AddComponent<OperatorAbsorbWaveEffect>();
        }

        waveEffect.Init(effectColor);
    }

    private Color GetAbsorbEffectColor()
    {
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                return new Color(0.15f, 1f, 0.25f);

            case GravityInteract.PencilOperator.Subtraction:
                return new Color(1f, 0.72f, 0.08f);

            case GravityInteract.PencilOperator.Multiplication:
                return new Color(0.1f, 0.45f, 1f);

            case GravityInteract.PencilOperator.Division:
                return new Color(1f, 0.08f, 0.06f);

            default:
                return Color.white;
        }
    }

    private void SetScenePresence(bool isVisible)
    {
        useWobble = isVisible && configuredUseWobble;

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        foreach (Renderer targetRenderer in cachedRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = isVisible;
            }
        }

        foreach (Collider targetCollider in cachedColliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = isVisible;
            }
        }
    }

    private void CacheLights()
    {
        cachedLights = GetComponentsInChildren<Light>(true);
        originalLightIntensities = new float[cachedLights.Length];

        for (int i = 0; i < cachedLights.Length; i++)
        {
            if (cachedLights[i] != null)
            {
                originalLightIntensities[i] = cachedLights[i].intensity;
            }
        }
    }

    private void FadeLightsIn()
    {
        if (!usePointLightFade)
            return;

        EnsureLightsCached();
        StopLightFade();

        for (int i = 0; i < cachedLights.Length; i++)
        {
            if (cachedLights[i] != null)
            {
                cachedLights[i].enabled = true;
                cachedLights[i].intensity = 0f;
            }
        }

        lightFadeRoutine = StartCoroutine(FadeLightsRoutine(true, lightFadeInDuration));
    }

    private void FadeLightsOut()
    {
        if (!usePointLightFade)
            return;

        EnsureLightsCached();
        StopLightFade();
        lightFadeRoutine = StartCoroutine(FadeLightsRoutine(false, lightFadeOutDuration));
    }

    private void EnsureLightsCached()
    {
        if (cachedLights == null || cachedLights.Length == 0)
        {
            CacheLights();
        }
    }

    private void StopLightFade()
    {
        if (lightFadeRoutine == null)
            return;

        StopCoroutine(lightFadeRoutine);
        lightFadeRoutine = null;
    }

    private System.Collections.IEnumerator FadeLightsRoutine(bool fadeIn, float duration)
    {
        float safeDuration = Mathf.Max(duration, 0.01f);
        float elapsed = 0f;
        float[] startIntensities = new float[cachedLights.Length];

        for (int i = 0; i < cachedLights.Length; i++)
        {
            if (cachedLights[i] != null)
            {
                startIntensities[i] = cachedLights[i].intensity;
            }
        }

        while (elapsed < safeDuration)
        {
            float t = elapsed / safeDuration;
            ApplyLightFade(fadeIn, startIntensities, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyLightFade(fadeIn, startIntensities, 1f);

        if (!fadeIn)
        {
            for (int i = 0; i < cachedLights.Length; i++)
            {
                if (cachedLights[i] != null)
                {
                    cachedLights[i].enabled = false;
                }
            }
        }

        lightFadeRoutine = null;
    }

    private void ApplyLightFade(bool fadeIn, float[] startIntensities, float t)
    {
        for (int i = 0; i < cachedLights.Length; i++)
        {
            if (cachedLights[i] == null)
                continue;

            float targetIntensity = originalLightIntensities[i];
            float endIntensity = fadeIn ? targetIntensity : 0f;
            cachedLights[i].intensity = Mathf.Lerp(startIntensities[i], endIntensity, t);
        }
    }

    private void PlayReactivationAnimation()
    {
        if (reactivationAnimation == null)
            return;

        StopReactivationAnimation();

        reactivationRoutine = StartCoroutine(PlayReactivationAnimationRoutine());
    }

    private void StopReactivationAnimation()
    {
        if (reactivationRoutine == null)
            return;

        StopCoroutine(reactivationRoutine);
        reactivationRoutine = null;
    }

    private System.Collections.IEnumerator PlayReactivationAnimationRoutine()
    {
        bool shouldResumeWobble = configuredUseWobble;
        useWobble = false;

        float duration = Mathf.Max(reactivationAnimation.length, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            reactivationAnimation.SampleAnimation(gameObject, elapsed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        reactivationAnimation.SampleAnimation(gameObject, duration);
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        transform.localScale = originalScale;
        useWobble = shouldResumeWobble;
        reactivationRoutine = null;
    }
}
