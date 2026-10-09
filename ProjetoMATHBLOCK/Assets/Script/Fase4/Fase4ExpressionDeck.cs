using System;
using System.Collections.Generic;

public sealed class Fase4ExpressionDeck
{
    private readonly IReadOnlyList<Fase4ExpressionDefinition> expressions;
    private readonly Random random;
    private readonly List<Fase4ExpressionDefinition> remaining = new List<Fase4ExpressionDefinition>();
    private Fase4ExpressionDefinition lastDrawn;

    public Fase4ExpressionDeck(IReadOnlyList<Fase4ExpressionDefinition> source, Random randomSource)
    {
        expressions = source ?? throw new ArgumentNullException(nameof(source));
        random = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        if (expressions.Count == 0) throw new ArgumentException("O banco precisa conter expressões.", nameof(source));
        Refill();
    }

    public Fase4ExpressionDefinition Draw()
    {
        if (remaining.Count == 0) Refill();
        Fase4ExpressionDefinition selected = remaining[remaining.Count - 1];
        remaining.RemoveAt(remaining.Count - 1);
        lastDrawn = selected;
        return selected;
    }

    private void Refill()
    {
        remaining.Clear();
        for (int i = 0; i < expressions.Count; i++)
        {
            if (expressions[i] == null) throw new ArgumentException("O banco contém expressão nula.", nameof(expressions));
            remaining.Add(expressions[i]);
        }

        for (int i = remaining.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (remaining[i], remaining[j]) = (remaining[j], remaining[i]);
        }

        if (remaining.Count > 1 && ReferenceEquals(remaining[remaining.Count - 1], lastDrawn))
            (remaining[0], remaining[remaining.Count - 1]) = (remaining[remaining.Count - 1], remaining[0]);
    }
}
