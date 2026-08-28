using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MathBlockValue : MonoBehaviour
{
    private static Font cachedBuiltinFont;
    private const string LabelRootName = "ValueLabels";
    private const string LabelShaderName = "MathBlock/LabelOverlay";
    private const string ToonShaderName = "Custom/URPToonShader";
    private const string StretchBlockToonShaderName = "MathBlock/Stretch Block Toon";
    private const float LabelWorldScale = 0.75f;
    private const float MinimumParentScale = 0.0001f;

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
    [SerializeField] private float labelSurfaceOffset = 0.002f;
    [SerializeField] private float labelFontSize = 120f;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private bool randomizeColorOnStart = true;
    [SerializeField] private int blockId = -1;

    private Vector3 baseScale;
    private Quaternion originalRotation;
    private Material labelMaterial;
    private Material[] runtimeColorMaterials;
    private TextMesh[] valueLabels;
    private Camera labelCamera;
    private MaterialPropertyBlock propertyBlock;
    private Stack<DesfazerManager.Acao> operationStack = new Stack<DesfazerManager.Acao>();
    private bool hasPreviewValue;
    private int previewValue;
    private float labelOpacity = 1f;

    public int CurrentValue => currentValue;
    public int BlockId => blockId;
    public Stack<DesfazerManager.Acao> OperationStack => operationStack;
    public bool HasOperationsToUndo => operationStack != null && operationStack.Count > 0;

    public struct RendererColorSnapshot
    {
        public MaterialColorSnapshot[] materialColors;
        public bool hasPropertyBlock;
        public bool propertyBlockHasBaseColor;
        public Color propertyBlockBaseColor;
        public bool propertyBlockHasColor;
        public Color propertyBlockColor;
    }

    public struct MaterialColorSnapshot
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
        propertyBlock = new MaterialPropertyBlock();

        RemoveLegacyGridChildren();
        baseScale = transform.localScale;
        originalRotation = transform.rotation;
        currentValue = Mathf.Max(0, currentValue);

        ConfigureToonMaterials();
        RandomizeCubeColor();
        EnsureLabels();
        RefreshLabels();
        RefreshVisual();
        RegisterWithUndoManager();
    }

    private void LateUpdate()
    {
        UpdateLabelVisibility();
    }

    private void OnDestroy()
    {
        DesfazerManager undoManager;
        if (DesfazerManager.TryGetExistingInstance(out undoManager))
        {
            undoManager.UnregisterBlock(this);
        }

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
        ClearPreviewValue();
        currentValue = Mathf.Max(0, newValue);
        RefreshLabels();
        RefreshVisual();
    }

    public void SetPreviewValue(int newPreviewValue)
    {
        int clampedPreviewValue = Mathf.Max(0, newPreviewValue);
        if (hasPreviewValue && previewValue == clampedPreviewValue)
            return;

        previewValue = clampedPreviewValue;
        hasPreviewValue = true;
        RefreshLabels();
    }

    public void ClearPreviewValue()
    {
        if (!hasPreviewValue)
            return;

        hasPreviewValue = false;
        RefreshLabels();
    }

    public void SetLabelOpacity(float opacity)
    {
        labelOpacity = Mathf.Clamp01(opacity);
        ApplyLabelColors();
    }

    public void RestoreOriginalRotation()
    {
        transform.rotation = originalRotation;

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            if (!rigidbody.isKinematic)
            {
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.linearVelocity = Vector3.zero;
            }
        }
    }

    [System.Obsolete("Use RestoreOriginalRotation instead.")]
    public void ResetRotationToOriginal()
    {
        RestoreOriginalRotation();
    }

    public void SetBlockIdFromController(int newBlockId)
    {
        blockId = newBlockId;
    }

    public void InitializeDuplicatedBlock()
    {
        operationStack.Clear();
        DesfazerManager.Instance.AssignNewBlockId(this);
    }

    public void InitializeRestoredFromUndo(int restoredBlockId, Stack<DesfazerManager.Acao> restoredStack)
    {
        randomizeColorOnStart = false;
        operationStack = restoredStack ?? new Stack<DesfazerManager.Acao>();
        DesfazerManager.Instance.RestoreBlockId(this, restoredBlockId);
        RestoreOriginalRotation();
    }

    public void DetachFromUndoRuntime()
    {
        randomizeColorOnStart = false;
        DesfazerManager.Instance.UnregisterBlock(this);
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

        int previousTargetValue = currentValue;

        if (!DesfazerManager.Instance.TryRecordOperation(this, consumedBlock, operatorType, previousTargetValue))
            return false;

        SetValue(nextValue);
        AdditionCelebrationEffect.Play(this, operatorType);
        Debug.Log($"Bloco {name} atualizado para {currentValue} usando {operatorType}");
        return true;
    }

    public bool TryGetVisualColor(out Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            Material sharedMaterial = targetRenderer.sharedMaterial;
            if (sharedMaterial == null)
                continue;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            if (!propertyBlock.isEmpty)
            {
                if (sharedMaterial.HasProperty("_BaseColor"))
                {
                    color = propertyBlock.GetColor("_BaseColor");
                    return true;
                }

                if (sharedMaterial.HasProperty("_Color"))
                {
                    color = propertyBlock.GetColor("_Color");
                    return true;
                }
            }

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                color = sharedMaterial.GetColor("_BaseColor");
                return true;
            }

            if (sharedMaterial.HasProperty("_Color"))
            {
                color = sharedMaterial.GetColor("_Color");
                return true;
            }
        }

        color = Color.white;
        return false;
    }

    public bool TryUndoLastOperation(float spawnHeight)
    {
        return DesfazerManager.Instance.TryUndoLastOperation(this, spawnHeight);
    }

    private void RegisterWithUndoManager()
    {
        DesfazerManager.Instance.RegisterBlock(this);
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

    public void RefreshVisualLayout()
    {
        if (valueLabels == null)
            return;

        for (int i = 0; i < valueLabels.Length && i < FaceLabels.Length; i++)
        {
            TextMesh label = valueLabels[i];
            if (label != null)
            {
                UpdateLabelTransform(label.transform, FaceLabels[i].direction);
            }
        }
    }

    private void RefreshLabels()
    {
        if (valueLabels == null)
            return;

        string valueText = (hasPreviewValue ? previewValue : currentValue).ToString();
        for (int i = 0; i < valueLabels.Length; i++)
        {
            TextMesh label = valueLabels[i];
            if (label == null)
                continue;

            UpdateLabelTransform(label.transform, FaceLabels[i].direction);
            label.text = valueText;
            label.color = GetVisibleLabelColor();
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
        // Keep the glyphs physically outside the mesh. A tiny or negative offset
        // causes depth fighting on mobile GPUs and can expose labels from other faces.
        float offset = Mathf.Clamp(Mathf.Abs(labelSurfaceOffset), 0.002f, 0.01f);
        Vector3 faceOffset = center + new Vector3(
            direction.x * (halfExtents.x + offset),
            direction.y * (halfExtents.y + offset),
            direction.z * (halfExtents.z + offset)
        );

        labelTransform.localPosition = faceOffset;
        labelTransform.localRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        labelTransform.localScale = CalculateUnstretchedLabelScale(labelTransform.localRotation);
    }

    private Vector3 CalculateUnstretchedLabelScale(Quaternion labelLocalRotation)
    {
        Vector3 parentWorldScale = Abs(transform.lossyScale);
        Vector3 labelRightInParent = labelLocalRotation * Vector3.right;
        Vector3 labelUpInParent = labelLocalRotation * Vector3.up;
        Vector3 labelForwardInParent = labelLocalRotation * Vector3.forward;

        return new Vector3(
            LabelWorldScale / GetScaleAlongAxis(parentWorldScale, labelRightInParent),
            LabelWorldScale / GetScaleAlongAxis(parentWorldScale, labelUpInParent),
            LabelWorldScale / GetScaleAlongAxis(parentWorldScale, labelForwardInParent)
        );
    }

    private static float GetScaleAlongAxis(Vector3 parentWorldScale, Vector3 axisInParent)
    {
        return Mathf.Max(MinimumParentScale, Vector3.Scale(parentWorldScale, axisInParent).magnitude);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void UpdateLabelVisibility()
    {
        if (valueLabels == null)
            return;

        if (labelCamera == null)
            labelCamera = Camera.main;

        if (labelCamera == null)
            return;

        Vector3 cameraPosition = labelCamera.transform.position;
        for (int i = 0; i < valueLabels.Length && i < FaceLabels.Length; i++)
        {
            TextMesh label = valueLabels[i];
            if (label == null)
                continue;

            Vector3 faceNormal = transform.TransformDirection(FaceLabels[i].direction).normalized;
            Vector3 directionToCamera = labelCamera.orthographic
                ? -labelCamera.transform.forward
                : (cameraPosition - label.transform.position).normalized;

            Renderer labelRenderer = label.GetComponent<Renderer>();
            if (labelRenderer != null)
                labelRenderer.enabled = Vector3.Dot(faceNormal, directionToCamera) > 0.01f;
        }
    }

    private void ApplyLabelColors()
    {
        if (valueLabels == null)
            return;

        Color visibleColor = GetVisibleLabelColor();
        for (int i = 0; i < valueLabels.Length; i++)
        {
            if (valueLabels[i] != null)
                valueLabels[i].color = visibleColor;
        }
    }

    private Color GetVisibleLabelColor()
    {
        Color visibleColor = labelColor;
        visibleColor.a *= labelOpacity;
        return visibleColor;
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

        Color randomColor =
            VibrantBlockColors[Random.Range(0, VibrantBlockColors.Length)];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];

            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            ApplyPropertyBlockColor(targetRenderer, randomColor);
        }
    }

    private void ConfigureToonMaterials()
    {
        bool isResizable = GetComponent<ResizableBlock>() != null;
        string shaderName = isResizable ? StretchBlockToonShaderName : ToonShaderName;
        Shader toonShader = Shader.Find(shaderName);
        if (toonShader == null)
        {
            Debug.LogWarning($"Shader toon '{ToonShaderName}' não encontrado para {name}.", this);
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Material> createdMaterials = new List<Material>();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            Material[] sourceMaterials = targetRenderer.sharedMaterials;
            Material[] toonMaterials = new Material[sourceMaterials.Length];

            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material source = sourceMaterials[materialIndex];
                if (source == null)
                    continue;

                Color sourceColor = source.HasProperty("_BaseColor")
                    ? source.GetColor("_BaseColor")
                    : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                Texture sourceTexture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;

                Material toon = new Material(toonShader) { name = source.name + " (MathBlock Toon)" };
                toon.SetColor("_BaseColor", sourceColor);
                if (sourceTexture != null)
                    toon.SetTexture("_MainTex", sourceTexture);

                toon.SetFloat("_ShadeSteps", 3f);
                toon.SetFloat("_ShadeSmoothness", isResizable ? 1f : 0.08f);
                toon.SetFloat("_MinBrightness", 0.38f);
                toon.SetFloat("_AmbientStrength", 0.42f);
                if (isResizable)
                    toon.SetFloat("_OutlinePixels", 1.75f);
                else
                    toon.SetFloat("_OutlineWidth", 0.006f);
                toon.EnableKeyword("_OUTLINE_ON");
                toon.EnableKeyword("_RIM_ON");
                toon.EnableKeyword("_SPECULAR_ON");

                toonMaterials[materialIndex] = toon;
                createdMaterials.Add(toon);
            }

            targetRenderer.sharedMaterials = toonMaterials;
        }

        runtimeColorMaterials = createdMaterials.ToArray();
    }

    private void ApplyPropertyBlockColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        renderer.GetPropertyBlock(propertyBlock);

        Material sharedMat = renderer.sharedMaterial;

        if (sharedMat == null)
            return;

        if (sharedMat.HasProperty("_BaseColor"))
        {
            propertyBlock.SetColor("_BaseColor", color);
        }

        if (sharedMat.HasProperty("_Color"))
        {
            propertyBlock.SetColor("_Color", color);
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    public RendererColorSnapshot[] CaptureRendererColors()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        List<RendererColorSnapshot> snapshots = new List<RendererColorSnapshot>();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || IsLabelRenderer(targetRenderer))
                continue;

            Material sharedMaterial = targetRenderer.sharedMaterial;
            MaterialPropertyBlock rendererPropertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(rendererPropertyBlock);

            // sharedMaterials evita criar uma nova instância para cada captura/duplicação.
            Material[] materials = targetRenderer.sharedMaterials;
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
                materialColors = materialSnapshots,
                hasPropertyBlock = !rendererPropertyBlock.isEmpty,
                propertyBlockHasBaseColor = sharedMaterial != null && sharedMaterial.HasProperty("_BaseColor") && !rendererPropertyBlock.isEmpty,
                propertyBlockBaseColor = sharedMaterial != null && sharedMaterial.HasProperty("_BaseColor") && !rendererPropertyBlock.isEmpty
                    ? rendererPropertyBlock.GetColor("_BaseColor")
                    : Color.white,
                propertyBlockHasColor = sharedMaterial != null && sharedMaterial.HasProperty("_Color") && !rendererPropertyBlock.isEmpty,
                propertyBlockColor = sharedMaterial != null && sharedMaterial.HasProperty("_Color") && !rendererPropertyBlock.isEmpty
                    ? rendererPropertyBlock.GetColor("_Color")
                    : Color.white
            });
        }

        return snapshots.ToArray();
    }

    public void ApplyRendererColors(RendererColorSnapshot[] snapshots)
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

            Material[] materials = targetRenderer.sharedMaterials;
            MaterialColorSnapshot[] materialSnapshots = snapshots[snapshotIndex].materialColors;
            int materialCount = Mathf.Min(materials.Length, materialSnapshots.Length);

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                ApplyColorSnapshot(materials[materialIndex], materialSnapshots[materialIndex]);
            }

            targetRenderer.sharedMaterials = materials;
            ApplyPropertyBlockSnapshot(targetRenderer, snapshots[snapshotIndex]);
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

    private static void ApplyPropertyBlockSnapshot(Renderer targetRenderer, RendererColorSnapshot snapshot)
    {
        if (targetRenderer == null)
            return;

        if (!snapshot.hasPropertyBlock)
        {
            targetRenderer.SetPropertyBlock(null);
            return;
        }

        MaterialPropertyBlock rendererPropertyBlock = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(rendererPropertyBlock);

        if (snapshot.propertyBlockHasBaseColor)
        {
            rendererPropertyBlock.SetColor("_BaseColor", snapshot.propertyBlockBaseColor);
        }

        if (snapshot.propertyBlockHasColor)
        {
            rendererPropertyBlock.SetColor("_Color", snapshot.propertyBlockColor);
        }

        targetRenderer.SetPropertyBlock(rendererPropertyBlock);
    }
}
