using System;
using System.Collections.Generic;

/// <summary>Testable selection and value-pool helpers used by the arena spawner.</summary>
public static class Fase4SpawnSelection
{
    public static int ChooseRegionIndex(int regionCount, int previousIndex, Random random)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (regionCount <= 0) throw new ArgumentOutOfRangeException(nameof(regionCount));
        if (regionCount == 1) return 0;
        if (previousIndex < 0 || previousIndex >= regionCount)
            return random.Next(regionCount);

        int selected = random.Next(regionCount - 1);
        if (selected >= previousIndex)
            selected++;
        return selected;
    }

    public static int[] BuildValues(
        Fase4ExpressionDefinition expression,
        Random random,
        out int[] distractors)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (random == null) throw new ArgumentNullException(nameof(random));

        int[] operands = expression.Operands;
        if (operands.Length != 4)
            throw new ArgumentException("A expressão da Fase 4 precisa conter quatro operandos.", nameof(expression));

        distractors = Fase4DistractorGenerator.GenerateForExpression(expression, random);
        var values = new List<int>(operands.Length + distractors.Length);
        values.AddRange(operands);
        values.AddRange(distractors);
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values.ToArray();
    }
}
