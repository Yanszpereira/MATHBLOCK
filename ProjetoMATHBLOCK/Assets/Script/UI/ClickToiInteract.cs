using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ClickToiInteract : MonoBehaviour
{
    private const string SchoolbellFontPath = "Fonts & Materials/Schoolbell-Regular SDF";
    private static ClickToiInteract instance;

    private GameObject prompt;
    private TMP_Text promptText;
    private RectTransform promptRect;
    private Vector2 promptPosition;
    private GravityInteract gravityInteract;
    private Coroutine shakeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneInitializer()
    {
        GlobalSceneBootstrap.Register(InstallForGameplayScene);
    }

    private static void InstallForGameplayScene(Scene scene)
    {
        if (!scene.name.StartsWith("Fase", StringComparison.OrdinalIgnoreCase))
            return;

        if (instance == null)
            new GameObject("Click to Interact Hint").AddComponent<ClickToiInteract>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SetGravityInteract(FindFirstObjectByType<GravityInteract>());
        BuildPrompt();
    }

    private void OnDestroy()
    {
        SetGravityInteract(null);

        if (instance == this)
            instance = null;
    }

    private void BuildPrompt()
    {
        GameObject canvasObject = new GameObject("Interaction Hint Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        prompt = new GameObject("Interaction Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        prompt.transform.SetParent(canvasObject.transform, false);

        promptRect = prompt.GetComponent<RectTransform>();
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.18f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = Vector2.zero;
        promptPosition = promptRect.anchoredPosition;
        promptRect.sizeDelta = new Vector2(760f, 88f);

        Image background = prompt.GetComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Hint Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(prompt.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);

        promptText = textObject.GetComponent<TMP_Text>();
        TMP_FontAsset schoolbellFont = Resources.Load<TMP_FontAsset>(SchoolbellFontPath);
        if (schoolbellFont == null)
            Debug.LogWarning($"Fonte Schoolbell nao encontrada em Resources/{SchoolbellFontPath}.", this);
        else
            promptText.font = schoolbellFont;

        promptText.text = IsMobilePlatform()
            ? "Clique no botão para interagir"
            : "Clique E para interagir";
        promptText.fontSize = 34f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;
        promptText.raycastTarget = false;

        prompt.SetActive(false);
    }

    private void LateUpdate()
    {
        if (gravityInteract == null)
            SetGravityInteract(FindFirstObjectByType<GravityInteract>());

        bool heldBlockTouching = gravityInteract != null &&
            gravityInteract.IsHeldBlockTouchingAnotherBlock;
        bool hasNoEquippedOperator = gravityInteract == null ||
            gravityInteract.EquippedOperator == GravityInteract.PencilOperator.None;

        RefreshPrompt(heldBlockTouching);
        if (promptText != null)
            promptText.color = hasNoEquippedOperator
                ? new Color(1f, 0.2f, 0.2f, 1f)
                : Color.white;

        if (!IsMobilePlatform() &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryShakePrompt();
        }
    }

    private void SetGravityInteract(GravityInteract newGravityInteract)
    {
        if (gravityInteract == newGravityInteract)
            return;

        if (gravityInteract != null)
            gravityInteract.InteractionRequested -= OnInteractionRequested;

        gravityInteract = newGravityInteract;
        if (gravityInteract != null)
            gravityInteract.InteractionRequested += OnInteractionRequested;
    }

    private void OnInteractionRequested()
    {
        if (!IsMobilePlatform())
            return;

        TryShakePrompt();
    }

    private void TryShakePrompt()
    {
        if (gravityInteract == null
            || gravityInteract.EquippedOperator != GravityInteract.PencilOperator.None
            || !gravityInteract.IsHeldBlockTouchingAnotherBlock
            || promptRect == null)
            return;

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            promptRect.anchoredPosition = promptPosition;
        }

        shakeRoutine = StartCoroutine(ShakePrompt());
    }

    private IEnumerator ShakePrompt()
    {
        const float duration = 0.5f;
        const float horizontalDistance = 24f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float strength = 1f - elapsed / duration;
            float oscillation = Mathf.Sin(elapsed * 75f);
            float offset = oscillation * horizontalDistance * strength;
            promptRect.anchoredPosition = promptPosition + Vector2.right * offset;
            yield return null;
        }

        promptRect.anchoredPosition = promptPosition;
        shakeRoutine = null;
    }

    private void RefreshPrompt(bool visible)
    {
        if (prompt != null && prompt.activeSelf != visible)
            prompt.SetActive(visible);
    }

    private static bool IsMobilePlatform()
    {
        return Application.isMobilePlatform || UnityEngine.Device.Application.isMobilePlatform;
    }
}
