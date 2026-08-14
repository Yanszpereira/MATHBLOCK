using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class HudToonStyler : MonoBehaviour
{
    private static Sprite buttonSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SceneManager.GetActiveScene().name.StartsWith("Fase"))
            return;
        GameObject stylist = new GameObject("HUD Toon Dotted Style");
        stylist.AddComponent<HudToonStyler>();
    }

    private void Start()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button == null || !IsInsideHud(button.transform))
                continue;

            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null)
                continue;
            image.sprite = GetButtonSprite();
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.025f, 0.18f, 0.23f, 0.95f);
            outline.effectDistance = new Vector2(4f, -4f);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.88f, 0.90f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.62f, 0.64f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }
    }

    private static bool IsInsideHud(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.name.Equals("Hud", System.StringComparison.OrdinalIgnoreCase) ||
                current.name.Equals("HUD", System.StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static Sprite GetButtonSprite()
    {
        if (buttonSprite != null)
            return buttonSprite;

        const int size = 128;
        const int border = 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "HUD Toon Dotted Button",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color aqua = new Color(0.16f, 0.72f, 0.76f, 0.96f);
        Color edge = new Color(0.025f, 0.18f, 0.23f, 1f);
        Color dot = new Color(0.72f, 1f, 0.93f, 0.48f);
        Color clear = Color.clear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Max(14f - x, x - (size - 15f)), 0f);
            float dy = Mathf.Max(Mathf.Max(14f - y, y - (size - 15f)), 0f);
            bool inside = (dx * dx) + (dy * dy) <= 14f * 14f;
            if (!inside) { pixels[y * size + x] = clear; continue; }
            bool isEdge = x < border || y < border || x >= size - border || y >= size - border;
            int gx = (x + 3) % 18 - 9;
            int gy = (y + 3) % 18 - 9;
            bool isDot = gx * gx + gy * gy <= 8;
            pixels[y * size + x] = isEdge ? edge : isDot ? dot : aqua;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        buttonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f, 0,
            SpriteMeshType.FullRect, Vector4.one * 18f);
        buttonSprite.name = "HUD Toon Dotted Button";
        buttonSprite.hideFlags = HideFlags.HideAndDontSave;
        return buttonSprite;
    }
}
