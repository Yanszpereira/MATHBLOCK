using System;
using System.Collections.Generic;

public static class Fase4DistractorGenerator
{
    public static int[] Generate(
        IEnumerable<int> sourceValues,
        IEnumerable<int> requiredValues,
        Random random,
        int? requestedCount = null)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        var sources = new List<int>();
        if (sourceValues != null) sources.AddRange(sourceValues);
        var forbidden = new HashSet<int>();
        if (requiredValues != null) foreach (int value in requiredValues) forbidden.Add(value);

        int count = requestedCount ?? random.Next(2, 4);
        if (count < 2 || count > 3) throw new ArgumentOutOfRangeException(nameof(requestedCount));

        for (int radius = 3; radius <= 50; radius++)
        {
            var candidates = new List<int>();
            for (int value = 1; value <= 50; value++)
            {
                if (forbidden.Contains(value)) continue;
                bool near = sources.Count == 0;
                for (int i = 0; i < sources.Count && !near; i++)
                    near = Math.Abs(value - sources[i]) <= radius;
                if (near) candidates.Add(value);
            }

            if (candidates.Count < count) continue;
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            return candidates.GetRange(0, count).ToArray();
        }

        throw new InvalidOperationException("Não há valores distintos suficientes para os distratores solicitados.");
    }


public static int[] GenerateForExpression(
    Fase4ExpressionDefinition expression,
    Random random,
    int? requestedCount = null)
{
    if (expression == null) throw new ArgumentNullException(nameof(expression));
    if (!Fase4ExpressionEvaluator.TryEvaluateWithIntermediateValues(
        expression, out int _, out int[] intermediateResults))
        throw new ArgumentException("A expressão precisa ser válida para gerar distratores.", nameof(expression));

    var neededValues = new List<int>(expression.Operands);
    neededValues.AddRange(intermediateResults);
    return Generate(neededValues, neededValues, random, requestedCount);
}
}
