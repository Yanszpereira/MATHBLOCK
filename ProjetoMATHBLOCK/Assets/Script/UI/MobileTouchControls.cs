using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileTouchControls : MonoBehaviour
{
    private CanvasGroup group;
    private MobileInputJoystick joystick;
    private bool wasEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SceneManager.GetActiveScene().name.StartsWith("Fase"))
            return;

        if (!ShouldShowTouchControls())
            return;

        CreateControls();
    }

    public static bool ShouldShowTouchControls()
    {
        // Nao usa Touchscreen.current: um notebook touch continua sendo PC e
        // nao deve receber a HUD mobile. UnityEngine.Device reflete o aparelho
        // Android/iOS selecionado no Device Simulator.
        return Application.isMobilePlatform ||
               UnityEngine.Device.Application.isMobilePlatform;
    }

    private static void CreateControls()
    {
        if (!SceneManager.GetActiveScene().name.StartsWith("Fase"))
            return;

        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                !candidate.name.Equals("BotoesMobile", System.StringComparison.OrdinalIgnoreCase))
                continue;

            candidate.gameObject.SetActive(true);
            if (candidate.GetComponent<ButtonsMobile>() == null)
                candidate.gameObject.AddComponent<ButtonsMobile>();
            if (candidate.GetComponent<MobileTouchControls>() == null)
                candidate.gameObject.AddComponent<MobileTouchControls>();
            return;
        }

        Debug.LogWarning("HUD mobile: grupo BotoesMobile nao foi encontrado na cena.");
    }

    private void Awake()
    {
        if (GetComponent<MobileHudSafeArea>() == null)
            gameObject.AddComponent<MobileHudSafeArea>();

        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        Transform existingJoystick = transform.Find("Joystick - Input System");
        if (existingJoystick == null)
            CreateJoystick();
        else
            joystick = existingJoystick.GetComponent<MobileInputJoystick>();
    }

    private void Update()
    {
        bool enabledNow = Time.timeScale > 0f;
        group.alpha = enabledNow ? 1f : 0f;
        group.interactable = enabledNow;
        group.blocksRaycasts = enabledNow;

        if (wasEnabled && !enabledNow)
            joystick?.ResetStick();
        wasEnabled = enabledNow;
    }

    private void CreateJoystick()
    {
        GameObject baseObject = new GameObject("Joystick - Input System", typeof(RectTransform), typeof(Image));
        baseObject.transform.SetParent(transform, false);
        RectTransform baseRect = baseObject.GetComponent<RectTransform>();
        baseRect.anchorMin = baseRect.anchorMax = new Vector2(0f, 0f);
        baseRect.pivot = new Vector2(0.5f, 0.5f);
        baseRect.anchoredPosition = new Vector2(190f, 190f);
        baseRect.sizeDelta = new Vector2(250f, 250f);

        Image baseImage = baseObject.GetComponent<Image>();
        baseImage.sprite = CreateDottedCircleSprite(256, 12, new Color(0.08f, 0.66f, 0.73f, 0.48f));
        baseImage.color = Color.white;

        Outline baseOutline = baseObject.AddComponent<Outline>();
        baseOutline.effectColor = new Color(0.02f, 0.15f, 0.20f, 0.92f);
        baseOutline.effectDistance = new Vector2(5f, -5f);

        GameObject handleObject = new GameObject("Pencil Knob", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(baseObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(104f, 104f);
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.sprite = CreateDottedCircleSprite(128, 9, new Color(0.96f, 0.76f, 0.12f, 0.98f));
        handleImage.raycastTarget = false;
        Outline handleOutline = handleObject.AddComponent<Outline>();
        handleOutline.effectColor = new Color(0.12f, 0.10f, 0.05f, 1f);
        handleOutline.effectDistance = new Vector2(4f, -4f);

        joystick = baseObject.AddComponent<MobileInputJoystick>();
        joystick.Configure(handleRect, 76f);
    }

    private static Sprite CreateDottedCircleSprite(int size, int edgeWidth, Color fill)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color edge = new Color(0.02f, 0.15f, 0.20f, 1f);
        Color dot = new Color(0.86f, 1f, 0.97f, 0.42f);
        Color[] pixels = new Color[size * size];
        float center = (size - 1f) * 0.5f;
        float radius = center - 2f;
        float inner = radius - edgeWidth;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - center;
            float dy = y - center;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance > radius) { pixels[y * size + x] = Color.clear; continue; }
            if (distance >= inner) { pixels[y * size + x] = edge; continue; }
            int dotX = (x + 4) % 24 - 12;
            int dotY = (y + 4) % 24 - 12;
            pixels[y * size + x] = dotX * dotX + dotY * dotY < 10 ? dot : fill;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
