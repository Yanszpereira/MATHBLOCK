using System.Collections;
using UnityEngine;

public class WinScreenController : MonoBehaviour
{
    [SerializeField, InspectorName("Tamanho inicial")]
    private Vector3 initialScale = new Vector3(1.5f, 1.5f, 1.5f);

    [SerializeField, InspectorName("Tamanho final")]
    private Vector3 finalScale = Vector3.one;

    [SerializeField, Min(0.01f), InspectorName("Velocidade da diminuição")]
    private float shrinkSpeed = 1f;

    private Coroutine animationCoroutine;

    private void OnEnable()
    {
        PlayAnimation();
    }

    private void OnDisable()
    {
        if (animationCoroutine == null)
            return;

        StopCoroutine(animationCoroutine);
        animationCoroutine = null;
    }

    public void PlayAnimation()
    {
        if (animationCoroutine != null)
            return;

        transform.localScale = initialScale;
        animationCoroutine = StartCoroutine(AnimateScale(initialScale, finalScale));
    }

    private IEnumerator AnimateScale(Vector3 from, Vector3 to)
    {
        float progress = 0f;

        while (progress < 1f)
        {
            progress = Mathf.Min(progress + Time.unscaledDeltaTime * shrinkSpeed, 1f);
            transform.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        transform.localScale = to;
        animationCoroutine = null;
    }
}
