  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.Rendering;
  using UnityEngine.SceneManagement;

  public sealed class MobileSceneToonMaterials : MonoBehaviour
  {
      private const string ToonShaderName = "Custom/URPToonShader";
      private readonly List<Material> runtimeMaterials = new List<Material>();

      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
      private static void Install()
      {
          GlobalSceneBootstrap.Register(OnSceneLoaded);
      }

      private static void OnSceneLoaded(Scene scene)
      {
          if (!scene.name.StartsWith("Fase", System.StringComparison.OrdinalIgnoreCase) &&
              !scene.name.Equals("MainScene", System.StringComparison.OrdinalIgnoreCase))
              return;

          if (FindFirstObjectByType<MobileSceneToonMaterials>() != null)
              return;

          GameObject installer = new GameObject("Mobile - Scene Toon Materials");
          SceneManager.MoveGameObjectToScene(installer, scene);
          installer.AddComponent<MobileSceneToonMaterials>();
      }

      private void Start()
      {
          Shader toonShader = Shader.Find(ToonShaderName);
          if (toonShader == null || !toonShader.isSupported)
          {
              Debug.LogError($"Mobile Toon: shader '{ToonShaderName}' indisponivel.", this);
              return;
          }

          foreach (Renderer target in FindObjectsByType<Renderer>(
                       FindObjectsInactive.Include,
                       FindObjectsSortMode.None))
          {
              // A geometria da Fase 2 deve conservar integralmente os materiais
              // e cores definidos na cena, mas sem reflexos nem ToonShader.
              if (BelongsToPhaseTwoScenarioGeometry(target))
              {
                  RemoveReflectionsPreservingAppearance(target);
                  continue;
              }

              // Operadores preservam seus materiais próprios e nunca recebem o
              // toon global, em nenhuma fase ou plataforma.
              if (target != null && target.GetComponentInParent<opItem>() != null)
              {
                  ConvertRenderer(target, toonShader, false, false);
                  continue;
              }

              if (IsPhaseOneCube(target))
              {
                  ApplySubtleDottedCubeMaterial(target);
                  continue;
              }

              if (IsGroundSurface(target))
              {
                  ApplyMatteGroundMaterial(target);
                  continue;
              }

              if (!CanConvert(target))
                  continue;

              ConvertRenderer(target, toonShader, false, false);
          }
      }

      private bool CanConvert(Renderer target)
      {
          if (target == null || target.gameObject.scene != gameObject.scene ||
              target is ParticleSystemRenderer || target is TrailRenderer || target is LineRenderer)
              return false;

          // Blocos são configurados pelo MathBlockValue. Operadores ficam fora
          // do toon global e mantêm seus materiais próprios.
          if (target.GetComponentInParent<opItem>() != null)
              return false;

          if (target.GetComponent<TextMesh>() != null || target.GetComponentInParent<Canvas>() != null ||
              target.GetComponentInParent<MathBlockValue>() != null ||
              target.GetComponentInParent<PencilGunOverlaySetup>() != null ||
              target.GetComponentInParent<ToonCloudSkyGenerator>() != null)
              return false;

          if (BelongsToSky(target.transform))
              return false;

          Material[] materials = target.sharedMaterials;
          if (materials == null || materials.Length == 0)
              return false;

          for (int i = 0; i < materials.Length; i++)
          {
              if (!IsOpaqueSceneMaterial(materials[i]))
                  return false;
          }

          return true;
      }

      private static bool IsOpaqueSceneMaterial(Material material)
      {
          if (material == null || material.shader == null)
              return false;

          string shaderName = material.shader.name;
          if (shaderName.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) ||
              shaderName.Contains("AbyssCylinder", System.StringComparison.OrdinalIgnoreCase) ||
              material.name.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) ||
              shaderName.Contains("Particle") ||
              shaderName.Contains("UI/") || shaderName.Contains("TextMesh") ||
              shaderName.Contains("Sprite"))
              return false;

          if (material.renderQueue >= (int)RenderQueue.AlphaTest)
              return false;

          if (material.IsKeywordEnabled("_EMISSION") ||
              (material.HasProperty("_EmissionColor") &&
               material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
              return false;

          if (material.HasProperty("_ZWrite") && material.GetFloat("_ZWrite") < 0.5f)
              return false;

          Color color = material.HasProperty("_BaseColor")
              ? material.GetColor("_BaseColor")
              : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
          return color.a >= 0.999f;
      }

      private static bool BelongsToSky(Transform target)
      {
          Transform current = target;
          while (current != null)
          {
              string objectName = current.name;
              if (objectName.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) ||
                  objectName.Contains("Ceu", System.StringComparison.OrdinalIgnoreCase) ||
                  objectName.Contains("Céu", System.StringComparison.OrdinalIgnoreCase))
                  return true;

              current = current.parent;
          }

          return false;
      }

      public static bool BelongsToPhaseTwoScenarioGeometry(Renderer target)
      {
          if (target == null ||
              !target.gameObject.scene.name.Equals("Fase 2", System.StringComparison.OrdinalIgnoreCase))
              return false;

          for (Transform current = target.transform; current != null; current = current.parent)
          {
              string objectName = current.name;
              if (objectName.Equals("02_Cenario_e_Geometria", System.StringComparison.OrdinalIgnoreCase) ||
                  (objectName.Contains("Cenario", System.StringComparison.OrdinalIgnoreCase) &&
                   objectName.Contains("Geometria", System.StringComparison.OrdinalIgnoreCase)))
                  return true;
          }

          return false;
      }

      internal static bool IsGroundSurface(Renderer target)
      {
          if (target == null || target is ParticleSystemRenderer ||
              target is TrailRenderer || target is LineRenderer ||
              target.GetComponentInParent<Canvas>() != null ||
              target.sharedMaterials == null || target.sharedMaterials.Length == 0)
              return false;

          foreach (Material material in target.sharedMaterials)
              if (material == null)
                  return false;

          Transform current = target.transform;
          while (current != null)
          {
              if (current.CompareTag("Ground"))
                  return true;

              string objectName = current.name;
              if (objectName.Contains("Ground", System.StringComparison.OrdinalIgnoreCase) ||
                  objectName.Contains("Floor", System.StringComparison.OrdinalIgnoreCase) ||
                  objectName.Contains("Piso", System.StringComparison.OrdinalIgnoreCase) ||
                  objectName.Contains("Chao", System.StringComparison.OrdinalIgnoreCase) ||
                  objectName.Contains("Chão", System.StringComparison.OrdinalIgnoreCase))
                  return true;

              current = current.parent;
          }

          Bounds bounds = target.bounds;
          float horizontalMinimum = Mathf.Min(bounds.size.x, bounds.size.z);
          float horizontalMaximum = Mathf.Max(bounds.size.x, bounds.size.z);
          if (horizontalMinimum >= 3f &&
              bounds.size.y <= Mathf.Max(0.75f, horizontalMaximum * 0.12f))
              return true;

          return false;
      }

      internal static bool IsPhaseOneCube(Renderer target)
      {
          if (target == null || !target.name.StartsWith("Cube", System.StringComparison.OrdinalIgnoreCase))
              return false;

          for (Transform current = target.transform.parent; current != null; current = current.parent)
          {
              if (current.name.Equals("fase1", System.StringComparison.OrdinalIgnoreCase) ||
                  current.name.Equals("Fase 1", System.StringComparison.OrdinalIgnoreCase))
                  return true;
          }

          return false;
      }

      private void RemoveReflectionsPreservingAppearance(Renderer target)
      {
          if (target == null || target.sharedMaterials == null)
              return;

          Material[] originals = target.sharedMaterials;
          Material[] replacements = new Material[originals.Length];
          for (int i = 0; i < originals.Length; i++)
          {
              Material source = originals[i];
              if (source == null)
                  continue;

              Material replacement = new Material(source)
              {
                  name = source.name + " (No Reflections)",
                  hideFlags = HideFlags.HideAndDontSave
              };
              if (replacement.HasProperty("_Metallic")) replacement.SetFloat("_Metallic", 0f);
              if (replacement.HasProperty("_Smoothness")) replacement.SetFloat("_Smoothness", 0f);
              if (replacement.HasProperty("_Glossiness")) replacement.SetFloat("_Glossiness", 0f);
              if (replacement.HasProperty("_SpecularHighlights")) replacement.SetFloat("_SpecularHighlights", 0f);
              if (replacement.HasProperty("_EnvironmentReflections")) replacement.SetFloat("_EnvironmentReflections", 0f);
              replacement.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
              replacement.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
              replacements[i] = replacement;
              runtimeMaterials.Add(replacement);
          }
          target.sharedMaterials = replacements;
      }

      private void ApplyMatteGroundMaterial(Renderer target)
      {
          Material[] originals = target.sharedMaterials;
          if (originals == null || originals.Length == 0)
              return;

          Shader matteShader = Shader.Find("Unlit/Color");
          if (matteShader == null || !matteShader.isSupported)
              matteShader = Shader.Find("Universal Render Pipeline/Unlit");
          if (matteShader == null || !matteShader.isSupported)
              return;

          Material[] converted = new Material[originals.Length];
          for (int i = 0; i < originals.Length; i++)
          {
              Material source = originals[i];
              if (source == null) continue;

              Material replacement = new Material(matteShader)
              {
                  name = source.name + " (Matte Ground)",
                  hideFlags = HideFlags.HideAndDontSave
              };

              Color color = new Color(0.72f, 0.74f, 0.77f, 1f);
              if (replacement.HasProperty("_Color"))
                  replacement.SetColor("_Color", color);
              if (replacement.HasProperty("_BaseColor"))
                  replacement.SetColor("_BaseColor", color);
              if (replacement.HasProperty("_Metallic"))
                  replacement.SetFloat("_Metallic", 0f);
              if (replacement.HasProperty("_Glossiness"))
                  replacement.SetFloat("_Glossiness", 0f);
              if (replacement.HasProperty("_Smoothness"))
                  replacement.SetFloat("_Smoothness", 0f);
              if (replacement.HasProperty("_MainTex"))
                  replacement.SetTexture("_MainTex", Texture2D.whiteTexture);
              if (replacement.HasProperty("_BaseMap"))
                  replacement.SetTexture("_BaseMap", Texture2D.whiteTexture);
              if (replacement.HasProperty("_Surface"))
                  replacement.SetFloat("_Surface", 0f);
              if (replacement.HasProperty("_AlphaClip"))
                  replacement.SetFloat("_AlphaClip", 0f);
              if (replacement.HasProperty("_Cutoff"))
                  replacement.SetFloat("_Cutoff", 0f);
              replacement.DisableKeyword("_ALPHATEST_ON");
              replacement.DisableKeyword("_ALPHABLEND_ON");
              replacement.DisableKeyword("_ALPHAPREMULTIPLY_ON");
              replacement.DisableKeyword("_DITHER_ON");
              replacement.renderQueue = (int)RenderQueue.Geometry;

              if (source.HasProperty("_MainTex") && replacement.HasProperty("_MainTex"))
              {
                  replacement.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                  replacement.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
                  replacement.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
              }

              converted[i] = replacement;
              runtimeMaterials.Add(replacement);
          }

          target.sharedMaterials = converted;
      }

      private void ApplySubtleDottedCubeMaterial(Renderer target)
      {
          Material[] originals = target.sharedMaterials;
          if (originals == null || originals.Length == 0)
              return;

          Shader dottedShader = Shader.Find("MathBlock/MatteDottedCube");
          if (dottedShader == null || !dottedShader.isSupported)
          {
              ApplyMatteGroundMaterial(target);
              return;
          }

          Material[] converted = new Material[originals.Length];
          for (int i = 0; i < originals.Length; i++)
          {
              Material source = originals[i];
              if (source == null)
                  continue;

              Material replacement = new Material(dottedShader)
              {
                  name = source.name + " (Subtle Dotted Matte)",
                  hideFlags = HideFlags.HideAndDontSave
              };

              Color color = source.HasProperty("_BaseColor")
                  ? source.GetColor("_BaseColor")
                  : source.HasProperty("_Color") ? source.GetColor("_Color") : new Color(0.72f, 0.74f, 0.77f, 1f);
              color.a = 1f;
              if (replacement.HasProperty("_Color")) replacement.SetColor("_Color", color);
              if (replacement.HasProperty("_DotStrength")) replacement.SetFloat("_DotStrength", 0.065f);
              if (replacement.HasProperty("_DotScale")) replacement.SetFloat("_DotScale", 4f);
              if (replacement.HasProperty("_DotColor"))
                  replacement.SetColor("_DotColor", new Color(0.16f, 0.10f, 0.20f, 1f));

              converted[i] = replacement;
              runtimeMaterials.Add(replacement);
          }

          target.sharedMaterials = converted;
      }

      private void ConvertRenderer(
          Renderer target,
          Shader toonShader,
          bool isGround,
          bool enableLegacyOutline)
      {
          Material[] originals = target.sharedMaterials;
          Material[] converted = new Material[originals.Length];

          for (int i = 0; i < originals.Length; i++)
          {
              Material source = originals[i];
              string textureProperty = source.HasProperty("_MainTex")
                  ? "_MainTex"
                  : source.HasProperty("_BaseMap") ? "_BaseMap" : null;
              Texture texture = textureProperty != null ? source.GetTexture(textureProperty) : null;
              Color color = source.HasProperty("_BaseColor")
                  ? source.GetColor("_BaseColor")
                  : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
              if (isGround)
                  color = new Color(0.72f, 0.74f, 0.77f, color.a);

              Material toon = new Material(toonShader)
              {
                  name = source.name + " (Mobile Scene Toon)",
                  hideFlags = HideFlags.HideAndDontSave
              };
              toon.SetColor("_BaseColor", color);
              toon.SetColor("_Color", color);
              if (texture != null)
              {
                  toon.SetTexture("_MainTex", texture);
                  toon.SetTextureScale("_MainTex", source.GetTextureScale(textureProperty));
                  toon.SetTextureOffset("_MainTex", source.GetTextureOffset(textureProperty));
              }
              toon.SetFloat("_ZWrite", 1f);
              toon.SetFloat("_SrcBlend", (float)BlendMode.One);
              toon.SetFloat("_DstBlend", (float)BlendMode.Zero);
              toon.renderQueue = (int)RenderQueue.Geometry;
              if (isGround)
              {
                  toon.DisableKeyword("_SPECULAR_ON");
                  toon.DisableKeyword("_RIM_ON");
                  toon.SetFloat("_EnableSpecular", 0f);
                  toon.SetFloat("_EnableRim", 0f);
                  toon.SetFloat("_Glossiness", 0.02f);
                  toon.SetFloat("_EmissionStrength", 0f);
                  toon.SetColor("_ShadeColor", new Color(0.62f, 0.65f, 0.69f, 1f));
                  toon.SetFloat("_MinBrightness", 0.72f);
                  toon.SetFloat("_AmbientStrength", 0.58f);
              }
              else
              {
                  toon.EnableKeyword("_SPECULAR_ON");
                  toon.EnableKeyword("_RIM_ON");
              }
              if (enableLegacyOutline)
              {
                  toon.EnableKeyword("_OUTLINE_ON");
                  toon.SetFloat("_EnableOutline", 1f);
              }
              else
              {
                  toon.DisableKeyword("_OUTLINE_ON");
                  toon.SetFloat("_EnableOutline", 0f);
                  toon.SetFloat("_OutlinePixels", 0f);
              }
              converted[i] = toon;
              runtimeMaterials.Add(toon);
          }

          target.sharedMaterials = converted;
      }

      private void OnDestroy()
      {
          for (int i = 0; i < runtimeMaterials.Count; i++)
          {
              if (runtimeMaterials[i] != null)
                  Destroy(runtimeMaterials[i]);
          }
      }
  }
