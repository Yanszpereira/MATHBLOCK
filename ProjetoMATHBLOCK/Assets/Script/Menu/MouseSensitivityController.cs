using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MouseSensitivityController : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;

    private void Start()
    {
        if (slider == null)
            slider = FindSensitivitySlider();

        // Algumas cenas mantem este controlador mesmo sem o menu de opcoes.
        // Nesse caso nao existe configuracao para fazer e nao e um erro.
        if (slider == null)
            return;

        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 50f);
        slider.SetValueWithoutNotify(sensitivity);
        ApplySensitivity(sensitivity);
        slider.onValueChanged.AddListener(ApplySensitivity);
    }

    private Slider FindSensitivitySlider()
    {
        Slider local = GetComponentInChildren<Slider>(true);
        if (local != null)
            return local;

        foreach (Slider candidate in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name.Equals("SliderSensibility", System.StringComparison.OrdinalIgnoreCase) ||
                candidate.name.Equals("SliderSensitivity", System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    private void ApplySensitivity(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();

        foreach (Look look in FindObjectsByType<Look>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            look.SetMouseSensitivity(value);

        if (valueText != null)
            valueText.text = Mathf.RoundToInt(value).ToString();
    }
}
