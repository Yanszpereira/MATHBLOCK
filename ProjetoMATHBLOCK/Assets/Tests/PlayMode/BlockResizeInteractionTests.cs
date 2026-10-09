using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class BlockResizeInteractionTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null) UnityEngine.Object.Destroy(createdObjects[i]);
        createdObjects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator EnterMode_SelectsValidBlockThroughRaycast()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Assert.That(GetProperty(h.Controller, "SelectedBlock"), Is.EqualTo(h.Block));
        Assert.That(GetProperty(h.Controller, "SelectedFace").ToString(), Is.EqualTo("PositiveZ"));
    }

    [UnityTest]
    public IEnumerator SpawnPop_CompletesAndRestoresBodyAndCollidersWithoutKinematicVelocityWarnings()
    {
        GameObject block = Track(new GameObject("SpawnPopPlayModeRegression"));
        block.transform.localScale = new Vector3(1.4f, 0.8f, 1.2f);
        Vector3 originalScale = block.transform.localScale;

        Rigidbody body = block.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.forward * 3f;
        body.angularVelocity = Vector3.up;

        BoxCollider enabledCollider = block.AddComponent<BoxCollider>();
        GameObject child = new GameObject("InitiallyDisabledCollider");
        child.transform.SetParent(block.transform, false);
        BoxCollider disabledCollider = child.AddComponent<BoxCollider>();
        disabledCollider.enabled = false;

        var kinematicVelocityWarnings = new List<string>();
        Application.LogCallback capture = (condition, _, type) =>
        {
            if (type == LogType.Warning &&
                condition.IndexOf("kinematic", StringComparison.OrdinalIgnoreCase) >= 0 &&
                condition.IndexOf("velocity", StringComparison.OrdinalIgnoreCase) >= 0)
                kinematicVelocityWarnings.Add(condition);
        };

        Application.logMessageReceived += capture;
        try
        {
            InvokeStatic("BlockSpawnPop", "Play", block, 0.2f);
            Type effectType = RequireType("BlockSpawnPop");
            Component effect = block.GetComponent(effectType);

            Assert.That(effect, Is.Not.Null);
            Assert.That((bool)GetProperty(effect, "IsAnimating"), Is.True);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.useGravity, Is.False);
            Assert.That(enabledCollider.enabled, Is.False);
            Assert.That(disabledCollider.enabled, Is.False);

            float deadline = Time.realtimeSinceStartup + 3f;
            while ((bool)GetProperty(effect, "IsAnimating") && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That((bool)GetProperty(effect, "IsAnimating"), Is.False, "Spawn animation did not finish in time.");
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(enabledCollider.enabled, Is.True);
            Assert.That(disabledCollider.enabled, Is.False, "The original disabled collider state was not preserved.");
            Assert.That(block.transform.localScale, Is.EqualTo(originalScale));
            Assert.That(kinematicVelocityWarnings, Is.Empty, string.Join("\n", kinematicVelocityWarnings));
        }
        finally
        {
            Application.logMessageReceived -= capture;
        }
    }

    [UnityTest]
    public IEnumerator SpawnPop_PreservesInitiallyKinematicBodyState()
    {
        GameObject block = Track(new GameObject("SpawnPopInitiallyKinematicRegression"));
        Rigidbody body = block.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.linearVelocity = Vector3.right;
        body.isKinematic = true;
        BoxCollider collider = block.AddComponent<BoxCollider>();

        var kinematicVelocityWarnings = new List<string>();
        Application.LogCallback capture = (condition, _, type) =>
        {
            if (type == LogType.Warning &&
                condition.IndexOf("kinematic", StringComparison.OrdinalIgnoreCase) >= 0 &&
                condition.IndexOf("velocity", StringComparison.OrdinalIgnoreCase) >= 0)
                kinematicVelocityWarnings.Add(condition);
        };

        Application.logMessageReceived += capture;
        try
        {
            InvokeStatic("BlockSpawnPop", "Play", block, 0.2f);
            Component effect = block.GetComponent(RequireType("BlockSpawnPop"));
            Assert.That(effect, Is.Not.Null);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.useGravity, Is.False);
            Assert.That(collider.enabled, Is.False);

            float deadline = Time.realtimeSinceStartup + 3f;
            while ((bool)GetProperty(effect, "IsAnimating") && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That((bool)GetProperty(effect, "IsAnimating"), Is.False, "Spawn animation did not finish in time.");
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.useGravity, Is.False);
            Assert.That(collider.enabled, Is.True);
            Assert.That(kinematicVelocityWarnings, Is.Empty, string.Join("\n", kinematicVelocityWarnings));
        }
        finally
        {
            Application.logMessageReceived -= capture;
        }
    }

    [UnityTest]
    public IEnumerator BlackboardView_GeneratesUntruncatedGlyphsInsideTheScaledWritingSurface()
    {
        GameObject board = Track(new GameObject("BlackboardPlayModeRegression"));
        board.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
        board.transform.localScale = Vector3.one * 7.9f;

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "QuadroNegro_Superficie";
        surface.transform.SetParent(board.transform, false);
        surface.transform.localScale = new Vector3(3.16f, 1.48f, 0.02f);

        Component view = board.AddComponent(RequireType("Fase4BlackboardView"));
        yield return null;

        Invoke(view, "SetExpression", "(12 + 8) × 2 − 10");
        Invoke(view, "SetRound", 2, 3);
        Invoke(view, "SetTimer", 45f);
        Invoke(view, "SetErrors", 2);
        Invoke(view, "SetStatus", "INTERAJA COM O RECEPTÁCULO PARA CONTINUAR");
        Invoke(view, "SetTransitionCountdown", 4);
        Invoke(view, "ShowTimeChange", "−5s", Color.red);

        Type tmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        Assert.That(tmpTextType, Is.Not.Null, "TextMeshPro assembly was not available.");
        Component[] labels = board.GetComponentsInChildren(tmpTextType, true);
        Assert.That(labels, Has.Length.EqualTo(7));

        Renderer surfaceRenderer = surface.GetComponent<Renderer>();
        Bounds surfaceBounds = surfaceRenderer.bounds;
        Vector3 expectedFront = board.transform.forward;
        float surfaceHalfDepth = Mathf.Abs(expectedFront.x) * surfaceBounds.extents.x +
                                 Mathf.Abs(expectedFront.y) * surfaceBounds.extents.y +
                                 Mathf.Abs(expectedFront.z) * surfaceBounds.extents.z;
        foreach (Component label in labels)
        {
            string labelName = label.name;
            string text = (string)GetProperty(label, "text");
            Assert.That(text, Is.Not.Empty, labelName);

            Invoke(label, "ForceMeshUpdate", true, true);
            object textInfo = GetProperty(label, "textInfo");
            int characterCount = (int)GetMember(textInfo, "characterCount");
            Assert.That(characterCount, Is.GreaterThan(0),
                labelName + " must produce glyphs, not only keep its text string.");
            Assert.That((bool)GetProperty(label, "isTextTruncated"), Is.False, labelName);

            Renderer labelRenderer = label.GetComponent<Renderer>();
            Assert.That(labelRenderer, Is.Not.Null, labelName);
            Assert.That(labelRenderer.enabled, Is.True, labelName);
            Assert.That(labelRenderer.sharedMaterial, Is.Not.Null, labelName);
            Assert.That(labelRenderer.sharedMaterial.shader, Is.Not.Null, labelName);
            Assert.That(Vector3.Dot(label.transform.forward, -expectedFront), Is.GreaterThan(0.99f),
                labelName + " glyph plane must face the board's authored front.");
            Assert.That(Vector3.Dot(label.transform.up, Vector3.up), Is.GreaterThan(0.99f),
                labelName + " must preserve upright world orientation.");
            Assert.That(Vector3.Dot(label.transform.position - surfaceBounds.center, expectedFront),
                Is.GreaterThan(surfaceHalfDepth), labelName + " must be in front of the writing surface.");

            IList characterInfo = (IList)GetMember(textInfo, "characterInfo");
            Vector3 screenRight = label.transform.right;
            int visibleGlyphsForLabel = 0;
            for (int i = 0; i < characterCount; i++)
            {
                object character = characterInfo[i];
                if (!(bool)GetMember(character, "isVisible"))
                    continue;

                Vector3 bottomLeft = label.transform.TransformPoint((Vector3)GetMember(character, "bottomLeft"));
                Vector3 topRight = label.transform.TransformPoint((Vector3)GetMember(character, "topRight"));
                AssertGlyphInsideSurface(bottomLeft, surfaceBounds, screenRight, labelName);
                AssertGlyphInsideSurface(topRight, surfaceBounds, screenRight, labelName);
                visibleGlyphsForLabel++;
            }

            Assert.That(visibleGlyphsForLabel, Is.GreaterThan(0), labelName + " produced no visible glyph geometry.");
        }
    }

    private static void AssertGlyphInsideSurface(Vector3 point, Bounds surfaceBounds, Vector3 screenRight, string labelName)
    {
        Vector3 offset = point - surfaceBounds.center;
        float halfWidth = Mathf.Abs(screenRight.x) * surfaceBounds.extents.x +
                          Mathf.Abs(screenRight.y) * surfaceBounds.extents.y +
                          Mathf.Abs(screenRight.z) * surfaceBounds.extents.z;
        Assert.That(Mathf.Abs(Vector3.Dot(offset, screenRight)), Is.LessThan(halfWidth), labelName + " exceeded the board's horizontal bounds.");
        Assert.That(Mathf.Abs(offset.y), Is.LessThan(surfaceBounds.extents.y), labelName + " exceeded the board's vertical bounds.");
    }

    private static object GetMember(object target, string memberName)
    {
        PropertyInfo property = target.GetType().GetProperty(memberName, InstanceFlags);
        if (property != null)
            return property.GetValue(target);

        FieldInfo field = target.GetType().GetField(memberName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Field or property {memberName} not found on {target.GetType().Name}.");
        return field.GetValue(target);
    }

    [UnityTest]
    public IEnumerator EnterMode_RejectsExistingOversizedBlockWithoutMutatingIt()
    {
        Harness h = CreateHarness(4);
        SetField(h.Block, "width", 3);
        SetField(h.Block, "height", 2);
        SetField(h.Block, "depth", 1);
        InvokeNonPublic(h.Block, "ApplyCurrentDimensions");
        yield return null;

        Assert.That(h.Dimensions, Is.EqualTo(new Vector3Int(3, 2, 1)));
        Assert.That(h.BeginFromFace("PositiveZ"), Is.False);
        Assert.That(h.Dimensions, Is.EqualTo(new Vector3Int(3, 2, 1)));
    }

    [UnityTest]
    public IEnumerator Raycast_SelectsAllSixFaces()
    {
        Harness h = CreateHarness(9); yield return null;
        string[] faces = { "PositiveX", "NegativeX", "PositiveY", "NegativeY", "PositiveZ", "NegativeZ" };
        foreach (string face in faces)
        {
            Assert.That(h.BeginFromFace(face), Is.True, face);
            Assert.That(GetProperty(h.Controller, "SelectedFace").ToString(), Is.EqualTo(face));
            Assert.That(GetProperty(h.Gizmo, "SelectedFace").ToString(), Is.EqualTo(face));
            Assert.That(h.Gizmo.gameObject.activeSelf, Is.True);
            Invoke(h.Controller, "ConfirmResizeSession");
        }
    }

    [UnityTest]
    public IEnumerator RotatedBlock_SelectsCorrectLocalFace()
    {
        Harness h = CreateHarness(9, blockRotation: Quaternion.Euler(20f, 55f, 10f)); yield return null;
        Assert.That(h.BeginFromFace("NegativeX"), Is.True);
        Assert.That(GetProperty(h.Controller, "SelectedFace").ToString(), Is.EqualTo("NegativeX"));
        Vector3 expected = (Vector3)Invoke(h.Block, "GetFaceNormalWorld", EnumValue("ResizeFace", "NegativeX"));
        Vector3 actual = (Vector3)GetProperty(h.Gizmo, "FaceNormalWorld");
        Assert.That(Vector3.Dot(expected, actual), Is.GreaterThan(0.999f));
    }

    [UnityTest]
    public IEnumerator WallOcclusion_PreventsSelection()
    {
        Harness h = CreateHarness(9);
        h.PositionCameraForFace("NegativeZ");
        Vector3 midpoint = (h.Camera.transform.position + h.Block.transform.position) * 0.5f;
        CreateObstacle("OccludingWall", midpoint, new Vector3(3f, 3f, 0.2f));
        Physics.SyncTransforms(); yield return null;
        Assert.That(h.BeginCurrentRay(), Is.False);
    }

    [UnityTest]
    public IEnumerator Face_RemainsLockedUntilSessionEnds()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        h.PositionCameraForFace("PositiveX");
        h.Drag("Right", 1.1f);
        Assert.That(GetProperty(h.Controller, "SelectedFace").ToString(), Is.EqualTo("PositiveZ"));
        Assert.That(GetProperty(h.Gizmo, "SelectedFace").ToString(), Is.EqualTo("PositiveZ"));
    }

    [UnityTest]
    public IEnumerator NewSession_CanSelectAnotherFace()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(h.BeginFromFace("PositiveX"), Is.True);
        Assert.That(GetProperty(h.Controller, "SelectedFace").ToString(), Is.EqualTo("PositiveX"));
    }

    [UnityTest]
    public IEnumerator FourArrows_WorkOnEveryFaceAndOnlyChangePlaneDimensions()
    {
        Harness h = CreateHarness(30); yield return null;
        string[] faces = { "PositiveX", "NegativeX", "PositiveY", "NegativeY", "PositiveZ", "NegativeZ" };
        string[] handles = { "Top", "Bottom", "Left", "Right" };

        foreach (string face in faces)
        {
            foreach (string handle in handles)
            {
                Assert.That(h.BeginFromFace(face), Is.True, face + "/" + handle);
                int fixedAxis = (int)InvokeStatic("ResizableBlock", "GetFaceNormalAxis", EnumValue("ResizeFace", face));
                Vector3Int before = h.Dimensions;
                Assert.That(h.Drag(handle, 1.1f), Is.True, face + "/" + handle);
                Vector3Int after = h.Dimensions;
                Assert.That(GetAxis(after, fixedAxis), Is.EqualTo(GetAxis(before, fixedAxis)), face + "/" + handle);
                Assert.That((after - before).sqrMagnitude, Is.EqualTo(1), face + "/" + handle);
                Invoke(h.Controller, "CancelResizeSession");
            }
        }
    }

    [UnityTest]
    public IEnumerator RealFlow_RaycastFaceHandleDragProposalValidationAndApplication()
    {
        Harness h = CreateHarness(3); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Assert.That(h.Drag("Right", 1.1f), Is.True);
        Assert.That(h.Dimensions.x * h.Dimensions.y * h.Dimensions.z, Is.EqualTo(2));
        Invoke(h.Controller, "EndHandleDrag");
        Assert.That(GetProperty(h.Controller, "State").ToString(), Is.EqualTo("ResizeMode"));
    }

    [UnityTest]
    public IEnumerator ResizeMode_CreatesFadingStarParticlesAndTracksBlockSize()
    {
        Harness h = CreateHarness(9);
        Texture2D starTexture = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        starTexture.SetPixels(pixels);
        starTexture.Apply();

        Color expectedTint = new Color(1f, 0.82f, 0.12f, 1f);
        SetField(h.Controller, "resizeParticleTexture", starTexture);
        SetField(h.Controller, "resizeParticleColor", expectedTint);
        yield return null;

        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Component effect = (Component)GetProperty(h.Controller, "ActiveParticleEffect");
        Assert.That(effect, Is.Not.Null);
        createdObjects.Add(effect.gameObject);

        ParticleSystem particles = (ParticleSystem)GetProperty(effect, "Particles");
        Assert.That(particles, Is.Not.Null);
        Assert.That(particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(particles.main.startColor.color, Is.EqualTo(expectedTint));
        Assert.That(particles.colorOverLifetime.enabled, Is.True);
        Assert.That(particles.rotationOverLifetime.enabled, Is.True);
        Assert.That(particles.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture, Is.EqualTo(starTexture));

        Vector3 initialEmissionSize = (Vector3)GetProperty(effect, "EmissionSize");
        Assert.That(Vector3.Distance(initialEmissionSize, Vector3.one * 1.16f), Is.LessThan(0.001f));
        Assert.That(h.Drag("Right", 1.1f), Is.True);
        Vector3 resizedEmissionSize = (Vector3)GetProperty(effect, "EmissionSize");
        Assert.That(resizedEmissionSize.x, Is.EqualTo(initialEmissionSize.x + 1f).Within(0.001f));
        Assert.That(resizedEmissionSize.y, Is.EqualTo(initialEmissionSize.y).Within(0.001f));
        Assert.That(resizedEmissionSize.z, Is.EqualTo(initialEmissionSize.z).Within(0.001f));

        yield return new WaitForSeconds(0.5f);
        Assert.That(particles.particleCount, Is.GreaterThan(0));
        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(GetProperty(h.Controller, "ActiveParticleEffect"), Is.Null);
        Assert.That(particles.isEmitting, Is.False);
        Assert.That(GetProperty(effect, "TargetBlock"), Is.Null);
    }

    [UnityTest]
    public IEnumerator AnchoredBlock_FEntersResizeWithoutReleasingAnchorOrDuplicatingParticles()
    {
        Harness h = CreateHarness(9);
        Texture2D starTexture = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        starTexture.SetPixels(pixels);
        starTexture.Apply();
        SetField(h.Controller, "resizeParticleTexture", starTexture);

        Component airAnchor = h.Block.gameObject.AddComponent(RequireType("ResizableBlockAirAnchor"));
        Assert.That((bool)Invoke(airAnchor, "AnchorFromHeldState", starTexture, Color.yellow), Is.True);
        Component anchorEffect = (Component)GetProperty(airAnchor, "ActiveParticleEffect");
        Assert.That(anchorEffect, Is.Not.Null);
        createdObjects.Add(anchorEffect.gameObject);
        yield return null;

        h.PositionCameraForFace("PositiveZ");
        Assert.That((bool)Invoke(h.Controller, "TryHandleResizeKey"), Is.True);
        Assert.That(GetProperty(h.Controller, "State").ToString(), Is.EqualTo("ResizeMode"));
        Assert.That(GetProperty(h.Controller, "SelectedBlock"), Is.EqualTo(h.Block));
        Assert.That(GetProperty(airAnchor, "IsAnchored"), Is.True);
        Assert.That(GetProperty(h.Controller, "ActiveParticleEffect"), Is.Null);
        Assert.That(GetProperty(airAnchor, "ActiveParticleEffect"), Is.SameAs(anchorEffect));

        Vector3 initialEmissionSize = (Vector3)GetProperty(anchorEffect, "EmissionSize");
        Assert.That(h.Drag("Right", 1.1f), Is.True);
        Vector3 resizedEmissionSize = (Vector3)GetProperty(anchorEffect, "EmissionSize");
        Assert.That(resizedEmissionSize.x, Is.EqualTo(initialEmissionSize.x + 1f).Within(0.001f));

        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(GetProperty(h.Controller, "State").ToString(), Is.EqualTo("Idle"));
        Assert.That(GetProperty(airAnchor, "IsAnchored"), Is.True);
        Assert.That(h.Rigidbody.isKinematic, Is.True);
        Assert.That(h.Rigidbody.useGravity, Is.False);
        Assert.That(GetProperty(airAnchor, "ActiveParticleEffect"), Is.SameAs(anchorEffect));
    }

    [UnityTest]
    public IEnumerator ChangingFaces_CannotExceedGlobalVolume()
    {
        Harness h = CreateHarness(4); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Assert.That(h.Drag("Right", 1.1f), Is.True);
        Invoke(h.Controller, "EndHandleDrag");
        Assert.That(h.Drag("Top", 1.1f), Is.True);
        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(h.Dimensions, Is.EqualTo(new Vector3Int(2, 2, 1)));

        Assert.That(h.BeginFromFace("PositiveX"), Is.True);
        Vector3Int before = h.Dimensions;
        bool applied = h.Drag("Right", 1.1f);
        Assert.That(applied, Is.False);
        Assert.That(h.Dimensions, Is.EqualTo(before));
    }

    [UnityTest]
    public IEnumerator Obstacle_BlocksGrowthAlongSelectedArrow()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Vector3 direction = (Vector3)Invoke(h.Gizmo, "GetDragAxisWorld", EnumValue("ResizeHandlePosition", "Right"));
        CreateObstacle("GrowthWall", h.Block.transform.position + direction * 1.4f, Vector3.one * 0.2f);
        Physics.SyncTransforms();
        Assert.That(h.Drag("Right", 1.1f), Is.False);
        Assert.That(h.Dimensions, Is.EqualTo(Vector3Int.one));
    }

    [UnityTest]
    public IEnumerator Obstacles_BlockGrowthInAllSixLocalDirections()
    {
        Harness h = CreateHarness(30); yield return null;
        string[] directions = { "PositiveX", "NegativeX", "PositiveY", "NegativeY", "PositiveZ", "NegativeZ" };

        foreach (string directionName in directions)
        {
            string face = directionName.EndsWith("Z", StringComparison.Ordinal) ? "PositiveX" : "PositiveZ";
            Assert.That(h.BeginFromFace(face), Is.True, directionName);
            string handle = h.FindHandleForDirection(directionName);
            Vector3 axis = (Vector3)Invoke(h.Gizmo, "GetDragAxisWorld", EnumValue("ResizeHandlePosition", handle));
            GameObject obstacle = CreateObstacle("GrowthWall_" + directionName, h.Block.transform.position + axis * 1.4f, Vector3.one * 0.2f);
            Physics.SyncTransforms();
            Assert.That(h.Drag(handle, 1.1f), Is.False, directionName);
            Assert.That(h.Dimensions, Is.EqualTo(Vector3Int.one), directionName);
            Invoke(h.Controller, "CancelResizeSession");
            UnityEngine.Object.Destroy(obstacle);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Rigidbody_IsStableDuringSession()
    {
        Harness h = CreateHarness(9);
        h.Rigidbody.isKinematic = false;
        h.Rigidbody.useGravity = true;
        h.Rigidbody.linearVelocity = Vector3.right * 3f;
        yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Assert.That(h.Rigidbody.isKinematic, Is.True);
        Assert.That(h.Rigidbody.useGravity, Is.False);
        Assert.That(h.Rigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
    }

    [UnityTest]
    public IEnumerator Confirm_KeepsCurrentSize()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        h.Drag("Right", 1.1f);
        Vector3Int resized = h.Dimensions;
        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(h.Dimensions, Is.EqualTo(resized));
        Assert.That(GetProperty(h.Controller, "State").ToString(), Is.EqualTo("Idle"));
    }

    [UnityTest]
    public IEnumerator Cancel_RestoresSessionDimensionsAndPosition()
    {
        Harness h = CreateHarness(9); yield return null;
        Vector3 initialPosition = h.Block.transform.position;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        h.Drag("Right", 1.1f);
        Invoke(h.Controller, "CancelResizeSession");
        Assert.That(h.Dimensions, Is.EqualTo(Vector3Int.one));
        Assert.That(Vector3.Distance(h.Block.transform.position, initialPosition), Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator Exit_RestoresControlsCursorRigidbodyAndActionMap()
    {
        Harness h = CreateHarness(9, true); yield return null;
        bool previousLook = h.Look.enabled;
        bool previousMovement = h.Movement.enabled;
        CursorLockMode previousLock = Cursor.lockState;
        bool previousVisible = Cursor.visible;
        Assert.That(h.BeginFromFace("PositiveZ"), Is.True);
        Assert.That(h.Look.enabled, Is.False);
        Assert.That(h.Movement.enabled, Is.False);
        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(h.Look.enabled, Is.EqualTo(previousLook));
        Assert.That(h.Movement.enabled, Is.EqualTo(previousMovement));
        Assert.That(Cursor.lockState, Is.EqualTo(previousLock));
        Assert.That(Cursor.visible, Is.EqualTo(previousVisible));
        Assert.That(h.PlayerInput.currentActionMap.name, Is.EqualTo("Player"));
        Assert.That(h.Rigidbody.isKinematic, Is.False);
        Assert.That(h.Rigidbody.useGravity, Is.True);
    }

    private Harness CreateHarness(int value, bool includePlayerBehaviours = false, Quaternion? blockRotation = null)
    {
        GameObject player = Track(new GameObject("ResizeTestPlayer"));
        PlayerInput playerInput = player.AddComponent<PlayerInput>();
        InputActionAsset actions = Track(ScriptableObject.CreateInstance<InputActionAsset>());
        InputActionMap playerMap = actions.AddActionMap("Player");
        playerMap.AddAction("EnterResize", InputActionType.Button);
        InputActionMap resizeMap = actions.AddActionMap("Resize");
        resizeMap.AddAction("Point", InputActionType.PassThrough);
        resizeMap.AddAction("Click", InputActionType.Button);
        resizeMap.AddAction("Cancel", InputActionType.Button);
        resizeMap.AddAction("ExitResize", InputActionType.Button);
        playerInput.actions = actions;
        playerInput.defaultActionMap = "Player";
        playerInput.ActivateInput();
        playerInput.SwitchCurrentActionMap("Player");

        GameObject cameraObject = Track(new GameObject("TestCamera"));
        cameraObject.transform.SetParent(player.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        Component look = null;
        Component movement = null;
        if (includePlayerBehaviours)
        {
            player.AddComponent<CharacterController>();
            movement = player.AddComponent(RequireType("PlayerMovement"));
            look = player.AddComponent(RequireType("Look"));
            SetField(movement, "controller", player.GetComponent<CharacterController>());
            SetField(look, "cameraTransform", camera.transform);
        }

        Component controller = player.AddComponent(RequireType("BlockResizeController"));
        Component gizmoPrefab = CreateGizmoPrefab();
        SetField(controller, "playerCamera", camera);
        SetField(controller, "playerInput", playerInput);
        SetField(controller, "resizeGizmoPrefab", gizmoPrefab);
        SetField(controller, "interactionDistance", 5f);
        SetField(controller, "targetingMask", (LayerMask)~(1 << 8));
        SetField(controller, "resizeHandleMask", (LayerMask)(1 << 8));
        if (look != null) SetField(controller, "playerLook", look);
        if (movement != null) SetField(controller, "playerMovement", movement);
        Component block = CreateResizableBlock(value, blockRotation ?? Quaternion.identity);
        return new Harness(controller, block, block.GetComponent<Rigidbody>(), playerInput, camera, look, movement);
    }

    private Component CreateResizableBlock(int value, Quaternion rotation)
    {
        GameObject root = Track(new GameObject("TestResizableBlock"));
        root.transform.SetPositionAndRotation(new Vector3(0f, 0f, 3f), rotation);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.useGravity = true;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        GameObject visual = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.name = "BlockVisual";
        visual.transform.SetParent(root.transform, false);
        Component mathValue = root.AddComponent(RequireType("MathBlockValue"));
        Invoke(mathValue, "SetValue", value);
        Component block = root.AddComponent(RequireType("ResizableBlock"));
        SetField(block, "mathBlockValue", mathValue);
        SetField(block, "visualRoot", visual.transform);
        SetField(block, "blockCollider", collider);
        SetField(block, "blockRigidbody", body);
        SetField(block, "obstacleMask", (LayerMask)~(1 << 8));
        InvokeNonPublic(block, "ApplyCurrentDimensions");
        Physics.SyncTransforms();
        return block;
    }

    private Component CreateGizmoPrefab()
    {
        GameObject root = Track(new GameObject("TestBlockResizeGizmo"));
        root.SetActive(false);
        Type handleType = RequireType("BlockResizeHandle");
        Type positionType = RequireType("ResizeHandlePosition");
        foreach (string position in new[] { "Top", "Bottom", "Left", "Right" })
        {
            GameObject handleObject = new GameObject(position + "Handle");
            handleObject.layer = 8;
            handleObject.transform.SetParent(root.transform, false);
            GameObject interaction = new GameObject("InteractionCollider");
            interaction.layer = 8;
            interaction.transform.SetParent(handleObject.transform, false);
            BoxCollider interactionCollider = interaction.AddComponent<BoxCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.size = Vector3.one * 0.45f;
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(handleObject.transform, false);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(visualRoot.transform, false);
            Component handle = handleObject.AddComponent(handleType);
            SetField(handle, "position", Enum.Parse(positionType, position));
            SetField(handle, "interactionCollider", interactionCollider);
            SetField(handle, "visualRoot", visualRoot.transform);
            SetField(handle, "visualRenderers", visual.GetComponents<Renderer>());
        }
        return root.AddComponent(RequireType("BlockResizeGizmo"));
    }

    private GameObject CreateObstacle(string name, Vector3 position, Vector3 scale)
    {
        GameObject obstacle = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        obstacle.name = name;
        obstacle.transform.position = position;
        obstacle.transform.localScale = scale;
        return obstacle;
    }

    private T Track<T>(T target) where T : UnityEngine.Object
    {
        createdObjects.Add(target);
        return target;
    }

    private static object EnumValue(string typeName, string value)
    {
        return Enum.Parse(RequireType(typeName), value);
    }

    private static int GetAxis(Vector3Int vector, int axis)
    {
        return axis == 0 ? vector.x : axis == 1 ? vector.y : vector.z;
    }

    private static Type RequireType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Tipo {name} nao encontrado.");
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Metodo {methodName} nao encontrado.");
        return method.Invoke(target, arguments);
    }

    private static object InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Metodo {methodName} nao encontrado.");
        return method.Invoke(target, arguments);
    }

    private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
    {
        MethodInfo method = RequireType(typeName).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, $"Metodo {typeName}.{methodName} nao encontrado.");
        return method.Invoke(null, arguments);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Propriedade {propertyName} nao encontrada.");
        return property.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Campo {fieldName} nao encontrado.");
        field.SetValue(target, value);
    }

    private sealed class Harness
    {
        public Component Controller { get; }
        public Component Block { get; }
        public Rigidbody Rigidbody { get; }
        public PlayerInput PlayerInput { get; }
        public Camera Camera { get; }
        public Behaviour Look { get; }
        public Behaviour Movement { get; }
        public Component Gizmo => (Component)GetProperty(Controller, "ActiveGizmo");
        public Vector3Int Dimensions => new Vector3Int(
            (int)GetProperty(Block, "Width"),
            (int)GetProperty(Block, "Height"),
            (int)GetProperty(Block, "Depth")
        );

        public Harness(Component controller, Component block, Rigidbody rigidbody, PlayerInput input, Camera camera, Component look, Component movement)
        {
            Controller = controller;
            Block = block;
            Rigidbody = rigidbody;
            PlayerInput = input;
            Camera = camera;
            Look = look as Behaviour;
            Movement = movement as Behaviour;
        }

        public void PositionCameraForFace(string faceName)
        {
            object face = EnumValue("ResizeFace", faceName);
            Vector3 normal = (Vector3)Invoke(Block, "GetFaceNormalWorld", face);
            Vector3 center = (Vector3)Invoke(Block, "GetFaceCenterWorld", face);
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Block.transform.forward : Vector3.up;
            Camera.transform.SetPositionAndRotation(center + normal * 3f, Quaternion.LookRotation(-normal, up));
            Physics.SyncTransforms();
        }

        public bool BeginFromFace(string faceName)
        {
            PositionCameraForFace(faceName);
            return BeginCurrentRay();
        }

        public bool BeginCurrentRay()
        {
            return (bool)Invoke(Controller, "TryBeginResizeFromRay", new Ray(Camera.transform.position, Camera.transform.forward));
        }

        public bool Drag(string handlePosition, float distance)
        {
            Component gizmo = Gizmo;
            Type handleType = RequireType("BlockResizeHandle");
            Component selected = null;
            foreach (Component handle in gizmo.GetComponentsInChildren(handleType, true))
            {
                if (GetProperty(handle, "Position").ToString() == handlePosition)
                {
                    selected = handle;
                    break;
                }
            }
            Assert.That(selected, Is.Not.Null, handlePosition);
            object handleEnum = EnumValue("ResizeHandlePosition", handlePosition);
            Vector3 axis = (Vector3)Invoke(gizmo, "GetDragAxisWorld", handleEnum);
            Vector3 planePoint = (Vector3)GetProperty(gizmo, "FacePlanePoint");
            Assert.That((bool)Invoke(Controller, "BeginHandleDrag", selected, new Ray(Camera.transform.position, planePoint - Camera.transform.position)), Is.True);
            Vector3 targetPoint = planePoint + axis * distance;
            return (bool)Invoke(Controller, "UpdateHandleDrag", new Ray(Camera.transform.position, targetPoint - Camera.transform.position));
        }

        public string FindHandleForDirection(string directionName)
        {
            foreach (string handle in new[] { "Top", "Bottom", "Left", "Right" })
            {
                object position = EnumValue("ResizeHandlePosition", handle);
                if (Invoke(Gizmo, "GetResizeDirection", position).ToString() == directionName)
                    return handle;
            }

            Assert.Fail("Nenhum handle corresponde a " + directionName + ".");
            return null;
        }

    }
}
