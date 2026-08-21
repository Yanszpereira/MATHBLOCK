using UnityEngine;
using UnityEngine.UI;

public sealed class ButtonsMobile : MonoBehaviour
{
    private PlayerMovement movement;
    private GravityInteract gravityInteract;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.isMobilePlatform)
            return;

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null || !candidate.name.Equals("BotoesMobile", System.StringComparison.OrdinalIgnoreCase))
                continue;

            candidate.gameObject.SetActive(true);
            if (candidate.GetComponent<ButtonsMobile>() == null)
                candidate.gameObject.AddComponent<ButtonsMobile>();
        }
    }

    private void Start()
    {
        movement = FindFirstObjectByType<PlayerMovement>();
        gravityInteract = FindFirstObjectByType<GravityInteract>();

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

    private void RequestJump()
    {
        movement?.RequestJumpFromUI();
    }

    private void Interact()
    {
        gravityInteract?.InteractFromUI();
    }

    private void Duplicate()
    {
        gravityInteract?.DuplicateFromUI();
    }

    private void Undo()
    {
        gravityInteract?.UndoFromUI();
    }
}
