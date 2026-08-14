using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class BlockResizeParticleEffect : MonoBehaviour
{
    private const float BoundsPadding = 0.16f;
    private const float FadeOutSafetyTime = 0.15f;

    private ParticleSystem particles;
    private ParticleSystemRenderer particleRenderer;
    private ResizableBlock targetBlock;
    private Material runtimeMaterial;
    private Vector3 lastEmissionSize = Vector3.zero;

    public ParticleSystem Particles => particles;
    public ResizableBlock TargetBlock => targetBlock;
    public Vector3 EmissionSize => lastEmissionSize;

    public static BlockResizeParticleEffect Create(
        ResizableBlock target,
        Texture2D starTexture,
        Color tint)
    {
        if (target == null || starTexture == null)
            return null;

        GameObject effectObject = new GameObject("BlockResizeParticles");
        effectObject.AddComponent<ParticleSystem>();
        BlockResizeParticleEffect effect = effectObject.AddComponent<BlockResizeParticleEffect>();
        if (effect.Initialize(target, starTexture, tint))
            return effect;

        Destroy(effectObject);
        return null;
    }

    public bool Initialize(ResizableBlock target, Texture2D starTexture, Color tint)
    {
        ResolveReferences();
        if (target == null || starTexture == null || particles == null || particleRenderer == null)
            return false;

        targetBlock = target;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticles(tint);

        runtimeMaterial = CreateParticleMaterial(starTexture);
        if (runtimeMaterial == null)
        {
            Debug.LogError("Nao foi encontrado um shader transparente para as particulas do resize.", this);
            return false;
        }

        particleRenderer.sharedMaterial = runtimeMaterial;
        RefreshBounds();
        particles.Play(true);
        return true;
    }

    public void RefreshBounds()
    {
        if (targetBlock == null)
            return;

        transform.SetPositionAndRotation(targetBlock.WorldCenter, targetBlock.transform.rotation);
        transform.localScale = Vector3.one;

        lastEmissionSize = new Vector3(
            targetBlock.GetWorldSize(0) + BoundsPadding,
            targetBlock.GetWorldSize(1) + BoundsPadding,
            targetBlock.GetWorldSize(2) + BoundsPadding
        );

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.scale = lastEmissionSize;
    }

    public void StopAndFadeOut()
    {
        targetBlock = null;
        if (particles == null)
        {
            Destroy(gameObject);
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        float remainingLifetime = particles.main.startLifetime.constantMax + FadeOutSafetyTime;
        Destroy(gameObject, remainingLifetime);
    }

    private void LateUpdate()
    {
        if (targetBlock != null)
            RefreshBounds();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void ResolveReferences()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();
        if (particleRenderer == null)
            particleRenderer = GetComponent<ParticleSystemRenderer>();
    }

    private void ConfigureParticles(Color tint)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.76f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = tint;
        main.gravityModifier = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 64;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 6f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, -0.45f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.3f, 1.3f);

        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.8f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0.65f)
        );
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 0.25f;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }

    private static Material CreateParticleMaterial(Texture2D starTexture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "RuntimeResizeStarMaterial",
            mainTexture = starTexture,
            color = Color.white,
            hideFlags = HideFlags.DontSave
        };
        return material;
    }
}
