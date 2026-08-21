using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mantem todos os controles mobile dentro da area segura da tela.
/// O joystick continua filho de BotoesMobile e recebe exatamente a mesma escala
/// e a mesma margem dos botoes de acao e dos operadores.
/// </summary>
[DisallowMultipleComponent]
public sealed class MobileHudSafeArea : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private const float ReferenceMargin = 0f;

    private readonly List<RectTransform> hudGroups = new List<RectTransform>();
    private Canvas rootCanvas;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            Debug.LogWarning("HUD mobile: Canvas pai nao foi encontrado.", this);
            enabled = false;
            return;
        }

        ConfigureCanvasScaler(rootCanvas);
        CollectHudGroups();
        ApplySafeArea(true);
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += RefreshBeforeRender;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= RefreshBeforeRender;
    }

    private void RefreshBeforeRender()
    {
        ApplySafeArea(false);
    }

    private static void ConfigureCanvasScaler(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
    }

    private void CollectHudGroups()
    {
        hudGroups.Clear();

        RectTransform mobileButtons = transform as RectTransform;
        if (mobileButtons != null)
            hudGroups.Add(mobileButtons);

        foreach (RectTransform rect in rootCanvas.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == null || rect == mobileButtons ||
                !rect.name.Equals("Operadores", StringComparison.OrdinalIgnoreCase))
                continue;

            hudGroups.Add(rect);
            break;
        }
    }

    private void ApplySafeArea(bool force)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        if (!force && safeArea == lastSafeArea &&
            Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            return;

        lastSafeArea = safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        for (int i = hudGroups.Count - 1; i >= 0; i--)
        {
            RectTransform group = hudGroups[i];
            if (group == null)
            {
                hudGroups.RemoveAt(i);
                continue;
            }

            group.anchorMin = anchorMin;
            group.anchorMax = anchorMax;
            group.offsetMin = new Vector2(ReferenceMargin, ReferenceMargin);
            group.offsetMax = new Vector2(-ReferenceMargin, -ReferenceMargin);
            group.localScale = Vector3.one;
        }
    }
}
