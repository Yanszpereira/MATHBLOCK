using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public sealed class MathBlockBeamController : MonoBehaviour
{
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

    private Vector3[] points;
    private Transform cachedHeldBlock;
    private ResizableBlock cachedResizableBlock;
    private BoxCollider cachedCollider;
    private Renderer cachedRenderer;
    private PencilTipOperatorColor operatorColorSource;
    private GravityInteract.PencilOperator cachedOperator;

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

        if (heldBlock == null || currentOperator == GravityInteract.PencilOperator.None)
        {
            DisableBeam();
            return;
        }

        if (heldBlock != cachedHeldBlock)
            CacheHeldBlock(heldBlock);

        if (currentOperator != cachedOperator)
        {
            cachedOperator = currentOperator;
            if (operatorColorSource == null ||
                !operatorColorSource.TryGetOperatorColor(currentOperator, out Color color))
            {
                DisableBeam();
                return;
            }

            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        Vector3 start = beamOrigin.position;
        Vector3 end = GetHeldBlockCenter();
        Vector3 midpoint = (start + end) * 0.5f;
        Vector3 control = midpoint + beamOrigin.forward * curveOffset;

        for (int index = 0; index < points.Length; index++)
        {
            float t = index / (float)(points.Length - 1);
            float inverseT = 1f - t;
            points[index] = inverseT * inverseT * start
                + 2f * inverseT * t * control
                + t * t * end;
        }

        lineRenderer.SetPositions(points);
        lineRenderer.enabled = true;
    }

    private void CacheHeldBlock(Transform heldBlock)
    {
        cachedHeldBlock = heldBlock;
        cachedResizableBlock = heldBlock != null
            ? heldBlock.GetComponent<ResizableBlock>()
            : null;
        cachedCollider = heldBlock != null
            ? heldBlock.GetComponent<BoxCollider>()
            : null;
        cachedRenderer = heldBlock != null
            ? heldBlock.GetComponentInChildren<Renderer>(true)
            : null;
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
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        if (cachedHeldBlock == null)
        {
            cachedResizableBlock = null;
            cachedCollider = null;
            cachedRenderer = null;
        }
    }

    private void OnDisable()
    {
        DisableBeam();
    }
}
