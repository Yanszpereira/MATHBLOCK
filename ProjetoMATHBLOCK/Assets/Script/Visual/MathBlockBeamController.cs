using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public sealed class MathBlockBeamController : MonoBehaviour
{
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlinePixelsId = Shader.PropertyToID("_OutlinePixels");

    [Header("Referências")]
    [SerializeField] private GravityInteract gravityInteract;
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Curva")]
    [SerializeField, Range(8, 16)] private int pointCount = 12;
    [SerializeField, Min(0f)] private float curveOffset = 0.35f;

    [Header("Largura")]
    [SerializeField, Min(0f)] private float originWidth = 0.07f;
    [SerializeField, Min(0f)] private float targetWidth = 0.04f;

    [Header("Cor sem operador")]
    [SerializeField] private Color noOperatorColor = new Color(0.82f, 0.84f, 0.88f, 1f);

    [Header("Entrada do raio")]
    [SerializeField, Min(0.01f)] private float beamRevealDuration = 0.5f;

    private Vector3[] points;
    private Transform cachedHeldBlock;
    private ResizableBlock cachedResizableBlock;
    private MathBlockValue cachedMathBlockValue;
    private BoxCollider cachedCollider;
    private Renderer cachedRenderer;
    private Renderer[] cachedOutlineRenderers;
    private PencilTipOperatorColor operatorColorSource;
    private GravityInteract.PencilOperator cachedOperator;
    private bool hasAppliedOperatorColor;
    private bool isBeamActive;
    private float beamRevealElapsed;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        pointCount = Mathf.Clamp(pointCount, 8, 16);
        points = new Vector3[pointCount];

        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = pointCount;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.generateLightingData = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.numCapVertices = 3;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.widthMultiplier = originWidth;
        lineRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, originWidth > 0f ? targetWidth / originWidth : 0f));
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        if (gravityInteract != null)
            operatorColorSource = gravityInteract.GetComponent<PencilTipOperatorColor>();

        if (gravityInteract == null)
            Debug.LogWarning("MathBlockBeamController sem GravityInteract configurado.", this);
        if (beamOrigin == null)
            Debug.LogWarning("MathBlockBeamController sem BeamOrigin configurado.", this);
        if (lineRenderer == null)
            Debug.LogWarning("MathBlockBeamController sem LineRenderer configurado.", this);
        if (operatorColorSource == null)
            Debug.LogWarning("MathBlockBeamController não encontrou PencilTipOperatorColor no PencilGun.", this);
    }

    private void LateUpdate()
    {
        if (lineRenderer == null || gravityInteract == null || beamOrigin == null)
        {
            DisableBeam();
            return;
        }

        Transform heldBlock = gravityInteract.HeldBlock;
        GravityInteract.PencilOperator currentOperator = gravityInteract.EquippedOperator;

        if (heldBlock == null)
        {
            DisableBeam();
            return;
        }

        if (heldBlock != cachedHeldBlock)
        {
            RestoreHeldBlockOutline();
            CacheHeldBlock(heldBlock);
            RestartBeamReveal();
            hasAppliedOperatorColor = false;
        }

        if (!isBeamActive)
            RestartBeamReveal();

        if (!hasAppliedOperatorColor || currentOperator != cachedOperator)
        {
            cachedOperator = currentOperator;
            Color color;
            if (currentOperator == GravityInteract.PencilOperator.None)
            {
                color = noOperatorColor;
            }
            else if (operatorColorSource == null ||
                     !operatorColorSource.TryGetOperatorColor(currentOperator, out color))
            {
                DisableBeam();
                return;
            }

            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            ApplyHeldBlockOutline(color);
            hasAppliedOperatorColor = true;
        }

        cachedMathBlockValue?.SetHeldOutline(lineRenderer.startColor);

        Vector3 start = beamOrigin.position;
        Vector3 end = GetHeldBlockCenter();
        Vector3 midpoint = (start + end) * 0.5f;
        Vector3 control = midpoint + beamOrigin.forward * curveOffset;
        float duration = Mathf.Max(0.01f, beamRevealDuration);
        beamRevealElapsed = Mathf.Min(beamRevealElapsed + Time.deltaTime, duration);
        float revealProgress = Mathf.Clamp01(beamRevealElapsed / duration);

        for (int index = 0; index < points.Length; index++)
        {
            float t = revealProgress * index / (float)(points.Length - 1);
            float inverseT = 1f - t;
            points[index] = inverseT * inverseT * start
                + 2f * inverseT * t * control
                + t * t * end;
        }

        lineRenderer.SetPositions(points);
        lineRenderer.enabled = true;
        isBeamActive = true;
    }

    private void CacheHeldBlock(Transform heldBlock)
    {
        ClearCachedHeldBlockOutline();
        cachedHeldBlock = heldBlock;
        cachedMathBlockValue = heldBlock != null
            ? heldBlock.GetComponent<MathBlockValue>()
            : null;
        cachedResizableBlock = heldBlock != null
            ? heldBlock.GetComponent<ResizableBlock>()
            : null;
        cachedCollider = heldBlock != null
            ? heldBlock.GetComponent<BoxCollider>()
            : null;
        cachedRenderer = heldBlock != null
            ? heldBlock.GetComponentInChildren<Renderer>(true)
            : null;
        cachedOutlineRenderers = heldBlock != null
            ? heldBlock.GetComponentsInChildren<Renderer>(true)
            : null;
    }

    private void ApplyHeldBlockOutline(Color color)
    {
        if (cachedOutlineRenderers == null)
            return;

        color.a = 1f;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        for (int rendererIndex = 0; rendererIndex < cachedOutlineRenderers.Length; rendererIndex++)
        {
            Renderer target = cachedOutlineRenderers[rendererIndex];
            if (!TryGetOutlineMaterial(target, out _))
                continue;

            target.GetPropertyBlock(properties);
            properties.SetColor(OutlineColorId, color);
            properties.SetFloat(OutlinePixelsId, cachedResizableBlock != null ? 1.75f : 2.5f);
            target.SetPropertyBlock(properties);
            properties.Clear();
        }
    }

    private void RestoreHeldBlockOutline()
    {
        if (cachedOutlineRenderers == null)
            return;

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        for (int rendererIndex = 0; rendererIndex < cachedOutlineRenderers.Length; rendererIndex++)
        {
            Renderer target = cachedOutlineRenderers[rendererIndex];
            if (!TryGetOutlineMaterial(target, out Material material))
                continue;

            target.GetPropertyBlock(properties);
            properties.SetColor(OutlineColorId, material.GetColor(OutlineColorId));
            properties.SetFloat(OutlinePixelsId, 0f);
            target.SetPropertyBlock(properties);
            properties.Clear();
        }
    }

    private static bool TryGetOutlineMaterial(Renderer target, out Material outlineMaterial)
    {
        outlineMaterial = null;
        if (target == null)
            return false;

        Material[] materials = target.sharedMaterials;
        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];
            if (material == null || !material.HasProperty(OutlineColorId))
                continue;

            outlineMaterial = material;
            return true;
        }

        return false;
    }

    private Vector3 GetHeldBlockCenter()
    {
        if (cachedResizableBlock != null)
            return cachedResizableBlock.WorldCenter;

        if (cachedCollider != null)
            return cachedCollider.bounds.center;

        if (cachedRenderer != null)
            return cachedRenderer.bounds.center;

        return cachedHeldBlock != null ? cachedHeldBlock.position : transform.position;
    }

    private void DisableBeam()
    {
        ClearCachedHeldBlockOutline();

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        RestoreHeldBlockOutline();
        isBeamActive = false;
        beamRevealElapsed = 0f;
        cachedHeldBlock = null;
<<<<<<< HEAD
        cachedResizableBlock = null;
        cachedCollider = null;
        cachedRenderer = null;
        cachedOutlineRenderers = null;
        hasAppliedOperatorColor = false;
=======
        cachedMathBlockValue = null;
        cachedResizableBlock = null;
        cachedCollider = null;
        cachedRenderer = null;
    }

    private void ClearCachedHeldBlockOutline()
    {
        if (cachedMathBlockValue != null)
            cachedMathBlockValue.ClearHeldOutline();

        cachedMathBlockValue = null;
>>>>>>> f6d3b363c6be784869f6b52c7564c8fb55016096
    }

    private void RestartBeamReveal()
    {
        isBeamActive = false;
        beamRevealElapsed = 0f;
    }

    private void OnDisable()
    {
        DisableBeam();
    }
}
