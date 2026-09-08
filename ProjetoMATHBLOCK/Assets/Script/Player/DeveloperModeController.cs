using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public sealed class DeveloperModeController : MonoBehaviour
{
    private const string ActivationCode = "7234";
    private const string DeveloperStatusText = "mod dev ativo";

    private static bool developerModeActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDeveloperModeState()
    {
        developerModeActive = false;
    }

    [SerializeField] private int statusFontSize = 28;
    [SerializeField] private Vector2 statusOffset = new Vector2(24f, -24f);

    private int activationProgress;
    private Text statusText;

    private void Start()
    {
        if (developerModeActive)
            EnsureStatusText();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!developerModeActive)
        {
            ProcessActivationInput(keyboard);
            return;
        }

        if (WasPressed(keyboard.numpad1Key))
            LoadPhase("Fase 1");
        else if (WasPressed(keyboard.numpad2Key))
            LoadPhase("Fase 2");
        else if (WasPressed(keyboard.numpad3Key))
            LoadPhase("Fase 3");
    }

    private void ProcessActivationInput(Keyboard keyboard)
    {
        if (!TryGetPressedDigit(keyboard, out char digit))
            return;

        if (digit == ActivationCode[activationProgress])
        {
            activationProgress++;
            if (activationProgress >= ActivationCode.Length)
                ActivateDeveloperMode();
            return;
        }

        activationProgress = digit == ActivationCode[0] ? 1 : 0;
    }

    private static bool TryGetPressedDigit(Keyboard keyboard, out char digit)
    {
        if (WasPressed(keyboard.numpad0Key)) { digit = '0'; return true; }
        if (WasPressed(keyboard.numpad1Key)) { digit = '1'; return true; }
        if (WasPressed(keyboard.numpad2Key)) { digit = '2'; return true; }
        if (WasPressed(keyboard.numpad3Key)) { digit = '3'; return true; }
        if (WasPressed(keyboard.numpad4Key)) { digit = '4'; return true; }
        if (WasPressed(keyboard.numpad5Key)) { digit = '5'; return true; }
        if (WasPressed(keyboard.numpad6Key)) { digit = '6'; return true; }
        if (WasPressed(keyboard.numpad7Key)) { digit = '7'; return true; }
        if (WasPressed(keyboard.numpad8Key)) { digit = '8'; return true; }
        if (WasPressed(keyboard.numpad9Key)) { digit = '9'; return true; }

        digit = default;
        return false;
    }

    private static bool WasPressed(KeyControl keyboardKey)
    {
        return keyboardKey != null && keyboardKey.wasPressedThisFrame;
    }

    private void ActivateDeveloperMode()
    {
        developerModeActive = true;
        activationProgress = 0;
        EnsureStatusText();
        Debug.Log("Modo desenvolvedor ativado.", this);
    }

    private void LoadPhase(string sceneName)
    {
        SceneTransitionTrigger.TryLoadScene(sceneName, this);
    }

    private void EnsureStatusText()
    {
        if (statusText != null)
            return;

        if (transform == null)
            return;

        GameObject canvasObject = new GameObject(
            "Developer Mode Canvas",
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            Destroy(canvasObject);
            return;
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32001;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            Destroy(canvasObject);
            return;
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("Developer Mode Status", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            Destroy(canvasObject);
            return;
        }
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = statusOffset;
        rect.sizeDelta = new Vector2(360f, 48f);

        statusText = textObject.GetComponent<Text>();
        if (statusText == null)
        {
            Destroy(canvasObject);
            return;
        }
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.text = DeveloperStatusText;
        statusText.fontSize = Mathf.Max(1, statusFontSize);
        statusText.fontStyle = FontStyle.Bold;
        statusText.alignment = TextAnchor.UpperLeft;
        statusText.color = Color.red;
        statusText.raycastTarget = false;
    }
}
