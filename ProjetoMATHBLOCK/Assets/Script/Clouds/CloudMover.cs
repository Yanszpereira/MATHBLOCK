using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudMover : MonoBehaviour
{
    private const float FadeDuration = 2f;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorPropertyId = Shader.PropertyToID("_TintColor");

    [SerializeField] private Vector3 direction = Vector3.right;
    [SerializeField] private float speed = 2f;
    [SerializeField] private ParticleSystem targetParticleSystem;
    [SerializeField] private Vector3 minShapeScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private Vector3 maxShapeScale = new Vector3(2f, 2f, 2f);

    private readonly List<MaterialFadeTarget> fadeTargets = new List<MaterialFadeTarget>();
    private readonly List<ParticleFadeTarget> particleFadeTargets = new List<ParticleFadeTarget>();
    private Coroutine fadeCoroutine;
    private bool isFadingOut;

    private void Awake()
    {
        ConfigureCleanToonClouds();
        RandomizeParticleShapeScale();
        CacheFadeTargets();
        CacheParticleFadeTargets();
        SetFadeAlpha(0f);
        fadeCoroutine = StartCoroutine(FadeToAlpha(1f));
    }

    private void ConfigureCleanToonClouds()
    {
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particles in particleSystems)
        {
            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = Application.isMobilePlatform ? 34 : 52;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.8f, 5.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.76f, 0.96f, 0.90f, 0.34f),
                new Color(0.94f, 1f, 0.98f, 0.58f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(Application.isMobilePlatform ? 0.55f : 0.85f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            noise.frequency = 0.18f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.05f);

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient cloudGradient = new Gradient();
            cloudGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.76f, 0.96f, 0.90f), 0f),
                    new GradientColorKey(new Color(0.95f, 1f, 0.98f), 0.5f),
                    new GradientColorKey(new Color(0.64f, 0.88f, 0.91f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.56f, 0.18f),
                    new GradientAlphaKey(0.56f, 0.78f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(cloudGradient);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.72f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.8f, 0.94f),
                new Keyframe(1f, 0.70f)));

            ParticleSystemRenderer cloudRenderer = particles.GetComponent<ParticleSystemRenderer>();
            if (cloudRenderer != null)
            {
                cloudRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                cloudRenderer.alignment = ParticleSystemRenderSpace.View;
                cloudRenderer.sortMode = ParticleSystemSortMode.Distance;
                cloudRenderer.maxParticleSize = 0.18f;
                cloudRenderer.enableGPUInstancing = true;
            }
        }
    }

    private void Update()
    {
        if (direction == Vector3.zero)
        {
            return;
        }

        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    public void FadeOutAndDestroy()
    {
        if (isFadingOut)
        {
            return;
        }

        isFadingOut = true;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeOutThenDestroy());
    }

    private void RandomizeParticleShapeScale()
    {
        ParticleSystem particleSystemToChange = targetParticleSystem != null
            ? targetParticleSystem
            : GetComponent<ParticleSystem>();

        if (particleSystemToChange == null)
        {
            particleSystemToChange = GetComponentInChildren<ParticleSystem>();
        }

        if (particleSystemToChange == null)
        {
            return;
        }

        Vector3 shapeScale = new Vector3(
            Random.Range(minShapeScale.x, maxShapeScale.x),
            Random.Range(minShapeScale.y, maxShapeScale.y),
            Random.Range(minShapeScale.z, maxShapeScale.z)
        );

        ParticleSystem.ShapeModule shape = particleSystemToChange.shape;
        shape.scale = shapeScale;
    }

    private void CacheFadeTargets()
    {
        fadeTargets.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer targetRenderer in renderers)
        {
            foreach (Material material in targetRenderer.materials)
            {
                if (material.HasProperty(TintColorPropertyId))
                {
                    fadeTargets.Add(new MaterialFadeTarget(material, TintColorPropertyId, material.GetColor(TintColorPropertyId)));
                }
                else if (material.HasProperty(ColorPropertyId))
                {
                    fadeTargets.Add(new MaterialFadeTarget(material, ColorPropertyId, material.GetColor(ColorPropertyId)));
                }
                else if (material.HasProperty(BaseColorPropertyId))
                {
                    fadeTargets.Add(new MaterialFadeTarget(material, BaseColorPropertyId, material.GetColor(BaseColorPropertyId)));
                }
            }
        }
    }

    private void CacheParticleFadeTargets()
    {
        particleFadeTargets.Clear();

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleFadeTargets.Add(new ParticleFadeTarget(particleSystem, particleSystem.main.startColor));
        }
    }

    private IEnumerator FadeToAlpha(float targetAlpha)
    {
        float elapsedTime = 0f;
        float startAlpha = fadeTargets.Count > 0 ? fadeTargets[0].CurrentAlpha : targetAlpha;

        while (elapsedTime < FadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / FadeDuration);
            SetFadeAlpha(alpha);
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
    }

    private IEnumerator FadeOutThenDestroy()
    {
        yield return FadeToAlpha(0f);
        Destroy(gameObject);
    }

    private void SetFadeAlpha(float alpha)
    {
        foreach (MaterialFadeTarget fadeTarget in fadeTargets)
        {
            Color color = fadeTarget.OriginalColor;
            color.a *= alpha;
            fadeTarget.Material.SetColor(fadeTarget.ColorPropertyId, color);
        }

        foreach (ParticleFadeTarget particleFadeTarget in particleFadeTargets)
        {
            ParticleSystem.MainModule main = particleFadeTarget.ParticleSystem.main;
            main.startColor = ApplyAlphaToGradient(particleFadeTarget.OriginalStartColor, alpha);
        }
    }

    private static ParticleSystem.MinMaxGradient ApplyAlphaToGradient(
        ParticleSystem.MinMaxGradient originalGradient,
        float alpha)
    {
        switch (originalGradient.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ApplyAlphaToColor(originalGradient.color, alpha));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    ApplyAlphaToColor(originalGradient.colorMin, alpha),
                    ApplyAlphaToColor(originalGradient.colorMax, alpha)
                );
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(CopyGradientWithAlpha(originalGradient.gradient, alpha));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    CopyGradientWithAlpha(originalGradient.gradientMin, alpha),
                    CopyGradientWithAlpha(originalGradient.gradientMax, alpha)
                );
            default:
                return new ParticleSystem.MinMaxGradient(ApplyAlphaToColor(originalGradient.color, alpha));
        }
    }

    private static Color ApplyAlphaToColor(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    private static Gradient CopyGradientWithAlpha(Gradient source, float alpha)
    {
        Gradient gradient = new Gradient();
        GradientAlphaKey[] alphaKeys = source.alphaKeys;

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha *= alpha;
        }

        gradient.SetKeys(source.colorKeys, alphaKeys);
        return gradient;
    }

    private void OnValidate()
    {
        minShapeScale.x = Mathf.Max(0.01f, minShapeScale.x);
        minShapeScale.y = Mathf.Max(0.01f, minShapeScale.y);
        minShapeScale.z = Mathf.Max(0.01f, minShapeScale.z);

        maxShapeScale.x = Mathf.Max(minShapeScale.x, maxShapeScale.x);
        maxShapeScale.y = Mathf.Max(minShapeScale.y, maxShapeScale.y);
        maxShapeScale.z = Mathf.Max(minShapeScale.z, maxShapeScale.z);
    }

    private class MaterialFadeTarget
    {
        public MaterialFadeTarget(Material material, int colorPropertyId, Color originalColor)
        {
            Material = material;
            ColorPropertyId = colorPropertyId;
            OriginalColor = originalColor;
        }

        public Material Material { get; }
        public int ColorPropertyId { get; }
        public Color OriginalColor { get; }
        public float CurrentAlpha => Material.GetColor(ColorPropertyId).a;
    }

    private class ParticleFadeTarget
    {
        public ParticleFadeTarget(ParticleSystem particleSystem, ParticleSystem.MinMaxGradient originalStartColor)
        {
            ParticleSystem = particleSystem;
            OriginalStartColor = originalStartColor;
        }

        public ParticleSystem ParticleSystem { get; }
        public ParticleSystem.MinMaxGradient OriginalStartColor { get; }
    }
}
