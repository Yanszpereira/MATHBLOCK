using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ponto unico para sistemas que precisam ser reaplicados depois de qualquer
/// troca de cena (MainMenu, LoadingScene, fases, reinicios e transicoes).
/// </summary>
public static class GlobalSceneBootstrap
{
    private sealed class InitializerEntry
    {
        public Action<Scene> Callback;
        public int Priority;
    }

    private static readonly List<InitializerEntry> Initializers = new List<InitializerEntry>();
    private static bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Initializers.Clear();
        listening = false;
    }

    public static void Register(Action<Scene> initializer, int priority = 0)
    {
        if (initializer == null)
            return;

        bool alreadyRegistered = Initializers.Exists(entry => entry.Callback == initializer);
        if (!alreadyRegistered)
        {
            Initializers.Add(new InitializerEntry
            {
                Callback = initializer,
                Priority = priority
            });
            Initializers.Sort((left, right) => left.Priority.CompareTo(right.Priority));
        }

        if (listening)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        listening = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        InitializerEntry[] snapshot = Initializers.ToArray();
        foreach (InitializerEntry initializer in snapshot)
        {
            try
            {
                initializer.Callback(scene);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
