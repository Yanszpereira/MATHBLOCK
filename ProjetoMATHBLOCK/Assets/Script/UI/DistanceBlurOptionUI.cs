using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DistanceBlurOptionUI : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (DistanceBlurOptionUI existing in FindObjectsByType<DistanceBlurOptionUI>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (existing != null && existing.gameObject.scene == scene)
                return;
        }

        if (!scene.name.Equals("MainMenu", StringComparison.OrdinalIgnoreCase) &&
            !scene.name.StartsWith("Fase", StringComparison.OrdinalIgnoreCase))
            return;

        GameObject logicObject = new GameObject("Menu Options Logic");
        SceneManager.MoveGameObjectToScene(logicObject, scene);
        logicObject.AddComponent<DistanceBlurOptionUI>();
    }

    private void Start()
    {
        RectTransform optionsMenu = FindOptionsMenu();
        if (optionsMenu == null)
            return;

        ConfigureVolume(optionsMenu);
        ConfigureSensitivity(optionsMenu);
        CreateTouchSensitivity(optionsMenu);
        CreateBlurToggle(optionsMenu);
        ArrangeOptionsLayout(optionsMenu);
        ApplyRecordedLayout(optionsMenu);
    }

    private static void ConfigureVolume(RectTransform optionsMenu)
    {
        Slider slider = FindSlider(optionsMenu, "SliderVolume", "Volume");
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, 1f));
        slider.onValueChanged.AddListener(SetMasterVolume);
        SetMasterVolume(slider.value);
    }

    private static void ConfigureSensitivity(RectTransform optionsMenu)
    {
        Slider slider = FindSlider(optionsMenu, "SliderSensibility", "Sensibility");
        if (slider == null)
            return;

        slider.minValue = 1f;
        slider.maxValue = 100f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSensitivity", 50f));
        slider.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetFloat("MouseSensitivity", value);
            PlayerPrefs.Save();
            foreach (Look look in FindObjectsByType<Look>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                look.SetMouseSensitivity(value);
        });

        foreach (Look look in FindObjectsByType<Look>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            look.SetMouseSensitivity(slider.value);
    }

    private static void CreateTouchSensitivity(RectTransform optionsMenu)
    {
        if (FindChild(optionsMenu, "Touch Sensitivity") != null)
            return;

        Slider mouseSlider = FindSlider(optionsMenu, "SliderSensibility", "Sensibility");
        if (mouseSlider == null || mouseSlider.transform.parent == null)
            return;

        GameObject row = Instantiate(mouseSlider.transform.parent.gameObject, optionsMenu);
        row.name = "Touch Sensitivity";
        RectTransform rowRect = row.transform as RectTransform;
        RectTransform sourceRect = mouseSlider.transform.parent as RectTransform;
        if (rowRect != null && sourceRect != null)
            rowRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -135f);

        foreach (MonoBehaviour behaviour in row.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null && behaviour.GetType().Name.Contains("Sensitivity", StringComparison.OrdinalIgnoreCase))
                Destroy(behaviour);

        Slider touchSlider = row.GetComponentInChildren<Slider>(true);
        if (touchSlider == null)
        {
            Destroy(row);
            return;
        }

        touchSlider.name = "SliderTouchSensitivity";
        touchSlider.minValue = 1f;
        touchSlider.maxValue = 100f;
        touchSlider.wholeNumbers = false;
        float savedValue = PlayerPrefs.GetFloat("TouchSensitivity", 40f);
        touchSlider.SetValueWithoutNotify(savedValue);
        touchSlider.onValueChanged.RemoveAllListeners();
        touchSlider.onValueChanged.AddListener(SetTouchSensitivity);

        TextMeshProUGUI[] labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label.text.Contains("SENS", StringComparison.OrdinalIgnoreCase))
                label.text = "TOUCH";
            else if (int.TryParse(label.text, out _))
                label.text = Mathf.RoundToInt(savedValue).ToString();
        }

        touchSlider.onValueChanged.AddListener(value =>
        {
            foreach (TextMeshProUGUI label in labels)
                if (int.TryParse(label.text, out _))
                    label.text = Mathf.RoundToInt(value).ToString();
        });
        SetTouchSensitivity(savedValue);
    }

    private static void SetTouchSensitivity(float value)
    {
        value = Mathf.Clamp(value, 1f, 100f);
        PlayerPrefs.SetFloat("TouchSensitivity", value);
        PlayerPrefs.Save();
        foreach (Look look in FindObjectsByType<Look>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            look.SetTouchSensitivity(value);
    }

    private static void ArrangeOptionsLayout(RectTransform optionsMenu)
    {
        Transform volume = FindChild(optionsMenu, "Volume");
        Transform mouse = FindChild(optionsMenu, "Sensibility");
        Transform touch = FindChild(optionsMenu, "Touch Sensitivity");
        Transform blur = FindChild(optionsMenu, "Distance Blur Option");
        Transform back = FindChild(optionsMenu, "Back");

        if (volume == null || mouse == null || touch == null || blur == null || back == null)
            return;

        Transform existingContainer = FindChild(optionsMenu, "Responsive Options Rows");
        RectTransform container;
        if (existingContainer == null)
        {
            GameObject containerObject = new GameObject("Responsive Options Rows", typeof(RectTransform));
            containerObject.transform.SetParent(optionsMenu, false);
            container = containerObject.GetComponent<RectTransform>();
        }
        else
        {
            container = existingContainer as RectTransform;
        }

        container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
        container.pivot = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = new Vector2(-8f, -35f);
        container.sizeDelta = new Vector2(500f, 620f);
        container.localScale = Vector3.one;

        ConfigureRow(volume, container, 205f, new Vector2(360f, 90f), 1.12f);
        ConfigureRow(mouse, container, 90f, new Vector2(360f, 90f), 1.12f);
        ConfigureRow(touch, container, -25f, new Vector2(360f, 90f), 1.12f);
        ConfigureRow(blur, container, -140f, new Vector2(330f, 60f), 1.12f);
        ConfigureRow(back, container, -260f, new Vector2(300f, 72f), 1.08f);

        SetRowLabel(volume, "VOLUME");
        SetRowLabel(mouse, "MOUSE");
        SetRowLabel(touch, "TOUCH");
        SetRowLabel(blur, "BLUR");

        Canvas.ForceUpdateCanvases();
    }

    private static void ConfigureRow(
        Transform row,
        RectTransform container,
        float y,
        Vector2 size,
        float scale)
    {
        row.SetParent(container, false);
        RectTransform rect = row as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one * scale;
    }

    private static void SetRowLabel(Transform row, string text)
    {
        foreach (TextMeshProUGUI label in row.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string current = label.text.Trim();
            if (string.IsNullOrEmpty(current) || int.TryParse(current, out _))
                continue;

            label.text = text;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return;
        }
    }

    private static void ApplyRecordedLayout(RectTransform optionsMenu)
    {
        Transform volume = FindChild(optionsMenu, "Volume");
        Transform mouse = FindChild(optionsMenu, "Sensibility");
        Transform touch = FindChild(optionsMenu, "Touch Sensitivity");
        Transform blur = FindChild(optionsMenu, "Distance Blur Option");

        ApplyGroupLayout(
            volume,
            new Vector2(-131f, 131f),
            new Vector2(169f, 36f),
            new Vector2(-84f, 79f),
            new Vector2(263f, 38f),
            6f, -39f, 135f, -24f,
            null);

        ApplyGroupLayout(
            mouse,
            new Vector2(-103f, 121f),
            new Vector2(169f, 36f),
            new Vector2(-1f, 79f),
            new Vector2(370f, 65f),
            15f, -41f, 154f, -27f,
            new RecordedRect(new Vector2(28f, 46f), new Vector2(72f, 29f), 2f));

        ApplyGroupLayout(
            touch,
            new Vector2(-94f, 103f),
            new Vector2(169f, 36f),
            new Vector2(7f, 64f),
            new Vector2(366f, 57f),
            0f, -45f, 152f, -29f,
            new RecordedRect(new Vector2(23f, 22f), new Vector2(72f, 29f), 2f));

        if (blur != null)
        {
            RectTransform label = FindDirectRect(blur, "Label");
            if (label != null)
            {
                label.anchorMin = Vector2.zero;
                label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(0f, 65f);
                label.offsetMax = new Vector2(-62f, 65f);
                label.localEulerAngles = Vector3.zero;
                label.localScale = Vector3.one;
            }

            RectTransform toggle = FindDirectRect(blur, "Distance Blur Toggle");
            SetFixedRect(toggle, new Vector2(-193f, 65f), new Vector2(48f, 24f), 0f);
            if (toggle != null)
            {
                toggle.anchorMin = toggle.anchorMax = new Vector2(1f, 0.5f);
                toggle.pivot = new Vector2(1f, 0.5f);
            }
        }

        RectTransform back = FindChild(optionsMenu, "Back") as RectTransform;
        SetFixedRect(back, new Vector2(0f, -167f), new Vector2(300f, 72f), 0f);

        RectTransform note = FindChild(optionsMenu, "Note") as RectTransform;
        SetFixedRect(note, new Vector2(170f, 324f), new Vector2(150f, 145f), 14f);
        if (note != null)
            note.localScale = Vector3.one * 2f;
    }

    private static void ApplyGroupLayout(
        Transform group,
        Vector2 labelPosition,
        Vector2 labelSize,
        Vector2 sliderPosition,
        Vector2 sliderSize,
        float handleX,
        float handleTop,
        float handleWidth,
        float handleBottom,
        RecordedRect? valueLayout)
    {
        if (group == null)
            return;

        Slider slider = group.GetComponentInChildren<Slider>(true);
        RectTransform sliderRect = slider != null ? slider.transform as RectTransform : null;
        SetFixedRect(sliderRect, sliderPosition, sliderSize, 2f);

        TextMeshProUGUI label = FindGroupLabel(group);
        SetFixedRect(label != null ? label.rectTransform : null, labelPosition, labelSize, 2f);

        TextMeshProUGUI value = FindValueLabel(group);
        if (value != null && valueLayout.HasValue)
        {
            RecordedRect layout = valueLayout.Value;
            SetFixedRect(value.rectTransform, layout.Position, layout.Size, layout.RotationZ);
        }

        RectTransform handle = FindHandle(slider);
        if (handle != null)
        {
            Vector2 position = handle.anchoredPosition;
            position.x = handleX;
            handle.anchoredPosition = position;
            handle.sizeDelta = new Vector2(handleWidth, handle.sizeDelta.y);
            handle.offsetMin = new Vector2(handle.offsetMin.x, handleBottom);
            handle.offsetMax = new Vector2(handle.offsetMax.x, -handleTop);
            handle.localEulerAngles = new Vector3(0f, 0f, 3f);
            handle.localScale = Vector3.one;
        }
    }

    private static void SetFixedRect(RectTransform rect, Vector2 position, Vector2 size, float rotationZ)
    {
        if (rect == null)
            return;

        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        rect.localScale = Vector3.one;
    }

    private static TextMeshProUGUI FindGroupLabel(Transform group)
    {
        foreach (TextMeshProUGUI text in group.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (!int.TryParse(text.text.Trim(), out _))
                return text;
        return null;
    }

    private static TextMeshProUGUI FindValueLabel(Transform group)
    {
        foreach (TextMeshProUGUI text in group.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (int.TryParse(text.text.Trim(), out _))
                return text;
        return null;
    }

    private static RectTransform FindHandle(Slider slider)
    {
        if (slider == null)
            return null;

        foreach (RectTransform rect in slider.GetComponentsInChildren<RectTransform>(true))
            if (rect.name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
                return rect;
        return null;
    }

    private static RectTransform FindDirectRect(Transform parent, string childName)
    {
        foreach (Transform child in parent)
            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return child as RectTransform;
        return null;
    }

    private readonly struct RecordedRect
    {
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly float RotationZ;

        public RecordedRect(Vector2 position, Vector2 size, float rotationZ)
        {
            Position = position;
            Size = size;
            RotationZ = rotationZ;
        }
    }

    private static void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();

        try
        {
            FMODUnity.RuntimeManager.GetBus("bus:/").setVolume(value);
        }
        catch (Exception)
        {
            // O audio nativo continua funcionando mesmo se os bancos FMOD ainda nao carregaram.
        }
    }

    private static void CreateBlurToggle(RectTransform optionsMenu)
    {
        if (FindChild(optionsMenu, "Distance Blur Option") != null)
            return;

        GameObject row = new GameObject("Distance Blur Option", typeof(RectTransform));
        row.transform.SetParent(optionsMenu, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, -132f);
        rowRect.sizeDelta = new Vector2(270f, 34f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(row.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(0f, 0f);
        labelRect.offsetMax = new Vector2(-62f, 0f);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "Blur de distancia";
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        CopyMenuTextStyle(optionsMenu, label);
        label.raycastTarget = false;

        GameObject toggleObject = new GameObject("Distance Blur Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        toggleObject.transform.SetParent(row.transform, false);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = Vector2.zero;
        toggleRect.sizeDelta = new Vector2(48f, 24f);
        Image background = toggleObject.GetComponent<Image>();
        background.color = new Color(0.72f, 0.69f, 0.60f, 1f);

        GameObject checkObject = new GameObject("On", typeof(RectTransform), typeof(Image));
        checkObject.transform.SetParent(toggleObject.transform, false);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.sizeDelta = new Vector2(38f, 14f);
        Image check = checkObject.GetComponent<Image>();
        check.color = new Color(0.20f, 0.58f, 0.43f, 1f);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.SetIsOnWithoutNotify(DistanceFogBlur.UserEnabled);
        toggle.onValueChanged.AddListener(DistanceFogBlur.SetUserEnabled);
    }

    private static void CopyMenuTextStyle(Transform optionsMenu, TextMeshProUGUI target)
    {
        TextMeshProUGUI[] texts = optionsMenu.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI source in texts)
        {
            if (!source.name.Equals("Volume", StringComparison.OrdinalIgnoreCase) &&
                !source.text.Contains("Volume", StringComparison.OrdinalIgnoreCase))
                continue;

            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.fontStyle = source.fontStyle;
            target.color = source.color;
            target.fontSize = source.fontSize;
            return;
        }

        target.color = new Color(0.23f, 0.22f, 0.21f, 1f);
    }

    private static Slider FindSlider(Transform root, params string[] names)
    {
        Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
        foreach (string name in names)
            foreach (Slider slider in sliders)
                if (slider.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    slider.transform.parent.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return slider;
        return null;
    }

    private static RectTransform FindOptionsMenu()
    {
        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RectTransform rect in rects)
            if (rect != null && rect.name.Equals("OptionsMenu", StringComparison.OrdinalIgnoreCase))
                return rect;
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return child;
        return null;
    }
}
