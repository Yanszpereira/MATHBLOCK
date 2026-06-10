using UnityEngine;

public class DoorValueVerifier : MonoBehaviour
{
    [SerializeField] private int requiredValue = 1;
    [SerializeField, HideInInspector] private bool useRequiredRange;
    [SerializeField, HideInInspector] private int requiredMinValue = 1;
    [SerializeField, HideInInspector] private int requiredMaxValue = 1;
    [SerializeField] private GameObject acceptedPadObject;
    [SerializeField] private GameObject successParticleObject;

    public int RequiredValue => requiredValue;
    public int RequiredMinValue => useRequiredRange ? requiredMinValue : requiredValue;
    public int RequiredMaxValue => useRequiredRange ? requiredMaxValue : requiredValue;

    private void OnValidate()
    {
        requiredValue = Mathf.Max(0, requiredValue);
        requiredMinValue = Mathf.Max(0, requiredMinValue);
        requiredMaxValue = Mathf.Max(requiredMinValue, requiredMaxValue);
    }

    public void SetRequiredRange(int minValue, int maxValue)
    {
        requiredMinValue = Mathf.Max(0, Mathf.Min(minValue, maxValue));
        requiredMaxValue = Mathf.Max(requiredMinValue, Mathf.Max(minValue, maxValue));
        requiredValue = requiredMinValue;
        useRequiredRange = true;

        if (requiredMinValue == requiredMaxValue)
        {
            Debug.Log($"Verificador {name}: valor exigido configurado para {requiredMinValue}.");
            Debug.Log($"[TEMP] Valor da porta: {requiredMinValue}.");
        }
        else
        {
            Debug.Log($"Verificador {name}: intervalo exigido configurado para {requiredMinValue} ate {requiredMaxValue}.");
            Debug.Log($"[TEMP] Valor da porta: {requiredMinValue} ate {requiredMaxValue}.");
        }
    }

    public void ReceiveValueFromPad(GameObject sourcePad, int receivedValue, GameObject sourceBlock)
    {
        if (acceptedPadObject != null && sourcePad != acceptedPadObject)
        {
            string sourcePadName = sourcePad != null ? sourcePad.name : "Pad desconhecido";
            Debug.LogWarning(
                $"Verificador {name} ignorou valor {receivedValue} de {sourcePadName}, pois aceita apenas o Pad {acceptedPadObject.name}."
            );
            return;
        }

        string blockName = sourceBlock != null ? sourceBlock.name : "bloco desconhecido";
        if (receivedValue >= RequiredMinValue && receivedValue <= RequiredMaxValue)
        {
            Debug.Log($"Verificador {name}: valor certo ({receivedValue}) recebido de {blockName}. Intervalo aceito: {RequiredMinValue} ate {RequiredMaxValue}.");
            PlaySuccessParticles();
        }
        else
        {
            Debug.Log($"Verificador {name}: valor errado ({receivedValue}). Intervalo necessario: {RequiredMinValue} ate {RequiredMaxValue}.");
        }
    }

    private void PlaySuccessParticles()
    {
        if (successParticleObject == null)
            return;

        successParticleObject.SetActive(true);

        ParticleSystem[] particleSystems = successParticleObject.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Clear();
            particleSystem.Play();
        }
    }
}
