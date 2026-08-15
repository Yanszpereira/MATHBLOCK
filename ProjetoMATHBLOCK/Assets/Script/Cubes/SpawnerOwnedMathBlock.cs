using UnityEngine;

/// <summary>
/// Mantém a origem de um MathBlock. O componente é copiado junto com o bloco,
/// então duplicatas também pertencem ao desafio que as gerou.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpawnerOwnedMathBlock : MonoBehaviour
{
    [SerializeField] private ProceduralMathBlockSpawner owner;

    public ProceduralMathBlockSpawner Owner => owner;

    public void Initialize(ProceduralMathBlockSpawner sourceSpawner)
    {
        owner = sourceSpawner;
    }
}
