using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(ParticleSystem))]
public class OperatorAbsorbEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private Transform target;
    [SerializeField] private float startingAttractionStrength = 3.5f;
    [SerializeField] private float endingAttractionStrength = 18f;
    [SerializeField] private float closeDistance = 0.12f;
    [SerializeField] private float maxLifetimeAfterReach = 0.08f;
    [SerializeField] private float emissionDuration = 0.6f;
    [SerializeField] private bool playOnInit = true;

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
        target = newTarget != null ? newTarget : ResolveFallbackTarget();
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
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
            new ParticleSystem.Burst(0f, 12, 16, 6, emissionDuration / 6f)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        shape.radiusThickness = 1f;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        ParticleSystem.ForceOverLifetimeModule forceOverLifetime = particles.forceOverLifetime;
        forceOverLifetime.enabled = false;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.18f);
        noise.frequency = 1.8f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
        noise.damping = true;

        ConfigureColorOverLifetime(baseColor);
        ConfigureSizeOverLifetime();
        ConfigureTrails(baseColor);
        ConfigureRenderer(baseColor);
    }

    private void ConfigureColorOverLifetime(Color baseColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(baseColor, 0f),
                new GradientColorKey(baseColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.75f),
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
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 1f),
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
        trails.ratio = 0.65f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.32f);
        trails.minVertexDistance = 0.05f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(1f, 0f))
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
                new GradientAlphaKey(0.55f, 0f),
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

        if (particleRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                Material material = new Material(shader)
                {
                    name = "OperatorAbsorbParticleMaterial"
                };
                particleRenderer.sharedMaterial = material;
            }
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
