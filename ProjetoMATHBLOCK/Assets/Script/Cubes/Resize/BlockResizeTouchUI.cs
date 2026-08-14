using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlockResizeTouchUI : MonoBehaviour
{
    private BlockResizeController controller;
    private Canvas canvas;
    private Button stretchButton;
    private Button confirmButton;
    private Button cancelButton;
    private readonly List<GameObject> hiddenHudRoots = new List<GameObject>();
    private float nextTargetCheck;

    private void Awake()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            enabled = false;
            return;
        }

        controller = GetComponent<BlockResizeController>();
        CreateInterface();
    }

    private void OnEnable()
    {
        if (controller != null)
            controller.ResizeModeChanged += HandleResizeModeChanged;
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.ResizeModeChanged -= HandleResizeModeChanged;
        RestoreHud();
    }

    private void Update()
    {
        if (controller == null || controller.State != BlockResizeInteractionState.Idle)
            return;
        if (Time.unscaledTime < nextTargetCheck)
            return;

        nextTargetCheck = Time.unscaledTime + 0.1f;
        stretchButton.gameObject.SetActive(controller.HasAvailableResizeTarget());
    }

    private void HandleResizeModeChanged(bool active)
    {
        stretchButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(active);
        cancelButton.gameObject.SetActive(active);
        if (active)
            HideGameplayHud();
        else
            RestoreHud();
    }

    private void CreateInterface()
    {
        GameObject canvasObject = new GameObject("Special Block Touch Controls", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        stretchButton = CreateButton("ESTICAR", new Vector2(1f, 0.5f), new Vector2(-150f, -120f), new Vector2(224f, 76f));
        stretchButton.onClick.AddListener(() => controller.TryHandleResizeTouchButton());
        stretchButton.gameObject.SetActive(false);

        confirmButton = CreateButton("CONFIRMAR", new Vector2(1f, 0f), new Vector2(-150f, 46f), new Vector2(238f, 72f));
        confirmButton.onClick.AddListener(controller.ConfirmResizeSession);
        confirmButton.gameObject.SetActive(false);

        cancelButton = CreateButton("CANCELAR", new Vector2(0f, 0f), new Vector2(150f, 46f), new Vector2(224f, 72f));
        cancelButton.onClick.AddListener(controller.CancelResizeSession);
        cancelButton.gameObject.SetActive(false);
    }

    private Button CreateButton(string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        HudToonStyler.ApplyButtonStyle(button);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 27;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private void HideGameplayHud()
    {
        hiddenHudRoots.Clear();
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas targetCanvas in canvases)
        {
            if (targetCanvas == null || targetCanvas == canvas || !targetCanvas.gameObject.activeInHierarchy)
                continue;
            string lowerName = targetCanvas.name.ToLowerInvariant();
            if (!lowerName.Contains("hud") && !lowerName.Contains("touch"))
                continue;
            hiddenHudRoots.Add(targetCanvas.gameObject);
            targetCanvas.gameObject.SetActive(false);
        }
    }

    private void RestoreHud()
    {
        foreach (GameObject hudRoot in hiddenHudRoots)
            if (hudRoot != null)
                hudRoot.SetActive(true);
        hiddenHudRoots.Clear();
    }
}
