using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Door Progress")]
    [SerializeField] private int correctDoorValueCount;

    public int CorrectDoorValueCount => correctDoorValueCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"GameManager duplicado detectado em {name}. Mantendo {Instance.name} como instancia ativa.");
            return;
        }

        Instance = this;
    }

    public void RegisterCorrectDoorValue(GameObject sourcePad, int receivedValue, GameObject sourceBlock, DoorValueVerifier sourceVerifier)
    {
        correctDoorValueCount++;
        Debug.Log($"{name}: valor correto de porta registrado. Total={correctDoorValueCount}. Valor={receivedValue}, pad={ObjectName(sourcePad)}, bloco={ObjectName(sourceBlock)}, verificador={ObjectName(sourceVerifier)}.");
    }

    private static string ObjectName(Object target)
    {
        return target != null ? target.name : "null";
    }
}
