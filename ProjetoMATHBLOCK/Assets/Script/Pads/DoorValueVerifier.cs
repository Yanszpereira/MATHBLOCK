using UnityEngine;

public class DoorValueVerifier : MonoBehaviour
{
    [SerializeField] private int requiredValue = 1;
    [SerializeField] private GameObject acceptedPadObject;
    [SerializeField] private GameObject successParticleObject;

    public int RequiredValue => requiredValue;

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
        if (receivedValue == requiredValue)
        {
            Debug.Log($"Verificador {name}: valor certo ({receivedValue}) recebido de {blockName}.");
            PlaySuccessParticles();
        }
        else
        {
            Debug.Log($"Verificador {name}: valor errado ({receivedValue}). Valor necessario: {requiredValue}.");
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
