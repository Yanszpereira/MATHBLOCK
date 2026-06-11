using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class DoorOpener : MonoBehaviour
{
    [Header("Right Door")]
    [SerializeField] private Animator rightDoorAnimator;
    [SerializeField] private AnimationClip rightDoorOpeningClip;

    [Header("Left Door")]
    [SerializeField] private Animator leftDoorAnimator;
    [SerializeField] private AnimationClip leftDoorOpeningClip;

    [Header("State")]
    [SerializeField] private bool hasOpened;

    private readonly List<PlayableGraph> activeGraphs = new List<PlayableGraph>();

    public bool HasOpened => hasOpened;

    private void Awake()
    {
        hasOpened = false;
        PrepareDoorForManualPlayback(rightDoorAnimator, "direita");
        PrepareDoorForManualPlayback(leftDoorAnimator, "esquerda");
    }

    public void OpenOnce()
    {
        if (hasOpened)
        {
            Debug.Log($"{name}: abertura da porta ignorada porque ja aconteceu nesta partida.");
            return;
        }

        hasOpened = true;
        bool openedRight = PlayDoorClip(rightDoorAnimator, rightDoorOpeningClip, "direita");
        bool openedLeft = PlayDoorClip(leftDoorAnimator, leftDoorOpeningClip, "esquerda");

        if (!openedRight && !openedLeft)
        {
            Debug.LogWarning($"{name}: nenhuma animacao de porta foi iniciada. Verifique Animator e AnimationClip no Inspector.");
        }
    }

    private bool PlayDoorClip(Animator animator, AnimationClip clip, string sideName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name}: Animator da porta {sideName} nao configurado.");
            return false;
        }

        if (clip == null)
        {
            Debug.LogWarning($"{name}: AnimationClip da porta {sideName} nao configurado.");
            return false;
        }

        PrepareDoorForManualPlayback(animator, sideName);
        animator.enabled = true;
        AnimationPlayableUtilities.PlayClip(animator, clip, out PlayableGraph graph);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        activeGraphs.Add(graph);
        StartCoroutine(HoldFinalPose(graph, clip.length));
        Debug.Log($"{name}: tocando animacao {clip.name} na porta {sideName}.");
        return true;
    }

    private void PrepareDoorForManualPlayback(Animator animator, string sideName)
    {
        if (animator == null)
            return;

        DisableLegacyAnimations(animator.gameObject);

        if (animator.runtimeAnimatorController != null)
        {
            Debug.Log($"{name}: removendo controller {animator.runtimeAnimatorController.name} da porta {sideName} para evitar autoplay do Animator.");
            animator.runtimeAnimatorController = null;
        }

        animator.enabled = false;
    }

    private static void DisableLegacyAnimations(GameObject root)
    {
        if (root == null)
            return;

        Animation[] legacyAnimations = root.GetComponentsInChildren<Animation>(true);
        foreach (Animation legacyAnimation in legacyAnimations)
        {
            legacyAnimation.playAutomatically = false;
            legacyAnimation.Stop();
            legacyAnimation.enabled = false;
        }
    }

    private IEnumerator HoldFinalPose(PlayableGraph graph, float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

        if (!graph.IsValid())
            yield break;

        Playable rootPlayable = graph.GetRootPlayable(0);
        if (rootPlayable.IsValid())
        {
            rootPlayable.SetTime(duration);
            rootPlayable.SetSpeed(0f);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < activeGraphs.Count; i++)
        {
            PlayableGraph graph = activeGraphs[i];
            if (graph.IsValid())
            {
                graph.Destroy();
            }
        }

        activeGraphs.Clear();
    }
}
