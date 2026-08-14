using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SensitivitySlider : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;

    private void Start()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        if (slider == null)
        {
            Debug.LogWarning("SensitivitySlider nao encontrou um Slider para configurar.", this);
            return;
        }

        float sensibilidade = PlayerPrefs.GetFloat("MouseSensitivity", 100f);

        slider.value = sensibilidade;
        UpdateSensitivity(sensibilidade);

        slider.onValueChanged.AddListener(UpdateSensitivity);
    }

    private void UpdateSensitivity(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();

        if (valueText != null)
            valueText.text = Mathf.RoundToInt(value).ToString();
    }
}