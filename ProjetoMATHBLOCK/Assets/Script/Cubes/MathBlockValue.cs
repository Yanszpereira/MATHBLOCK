using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MathBlockValue : MonoBehaviour
{
    private static Font cachedBuiltinFont;
    private const string LabelRootName = "ValueLabels";
    private const string LabelShaderName = "MathBlock/LabelOverlay";

    private static readonly (string name, Vector3 direction)[] FaceLabels =
    {
        ("ValueLabel_Front", Vector3.forward),
        ("ValueLabel_Back", Vector3.back),
        ("ValueLabel_Left", Vector3.left),
        ("ValueLabel_Right", Vector3.right)
    };

    private static readonly Color[] VibrantBlockColors =
    {
        new Color(1f, 0.05f, 0.02f),
        new Color(0.05f, 0.25f, 1f),
        new Color(1f, 0.88f, 0.02f),
        new Color(0.02f, 0.85f, 0.18f),
        new Color(1f, 0.28f, 0.02f),
        new Color(0.02f, 0.8f, 0.95f),
        new Color(0.55f, 0.02f, 1f),
        new Color(0.85f, 1f, 0.02f)
    };

    [SerializeField] private int currentValue = 1;
    [SerializeField] private bool updateScaleFromValue = false;
    [SerializeField] private float scaleStep = 0.15f;
    [SerializeField] private float minimumScaleMultiplier = 0.5f;
    [SerializeField] private float maximumScaleMultiplier = 3f;
    [SerializeField] private float labelSurfaceOffset = -0.0005f;
    [SerializeField] private float labelFontSize = 120f;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private bool randomizeColorOnStart = true;

    private Vector3 baseScale;
    private Quaternion originalRotation;
    private Material labelMaterial;
    private Material[] runtimeColorMaterials;
    private TextMesh[] valueLabels;
    private readonly List<AppliedBlockOperation> appliedOperationHistory = new List<AppliedBlockOperation>();

    public int CurrentValue => currentValue;

    private struct AppliedBlockOperation
    {
        public int previousTargetValue;
        public int consumedBlockValue;
        public string consumedBlockName;
        public RendererColorSnapshot[] consumedBlockColors;
    }

    private struct RendererColorSnapshot
    {
        public MaterialColorSnapshot[] materialColors;
    }

    private struct MaterialColorSnapshot
    {
        public bool hasBaseColor;
        public Color baseColor;
        public bool hasColor;
        public Color color;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapMathBlockLabels()
    {
        foreach (GameObject block in GameObject.FindGameObjectsWithTag("MathBlock"))
        {
            if (block.GetComponent<MathBlockValue>() == null)
            {
                MathBlockValue blockValue = block.AddComponent<MathBlockValue>();
                blockValue.randomizeColorOnStart = false;
            }
        }
    }

    private void Awake()
    {
        RemoveLegacyGridChildren();
        baseScale = transform.localScale;
        originalRotation = transform.rotation;
        currentValue = Mathf.Max(0, currentValue);
        RandomizeCubeColor();
        EnsureLabels();
        RefreshLabels();
        RefreshVisual();
    }

    private void OnDestroy()
    {
        if (labelMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(labelMaterial);
            }
            else
            {
                DestroyImmediate(labelMaterial);
            }
        }

        if (runtimeColorMaterials != null)
        {
            for (int i = 0; i < runtimeColorMaterials.Length; i++)
            {
                Material material = runtimeColorMaterials[i];
                if (material == null)
                    continue;

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
        }
    }

    public void SetValue(int newValue)
    {
        currentValue = Mathf.Max(0, newValue);
        RefreshLabels();
        RefreshVisual();
    }

    public void ResetRotationToOriginal()
    {
        transform.rotation = originalRotation;

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.linearVelocity = Vector3.zero;
        }
    }

    public bool TryApplyOperator(GravityInteract.PencilOperator operatorType, MathBlockValue consumedBlock)
    {
        if (consumedBlock == null)
            return false;

        int operandValue = consumedBlock.CurrentValue;
        int nextValue = currentValue;

        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                nextValue = currentValue + operandValue;
                break;

            case GravityInteract.PencilOperator.Subtraction:
                nextValue = currentValue - operandValue;
                if (nextValue < 0)
                    return false;
                break;

            case GravityInteract.PencilOperator.Multiplication:
                nextValue = currentValue * operandValue;
                break;

            case GravityInteract.PencilOperator.Division:
                if (operandValue <= 0 || currentValue % operandValue != 0)
                    return false;
                nextValue = currentValue / operandValue;
                break;

            default:
                return false;
        }

        AppliedBlockOperation operation = new AppliedBlockOperation
        {
            previousTargetValue = currentValue,
            consumedBlockValue = consumedBlock.CurrentValue,
            consumedBlockName = consumedBlock.name,
            consumedBlockColors = consumedBlock.CaptureRendererColors()
        };

        appliedOperationHistory.Add(operation);
        SetValue(nextValue);
        Debug.Log($"Bloco {name} atualizado para {currentValue} usando {operatorType}");
        return true;
    }

    public bool TryUndoLastOperation(float spawnHeight)
    {
        if (appliedOperationHistory.Count == 0)
            return false;

        int lastIndex = appliedOperationHistory.Count - 1;
        AppliedBlockOperation operation = appliedOperationHistory[lastIndex];
        appliedOperationHistory.RemoveAt(lastIndex);

        SetValue(operation.previousTargetValue);
        RestoreConsumedBlock(operation, spawnHeight);

        Debug.Log($"Bloco {name} desfez a ultima operacao e voltou para {currentValue}.");
        return true;
    }

    private void RemoveLegacyGridChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Grid"))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void EnsureLabels()
    {
        Transform labelsRoot = transform.Find(LabelRootName);
        if (labelsRoot == null)
        {
            GameObject rootObject = new GameObject(LabelRootName);
            rootObject.transform.SetParent(transform, false);
            labelsRoot = rootObject.transform;
        }

        if (valueLabels != null && valueLabels.Length == FaceLabels.Length)
            return;

        valueLabels = new TextMesh[FaceLabels.Length];
        Font builtinFont = GetBuiltinFont();
        if (builtinFont == null)
        {
            Debug.LogWarning("Nao foi possivel carregar a fonte embutida para os valores dos blocos.");
            return;
        }

        Material resolvedMaterial = GetOrCreateLabelMaterial(builtinFont);

        for (int i = 0; i < FaceLabels.Length; i++)
        {
            string childName = FaceLabels[i].name;
            Transform labelTransform = labelsRoot.Find(childName);
            if (labelTransform == null)
            {
                GameObject labelObject = new GameObject(childName);
                labelObject.transform.SetParent(labelsRoot, false);
                labelTransform = labelObject.transform;
            }

            TextMesh labelMesh = labelTransform.GetComponent<TextMesh>();
            if (labelMesh == null)
            {
                labelMesh = labelTransform.gameObject.AddComponent<TextMesh>();
            }

            labelMesh.font = builtinFont;
            labelMesh.characterSize = 0.04f;
            labelMesh.anchor = TextAnchor.MiddleCenter;
            labelMesh.alignment = TextAlignment.Center;
            labelMesh.color = labelColor;
            labelMesh.richText = false;

            Renderer labelRenderer = labelTransform.GetComponent<Renderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sharedMaterial = resolvedMaterial;
                labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
                labelRenderer.receiveShadows = false;
                labelRenderer.allowOcclusionWhenDynamic = false;
                labelRenderer.lightProbeUsage = LightProbeUsage.Off;
                labelRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            UpdateLabelTransform(labelTransform, FaceLabels[i].direction);
            valueLabels[i] = labelMesh;
        }
    }

    private Font GetBuiltinFont()
    {
        if (cachedBuiltinFont != null)
            return cachedBuiltinFont;

        cachedBuiltinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedBuiltinFont;
    }

    private Material GetOrCreateLabelMaterial(Font builtinFont)
    {
        if (labelMaterial != null)
        {
            labelMaterial.color = labelColor;
            labelMaterial.mainTexture = builtinFont.material.mainTexture;
            return labelMaterial;
        }

        Shader labelShader = Shader.Find(LabelShaderName);
        Material sourceMaterial = builtinFont.material;
        labelMaterial = labelShader != null
            ? new Material(labelShader)
            : new Material(sourceMaterial);

        labelMaterial.name = $"{name}_LabelMaterial";
        labelMaterial.hideFlags = HideFlags.HideAndDontSave;

        if (labelShader == null)
        {
            labelMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            labelMaterial.shader = sourceMaterial.shader;
            labelMaterial.renderQueue = sourceMaterial.renderQueue;
        }

        labelMaterial.mainTexture = sourceMaterial.mainTexture;
        labelMaterial.color = labelColor;
        return labelMaterial;
    }

    private Vector3 GetLocalCenter()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            return boxCollider.center;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.localBounds.center;
        }

        return Vector3.zero;
    }

    private Vector3 GetLocalHalfExtents()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            return boxCollider.size * 0.5f;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.localBounds.extents;
        }

        return Vector3.one * 0.5f;
    }

    private void RefreshLabels()
    {
        if (valueLabels == null)
            return;

        string valueText = currentValue.ToString();
        for (int i = 0; i < valueLabels.Length; i++)
        {
            TextMesh label = valueLabels[i];
            if (label == null)
                continue;

            UpdateLabelTransform(label.transform, FaceLabels[i].direction);
            label.text = valueText;
            label.color = labelColor;
            label.fontSize = Mathf.RoundToInt(labelFontSize);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;

            Renderer labelRenderer = label.GetComponent<Renderer>();
            if (labelRenderer != null && labelMaterial != null)
            {
                labelRenderer.sharedMaterial = labelMaterial;
            }
        }
    }

    private void UpdateLabelTransform(Transform labelTransform, Vector3 direction)
    {
        Vector3 center = GetLocalCenter();
        Vector3 halfExtents = GetLocalHalfExtents();
        float offset = Mathf.Clamp(labelSurfaceOffset, -0.01f, 0.01f);
        Vector3 faceOffset = center + new Vector3(
            direction.x * (halfExtents.x + offset),
            direction.y * (halfExtents.y + offset),
            direction.z * (halfExtents.z + offset)
        );

        labelTransform.localPosition = faceOffset;
        labelTransform.localRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        labelTransform.localScale = Vector3.one * 0.75f;
    }

    private void RefreshVisual()
    {
        if (!updateScaleFromValue)
            return;

        float multiplier = 1f + ((currentValue - 1f) * scaleStep);
        multiplier = Mathf.Clamp(multiplier, minimumScaleMultiplier, maximumScaleMultiplier);
        transform.localScale = baseScale * multiplier;
    }

    private void RandomizeCubeColor()
    {
        if (!Application.isPlaying || !randomizeColorOnStart)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return;

        Color randomColor = VibrantBlockColors[Random.Range(0, VibrantBlockColors.Length)];

        runtimeColorMaterials = new Material[renderers.Length];
        int materialCount = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            Material sourceMaterial = targetRenderer.sharedMaterial;
            if (sourceMaterial == null)
                continue;

            Material runtimeMaterial = new Material(sourceMaterial)
            {
                name = $"{name}_RuntimeColorMaterial"
            };

            ApplyColor(runtimeMaterial, randomColor);
            targetRenderer.material = runtimeMaterial;
            runtimeColorMaterials[materialCount] = runtimeMaterial;
            materialCount++;
        }

        if (materialCount != runtimeColorMaterials.Length)
        {
            System.Array.Resize(ref runtimeColorMaterials, materialCount);
        }
    }

    private void RestoreConsumedBlock(AppliedBlockOperation operation, float spawnHeight)
    {
        Vector3 spawnPosition = transform.position + Vector3.up * spawnHeight;
        GameObject restoredBlock = Instantiate(gameObject, spawnPosition, transform.rotation);
        restoredBlock.name = $"{operation.consumedBlockName}_Restored";

        MathBlockValue restoredValue = restoredBlock.GetComponent<MathBlockValue>();
        if (restoredValue != null)
        {
            restoredValue.InitializeRestoredBlock(operation.consumedBlockValue, operation.consumedBlockColors);
        }

        Rigidbody restoredRigidbody = restoredBlock.GetComponent<Rigidbody>();
        if (restoredRigidbody != null)
        {
            restoredRigidbody.isKinematic = false;
            restoredRigidbody.useGravity = true;
            restoredRigidbody.linearVelocity = Vector3.zero;
            restoredRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void InitializeRestoredBlock(int restoredValue, RendererColorSnapshot[] restoredColors)
    {
        appliedOperationHistory.Clear();
        SetValue(restoredValue);
        ApplyRendererColors(restoredColors);
        ResetRotationToOriginal();
    }

    private RendererColorSnapshot[] CaptureRendererColors()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        List<RendererColorSnapshot> snapshots = new List<RendererColorSnapshot>();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            Material[] materials = targetRenderer.materials;
            MaterialColorSnapshot[] materialSnapshots = new MaterialColorSnapshot[materials.Length];

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                    continue;

                MaterialColorSnapshot materialSnapshot = new MaterialColorSnapshot();
                if (material.HasProperty("_BaseColor"))
                {
                    materialSnapshot.hasBaseColor = true;
                    materialSnapshot.baseColor = material.GetColor("_BaseColor");
                }

                if (material.HasProperty("_Color"))
                {
                    materialSnapshot.hasColor = true;
                    materialSnapshot.color = material.GetColor("_Color");
                }

                materialSnapshots[materialIndex] = materialSnapshot;
            }

            snapshots.Add(new RendererColorSnapshot
            {
                materialColors = materialSnapshots
            });
        }

        return snapshots.ToArray();
    }

    private void ApplyRendererColors(RendererColorSnapshot[] snapshots)
    {
        if (snapshots == null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        int snapshotIndex = 0;

        for (int rendererIndex = 0; rendererIndex < renderers.Length && snapshotIndex < snapshots.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            Material[] materials = targetRenderer.materials;
            MaterialColorSnapshot[] materialSnapshots = snapshots[snapshotIndex].materialColors;
            int materialCount = Mathf.Min(materials.Length, materialSnapshots.Length);

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                ApplyColorSnapshot(materials[materialIndex], materialSnapshots[materialIndex]);
            }

            targetRenderer.materials = materials;
            snapshotIndex++;
        }
    }

    private bool IsLabelRenderer(Renderer targetRenderer)
    {
        Transform current = targetRenderer.transform;
        while (current != null && current != transform)
        {
            if (current.name == LabelRootName || current.GetComponent<TextMesh>() != null)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void ApplyColorSnapshot(Material material, MaterialColorSnapshot snapshot)
    {
        if (material == null)
            return;

        if (snapshot.hasBaseColor && material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", snapshot.baseColor);
        }

        if (snapshot.hasColor && material.HasProperty("_Color"))
        {
            material.SetColor("_Color", snapshot.color);
        }
    }
}
