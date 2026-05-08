using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ConfettiParticleConfigurator : MonoBehaviour
{
    [SerializeField] private Material confettiMaterial;
    [SerializeField] private Mesh confettiMesh;
    [SerializeField] private bool configureOnAwake = true;

    private void Awake()
    {
        if (configureOnAwake)
        {
            Configure();
        }
    }

    private void Reset()
    {
        Configure();
    }

    [ContextMenu("Configure Confetti Particles")]
    public void Configure()
    {
        ParticleSystem particles = GetComponent<ParticleSystem>();
        ConfigureMain(particles);
        ConfigureEmission(particles);
        ConfigureShape(particles);
        ConfigureVelocityOverLifetime(particles);
        ConfigureLimitVelocityOverLifetime(particles);
        ConfigureForceOverLifetime(particles);
        ConfigureColorOverLifetime(particles);
        ConfigureSizeOverLifetime(particles);
        ConfigureRotationOverLifetime(particles);
        ConfigureNoise(particles);
        ConfigureCollision(particles);
        ConfigureRenderer(particles);
    }

    private static void ConfigureMain(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.loop = false;
        main.prewarm = false;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.1f, 0.1f),
            new Color(0.1f, 0.8f, 1f)
        );
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 1f;
        main.maxParticles = 800;
        main.playOnAwake = false;
    }

    private static void ConfigureEmission(ParticleSystem particles)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 250, 400, 1, 0.01f)
        });
    }

    private static void ConfigureShape(ParticleSystem particles)
    {
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.randomDirectionAmount = 1f;
        shape.sphericalDirectionAmount = 1f;
    }

    private static void ConfigureVelocityOverLifetime(ParticleSystem particles)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
    }

    private static void ConfigureLimitVelocityOverLifetime(ParticleSystem particles)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particles.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = 5f;
        limitVelocity.dampen = 0.25f;
    }

    private static void ConfigureForceOverLifetime(ParticleSystem particles)
    {
        ParticleSystem.ForceOverLifetimeModule force = particles.forceOverLifetime;
        force.enabled = true;
        force.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        force.y = -0.3f;
        force.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        force.randomized = true;
    }

    private static void ConfigureColorOverLifetime(ParticleSystem particles)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.1f, 0.1f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.05f), 0.25f),
                new GradientColorKey(new Color(0.1f, 0.8f, 1f), 0.5f),
                new GradientColorKey(new Color(0.3f, 1f, 0.2f), 0.7f),
                new GradientColorKey(new Color(0.9f, 0.2f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void ConfigureSizeOverLifetime(ParticleSystem particles)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0.2f)
        );

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static void ConfigureRotationOverLifetime(ParticleSystem particles)
    {
        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);
        rotation.y = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);
        rotation.z = new ParticleSystem.MinMaxCurve(-720f * Mathf.Deg2Rad, 720f * Mathf.Deg2Rad);
    }

    private static void ConfigureNoise(ParticleSystem particles)
    {
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        noise.frequency = 0.8f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;
    }

    private static void ConfigureCollision(ParticleSystem particles)
    {
        ParticleSystem.CollisionModule collision = particles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.dampen = 0.4f;
        collision.bounce = 0.3f;
        collision.lifetimeLoss = 0.2f;
    }

    private void ConfigureRenderer(ParticleSystem particles)
    {
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = confettiMesh != null ? confettiMesh : CreateQuadMesh();
        particleRenderer.sharedMaterial = confettiMaterial != null ? confettiMaterial : CreateDefaultConfettiMaterial();
        particleRenderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 0.5f;
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "RuntimeConfettiQuad";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Material CreateDefaultConfettiMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.name = "RuntimeConfettiUnlit";
        material.color = Color.white;
        return material;
    }
}
