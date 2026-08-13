using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(ParticleSystem))]
public class OperatorAbsorbWaveEffect : MonoBehaviour
{
    private static Texture2D circleTexture;

    [SerializeField] private ParticleSystem particles;
    [SerializeField] private float duration = 0.7f;
    [SerializeField] private float startRadius = 0.12f;
    [SerializeField] private float outwardSpeed = 1.15f;
    [SerializeField] private int particleCount = 48;
    [SerializeField] private float particleSize = 0.115f;
    [SerializeField] private Sprite particleSprite;

    private bool initialized;
    private Vector3 impactDirection = Vector3.up;

    private void Awake()
    {
        if (particles == null)
        {
            particles = GetComponent<ParticleSystem>();
        }

        Configure(Color.white);
    }

    private void Update()
    {
        if (initialized && particles != null && !particles.IsAlive(true))
        {
            Destroy(gameObject);
        }
    }

    public void Init(Color color)
    {
        Init(color, particleSprite, Vector3.up);
    }

    public void Init(Color color, Sprite sprite)
    {
        Init(color, sprite, Vector3.up);
    }

    public void Init(Color color, Vector3 direction)
    {
        Init(color, particleSprite, direction);
    }

    public void Init(Color color, Sprite sprite, Vector3 direction)
    {
        particleSprite = sprite;
        impactDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.up;
        Configure(color);
        initialized = true;
        particles.Play(true);
    }

    private void Configure(Color color)
    {
        if (particles == null)
            return;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.loop = false;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(duration * 0.72f, duration);
        main.startSpeed = new ParticleSystem.MinMaxCurve(outwardSpeed * 0.55f, outwardSpeed * 0.85f);
        main.startSize = particleSize;
        main.startColor = WithAlpha(Color.Lerp(color, Color.white, 0.3f), 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = particleCount + 8;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)particleCount)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = startRadius;
        shape.radiusThickness = 1f;
        shape.alignToDirection = false;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(impactDirection.x * outwardSpeed * 0.45f);
        velocity.y = new ParticleSystem.MinMaxCurve(impactDirection.y * outwardSpeed * 0.45f);
        velocity.z = new ParticleSystem.MinMaxCurve(impactDirection.z * outwardSpeed * 0.45f);

        ParticleSystem.ForceOverLifetimeModule force = particles.forceOverLifetime;
        force.enabled = false;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = false;

        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particles.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = new ParticleSystem.MinMaxCurve(outwardSpeed * 0.8f);
        limitVelocity.dampen = 0.28f;

        ConfigureColorOverLifetime(color);
        ConfigureSizeOverLifetime();
        ConfigureSpriteSheet();
        ConfigureRenderer(WithAlpha(color, 0.32f));
    }

    private void ConfigureColorOverLifetime(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.Lerp(color, Color.white, 0.62f), 0.2f),
                new GradientColorKey(Color.Lerp(color, Color.white, 0.28f), 0.75f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.58f, 0.12f),
                new GradientAlphaKey(0.25f, 0.55f),
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
            new Keyframe(0f, 0.1f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.38f, 0.76f),
            new Keyframe(0.56f, 0.94f),
            new Keyframe(0.74f, 0.64f),
            new Keyframe(1f, 0f)
        );

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    private void ConfigureRenderer(Color color)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingFudge = 1.2f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        renderer.sharedMaterial = CreateParticleMaterial(color, particleSprite);

        if (renderer.sharedMaterial == null)
            return;

        if (renderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            renderer.sharedMaterial.SetColor("_BaseColor", color);
        }

        if (renderer.sharedMaterial.HasProperty("_Color"))
        {
            renderer.sharedMaterial.SetColor("_Color", color);
        }
    }

    private void ConfigureSpriteSheet()
    {
        ParticleSystem.TextureSheetAnimationModule textureSheet = particles.textureSheetAnimation;
        textureSheet.enabled = false;
    }

    private static Material CreateParticleMaterial(Color color, Sprite sprite)
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
            name = "OperatorAbsorbWaveCircleMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        ConfigureTransparentBlend(material);

        Texture2D texture = GetCircleTexture();
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

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

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Texture2D GetCircleTexture()
    {
        if (circleTexture != null)
            return circleTexture;

        const int textureSize = 128;
        circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "OperatorAbsorbWaveCircleTexture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.5f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDistance = distance / radius;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - normalizedDistance), 2.2f);
                pixels[(y * textureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        circleTexture.SetPixels(pixels);
        circleTexture.Apply(false, true);
        return circleTexture;
    }
}
