using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlockResizeTouchUI : MonoBehaviour
{
    private const string MobileGroupName = "BotoesMobile";
    private const string OperatorsGroupName = "Operadores";
    private const string MenuButtonName = "MenuButton";
    private const string StretchButtonName = "Stretch";
    private const string ExitButtonName = "ExitStretch";

    private readonly Dictionary<GameObject, bool> previousVisibility = new Dictionary<GameObject, bool>();
    private BlockResizeController controller;
    private Transform mobileGroup;
    private GameObject operatorsGroup;
    private Button stretchButton;
    private Button exitButton;
    private float nextTargetCheck;
    private bool resizeHudActive;

    private void Awake()
    {
        if (!MobileTouchControls.ShouldShowTouchControls())
        {
            enabled = false;
            return;
        }
        controller = GetComponent<BlockResizeController>();
    }

    private void Start() => EnsureInterface();

    private void OnEnable()
    {
        controller ??= GetComponent<BlockResizeController>();
        if (controller != null)
            controller.ResizeModeChanged += HandleResizeModeChanged;
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.ResizeModeChanged -= HandleResizeModeChanged;
        RestoreGameplayHud();
    }

    private void Update()
    {
        if (controller == null)
            return;
        if (stretchButton == null || exitButton == null)
        {
            EnsureInterface();
            return;
        }
        if (controller.State != BlockResizeInteractionState.Idle || Time.unscaledTime < nextTargetCheck)
            return;

        nextTargetCheck = Time.unscaledTime + 0.1f;
        stretchButton.gameObject.SetActive(controller.HasResizeTargetAtCameraCenter());
        exitButton.gameObject.SetActive(false);
    }

    private void EnsureInterface()
    {
        mobileGroup ??= FindSceneTransform(MobileGroupName);
        if (mobileGroup == null)
            return;

        operatorsGroup ??= FindSceneTransform(OperatorsGroupName)?.gameObject;
        stretchButton ??= FindDirectButton(StretchButtonName);
        exitButton ??= FindDirectButton(ExitButtonName);

        if (stretchButton == null)
        {
            stretchButton = CreateHudButton(StretchButtonName,
                Resources.Load<Sprite>("UI/Stretch/StretchMode"),
                new Vector2(-585f, 320f), new Vector2(150f, 150f));
            stretchButton.onClick.AddListener(BeginStretchMode);
        }
        if (exitButton == null)
        {
            exitButton = CreateHudButton(ExitButtonName,
                Resources.Load<Sprite>("UI/Stretch/ExitStretchMode"),
                new Vector2(-311f, 149f), new Vector2(230f, 230f));
            exitButton.onClick.AddListener(ExitStretchMode);
        }

        stretchButton.gameObject.SetActive(false);
        exitButton.gameObject.SetActive(false);
    }

    private void BeginStretchMode()
    {
        if (controller == null || !controller.HasResizeTargetAtCameraCenter())
        {
            stretchButton?.gameObject.SetActive(false);
            return;
        }
        controller.TryHandleResizeTouchButton();
    }

    private void ExitStretchMode() => controller?.ConfirmResizeSession();

    private void HandleResizeModeChanged(bool active)
    {
        if (active) ShowStretchModeHud();
        else RestoreGameplayHud();
    }

    private void ShowStretchModeHud()
    {
        if (mobileGroup == null)
            EnsureInterface();
        if (mobileGroup == null)
            return;

        previousVisibility.Clear();
        foreach (Transform child in mobileGroup)
        {
            GameObject item = child.gameObject;
            previousVisibility[item] = item.activeSelf;
            bool keepVisible = child.name.Equals(MenuButtonName, System.StringComparison.OrdinalIgnoreCase)
                || child.name.Equals(ExitButtonName, System.StringComparison.OrdinalIgnoreCase);
            item.SetActive(keepVisible);
        }

        if (operatorsGroup != null)
        {
            previousVisibility[operatorsGroup] = operatorsGroup.activeSelf;
            operatorsGroup.SetActive(false);
        }
        exitButton?.gameObject.SetActive(true);
        resizeHudActive = true;
    }

    private void RestoreGameplayHud()
    {
        if (!resizeHudActive && previousVisibility.Count == 0)
            return;
        foreach (KeyValuePair<GameObject, bool> entry in previousVisibility)
            if (entry.Key != null)
                entry.Key.SetActive(entry.Value);

        previousVisibility.Clear();
        resizeHudActive = false;
        exitButton?.gameObject.SetActive(false);
        stretchButton?.gameObject.SetActive(false);
        nextTargetCheck = 0f;
    }

    private Button CreateHudButton(string objectName, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = mobileGroup.gameObject.layer;
        buttonObject.transform.SetParent(mobileGroup, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.68f, 0.9f, 0.94f, 0.88f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    private Button FindDirectButton(string expectedName)
    {
        Transform child = mobileGroup != null ? mobileGroup.Find(expectedName) : null;
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Transform FindSceneTransform(string expectedName)
    {
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            if (candidate != null && candidate.gameObject.scene.IsValid()
                && candidate.name.Equals(expectedName, System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        return null;
    }
}
