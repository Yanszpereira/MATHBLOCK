using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ExpressionPuzzleController : MonoBehaviour
{
    public enum LayoutAxis
    {
        X,
        Y,
        Z
    }

    public enum LayoutDirection
    {
        Forward,
        Reverse
    }

    public enum ArithmeticOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public enum ExpressionGrouping
    {
        Left,
        Right
    }

    [Serializable]
    public sealed class ExpressionDefinition
    {
        [SerializeField] private string name = "Expressao";
        [SerializeField] private ArithmeticOperator firstOperator = ArithmeticOperator.Add;
        [SerializeField] private ArithmeticOperator secondOperator = ArithmeticOperator.Subtract;
        [SerializeField] private ExpressionGrouping grouping = ExpressionGrouping.Left;

        public string Name => name;
        public ArithmeticOperator FirstOperator => firstOperator;
        public ArithmeticOperator SecondOperator => secondOperator;
        public ExpressionGrouping Grouping => grouping;
        public bool UsesRestrictedOperands =>
            IsRestrictedOperator(firstOperator) || IsRestrictedOperator(secondOperator);

        public ExpressionDefinition()
        {
        }

        public ExpressionDefinition(
            string expressionName,
            ArithmeticOperator first,
            ArithmeticOperator second,
            ExpressionGrouping expressionGrouping)
        {
            name = expressionName;
            firstOperator = first;
            secondOperator = second;
            grouping = expressionGrouping;
        }

        public string BuildDisplayText()
        {
            string firstSymbol = GetOperatorSymbol(firstOperator);
            string secondSymbol = GetOperatorSymbol(secondOperator);

            return grouping == ExpressionGrouping.Left
                ? $"(Pad1 {firstSymbol} Pad2) {secondSymbol} Pad3"
                : $"Pad1 {firstSymbol} (Pad2 {secondSymbol} Pad3)";
        }

        private static bool IsRestrictedOperator(ArithmeticOperator operation)
        {
            return operation == ArithmeticOperator.Multiply || operation == ArithmeticOperator.Divide;
        }
    }

    [Header("Pads")]
    [SerializeField] private PadMathBlockDetector pad1;
    [SerializeField] private PadMathBlockDetector pad2;
    [SerializeField] private PadMathBlockDetector pad3;

    [Header("Layout")]
    [SerializeField] private Vector3 layoutCenter;
    [SerializeField] private Vector3 layoutSize = new Vector3(1f, 1f, 24f);
    [SerializeField] private LayoutAxis layoutAxis = LayoutAxis.Z;
    [SerializeField] private LayoutDirection layoutDirection = LayoutDirection.Forward;
    [SerializeField, Min(0f)] private float edgePadding = 2f;
    [SerializeField] private bool applyLayoutInEditMode = true;

    [Header("Puzzle")]
    [SerializeField] private List<ExpressionDefinition> expressions = new List<ExpressionDefinition>
    {
        new ExpressionDefinition(
            "Soma e subtracao",
            ArithmeticOperator.Add,
            ArithmeticOperator.Subtract,
            ExpressionGrouping.Left),
        new ExpressionDefinition(
            "Subtracao e soma",
            ArithmeticOperator.Subtract,
            ArithmeticOperator.Add,
            ExpressionGrouping.Left),
        new ExpressionDefinition(
            "Multiplicacao e subtracao",
            ArithmeticOperator.Multiply,
            ArithmeticOperator.Subtract,
            ExpressionGrouping.Left),
        new ExpressionDefinition(
            "Divisao e soma",
            ArithmeticOperator.Divide,
            ArithmeticOperator.Add,
            ExpressionGrouping.Left)
    };
    [SerializeField] private int minimumTarget;
    [SerializeField] private int maximumTarget = 20;
    [SerializeField] private bool randomizeOnStart = true;
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed = 12345;

    [Header("Apresentacao")]
    [SerializeField] private GameObject expressionDisplayObject;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text currentResultText;
    [SerializeField] private UnityEvent onSolved;

    [Header("Efeito visual de acerto")]
    [SerializeField] private Color verifierIdleColor = Color.red;
    [SerializeField] private Color verifierSolvedColor = Color.green;
    [SerializeField, Min(0f)] private float verifierTurnOnDuration = 0.12f;
    [SerializeField, Min(0f)] private float verifierTurnOffDuration = 0.75f;

    private TMP_Text expressionText;
    private Coroutine verifierColorRoutine;
    private ExpressionDefinition currentExpression;
    private int targetValue;
    private bool isSolved;
    private System.Random random;

    public ExpressionDefinition CurrentExpression => currentExpression;
    public int TargetValue => targetValue;
    public bool IsSolved => isSolved;
    public event Action Solved;
    public bool ApplyLayoutInEditMode => applyLayoutInEditMode;

    private void OnEnable()
    {
        SubscribeToPads();
    }

    private void Start()
    {
        SubscribeToPads();
        ApplyLayout();
        CacheExpressionText();

        if (randomizeOnStart)
        {
            RandomizePuzzle();
        }
        else
        {
            currentExpression = GetFirstValidExpression();
            targetValue = minimumTarget;
            RefreshPresentation();
            EvaluateCurrentState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromPads();
    }

    private void OnValidate()
    {
        layoutSize = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(layoutSize.x)),
            Mathf.Max(0.01f, Mathf.Abs(layoutSize.y)),
            Mathf.Max(0.01f, Mathf.Abs(layoutSize.z)));
        edgePadding = Mathf.Max(0f, edgePadding);
        verifierTurnOnDuration = Mathf.Max(0f, verifierTurnOnDuration);
        verifierTurnOffDuration = Mathf.Max(0f, verifierTurnOffDuration);

        if (maximumTarget < minimumTarget)
            maximumTarget = minimumTarget;
    }

    public void RandomizePuzzle()
    {
        currentExpression = GetRandomExpression();
        targetValue = NextInclusive(minimumTarget, maximumTarget);
        isSolved = false;

        RefreshPresentation();
        EvaluateCurrentState();
    }

    public void ApplyLayout()
    {
        PadMathBlockDetector[] pads = GetPads();

        for (int i = 0; i < pads.Length; i++)
        {
            if (pads[i] == null)
                continue;

            int layoutIndex = layoutDirection == LayoutDirection.Forward
                ? i
                : pads.Length - 1 - i;
            Vector3 localPosition = CalculatePadLocalPosition(
                layoutCenter,
                layoutSize,
                layoutAxis,
                edgePadding,
                layoutIndex,
                pads.Length);

            pads[i].transform.position = transform.TransformPoint(localPosition);
        }
    }

    public Transform[] GetPadTransforms()
    {
        PadMathBlockDetector[] pads = GetPads();
        Transform[] transforms = new Transform[pads.Length];

        for (int i = 0; i < pads.Length; i++)
            transforms[i] = pads[i] != null ? pads[i].transform : null;

        return transforms;
    }

    public void EvaluateCurrentState()
    {
        if (currentExpression == null)
        {
            SetResultText(string.Empty);
            return;
        }

        PadMathBlockDetector[] pads = GetPads();
        int[] values = new int[pads.Length];

        for (int i = 0; i < pads.Length; i++)
        {
            PadMathBlockDetector pad = pads[i];
            if (pad == null || pad.HasMultipleBlocks || !pad.TryGetCurrentValue(out values[i]))
            {
                SetResultText("...");
                return;
            }
        }

        if (currentExpression.UsesRestrictedOperands)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!IsAllowedRestrictedOperand(values[i]))
                {
                    SetResultText("Valor invalido");
                    return;
                }
            }
        }

        if (!TryEvaluate(currentExpression, values, out int result))
        {
            SetResultText("Operacao invalida");
            return;
        }

        SetResultText(result.ToString());

        if (!isSolved && result == targetValue)
        {
            isSolved = true;
            PlayVerifierSolvedEffect();
            onSolved?.Invoke();
            Solved?.Invoke();
        }
    }

    public static bool IsAllowedRestrictedOperand(int value)
    {
        return value >= 0 && (value <= 5 || value == 10);
    }

    public static bool TryEvaluate(
        ExpressionDefinition expression,
        IReadOnlyList<int> padValues,
        out int result)
    {
        result = 0;

        if (expression == null || padValues == null || padValues.Count != 3)
            return false;

        if (expression.UsesRestrictedOperands)
        {
            for (int i = 0; i < padValues.Count; i++)
            {
                if (!IsAllowedRestrictedOperand(padValues[i]))
                    return false;
            }
        }

        if (expression.Grouping == ExpressionGrouping.Left)
        {
            if (!TryApplyOperator(
                    padValues[0],
                    padValues[1],
                    expression.FirstOperator,
                    out int intermediate))
            {
                return false;
            }

            return TryApplyOperator(
                intermediate,
                padValues[2],
                expression.SecondOperator,
                out result);
        }

        if (!TryApplyOperator(
                padValues[1],
                padValues[2],
                expression.SecondOperator,
                out int rightIntermediate))
        {
            return false;
        }

        return TryApplyOperator(
            padValues[0],
            rightIntermediate,
            expression.FirstOperator,
            out result);
    }

    public static Vector3 CalculatePadLocalPosition(
        Vector3 center,
        Vector3 size,
        LayoutAxis axis,
        float padding,
        int index,
        int count)
    {
        if (count <= 0)
            return center;

        index = Mathf.Clamp(index, 0, count - 1);
        Vector3 direction = GetAxisDirection(axis);
        float axisLength = GetAxisLength(size, axis);
        float usableLength = Mathf.Max(0f, axisLength - (Mathf.Max(0f, padding) * 2f));
        float offset = count == 1
            ? 0f
            : (-usableLength * 0.5f) + (usableLength * index / (count - 1f));

        return center + (direction * offset);
    }

    private void HandlePadValueChanged(PadMathBlockDetector changedPad, int? value)
    {
        EvaluateCurrentState();
    }

    private void SubscribeToPads()
    {
        foreach (PadMathBlockDetector pad in GetPads())
        {
            if (pad == null)
                continue;

            pad.CurrentValueChanged -= HandlePadValueChanged;
            pad.CurrentValueChanged += HandlePadValueChanged;
        }
    }

    private void UnsubscribeFromPads()
    {
        foreach (PadMathBlockDetector pad in GetPads())
        {
            if (pad != null)
                pad.CurrentValueChanged -= HandlePadValueChanged;
        }
    }

    private PadMathBlockDetector[] GetPads()
    {
        return new[] { pad1, pad2, pad3 };
    }

    private ExpressionDefinition GetRandomExpression()
    {
        List<ExpressionDefinition> validExpressions = new List<ExpressionDefinition>();

        if (expressions != null)
        {
            foreach (ExpressionDefinition expression in expressions)
            {
                if (expression != null)
                    validExpressions.Add(expression);
            }
        }

        if (validExpressions.Count == 0)
        {
            Debug.LogError($"{name}: nenhuma expressao foi configurada.", this);
            return null;
        }

        EnsureRandom();
        return validExpressions[random.Next(0, validExpressions.Count)];
    }

    private ExpressionDefinition GetFirstValidExpression()
    {
        if (expressions == null)
            return null;

        foreach (ExpressionDefinition expression in expressions)
        {
            if (expression != null)
                return expression;
        }

        return null;
    }

    private int NextInclusive(int minimum, int maximum)
    {
        EnsureRandom();

        if (minimum >= maximum)
            return minimum;

        long range = (long)maximum - minimum + 1L;
        long offset = (long)(random.NextDouble() * range);
        return (int)((long)minimum + offset);
    }

    private void EnsureRandom()
    {
        if (random != null)
            return;

        int seed = useFixedSeed
            ? fixedSeed
            : unchecked(Environment.TickCount ^ GetInstanceID());

        random = new System.Random(seed);
    }

    private void RefreshPresentation()
    {
        TMP_Text displayText = GetExpressionText();
        if (displayText != null)
        {
            ResetVerifierColor();
            displayText.text = currentExpression != null
                ? $"{currentExpression.BuildDisplayText()} = {targetValue}"
                : "Expressao nao configurada";
        }

        if (targetText != null)
            targetText.text = targetValue.ToString();

        SetResultText(string.Empty);
    }

    private TMP_Text GetExpressionText()
    {
        if (expressionDisplayObject == null)
        {
            expressionText = null;
            return null;
        }

        if (expressionText == null
            || (expressionText.gameObject != expressionDisplayObject
                && !expressionText.transform.IsChildOf(expressionDisplayObject.transform)))
        {
            CacheExpressionText();
        }

        return expressionText;
    }

    private void CacheExpressionText()
    {
        expressionText = expressionDisplayObject != null
            ? expressionDisplayObject.GetComponentInChildren<TMP_Text>(true)
            : null;

        if (expressionDisplayObject != null && expressionText == null)
        {
            Debug.LogWarning(
                $"{name}: o objeto de apresentacao {expressionDisplayObject.name} nao possui TMP_Text nos filhos.",
                this);
        }
    }

    private void PlayVerifierSolvedEffect()
    {
        TMP_Text displayText = GetExpressionText();
        if (displayText == null)
            return;

        if (verifierColorRoutine != null)
            StopCoroutine(verifierColorRoutine);

        verifierColorRoutine = StartCoroutine(AnimateVerifierSolvedColor(displayText));
    }

    private IEnumerator AnimateVerifierSolvedColor(TMP_Text displayText)
    {
        yield return FadeTextColor(
            displayText,
            displayText.color,
            verifierSolvedColor,
            verifierTurnOnDuration);

        verifierColorRoutine = null;
    }

    private static IEnumerator FadeTextColor(
        TMP_Text displayText,
        Color startColor,
        Color endColor,
        float duration)
    {
        if (duration <= 0f)
        {
            displayText.color = endColor;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
            displayText.color = Color.Lerp(startColor, endColor, easedTime);
            yield return null;
        }

        displayText.color = endColor;
    }

    private void ResetVerifierColor()
    {
        if (verifierColorRoutine != null)
        {
            StopCoroutine(verifierColorRoutine);
            verifierColorRoutine = null;
        }

        if (expressionText != null)
            expressionText.color = verifierIdleColor;
    }

    private void SetResultText(string value)
    {
        if (currentResultText != null)
            currentResultText.text = value;
    }

    private static bool TryApplyOperator(
        int left,
        int right,
        ArithmeticOperator operation,
        out int result)
    {
        result = 0;
        long calculated;

        switch (operation)
        {
            case ArithmeticOperator.Add:
                calculated = (long)left + right;
                break;

            case ArithmeticOperator.Subtract:
                calculated = (long)left - right;
                break;

            case ArithmeticOperator.Multiply:
                calculated = (long)left * right;
                break;

            case ArithmeticOperator.Divide:
                if (right == 0 || left % right != 0)
                    return false;

                calculated = left / right;
                break;

            default:
                return false;
        }

        if (calculated < int.MinValue || calculated > int.MaxValue)
            return false;

        result = (int)calculated;
        return true;
    }

    private static string GetOperatorSymbol(ArithmeticOperator operation)
    {
        switch (operation)
        {
            case ArithmeticOperator.Add:
                return "+";
            case ArithmeticOperator.Subtract:
                return "-";
            case ArithmeticOperator.Multiply:
                return "x";
            case ArithmeticOperator.Divide:
                return "÷";
            default:
                return "?";
        }
    }

    private static Vector3 GetAxisDirection(LayoutAxis axis)
    {
        switch (axis)
        {
            case LayoutAxis.X:
                return Vector3.right;
            case LayoutAxis.Y:
                return Vector3.up;
            case LayoutAxis.Z:
                return Vector3.forward;
            default:
                return Vector3.forward;
        }
    }

    private static float GetAxisLength(Vector3 size, LayoutAxis axis)
    {
        switch (axis)
        {
            case LayoutAxis.X:
                return Mathf.Abs(size.x);
            case LayoutAxis.Y:
                return Mathf.Abs(size.y);
            case LayoutAxis.Z:
                return Mathf.Abs(size.z);
            default:
                return Mathf.Abs(size.z);
        }
    }

    private void OnDrawGizmos()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Color fill = new Color(0.15f, 0.75f, 1f, 0.12f);
        Color wire = new Color(0.15f, 0.75f, 1f, 0.9f);

        Gizmos.color = fill;
        Gizmos.DrawCube(layoutCenter, layoutSize);
        Gizmos.color = wire;
        Gizmos.DrawWireCube(layoutCenter, layoutSize);

        Gizmos.matrix = previousMatrix;
    }
}
