using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameplayButtonBinder : MonoBehaviour
{
    private PlayerMovement movement;
    private GravityInteract gravityInteract;
    private MenuController menuController;

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

        foreach (GameplayButtonBinder existing in FindObjectsByType<GameplayButtonBinder>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (existing != null && existing.gameObject.scene == scene)
                return;
        }

        GameObject binderObject = new GameObject("Gameplay Button Logic");
        SceneManager.MoveGameObjectToScene(binderObject, scene);
        binderObject.AddComponent<GameplayButtonBinder>();
    }

    private void Start()
    {
        ConfigureAndroidButtonGroups();
        movement = FindFirstObjectByType<PlayerMovement>();
        gravityInteract = FindFirstObjectByType<GravityInteract>();
        menuController = FindActiveMenuController();
        BindButtons();
    }

    private static MenuController FindActiveMenuController()
    {
        foreach (MenuController candidate in FindObjectsByType<MenuController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.isActiveAndEnabled)
                return candidate;
        }

        return null;
    }

    private static void ConfigureAndroidButtonGroups()
    {
        bool showOnMobile = MobileTouchControls.ShouldShowTouchControls();
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null
                && candidate.name.Equals("BotoesMobile", System.StringComparison.OrdinalIgnoreCase))
            {
                candidate.gameObject.SetActive(showOnMobile);
            }
        }
    }

    private void BindButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (IsMobileButton(button))
                continue;

            ConfigureButtonFeedback(button);
            switch (button.name.Trim().ToLowerInvariant())
            {
                case "jump":
                    button.onClick.AddListener(() => movement?.RequestJumpFromUI());
                    break;
                case "interact":
                    button.onClick.AddListener(() => gravityInteract?.InteractFromUI());
                    break;
                case "duplicate":
                    button.onClick.AddListener(() => gravityInteract?.DuplicateFromUI());
                    break;
                case "undo":
                    button.onClick.AddListener(() => gravityInteract?.UndoFromUI());
                    break;
                case "menubutton":
                    button.onClick.AddListener(() => menuController?.AbrirInicial());
                    break;
            }
        }
    }

    private static bool IsMobileButton(Button button)
    {
        Transform current = button.transform.parent;
        while (current != null)
        {
            if (current.name.Equals("BotoesMobile", System.StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void ConfigureButtonFeedback(Button button)
    {
        foreach (HoverScale oldHover in button.GetComponentsInChildren<HoverScale>(true))
            if (oldHover.gameObject != button.gameObject)
                oldHover.enabled = false;

        // O RectTransform do pai pode ocupar quase o Canvas inteiro. Usar o
        // primeiro grafico filho limita o clique ao texto/icone que o usuario ve.
        Graphic hitGraphic = null;
        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
            if (hitGraphic == null && graphic.transform != button.transform)
                hitGraphic = graphic;
        }

        if (hitGraphic == null)
            hitGraphic = button.GetComponent<Graphic>();

        if (hitGraphic != null)
        {
            hitGraphic.raycastTarget = true;
            button.targetGraphic = hitGraphic;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.76f, 1f, 0.92f, 1f);
        colors.pressedColor = new Color(0.48f, 0.82f, 0.72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        // Button.onClick e disparado somente ao soltar dentro do botao.
        if (button.GetComponent<HoverScale>() == null)
            button.gameObject.AddComponent<HoverScale>();
    }

}
