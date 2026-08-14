using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlockDuplicationCounter : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private Text counterText;
    private GameObject panelObject;
    private Coroutine hideRoutine;
    private const float VisibleDuration = 5f;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        CreateInterface();
    }

    private void OnEnable()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement != null)
        {
            playerMovement.BlockDuplicationsChanged += Refresh;
            playerMovement.BlockDuplicationRequested += ShowTemporarily;
        }
    }

    private void Start()
    {
        if (playerMovement != null)
        {
            Refresh(playerMovement.AvailableBlockDuplications, playerMovement.MaximumBlockDuplications);
            panelObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.BlockDuplicationsChanged -= Refresh;
            playerMovement.BlockDuplicationRequested -= ShowTemporarily;
        }
    }

    private void CreateInterface()
    {
        GameObject canvasObject = new GameObject("Block Duplication Counter", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panelObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(32f, 32f);
        panel.sizeDelta = new Vector2(300f, 64f);
        panelObject.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.82f);

        GameObject textObject = new GameObject("Count", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);

        counterText = textObject.GetComponent<Text>();
        counterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        counterText.fontSize = 25;
        counterText.fontStyle = FontStyle.Bold;
        counterText.alignment = TextAnchor.MiddleCenter;
        counterText.color = Color.white;
        counterText.raycastTarget = false;
    }

    private void Refresh(int remaining, int maximum)
    {
        if (counterText == null)
            return;

        counterText.text = $"DUPLICAÇÕES  {remaining}/{maximum}";
        counterText.color = remaining > 0
            ? Color.white
            : new Color(1f, 0.32f, 0.28f, 1f);
    }

    private void ShowTemporarily()
    {
        if (panelObject == null)
            return;

        Refresh(playerMovement.AvailableBlockDuplications, playerMovement.MaximumBlockDuplications);
        panelObject.SetActive(true);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(VisibleDuration);
        panelObject.SetActive(false);
        hideRoutine = null;
    }
}
