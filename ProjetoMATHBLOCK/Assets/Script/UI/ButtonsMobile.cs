using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ButtonsMobile : MonoBehaviour
{
    private PlayerMovement movement;
    private GravityInteract gravityInteract;
    private bool jumpAndInteractLayoutSwapped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("Fase", System.StringComparison.OrdinalIgnoreCase))
            return;

        if (!MobileTouchControls.ShouldShowTouchControls())
            return;

        ActivateHud(scene);
    }

    public static void ActivateHud(Scene scene)
    {
        // Resources.FindObjectsOfTypeAll inclui filhos desativados de instancias
        // de prefab. FindObjectsByType podia deixar BotoesMobile de fora antes
        // de seu primeiro SetActive(true) no Device Simulator.
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || candidate.gameObject.scene != scene ||
                !candidate.name.Equals("BotoesMobile", System.StringComparison.OrdinalIgnoreCase))
                continue;

            candidate.gameObject.SetActive(true);
            if (candidate.GetComponent<ButtonsMobile>() == null)
                candidate.gameObject.AddComponent<ButtonsMobile>();
            if (candidate.GetComponent<MobileTouchControls>() == null)
                candidate.gameObject.AddComponent<MobileTouchControls>();
        }
    }

    private void Start()
    {
        movement = FindFirstObjectByType<PlayerMovement>();
        gravityInteract = FindFirstObjectByType<GravityInteract>();

        SwapJumpAndInteractLayout();

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            switch (button.name.Trim().ToLowerInvariant())
            {
                case "jump":
                    button.onClick.AddListener(RequestJump);
                    break;
                case "interact":
                    button.onClick.AddListener(Interact);
                    break;
                case "duplicate":
                    button.onClick.AddListener(Duplicate);
                    break;
                case "undo":
                    button.onClick.AddListener(Undo);
                    break;
            }
        }
    }

    private void SwapJumpAndInteractLayout()
    {
        if (jumpAndInteractLayoutSwapped)
            return;

        RectTransform jump = null;
        RectTransform interact = null;

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name.Equals("Jump", System.StringComparison.OrdinalIgnoreCase))
                jump = button.transform as RectTransform;
            else if (button.name.Equals("Interact", System.StringComparison.OrdinalIgnoreCase))
                interact = button.transform as RectTransform;
        }

        if (jump == null || interact == null)
        {
            Debug.LogWarning("HUD mobile: Jump ou Interact nao foi encontrado para inverter o layout.", this);
            return;
        }

        RectLayout jumpLayout = RectLayout.Capture(jump);
        RectLayout interactLayout = RectLayout.Capture(interact);
        interactLayout.Apply(jump);
        jumpLayout.Apply(interact);
        jumpAndInteractLayoutSwapped = true;
    }

    private readonly struct RectLayout
    {
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 pivot;
        private readonly Vector2 anchoredPosition;
        private readonly Vector2 sizeDelta;
        private readonly Vector3 localScale;

        private RectLayout(RectTransform rect)
        {
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            anchoredPosition = rect.anchoredPosition;
            sizeDelta = rect.sizeDelta;
            localScale = rect.localScale;
        }

        public static RectLayout Capture(RectTransform rect)
        {
            return new RectLayout(rect);
        }

        public void Apply(RectTransform rect)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = localScale;
        }
    }

    private void RequestJump()
    {
        if (Time.timeScale <= 0f) return;
        movement?.RequestJumpFromUI();
    }

    private void Interact()
    {
        if (Time.timeScale <= 0f) return;
        gravityInteract?.InteractFromUI();
    }

    private void Duplicate()
    {
        if (Time.timeScale <= 0f) return;
        gravityInteract?.DuplicateFromUI();
    }

    private void Undo()
    {
        if (Time.timeScale <= 0f) return;
        gravityInteract?.UndoFromUI();
    }
}
