using System.Collections.Generic;
using UnityEngine;

public sealed class MathBlockDitherController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float overlapAmount = 0.7f;
    [SerializeField, Range(0.05f, 1f)] private float overlapOpacity = 0.38f;
    [SerializeField] private float transitionSpeed = 8f;
    [SerializeField, Range(2f, 40f)] private float dotScale = 7f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int AmountId = Shader.PropertyToID("_DitherAmount");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ScaleId = Shader.PropertyToID("_DotScale");

    private sealed class State
    {
        public Renderer Renderer;
        public Material[] Originals;
        public Material[] Effects;
    }

    private readonly List<State> states = new List<State>();
    private float currentAmount;
    private float currentOpacity = 1f;
    private float targetAmount;
    private float targetOpacity = 1f;
    private bool active;
    private MathBlockValue blockValue;

    private void Awake()
    {
        blockValue = GetComponent<MathBlockValue>();
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        states.Clear();
        foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
        {
            if (ShouldExclude(target)) continue;
            Material[] originals = target.sharedMaterials;
            Material[] effects = (Material[])originals.Clone();
            for (int i = 0; i < originals.Length; i++)
            {
                Material original = originals[i];
                if (original == null) continue;
                // Clona o próprio ToonShader: aparência, bandas de luz, rim,
                // especular e contorno permanecem idênticos ao material original.
                Material effect = new Material(original) { name = original.name + " (Toon Dither Runtime)" };
                effect.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                effect.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                effect.SetFloat("_ZWrite", 0f);
                effect.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                effects[i] = effect;
            }
            states.Add(new State { Renderer = target, Originals = (Material[])originals.Clone(), Effects = effects });
        }
    }

    private static bool ShouldExclude(Renderer target)
    {
        if (target == null || target.GetComponentInParent<MathBlockDitherExclude>() != null) return true;
        Transform current = target.transform;
        while (current != null)
        {
            if (current.name == "ValueLabels" || current.GetComponent<TextMesh>() != null) return true;
            current = current.parent;
        }
        return false;
    }

    public void SetOverlapping(bool overlapping)
    {
        targetAmount = overlapping ? overlapAmount : 0f;
        targetOpacity = overlapping ? overlapOpacity : 1f;
        if (overlapping && !active) Activate();
    }

    private void Update()
    {
        if (!active) return;
        float smoothing = 1f - Mathf.Exp(-Mathf.Max(0.01f, transitionSpeed) * Time.deltaTime);
        currentAmount = Mathf.Lerp(currentAmount, targetAmount, smoothing);
        currentOpacity = Mathf.Lerp(currentOpacity, targetOpacity, smoothing);
        ApplyValues();
        if (targetAmount <= 0f && currentAmount < 0.002f && currentOpacity > 0.998f) ForceRestore();
    }

    private void Activate()
    {
        foreach (State state in states) state.Renderer.sharedMaterials = state.Effects;
        active = true;
        ApplyValues();
    }

    private void ApplyValues()
    {
        blockValue?.SetLabelOpacity(currentOpacity);

        foreach (State state in states)
            foreach (Material material in state.Effects)
                if (material != null)
                {
                    material.SetFloat(AmountId, currentAmount);
                    material.SetFloat(OpacityId, currentOpacity);
                    material.SetFloat(ScaleId, dotScale);
                }
    }

    public void ForceRestore()
    {
        foreach (State state in states) if (state.Renderer != null) state.Renderer.sharedMaterials = state.Originals;
        blockValue?.SetLabelOpacity(1f);
        active = false; currentAmount = targetAmount = 0f; currentOpacity = targetOpacity = 1f;
    }

    private void OnDisable() { ForceRestore(); }
    private void OnDestroy()
    {
        ForceRestore();
        foreach (State state in states) foreach (Material material in state.Effects) if (material != null) Destroy(material);
    }
}
