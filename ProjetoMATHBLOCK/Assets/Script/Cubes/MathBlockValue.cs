using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MathBlockValue : MonoBehaviour
{
    private static Font cachedBuiltinFont;
    private const string LabelRootName = "ValueLabels";

    private static readonly (string name, Vector3 direction, Quaternion rotation)[] FaceLabels =
    {
        ("ValueLabel_Front", Vector3.forward, Quaternion.identity)
    };

    [SerializeField] private int currentValue = 1;
    [SerializeField] private bool updateScaleFromValue = false;
    [SerializeField] private float scaleStep = 0.15f;
    [SerializeField] private float minimumScaleMultiplier = 0.5f;
    [SerializeField] private float maximumScaleMultiplier = 3f;
    [SerializeField] private float labelSurfaceOffset = 0.002f;
    [SerializeField] private float labelFontSize = 28f;
    [SerializeField] private Color labelColor = Color.white;

    private Vector3 baseScale;
    private Quaternion originalRotation;
    private Material labelMaterial;
    private TextMesh[] valueLabels;

    public int CurrentValue => currentValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapMathBlockLabels()
    {
        foreach (GameObject block in GameObject.FindGameObjectsWithTag("MathBlock"))
        {
            if (block.GetComponent<MathBlockValue>() == null)
            {
                block.AddComponent<MathBlockValue>();
            }
        }
    }

    private void Awake()
    {
        RemoveLegacyGridChildren();
        baseScale = transform.localScale;
        originalRotation = transform.rotation;
        currentValue = Mathf.Max(0, currentValue);
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

    public bool TryApplyOperator(GravityInteract.PencilOperator operatorType, int operandValue)
    {
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

        SetValue(nextValue);
        Debug.Log($"Bloco {name} atualizado para {currentValue} usando {operatorType}");
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
        Vector3 center = GetLocalCenter();
        Vector3 halfExtents = GetLocalHalfExtents();
        float offset = Mathf.Max(labelSurfaceOffset, 0.001f);

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

            Vector3 direction = FaceLabels[i].direction;
            Vector3 faceOffset = center + new Vector3(
                direction.x * (halfExtents.x + offset),
                direction.y * (halfExtents.y + offset),
                direction.z * (halfExtents.z + offset)
            );

            labelTransform.localPosition = faceOffset;
            labelTransform.localRotation = FaceLabels[i].rotation;
            labelTransform.localScale = Vector3.one * 0.75f;
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
            return labelMaterial;
        }

        labelMaterial = new Material(builtinFont.material)
        {
            name = $"{name}_LabelMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        labelMaterial.CopyPropertiesFromMaterial(builtinFont.material);
        labelMaterial.mainTexture = builtinFont.material.mainTexture;
        labelMaterial.shader = builtinFont.material.shader;
        labelMaterial.renderQueue = builtinFont.material.renderQueue;
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

    private void RefreshVisual()
    {
        if (!updateScaleFromValue)
            return;

        float multiplier = 1f + ((currentValue - 1f) * scaleStep);
        multiplier = Mathf.Clamp(multiplier, minimumScaleMultiplier, maximumScaleMultiplier);
        transform.localScale = baseScale * multiplier;
    }
}
