using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Raycast")]
    public Camera playerCamera;
    public float rayDistance = 100f;

    [Header("Circle")]
    public int segments = 64;
    public float normalRadius = 0.015f;
    public float targetRadius = 0.04f;
    public float lerpSpeed = 12f;
    [Header("UI")]
    public bool useUI = true;
    public float normalRadiusPixels = 10f;
    public float targetRadiusPixels = 40f;
    public float uiLerpSpeed = 12f;
    [Range(0f, 0.9f)]
    public float ringInnerFraction = 0.55f;

    private LineRenderer lineRenderer;
    private float currentRadius;
    // UI runtime
    private UnityEngine.UI.RawImage uiRawImage;
    private RectTransform uiParent;
    private Texture2D uiTexture;
    private float currentOuterPixels;
    private float currentInnerPixels;
    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = false;
        lineRenderer.startWidth = 0.003f;
        lineRenderer.endWidth = 0.003f;
        if (lineRenderer.sharedMaterial == null)
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        segments = Mathf.Max(3, segments);
        lineRenderer.positionCount = segments + 1;

        currentRadius = normalRadius;

        DrawCircle(currentRadius);

        if (useUI)
            CreateUICrosshair();
    }

    void Update()
    {
        if (playerCamera == null)
            return;

        bool lookingAtInteractable = false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            if (hit.collider.CompareTag("MathBlock") || hit.collider.CompareTag("Operator"))
            {
                lookingAtInteractable = true;
            }
        }

        float targetRadiusValue = lookingAtInteractable ? targetRadius : normalRadius;

        currentRadius = Mathf.Lerp(currentRadius, targetRadiusValue, Time.deltaTime * lerpSpeed);

        DrawCircle(currentRadius);
    }

    void DrawCircle(float radius)
    {
        int count = Mathf.Max(3, segments);
        for (int i = 0; i <= count; i++)
        {
            float angle = (float)i / count * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    void CreateUICrosshair()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("No Canvas found in scene - UI crosshair won't be created.");
            useUI = false;
            return;
        }

        GameObject parent = new GameObject("CrosshairUI", typeof(RectTransform));
        parent.transform.SetParent(canvas.transform, false);
        uiParent = parent.GetComponent<RectTransform>();
        uiParent.anchorMin = uiParent.anchorMax = new Vector2(0.5f, 0.5f);
        uiParent.anchoredPosition = Vector2.zero;

        int texSize = Mathf.Max(64, Mathf.CeilToInt(targetRadiusPixels * 2f));
        uiParent.sizeDelta = new Vector2(texSize, texSize);

        GameObject rawGO = new GameObject("CrosshairRaw", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        rawGO.transform.SetParent(parent.transform, false);
        RectTransform rt = rawGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = uiParent.sizeDelta;

        uiRawImage = rawGO.GetComponent<UnityEngine.UI.RawImage>();
        uiTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        uiTexture.filterMode = FilterMode.Bilinear;
        ClearTexture(uiTexture);
        uiTexture.Apply();
        uiRawImage.texture = uiTexture;
        uiRawImage.color = Color.white;

        currentOuterPixels = normalRadiusPixels;
        currentInnerPixels = 0f;
        UpdateUITexture();
    }

    void ClearTexture(Texture2D tex)
    {
        Color32[] cols = new Color32[tex.width * tex.height];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(0, 0, 0, 0);
        tex.SetPixels32(cols);
    }

    void UpdateUITexture()
    {
        if (uiTexture == null) return;
        int w = uiTexture.width;
        int h = uiTexture.height;
        float cx = (w - 1) / 2f;
        float cy = (h - 1) / 2f;

        float outer = Mathf.Clamp(currentOuterPixels, 0f, Mathf.Min(w, h) / 2f);
        float inner = Mathf.Clamp(currentInnerPixels, 0f, outer);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= outer && dist >= inner)
                    uiTexture.SetPixel(x, y, Color.white);
                else
                    uiTexture.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }
        uiTexture.Apply();
    }

    void LateUpdate()
    {
        if (!useUI || uiTexture == null || uiRawImage == null) return;

        bool looking = false;
        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                if (hit.collider.CompareTag("MathBlock") || hit.collider.CompareTag("Operator"))
                    looking = true;
            }
        }

        float targetOuter = looking ? targetRadiusPixels : normalRadiusPixels;
        float targetInner = looking ? targetOuter * ringInnerFraction : 0f;

        currentOuterPixels = Mathf.Lerp(currentOuterPixels, targetOuter, Time.deltaTime * uiLerpSpeed);
        currentInnerPixels = Mathf.Lerp(currentInnerPixels, targetInner, Time.deltaTime * uiLerpSpeed);

        UpdateUITexture();
    }
}