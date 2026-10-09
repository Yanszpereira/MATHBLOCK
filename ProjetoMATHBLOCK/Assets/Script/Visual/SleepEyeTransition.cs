using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SleepEyeTransition : MonoBehaviour
{
    private const string PhaseOneSceneName = "Fase 1";
    private const string CutsceneAmbienceEventPath = "event:/SaladeAula";
    private const string PhaseOneMusicEventPath = "event:/MusicaFase1Certa";
    private const float MaximumFadeDeltaTime = 1f / 30f;

    private RectTransform upperLid;
    private RectTransform lowerLid;
    private float openingSpeed;
    private float phaseOneMusicFadeInDuration;
    private bool waitingForScene;
    private bool fadeInPhaseOneMusicAfterLoad;
    private bool phaseOneMusicFadeInRunning;

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

    public void BeginAudioCrossfade(
        string targetScene,
        float ambienceFadeOutDuration,
        float musicFadeInDuration)
    {
        if (!string.Equals(targetScene, PhaseOneSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        fadeInPhaseOneMusicAfterLoad = true;
        phaseOneMusicFadeInDuration = Mathf.Max(0f, musicFadeInDuration);

        StudioEventEmitter ambienceEmitter = FindEmitter(
            SceneManager.GetActiveScene(),
            CutsceneAmbienceEventPath);
        if (ambienceEmitter != null)
        {
            StartCoroutine(FadeEmitterVolume(
                ambienceEmitter,
                0f,
                Mathf.Max(0f, ambienceFadeOutDuration),
                true));
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!waitingForScene)
            return;

        waitingForScene = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (fadeInPhaseOneMusicAfterLoad &&
            string.Equals(scene.name, PhaseOneSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            fadeInPhaseOneMusicAfterLoad = false;
            phaseOneMusicFadeInRunning = true;
            StartCoroutine(FadeInPhaseOneMusic(scene));
        }

        StartCoroutine(OpenEyes());
    }

    private IEnumerator FadeInPhaseOneMusic(Scene scene)
    {
        StudioEventEmitter musicEmitter = FindEmitter(scene, PhaseOneMusicEventPath);
        if (musicEmitter == null)
        {
            phaseOneMusicFadeInRunning = false;
            yield break;
        }

        if (!musicEmitter.EventInstance.isValid())
            musicEmitter.Play();
        if (!musicEmitter.EventInstance.isValid())
        {
            phaseOneMusicFadeInRunning = false;
            yield break;
        }

        musicEmitter.EventInstance.setVolume(0f);
        yield return null;

        yield return FadeEmitterVolume(
            musicEmitter,
            1f,
            phaseOneMusicFadeInDuration,
            false);
        phaseOneMusicFadeInRunning = false;
    }

    private static IEnumerator FadeEmitterVolume(
        StudioEventEmitter emitter,
        float targetVolume,
        float duration,
        bool stopAtEnd)
    {
        if (emitter == null || !emitter.EventInstance.isValid())
            yield break;

        EventInstance instance = emitter.EventInstance;
        instance.getVolume(out float startVolume, out _);

        if (duration <= 0f)
        {
            instance.setVolume(targetVolume);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration && emitter != null && instance.isValid())
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaximumFadeDeltaTime);
                float progress = Mathf.Clamp01(elapsed / duration);
                instance.setVolume(Mathf.Lerp(startVolume, targetVolume, progress));
                yield return null;
            }

            if (instance.isValid())
                instance.setVolume(targetVolume);
        }

        if (stopAtEnd && emitter != null)
            emitter.Stop();
    }

    private static StudioEventEmitter FindEmitter(Scene scene, string eventPath)
    {
        FMOD.GUID eventGuid;
        try
        {
            eventGuid = RuntimeManager.PathToGUID(eventPath);
        }
        catch (EventNotFoundException)
        {
            return null;
        }

        foreach (StudioEventEmitter emitter in FindObjectsByType<StudioEventEmitter>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (emitter != null &&
                emitter.gameObject.scene == scene &&
                emitter.EventReference.Guid == eventGuid)
            {
                return emitter;
            }
        }

        return null;
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
        while (phaseOneMusicFadeInRunning)
            yield return null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
