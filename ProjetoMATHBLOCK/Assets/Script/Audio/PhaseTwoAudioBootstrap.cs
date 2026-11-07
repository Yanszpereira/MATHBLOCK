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
    private float musicVolume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneInitializer()
    {
        GlobalSceneBootstrap.Register(Install);
    }

    private static void Install(Scene scene)
    {
        if (scene.name != PhaseTwoName
            || FindFirstObjectByType<PhaseTwoAudioBootstrap>() != null)
            return;

        new GameObject("Fase 2 Audio").AddComponent<PhaseTwoAudioBootstrap>();
    }

    private void Start()
    {
        musicInstance = RuntimeManager.CreateInstance(MusicEvent);
        musicInstance.start();
        SetMusicVolume(PlayerPrefs.GetFloat("MusicVolume", 1f));
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (!musicInstance.isValid())
            return;

        FMOD.RESULT result = musicInstance.setVolume(musicVolume);
        if (result != FMOD.RESULT.OK)
            Debug.LogError($"Failed to set Phase 2 music volume: {result}");
    }

    private void OnDestroy()
    {
        if (!musicInstance.isValid()) return;
        // A troca de fase já possui fade visual. Parar imediatamente impede que
        // a música da Fase 2 se sobreponha ao áudio inicial da Fase 1.
        musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        musicInstance.release();
        musicInstance.clearHandle();
    }
}
