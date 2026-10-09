using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MATHBLOCK/Fase 4/Banco de expressões", fileName = "Fase4ExpressionBank")]
public sealed class Fase4ExpressionBank : ScriptableObject
{
    [SerializeField] private List<Fase4ExpressionDefinition> expressions = new List<Fase4ExpressionDefinition>();

    public IReadOnlyList<Fase4ExpressionDefinition> Expressions => expressions;

    public void Configure(IEnumerable<Fase4ExpressionDefinition> definitions)
    {
        expressions = definitions != null
            ? new List<Fase4ExpressionDefinition>(definitions)
            : new List<Fase4ExpressionDefinition>();
    }
}
