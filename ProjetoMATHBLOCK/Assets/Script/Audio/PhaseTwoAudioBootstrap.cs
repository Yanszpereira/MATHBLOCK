using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Completa a configuração sonora que a Fase 1 possuía na cena.</summary>
public sealed class PhaseTwoAudioBootstrap : MonoBehaviour
{
    private const string PhaseTwoName = "Fase 2";
    private const string MusicEvent = "event:/735157__rotlily__simple-music-loop-bass-keys-drums";
    private EventInstance musicInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (SceneManager.GetActiveScene().name != PhaseTwoName
            || FindFirstObjectByType<PhaseTwoAudioBootstrap>() != null)
            return;

        new GameObject("Fase 2 Audio").AddComponent<PhaseTwoAudioBootstrap>();
    }

    private void Start()
    {
        musicInstance = RuntimeManager.CreateInstance(MusicEvent);
        musicInstance.start();
    }

    private void OnDestroy()
    {
        if (!musicInstance.isValid()) return;
        musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicInstance.release();
        musicInstance.clearHandle();
    }
}
