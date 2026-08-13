using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Raycast")]
    public Camera playerCamera;
    public float rayDistance = 100f;

    [Header("Dynamic Dot")]
    [SerializeField] private float dotRadius = 3.25f;
    [SerializeField] private float expandedRadius = 9f;
    [SerializeField] private float ringThickness = 1.4f;
    [SerializeField] private float animationSpeed = 14f;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private GameObject crosshairCanvasObject;
    private CircleCrosshairGraphic circleGraphic;
    private Material invertOverlayMaterial;
    private float interactionProgress;

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

        CreateCrosshair();
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

    private void CreateCrosshair()
    {
        crosshairCanvasObject = new GameObject("CrosshairCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        Canvas canvas = crosshairCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = crosshairCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject circleObject = new GameObject("DynamicCircle", typeof(RectTransform), typeof(CircleCrosshairGraphic));
        circleObject.transform.SetParent(crosshairCanvasObject.transform, false);

        RectTransform rect = circleObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localPosition = Vector3.zero;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(expandedRadius * 2f, expandedRadius * 2f);

        circleGraphic = circleObject.GetComponent<CircleCrosshairGraphic>();
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
    }

    private void OnEnable()
    {
        if (crosshairCanvasObject != null)
            crosshairCanvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (crosshairCanvasObject != null)
            crosshairCanvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (crosshairCanvasObject != null)
            Destroy(crosshairCanvasObject);

        if (invertOverlayMaterial != null)
            Destroy(invertOverlayMaterial);
    }
}
