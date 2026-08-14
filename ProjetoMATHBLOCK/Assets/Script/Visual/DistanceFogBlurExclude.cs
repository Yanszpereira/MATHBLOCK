using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DistanceFogBlurExclude : MonoBehaviour
{
    private static readonly HashSet<DistanceFogBlurExclude> activeExclusions = new HashSet<DistanceFogBlurExclude>();
    private Renderer[] cachedRenderers;
    public static IEnumerable<DistanceFogBlurExclude> ActiveExclusions => activeExclusions;
    public Renderer[] Renderers => cachedRenderers ??= GetComponentsInChildren<Renderer>(true);

    private void OnEnable()
    {
        activeExclusions.Add(this);
        cachedRenderers = null;
    }

    private void OnDisable()
    {
        activeExclusions.Remove(this);
    }

    private void OnTransformChildrenChanged()
    {
        cachedRenderers = null;
    }
}
