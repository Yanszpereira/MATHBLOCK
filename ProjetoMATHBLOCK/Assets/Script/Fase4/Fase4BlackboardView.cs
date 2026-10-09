using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Fase4BlackboardView : MonoBehaviour
{
    private const float MinimumAutoSizeFontSize = 1.8f;

    [SerializeField] private Renderer writingSurface;
    [SerializeField] private TMP_FontAsset fontAsset;

    private TMP_Text expressionText;
    private TMP_Text roundText;
    private TMP_Text timerText;
    private TMP_Text errorsText;
    private TMP_Text statusText;
    private TMP_Text feedbackText;
    private TMP_Text transitionText;
    private bool timerAlert;
    private int lastDisplayedSeconds = int.MinValue;
    private float feedbackDuration;
    private Color feedbackColor;
    private Vector3 feedbackBasePosition;
    private Vector3 feedbackRiseDirection;

    private void Awake()
    {
        if (writingSurface == null)
        {
            Transform surface = transform.Find("QuadroNegro_Superficie");
            if (surface != null)
                writingSurface = surface.GetComponent<Renderer>();
        }

        if (writingSurface == null)
        {
            Debug.LogError("Fase4BlackboardView precisa da superfície verde do quadro.", this);
            enabled = false;
            return;
        }

        CreateLabelsIfMissing();
        SetTimer(60f);
        SetErrors(0);
        SetTransitionCountdown(null);
        ShowTimeChange(string.Empty, Color.white);
    }

    private void Update()
    {
        if (timerText != null)
        {
            Color color = timerAlert ? new Color(1f, 0.22f, 0.16f, 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.5f))) : Color.white;
            timerText.color = color;
        }

        if (feedbackText == null || feedbackDuration <= 0f)
            return;

        feedbackDuration -= Time.unscaledDeltaTime;
        float normalized = Mathf.Clamp01(feedbackDuration / 1.35f);
        Color colorWithFade = feedbackColor;
        colorWithFade.a = normalized;
        feedbackText.color = colorWithFade;
        feedbackText.transform.position = feedbackBasePosition + feedbackRiseDirection * (1f - normalized) * 0.42f;
        if (feedbackDuration <= 0f)
            feedbackText.transform.position = feedbackBasePosition;
    }

    public void SetExpression(string value)
    {
        if (expressionText != null)
            expressionText.text = value ?? string.Empty;
    }

    public void SetRound(int currentRound, int totalRounds)
    {
        if (roundText != null)
            roundText.text = $"RODADA {Mathf.Clamp(currentRound, 1, totalRounds)}/{totalRounds}";
    }

    public void SetTimer(float seconds)
    {
        if (timerText == null)
            return;

        timerAlert = seconds <= 15f;
        int roundedUpSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        if (roundedUpSeconds != lastDisplayedSeconds)
        {
            timerText.text = FormatTime(seconds);
            lastDisplayedSeconds = roundedUpSeconds;
        }
        timerText.color = timerAlert ? new Color(1f, 0.22f, 0.16f) : Color.white;
    }

    public void SetErrors(int errorCount)
    {
        if (errorsText == null)
            return;

        int filled = Mathf.Clamp(errorCount, 0, 3);
        errorsText.text = string.Concat(
            filled > 0 ? "<color=#E34B45>●</color>" : "<color=#B8C0C5>●</color>", "   ",
            filled > 1 ? "<color=#E34B45>●</color>" : "<color=#B8C0C5>●</color>", "   ",
            filled > 2 ? "<color=#E34B45>●</color>" : "<color=#B8C0C5>●</color>");
    }

    public void SetStatus(string value)
    {
        if (statusText != null)
            statusText.text = value ?? string.Empty;
    }

    public void SetTransitionCountdown(int? secondsRemaining)
    {
        if (transitionText == null)
            return;

        if (!secondsRemaining.HasValue)
        {
            transitionText.text = string.Empty;
            return;
        }

        transitionText.text = $"PRÓXIMA RODADA EM {Mathf.Clamp(secondsRemaining.Value, 1, 5)}...";
    }

    public void ShowTimeChange(string text, Color color)
    {
        if (feedbackText == null)
            return;

        feedbackText.text = text ?? string.Empty;
        feedbackColor = color;
        feedbackText.color = color;
        feedbackDuration = string.IsNullOrEmpty(text) ? 0f : 1.35f;
        feedbackText.transform.position = feedbackBasePosition;
        feedbackRiseDirection = feedbackText.transform.up;
    }

    public static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void CreateLabelsIfMissing()
    {
        if (TMP_Settings.defaultFontAsset == null)
            TMP_Settings.LoadDefaultSettings();
        TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogError("Fase4BlackboardView não encontrou uma fonte TextMeshPro padrão.", this);
            enabled = false;
            return;
        }

        Transform surface = writingSurface.transform;
        Vector3 center = writingSurface.bounds.center;
        // The imported board's forward axis defines its authored viewing side.
        // In the current scene that is -X; the surface transform's up axis is +X.
        Vector3 frontNormal = transform.forward;
        Transform checkpoint = FindNamedTransform("Arena_Checkpoint_Inicial");
        if (checkpoint != null && Vector3.Dot(frontNormal, checkpoint.position - center) < 0f)
            frontNormal = -frontNormal;

        Vector3 vertical = Vector3.up;
        Quaternion textRotation = CalculateGlyphFrontRotation(frontNormal);
        Vector3 horizontal = textRotation * Vector3.right;
        Vector3 extents = writingSurface.bounds.extents;
        float faceOffset = ProjectAabbHalfExtent(frontNormal, extents) + 0.03f;
        Vector3 origin = center + frontNormal * faceOffset;
        float usableHalfWidth = ProjectAabbHalfExtent(horizontal, extents) * 0.86f;
        float usableHalfHeight = ProjectAabbHalfExtent(vertical, extents) * 0.84f;
        float usableWidth = usableHalfWidth * 2f;
        float usableHeight = usableHalfHeight * 2f;

        expressionText = CreateLabel("Quadro_Fase4_Expressao", font, origin + vertical * usableHalfHeight * 0.46f, textRotation, usableWidth, usableHeight * 0.30f, 1.25f, TextAlignmentOptions.Center, new Color(0.97f, 0.96f, 0.87f));
        roundText = CreateLabel("Quadro_Fase4_Rodada", font, origin - horizontal * usableHalfWidth * 0.52f + vertical * usableHalfHeight * 0.78f, textRotation, usableWidth * 0.46f, usableHeight * 0.24f, 0.75f, TextAlignmentOptions.Left, new Color(0.93f, 0.91f, 0.82f));
        timerText = CreateLabel("Quadro_Fase4_Cronometro", font, origin + horizontal * usableHalfWidth * 0.52f + vertical * usableHalfHeight * 0.78f, textRotation, usableWidth * 0.46f, usableHeight * 0.24f, 0.85f, TextAlignmentOptions.Right, Color.white);
        errorsText = CreateLabel("Quadro_Fase4_Erros", font, origin + vertical * usableHalfHeight * 0.15f, textRotation, usableWidth * 0.45f, usableHeight * 0.06f, 0.72f, TextAlignmentOptions.Center, Color.white);
        statusText = CreateLabel("Quadro_Fase4_Status", font, origin - vertical * usableHalfHeight * 0.15f, textRotation, usableWidth, usableHeight * 0.17f, 0.95f, TextAlignmentOptions.Center, new Color(0.96f, 0.94f, 0.84f), true);
        feedbackText = CreateLabel("Quadro_Fase4_AjusteTempo", font, origin - vertical * usableHalfHeight * 0.50f, textRotation, usableWidth * 0.42f, usableHeight * 0.08f, 0.9f, TextAlignmentOptions.Center, Color.white);
        feedbackBasePosition = feedbackText.transform.position;
        transitionText = CreateLabel("Quadro_Fase4_Transicao", font, origin - vertical * usableHalfHeight * 0.78f, textRotation, usableWidth, usableHeight * 0.16f, 0.78f, TextAlignmentOptions.Center, new Color(0.98f, 0.91f, 0.55f), true);
    }

    internal static Quaternion CalculateGlyphFrontRotation(Vector3 boardFrontNormal)
    {
        if (boardFrontNormal.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        // 3D TMP glyphs face local -Z. This 180-degree yaw turns their visible
        // face toward the board's front while preserving global vertical +Y.
        return Quaternion.LookRotation(-boardFrontNormal.normalized, Vector3.up);
    }

    private static float ProjectAabbHalfExtent(Vector3 axis, Vector3 extents)
    {
        Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        return Vector3.Dot(absoluteAxis, extents);
    }

    private TMP_Text CreateLabel(string objectName, TMP_FontAsset font, Vector3 position, Quaternion rotation, float width, float height, float worldGlyphHeight, TextAlignmentOptions alignment, Color color, bool wrap = false)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.SetPositionAndRotation(position, rotation);
        float parentScale = Mathf.Max(0.001f, transform.lossyScale.x);
        float localScale = worldGlyphHeight / 0.36f / parentScale;
        labelObject.transform.localScale = Vector3.one * localScale;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.font = font;
        label.fontSize = 36f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        // With the current authored text rectangles, a minimum font size of
        // 18 prevents TMP Auto Size from fitting the text; Truncate then emits
        // an empty glyph mesh. Allow smaller sizes so each label can fit.
        label.fontSizeMin = MinimumAutoSizeFontSize;
        label.fontSizeMax = label.fontSize;
        label.richText = true;
        label.overflowMode = TextOverflowModes.Truncate;
        label.rectTransform.sizeDelta = new Vector2(width / (parentScale * localScale), height / (parentScale * localScale));
        label.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        label.renderer.receiveShadows = false;
        return label;
    }

    private Transform FindNamedTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null && found.scene == gameObject.scene ? found.transform : null;
    }
}
