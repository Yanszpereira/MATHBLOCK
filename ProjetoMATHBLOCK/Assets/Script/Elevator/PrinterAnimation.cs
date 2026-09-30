using System.Collections;
using UnityEngine;

public class PrinterAnimation : MonoBehaviour
{
    [SerializeField] private ElevatorTotemController elevator;
    [SerializeField] private Transform paper;
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
    [SerializeField] private Vector3 platformSideOffset = new Vector3(1.5f, 0f, 0f);
    [SerializeField, Min(0f)] private float paperEjectionDistance = 0.25f;
    [SerializeField, Min(0.01f)] private float paperEjectionDuration = 0.35f;
    [SerializeField] private float paperRotationDegrees = 20f;
    [SerializeField] private Vector3 paperRotationAxis = Vector3.right;
    [SerializeField, Min(0.01f)] private float paperRotationDuration = 0.3f;

    private Coroutine animation;
    private bool hasEjectedPaper;
    private Vector3 printerStartPosition;
    private Quaternion printerStartRotation;
    private Vector3 paperStartPosition;
    private Quaternion paperStartRotation;

    private void Awake()
    {
        printerStartPosition = transform.position;
        printerStartRotation = transform.rotation;

        if (paper != null)
        {
            paperStartPosition = paper.localPosition;
            paperStartRotation = paper.localRotation;
        }
    }

    private void OnEnable()
    {
        if (elevator != null)
        {
            elevator.ReachedFinalDestination += OnElevatorReachedFinalDestination;
            elevator.InitialCallRequested += OnInitialCallRequested;
        }
    }

    private void OnDisable()
    {
        if (elevator != null)
        {
            elevator.ReachedFinalDestination -= OnElevatorReachedFinalDestination;
            elevator.InitialCallRequested -= OnInitialCallRequested;
        }

        if (animation != null)
        {
            StopCoroutine(animation);
            animation = null;
        }
    }

    private void OnElevatorReachedFinalDestination(Transform platform)
    {
        if (platform == null)
            return;

        if (animation != null)
            StopCoroutine(animation);

        animation = StartCoroutine(MovePrinterAndEjectPaper(platform));
    }

    private void OnInitialCallRequested()
    {
        if (!hasEjectedPaper && transform.position == printerStartPosition)
            return;

        if (animation != null)
            StopCoroutine(animation);

        animation = StartCoroutine(ReturnPrinterAndPaper());
    }

    private IEnumerator MovePrinterAndEjectPaper(Transform platform)
    {
        Vector3 printerStart = transform.position;
        Vector3 printerDestination = platform.TransformPoint(platformSideOffset);
        float printerDistance = Vector3.Distance(printerStart, printerDestination);
        float printerDuration = printerDistance / Mathf.Max(moveSpeed, 0.01f);
        float elapsed = 0f;

        while (elapsed < printerDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / printerDuration);
            t = t * t * (3f - 2f * t);
            transform.position = Vector3.LerpUnclamped(printerStart, printerDestination, t);
            yield return null;
        }

        transform.position = printerDestination;

        if (paper != null && !hasEjectedPaper)
        {
            hasEjectedPaper = true;
            yield return EjectPaper();
        }

        animation = null;
    }

    private IEnumerator EjectPaper()
    {
        Vector3 paperStart = paper.localPosition;
        Vector3 paperDestination = paperStart + Vector3.forward * paperEjectionDistance;
        float elapsed = 0f;

        while (elapsed < paperEjectionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / paperEjectionDuration);
            t = t * t * (3f - 2f * t);
            paper.localPosition = Vector3.LerpUnclamped(paperStart, paperDestination, t);
            yield return null;
        }

        paper.localPosition = paperDestination;

        Quaternion rotationStart = paper.localRotation;
        Vector3 axis = paperRotationAxis.sqrMagnitude > 0.000001f
            ? paperRotationAxis.normalized
            : Vector3.right;
        Quaternion rotationDestination = rotationStart * Quaternion.AngleAxis(paperRotationDegrees, axis);
        elapsed = 0f;

        while (elapsed < paperRotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / paperRotationDuration);
            t = t * t * (3f - 2f * t);
            paper.localRotation = Quaternion.Slerp(rotationStart, rotationDestination, t);
            yield return null;
        }

        paper.localRotation = rotationDestination;
    }

    private IEnumerator ReturnPrinterAndPaper()
    {
        Vector3 printerPosition = transform.position;
        Quaternion printerRotation = transform.rotation;
        Vector3 paperPosition = paper != null ? paper.localPosition : Vector3.zero;
        Quaternion paperRotation = paper != null ? paper.localRotation : Quaternion.identity;
        float printerDuration = Vector3.Distance(printerPosition, printerStartPosition) / Mathf.Max(moveSpeed, 0.01f);
        float paperDuration = paper != null
            ? paperEjectionDuration + paperRotationDuration
            : 0f;
        float duration = Mathf.Max(printerDuration, paperDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float printerT = printerDuration > 0f ? Mathf.Clamp01(elapsed / printerDuration) : 1f;
            printerT = printerT * printerT * (3f - 2f * printerT);
            transform.position = Vector3.LerpUnclamped(printerPosition, printerStartPosition, printerT);
            transform.rotation = Quaternion.Slerp(printerRotation, printerStartRotation, printerT);

            if (paper != null)
            {
                float paperT = paperDuration > 0f ? Mathf.Clamp01(elapsed / paperDuration) : 1f;
                paperT = paperT * paperT * (3f - 2f * paperT);
                paper.localPosition = Vector3.LerpUnclamped(paperPosition, paperStartPosition, paperT);
                paper.localRotation = Quaternion.Slerp(paperRotation, paperStartRotation, paperT);
            }

            yield return null;
        }

        transform.position = printerStartPosition;
        transform.rotation = printerStartRotation;

        if (paper != null)
        {
            paper.localPosition = paperStartPosition;
            paper.localRotation = paperStartRotation;
        }

        hasEjectedPaper = false;
        animation = null;
    }
}
