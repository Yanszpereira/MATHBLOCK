using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public class CrosshairController : MonoBehaviour
{
    [Header("Raycast")]
    public Camera playerCamera;
    public float rayDistance = 100f;

    [Header("Crosshair")]
    [SerializeField] private float dotRadius = 3.25f;
    [SerializeField] private float expandedRadius = 9f;
    [SerializeField] private float ringThickness = 1.4f;
    [SerializeField] private float animationSpeed = 14f;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private GameObject crosshairCanvasObject;
    private CircleCrosshairGraphic circleGraphic;
    private float interactionProgress;

    private void Awake()
    {
        LineRenderer lr = GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        CreateCrosshair();
        UpdateCrosshairShape();
    }

    private void Update()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        bool isTargeting = playerCamera != null && IsLookingAtInteractable();
        float targetProgress = isTargeting ? 1f : 0f;
        float smoothing = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        interactionProgress = Mathf.Lerp(interactionProgress, targetProgress, smoothing);
        UpdateCrosshairShape();
    }

    private void UpdateCrosshairShape()
    {
        if (circleGraphic == null) return;

        float eased = Mathf.SmoothStep(0f, 1f, interactionProgress);
        float radius = Mathf.Lerp(dotRadius, expandedRadius, eased);

        float openingProgress = Mathf.SmoothStep(0.18f, 1f, interactionProgress);
        float finalHole = Mathf.Max(0f, expandedRadius - ringThickness);
        float holeRadius = Mathf.Lerp(0f, finalHole, openingProgress);

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

        GameObject circleObject = new GameObject("CrosshairCircle", typeof(RectTransform), typeof(CircleCrosshairGraphic));
        circleObject.transform.SetParent(crosshairCanvasObject.transform, false);

        RectTransform rect = circleObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localPosition = Vector3.zero;
        rect.localScale = Vector3.one;

        circleGraphic = circleObject.GetComponent<CircleCrosshairGraphic>();
        circleGraphic.raycastTarget = false;
        circleGraphic.color = crosshairColor;
    }

    private void OnEnable()
    {
        if (crosshairCanvasObject != null) crosshairCanvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (crosshairCanvasObject != null) crosshairCanvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (crosshairCanvasObject != null) Destroy(crosshairCanvasObject);
    }
}
