using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(ParticleSystem))]
public class OperatorAbsorbEffect : MonoBehaviour
{
    private static Texture2D glowTexture;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private Transform target;
    [SerializeField] private float startingAttractionStrength = 3.5f;
    [SerializeField] private float endingAttractionStrength = 18f;
    [SerializeField] private float closeDistance = 0.12f;
    [SerializeField] private float maxLifetimeAfterReach = 0.08f;
    [SerializeField] private float emissionDuration = 0.6f;
    [SerializeField] private bool playOnInit = true;
    [SerializeField] private Sprite particleSprite;

    private ParticleSystem.Particle[] particleBuffer;
    private bool initialized;
    private float startedAt;

    private void Awake()
    {
        if (particles == null)
        {
            particles = GetComponent<ParticleSystem>();
        }

        ConfigureParticleSystem(Color.white);
    }

    private void Start()
    {
        if (target == null)
        {
            target = ResolveFallbackTarget();
        }

        if (!initialized)
        {
            Init(target, Color.white);
        }
    }

    private void Update()
    {
        if (particles == null)
            return;

        if (target != null)
        {
            AttractParticlesToTarget();
        }

        if (initialized && !particles.IsAlive(true))
        {
            Destroy(gameObject);
        }
    }

    public void Init(Transform newTarget)
    {
        Init(newTarget, Color.white);
    }

    public void Init(Transform newTarget, Color color)
    {
        Init(newTarget, color, particleSprite);
    }

    public void Init(Transform newTarget, Color color, Sprite sprite)
    {
        target = newTarget != null ? newTarget : ResolveFallbackTarget();
        particleSprite = sprite;
        ConfigureParticleSystem(color);
        initialized = true;
        startedAt = Time.time;

        if (playOnInit && particles != null)
        {
            particles.Play(true);
        }
    }

    public void ApplyColor(Color color)
    {
        ConfigureParticleSystem(color);
    }

    private void AttractParticlesToTarget()
    {
        int maxParticles = particles.main.maxParticles;
        if (particleBuffer == null || particleBuffer.Length < maxParticles)
        {
            particleBuffer = new ParticleSystem.Particle[maxParticles];
        }

        int particleCount = particles.GetParticles(particleBuffer);
        Vector3 targetPosition = target.position;
        float effectAge = Mathf.Max(0f, Time.time - startedAt);
        float accelerationProgress = Mathf.Clamp01(effectAge / 1.15f);
        float easedProgress = accelerationProgress * accelerationProgress;
        float currentAttractionStrength = Mathf.Lerp(startingAttractionStrength, endingAttractionStrength, easedProgress);
        float attractionStep = currentAttractionStrength * Time.deltaTime;

        for (int i = 0; i < particleCount; i++)
        {
            Vector3 particlePosition = particleBuffer[i].position;
            Vector3 toTarget = targetPosition - particlePosition;
            float distance = toTarget.magnitude;

            if (distance <= closeDistance)
            {
                particleBuffer[i].remainingLifetime = Mathf.Min(
                    particleBuffer[i].remainingLifetime,
                    maxLifetimeAfterReach
                );
                particleBuffer[i].position = Vector3.Lerp(particlePosition, targetPosition, 0.65f);
                continue;
            }

            float lerpAmount = Mathf.Clamp01(attractionStep / Mathf.Max(distance, 0.001f));
            particleBuffer[i].position = Vector3.Lerp(particlePosition, targetPosition, lerpAmount);
        }

        particles.SetParticles(particleBuffer, particleCount);
    }

    private void ConfigureParticleSystem(Color baseColor)
    {
        if (particles == null)
            return;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = emissionDuration;
        main.loop = false;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor = baseColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 110;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 7, 10, 5, emissionDuration / 5f)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.28f;
        shape.radiusThickness = 1f;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        ParticleSystem.ForceOverLifetimeModule forceOverLifetime = particles.forceOverLifetime;
        forceOverLifetime.enabled = false;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.1f);
        noise.frequency = 1.25f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.3f);
        noise.damping = true;

        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particles.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = new ParticleSystem.MinMaxCurve(2.8f);
        limitVelocity.dampen = 0.22f;

        ConfigureColorOverLifetime(baseColor);
        ConfigureSizeOverLifetime();
        ConfigureTrails(baseColor);
        ConfigureSpriteSheet();
        ConfigureRenderer(baseColor);
    }

    private void ConfigureColorOverLifetime(Color baseColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.Lerp(baseColor, Color.white, 0.58f), 0.18f),
                new GradientColorKey(Color.Lerp(baseColor, Color.white, 0.25f), 0.72f),
                new GradientColorKey(baseColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.72f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private void ConfigureSizeOverLifetime()
    {
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.14f, 1f),
            new Keyframe(0.42f, 0.78f),
            new Keyframe(0.62f, 0.94f),
            new Keyframe(0.78f, 0.68f),
            new Keyframe(1f, 0f)
        );

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    private void ConfigureTrails(Color baseColor)
    {
        ParticleSystem.TrailModule trails = particles.trails;
        trails.enabled = true;
        trails.ratio = 0.35f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.2f);
        trails.minVertexDistance = 0.025f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(new Keyframe(0f, 0.025f), new Keyframe(1f, 0f))
        );

        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[]
            {
                new GradientColorKey(baseColor, 0f),
                new GradientColorKey(baseColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.38f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(trailGradient);
    }

    private void ConfigureRenderer(Color baseColor)
    {
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer == null)
            return;

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingFudge = 1f;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        Material material = CreateTransparentParticleMaterial("OperatorAbsorbParticleMaterial", baseColor);
        if (material != null)
        {
            particleRenderer.sharedMaterial = material;
            particleRenderer.trailMaterial = material;
        }

        if (particleRenderer.sharedMaterial != null)
        {
            if (particleRenderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                particleRenderer.sharedMaterial.SetColor("_BaseColor", baseColor);
            }

            if (particleRenderer.sharedMaterial.HasProperty("_Color"))
            {
                particleRenderer.sharedMaterial.SetColor("_Color", baseColor);
            }
        }
    }

    private void ConfigureSpriteSheet()
    {
        ParticleSystem.TextureSheetAnimationModule textureSheet = particles.textureSheetAnimation;
        textureSheet.enabled = false;
    }

    private static void ApplySpriteToMaterial(Material material, Sprite sprite)
    {
        if (material == null || sprite == null || sprite.texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", sprite.texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", sprite.texture);
        }
    }

    private static Material CreateTransparentParticleMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        ApplyTextureToMaterial(material, GetGlowTexture());
        ConfigureTransparentBlend(material);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private static void ConfigureTransparentBlend(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 2f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.One);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_ALPHABLEND_ON");
    }

    private static void ApplyTextureToMaterial(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    private static Texture2D GetGlowTexture()
    {
        if (glowTexture != null)
            return glowTexture;

        const int size = 128;
        glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "OperatorSoftGlow",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - normalizedDistance), 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        glowTexture.SetPixels(pixels);
        glowTexture.Apply(false, true);
        return glowTexture;
    }

    private Transform ResolveFallbackTarget()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return null;

        GameObject fallbackObject = new GameObject("OperatorAbsorbFallbackTarget");
        fallbackObject.transform.SetParent(mainCamera.transform, false);
        fallbackObject.transform.localPosition = new Vector3(0f, -0.55f, 0.45f);
        fallbackObject.transform.localRotation = Quaternion.identity;
        Destroy(fallbackObject, 1.5f);
        return fallbackObject.transform;
    }
}
