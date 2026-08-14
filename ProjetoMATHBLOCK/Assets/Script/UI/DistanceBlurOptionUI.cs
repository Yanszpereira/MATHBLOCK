using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DistanceBlurOptionUI : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<DistanceBlurOptionUI>() != null)
            return;

        new GameObject("Menu Options Logic").AddComponent<DistanceBlurOptionUI>();
    }

    private void Start()
    {
        RectTransform optionsMenu = FindOptionsMenu();
        if (optionsMenu == null)
            return;

        ConfigureVolume(optionsMenu);
        ConfigureSensitivity(optionsMenu);
        CreateBlurToggle(optionsMenu);
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
        });
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
