using UnityEngine;

public class RotationGlobe : MonoBehaviour
{
    [SerializeField] private Transform objetoParaGirar;
    [SerializeField] private Vector3 eixoLocal = Vector3.up;
    [SerializeField] private float velocidadeGrausPorSegundo = 30f;

    void Update()
    {
        Transform alvo = objetoParaGirar != null ? objetoParaGirar : transform;
        alvo.Rotate(eixoLocal.normalized * velocidadeGrausPorSegundo * Time.deltaTime, Space.Self);
    }
}
