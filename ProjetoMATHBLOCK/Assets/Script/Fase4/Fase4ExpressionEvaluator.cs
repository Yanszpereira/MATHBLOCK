using System;
using System.Collections.Generic;

public enum Fase4ArithmeticOperation
{
    Add,
    Subtract,
    Multiply
}

public static class Fase4ExpressionEvaluator
{
    public static bool TryEvaluate(Fase4ExpressionDefinition expression, out int result)
    {
        result = 0;
        if (expression == null) return false;
        return TryEvaluate(expression.Operands, expression.Operations, expression.ParenthesisPairs, out result);
    }

    public static bool TryEvaluate(
        int[] operands,
        Fase4ArithmeticOperation[] operations,
        Fase4ExpressionDefinition.ParenthesisPair[] pairs,
        out int result)
    {
        result = 0;
        if (operands == null || operands.Length != 4 || operations == null || operations.Length != 3)
            return false;
        if (pairs == null || pairs.Length < 1 || pairs.Length > 2)
            return false;
        foreach (int operand in operands)
            if (operand < 1 || operand > 20) return false;
        foreach (Fase4ArithmeticOperation operation in operations)
            if (!Enum.IsDefined(typeof(Fase4ArithmeticOperation), operation)) return false;

        var pairAtStart = new Dictionary<int, Fase4ExpressionDefinition.ParenthesisPair>();
        var occupied = new bool[4];
        foreach (var pair in pairs)
        {
            if (pair.firstOperand < 0 || pair.lastOperand >= 4 ||
                pair.lastOperand <= pair.firstOperand || pairAtStart.ContainsKey(pair.firstOperand))
                return false;
            for (int i = pair.firstOperand; i <= pair.lastOperand; i++)
            {
                if (occupied[i]) return false;
                occupied[i] = true;
            }
            pairAtStart.Add(pair.firstOperand, pair);
        }

        var values = new List<int>();
        var outerOps = new List<Fase4ArithmeticOperation>();
        int index = 0;
        while (index < operands.Length)
        {
            if (pairAtStart.TryGetValue(index, out var pair))
            {
                if (!TryEvaluateFlatRange(
                    operands, operations, pair.firstOperand, pair.lastOperand, out int grouped))
                    return false;
                values.Add(grouped);
                index = pair.lastOperand + 1;
                if (index < operands.Length)
                    outerOps.Add(operations[index - 1]);
            }
            else
            {
                values.Add(operands[index]);
                index++;
                if (index < operands.Length)
                    outerOps.Add(operations[index - 1]);
            }
        }

        if (outerOps.Count != values.Count - 1) return false;
        return TryEvaluateFlatValues(values, outerOps, out result);
    }

    public static bool IsCompatibleWithMathBlockOperations(Fase4ArithmeticOperation operation)
    {
        string memberName;
        switch (operation)
        {
            case Fase4ArithmeticOperation.Add: memberName = "Addition"; break;
            case Fase4ArithmeticOperation.Subtract: memberName = "Subtraction"; break;
            case Fase4ArithmeticOperation.Multiply: memberName = "Multiplication"; break;
            default: return false;
        }

        return Enum.IsDefined(typeof(GravityInteract.PencilOperator),
            Enum.Parse(typeof(GravityInteract.PencilOperator), memberName));
    }

    public static string GetSymbol(Fase4ArithmeticOperation operation)
    {
        switch (operation)
        {
            case Fase4ArithmeticOperation.Add: return "+";
            case Fase4ArithmeticOperation.Subtract: return "−";
            case Fase4ArithmeticOperation.Multiply: return "×";
            default: return "?";
        }
    }

    private static bool TryEvaluateFlatRange(
        int[] operands,
        Fase4ArithmeticOperation[] operations,
        int first,
        int last,
        out int result)
    {
        var values = new List<int>();
        var ops = new List<Fase4ArithmeticOperation>();
        for (int i = first; i <= last; i++)
        {
            values.Add(operands[i]);
            if (i < last) ops.Add(operations[i]);
        }
        return TryEvaluateFlatValues(values, ops, out result);
    }

    private static bool TryEvaluateFlatValues(
        List<int> values,
        List<Fase4ArithmeticOperation> operations,
        out int result)
    {
        result = 0;
        if (values == null || operations == null || values.Count == 0 ||
            operations.Count != values.Count - 1)
            return false;

        var terms = new List<int> { values[0] };
        var additiveOps = new List<Fase4ArithmeticOperation>();
        for (int i = 0; i < operations.Count; i++)
        {
            Fase4ArithmeticOperation operation = operations[i];
            int right = values[i + 1];
            if (operation == Fase4ArithmeticOperation.Multiply)
            {
                if (!TryApply(terms[terms.Count - 1], right, operation, out int product))
                    return false;
                terms[terms.Count - 1] = product;
            }
            else
            {
                additiveOps.Add(operation);
                terms.Add(right);
            }
        }

        int accumulated = terms[0];
        for (int i = 0; i < additiveOps.Count; i++)
            if (!TryApply(accumulated, terms[i + 1], additiveOps[i], out accumulated))
                return false;

        result = accumulated;
        return true;
    }

    private static bool TryApply(int left, int right, Fase4ArithmeticOperation operation, out int value)
    {
        long wide;
        switch (operation)
        {
            case Fase4ArithmeticOperation.Add: wide = (long)left + right; break;
            case Fase4ArithmeticOperation.Subtract: wide = (long)left - right; break;
            case Fase4ArithmeticOperation.Multiply: wide = (long)left * right; break;
            default: value = 0; return false;
        }

        if (wide < 1 || wide > 50)
        {
            value = 0;
            return false;
        }

        value = (int)wide;
        return true;
    }


private static bool TryEvaluateFlatValuesWithTrace(
    List<int> values,
    List<Fase4ArithmeticOperation> operations,
    List<int> trace,
    out int result)
{
    result = 0;
    if (values == null || operations == null || trace == null || values.Count == 0 ||
        operations.Count != values.Count - 1)
        return false;

    var terms = new List<int> { values[0] };
    var additiveOps = new List<Fase4ArithmeticOperation>();
    for (int i = 0; i < operations.Count; i++)
    {
        Fase4ArithmeticOperation operation = operations[i];
        int right = values[i + 1];
        if (operation == Fase4ArithmeticOperation.Multiply)
        {
            if (!TryApply(terms[terms.Count - 1], right, operation, out int product))
                return false;
            terms[terms.Count - 1] = product;
            trace.Add(product);
        }
        else
        {
            additiveOps.Add(operation);
            terms.Add(right);
        }
    }

    int accumulated = terms[0];
    for (int i = 0; i < additiveOps.Count; i++)
    {
        if (!TryApply(accumulated, terms[i + 1], additiveOps[i], out accumulated))
            return false;
        trace.Add(accumulated);
    }

    result = accumulated;
    return true;
}


private static bool TryEvaluateFlatRangeWithTrace(
    int[] operands,
    Fase4ArithmeticOperation[] operations,
    int first,
    int last,
    List<int> trace,
    out int result)
{
    var values = new List<int>();
    var ops = new List<Fase4ArithmeticOperation>();
    for (int i = first; i <= last; i++)
    {
        values.Add(operands[i]);
        if (i < last) ops.Add(operations[i]);
    }
    return TryEvaluateFlatValuesWithTrace(values, ops, trace, out result);
}


public static bool TryEvaluateWithIntermediateValues(
    Fase4ExpressionDefinition expression,
    out int result,
    out int[] intermediateResults)
{
    result = 0;
    intermediateResults = Array.Empty<int>();
    if (expression == null || !TryEvaluate(expression, out result))
        return false;

    int[] operands = expression.Operands;
    Fase4ArithmeticOperation[] operations = expression.Operations;
    var pairs = expression.ParenthesisPairs;
    var pairAtStart = new Dictionary<int, Fase4ExpressionDefinition.ParenthesisPair>();
    foreach (var pair in pairs)
        pairAtStart[pair.firstOperand] = pair;

    var trace = new List<int>();
    var values = new List<int>();
    var outerOps = new List<Fase4ArithmeticOperation>();
    int index = 0;
    while (index < operands.Length)
    {
        if (pairAtStart.TryGetValue(index, out var pair))
        {
            if (!TryEvaluateFlatRangeWithTrace(operands, operations, pair.firstOperand, pair.lastOperand, trace, out int grouped))
                return false;
            values.Add(grouped);
            index = pair.lastOperand + 1;
            if (index < operands.Length) outerOps.Add(operations[index - 1]);
        }
        else
        {
            values.Add(operands[index]);
            index++;
            if (index < operands.Length) outerOps.Add(operations[index - 1]);
        }
    }

    if (!TryEvaluateFlatValuesWithTrace(values, outerOps, trace, out int finalResult) || finalResult != result)
        return false;

    intermediateResults = trace.ToArray();
    return intermediateResults.Length == 3;
}
}
