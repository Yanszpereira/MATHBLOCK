using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ExpressionPuzzleController))]
public sealed class Fase4ExpressionDoorSignal : MonoBehaviour
{
    public enum DoorSide
    {
        Left,
        Right
    }

    [SerializeField] private ExpressionPuzzleController expression;
    [SerializeField] private Fase4DoorExpressionController door;
    [SerializeField] private DoorSide doorSide;

    private void Awake()
    {
        if (expression == null)
            expression = GetComponent<ExpressionPuzzleController>();
    }

    private void OnEnable()
    {
        if (expression == null)
            expression = GetComponent<ExpressionPuzzleController>();

        if (expression != null)
            expression.Solved += HandleExpressionSolved;
    }

    private void OnDisable()
    {
        if (expression != null)
            expression.Solved -= HandleExpressionSolved;
    }

    private void HandleExpressionSolved()
    {
        if (door == null)
            return;

        if (doorSide == DoorSide.Left)
            door.SignalLeftSolved();
        else
            door.SignalRightSolved();
    }
}
