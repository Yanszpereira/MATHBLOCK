using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SleepEyeTransition : MonoBehaviour
{
    private RectTransform upperLid;
    private RectTransform lowerLid;
    private float openingSpeed;
    private bool waitingForScene;

    public void Initialize(RectTransform upper, RectTransform lower)
    {
        upperLid = upper;
        lowerLid = lower;
    }

    public void SetClosedAmount(float amount)
    {
        float normalizedAmount = Mathf.Clamp01(amount);
        float upperLidSize = Mathf.SmoothStep(0f, 0.62f, normalizedAmount);
        float lowerLidProgress = Mathf.InverseLerp(0.18f, 1f, normalizedAmount);
        float lowerLidSize = Mathf.SmoothStep(0f, 0.4f, lowerLidProgress);

        upperLid.anchorMin = new Vector2(0f, 1f - upperLidSize);
        upperLid.anchorMax = Vector2.one;
        upperLid.offsetMin = Vector2.zero;
        upperLid.offsetMax = Vector2.zero;

        lowerLid.anchorMin = Vector2.zero;
        lowerLid.anchorMax = new Vector2(1f, lowerLidSize);
        lowerLid.offsetMin = Vector2.zero;
        lowerLid.offsetMax = Vector2.zero;
    }

    public void OpenEyesAfterNextScene(float speed)
    {
        openingSpeed = Mathf.Max(0.1f, speed);
        waitingForScene = true;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!waitingForScene)
            return;

        waitingForScene = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(OpenEyes());
    }

    private IEnumerator OpenEyes()
    {
        float closedAmount = 1f;
        while (closedAmount > 0f)
        {
            closedAmount = Mathf.MoveTowards(
                closedAmount,
                0f,
                openingSpeed * Time.unscaledDeltaTime);
            SetClosedAmount(closedAmount);
            yield return null;
        }

        SetClosedAmount(0f);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}