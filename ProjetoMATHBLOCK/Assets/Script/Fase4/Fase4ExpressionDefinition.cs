using System;
using UnityEngine;

[CreateAssetMenu(menuName = "MATHBLOCK/Fase 4/Expressão", fileName = "Fase4Expression")]
public sealed class Fase4ExpressionDefinition : ScriptableObject
{
    [Serializable]
    public struct ParenthesisPair
    {
        [Min(0)] public int firstOperand;
        [Min(1)] public int lastOperand;

        public ParenthesisPair(int first, int last)
        {
            firstOperand = first;
            lastOperand = last;
        }
    }

    [SerializeField] private string expressionId;
    [SerializeField] private int[] operands = new int[4];
    [SerializeField] private Fase4ArithmeticOperation[] operations = new Fase4ArithmeticOperation[3];
    [SerializeField] private ParenthesisPair[] parenthesisPairs = new ParenthesisPair[1];

    public string ExpressionId => expressionId;
    public int[] Operands => (int[])operands.Clone();
    public Fase4ArithmeticOperation[] Operations => (Fase4ArithmeticOperation[])operations.Clone();
    public ParenthesisPair[] ParenthesisPairs => (ParenthesisPair[])parenthesisPairs.Clone();

    public void Configure(string id, int[] values, Fase4ArithmeticOperation[] ops, ParenthesisPair[] pairs)
    {
        expressionId = id;
        operands = values != null ? (int[])values.Clone() : Array.Empty<int>();
        operations = ops != null ? (Fase4ArithmeticOperation[])ops.Clone() : Array.Empty<Fase4ArithmeticOperation>();
        parenthesisPairs = pairs != null ? (ParenthesisPair[])pairs.Clone() : Array.Empty<ParenthesisPair>();
    }

    public string BuildDisplayText()
    {
        if (operands == null || operations == null || operands.Length != 4 || operations.Length != 3)
            return string.Empty;

        var text = new System.Text.StringBuilder();
        for (int i = 0; i < operands.Length; i++)
        {
            foreach (ParenthesisPair pair in parenthesisPairs ?? Array.Empty<ParenthesisPair>())
                if (pair.firstOperand == i) text.Append('(');

            text.Append(operands[i]);

            foreach (ParenthesisPair pair in parenthesisPairs ?? Array.Empty<ParenthesisPair>())
                if (pair.lastOperand == i) text.Append(')');

            if (i < operations.Length)
            {
                text.Append(' ');
                text.Append(Fase4ExpressionEvaluator.GetSymbol(operations[i]));
                text.Append(' ');
            }
        }

        return text.ToString();
    }
}
