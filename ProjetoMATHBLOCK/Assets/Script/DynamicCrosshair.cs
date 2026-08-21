using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Raycast")]
    public Camera playerCamera;
    public float rayDistance = 100f;

    [Header("Dynamic Dot")]
    [SerializeField] private float dotRadius = 6.5f;
    [SerializeField] private float expandedRadius = 10f;
    [SerializeField] private float ringThickness = 2.2f;
    [SerializeField] private float animationSpeed = 14f;
    [SerializeField] private Color crosshairColor = Color.white;
    [Header("Global HUD")]
    [SerializeField] private RectTransform crosshairLayer;
    [SerializeField] private CircleCrosshairGraphic circleGraphic;

    private Material invertOverlayMaterial;
    private float interactionProgress;
    private bool requestedVisible = true;

    private void Awake()
    {
        LineRenderer legacyLineRenderer = GetComponent<LineRenderer>();
        if (legacyLineRenderer != null)
            legacyLineRenderer.enabled = false;
    }

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        ResolveOrCreateCrosshair();
        UpdateCrosshairShape();
    }

    private void Update()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        bool isTargeting = playerCamera != null && IsLookingAtInteractable();
        float targetProgress = isTargeting ? 1f : 0f;
        float smoothing = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        interactionProgress = Mathf.Lerp(interactionProgress, targetProgress, smoothing);
        UpdateCrosshairShape();
    }

    private void UpdateCrosshairShape()
    {
        if (circleGraphic == null)
            return;

        float easedProgress = Mathf.SmoothStep(0f, 1f, interactionProgress);
        float radius = Mathf.Lerp(dotRadius, expandedRadius, easedProgress);

        // The dot expands first; its center then opens until only a thin ring remains.
        float openingProgress = Mathf.SmoothStep(0.18f, 1f, interactionProgress);
        float finalHoleRadius = Mathf.Max(0f, expandedRadius - ringThickness);
        float holeRadius = Mathf.Lerp(0f, finalHoleRadius, openingProgress);

        circleGraphic.SetShape(radius, holeRadius, crosshairColor);
    }

    private bool IsLookingAtInteractable()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return false;

        return hit.collider.GetComponentInParent<MathBlockValue>() != null
            || hit.collider.GetComponentInParent<opItem>() != null;
    }

    private void ResolveOrCreateCrosshair()
    {
        if (crosshairLayer == null)
        {
            Transform existingLayer = transform.Find("CrosshairLayer");
            if (existingLayer != null)
                crosshairLayer = existingLayer as RectTransform;
        }

        if (crosshairLayer == null)
        {
            GameObject layerObject = new GameObject("CrosshairLayer", typeof(RectTransform));
            layerObject.layer = gameObject.layer;
            crosshairLayer = layerObject.GetComponent<RectTransform>();
            crosshairLayer.SetParent(transform, false);
            crosshairLayer.anchorMin = Vector2.zero;
            crosshairLayer.anchorMax = Vector2.one;
            crosshairLayer.offsetMin = Vector2.zero;
            crosshairLayer.offsetMax = Vector2.zero;
            crosshairLayer.localScale = Vector3.one;
            crosshairLayer.SetAsFirstSibling();
        }

        if (circleGraphic == null)
            circleGraphic = crosshairLayer.GetComponentInChildren<CircleCrosshairGraphic>(true);

        if (circleGraphic == null)
        {
            GameObject circleObject = new GameObject("DynamicCircle", typeof(RectTransform), typeof(CircleCrosshairGraphic));
            circleObject.layer = gameObject.layer;
            circleObject.transform.SetParent(crosshairLayer, false);
            circleGraphic = circleObject.GetComponent<CircleCrosshairGraphic>();
        }

        RectTransform rect = circleGraphic.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localPosition = Vector3.zero;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(expandedRadius * 2f, expandedRadius * 2f);

        circleGraphic.raycastTarget = false;

        Shader invertShader = Shader.Find("UI/MathBlock Invert Overlay");
        if (invertShader != null && invertShader.isSupported)
        {
            invertOverlayMaterial = new Material(invertShader)
            {
                name = "Crosshair Invert Overlay (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            circleGraphic.material = invertOverlayMaterial;
        }

        // O alpha controla a cobertura; o RGB é calculado pelo blend invertido.
        circleGraphic.color = new Color(1f, 1f, 1f, crosshairColor.a);
        crosshairLayer.gameObject.SetActive(requestedVisible);
    }

    public void SetVisible(bool visible)
    {
        requestedVisible = visible;
        if (crosshairLayer != null)
            crosshairLayer.gameObject.SetActive(visible);
    }

    private void OnEnable()
    {
        if (crosshairLayer != null)
            crosshairLayer.gameObject.SetActive(requestedVisible);
    }

    private void OnDisable()
    {
        if (crosshairLayer != null)
            crosshairLayer.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (invertOverlayMaterial != null)
            Destroy(invertOverlayMaterial);
    }
}
