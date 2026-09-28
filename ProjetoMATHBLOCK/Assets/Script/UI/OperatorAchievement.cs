using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OperatorAchievement : MonoBehaviour
{
    private const float SlideDuration = 0.42f;
    private const float VisibleDuration = 3.2f;
    private const float VisibleX = -115f;
    private const float HiddenX = 300f;
    private const float PanelY = -25f;
    private static OperatorAchievement instance;

    private RectTransform panel;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI messageText;
    private Coroutine animationRoutine;

    public static void ShowOperatorUnlocked(GravityInteract.PencilOperator operatorType)
    {
        string message = MobileTouchControls.ShouldShowTouchControls()
            ? $"Você desbloqueou o operador {GetOperatorName(operatorType)}.\nToque no ícone dele para equipá-lo."
            : $"Você desbloqueou o operador {GetOperatorName(operatorType)}.\nAgora você pode selecioná-lo com a tecla {GetOperatorKey(operatorType)}.";
        EnsureInstance().ShowMessage(message);
    }

    public static void ShowLockedOperator()
    {
        EnsureInstance().ShowMessage("Você não desbloqueou esse atalho.");
    }

    private static OperatorAchievement EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Operator Achievement Panel");
        instance = root.AddComponent<OperatorAchievement>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildInterface();
    }

    private void BuildInterface()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1900;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(transform, false);
        panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.sizeDelta = new Vector2(265f, 130f);
        panel.anchoredPosition = new Vector2(HiddenX, PanelY);

        Image background = panelObject.GetComponent<Image>();
        Sprite noteIcon = Resources.Load<Sprite>("HudImages/IconesPapel/NoteIcon");
        if (noteIcon != null)
        {
            background.sprite = noteIcon;
            background.preserveAspect = true;
            background.color = Color.white;
        }
        background.raycastTarget = false;

        canvasGroup = panelObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject textObject = new GameObject("Message Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(55f, 23f);
        textRect.offsetMax = new Vector2(-15f, -22f);

        messageText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset schoolbellFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Schoolbell-Regular SDF");
        if (schoolbellFont != null)
            messageText.font = schoolbellFont;
        messageText.fontSize = 19f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.enableAutoSizing = true;
        messageText.fontSizeMin = 15f;
        messageText.fontSizeMax = 19f;
        messageText.lineSpacing = -4f;
        messageText.enableWordWrapping = true;
        messageText.overflowMode = TextOverflowModes.Truncate;
        messageText.color = new Color(0.02f, 0.11f, 0.13f, 1f);
        messageText.alignment = TextAlignmentOptions.TopLeft;
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.raycastTarget = false;

        Shadow textShadow = textObject.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0.12f, 0.15f, 0.45f);
        textShadow.effectDistance = new Vector2(1.2f, -1.2f);
    }

    private void ShowMessage(string message)
    {
        messageText.text = message;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(AnimateMessage());
    }

    private IEnumerator AnimateMessage()
    {
        yield return Slide(HiddenX, VisibleX, 0f, 1f, SlideDuration);
        yield return new WaitForSecondsRealtime(VisibleDuration);
        yield return Slide(VisibleX, HiddenX, 1f, 0f, SlideDuration);
        animationRoutine = null;
    }

    private IEnumerator Slide(float fromX, float toX, float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), PanelY);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }

        panel.anchoredPosition = new Vector2(toX, PanelY);
        canvasGroup.alpha = toAlpha;
    }

    private static string GetOperatorName(GravityInteract.PencilOperator operatorType)
    {
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                return "de adição (+)";
            case GravityInteract.PencilOperator.Subtraction:
                return "de subtração (-)";
            case GravityInteract.PencilOperator.Multiplication:
                return "de multiplicação (×)";
            case GravityInteract.PencilOperator.Division:
                return "de divisão (÷)";
            default:
                return "desconhecido";
        }
    }

    private static string GetOperatorKey(GravityInteract.PencilOperator operatorType)
    {
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                return "1";
            case GravityInteract.PencilOperator.Subtraction:
                return "2";
            case GravityInteract.PencilOperator.Multiplication:
                return "3";
            case GravityInteract.PencilOperator.Division:
                return "4";
            default:
                return "correspondente";
        }
    }
}
