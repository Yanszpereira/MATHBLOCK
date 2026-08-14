using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class ResizableBlockAirAnchorTests
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
    public IEnumerator ContextualF_AnchorsHeldResizableBlock_AndNextClickGrabsItAgain()
    {
        PlayerHarness player = CreatePlayerHarness();
        Component airAnchor;
        Component block = CreateMathBlock(true, out airAnchor);
        Texture2D starTexture = CreateWhiteTexture();
        SetField(player.Controller, "resizeParticleTexture", starTexture);
        yield return null;

        Assert.That(player.ClickBlock(), Is.True);
        Assert.That(GetProperty(player.GravityInteract, "IsHoldingBlock"), Is.True);
        Assert.That((bool)Invoke(player.Controller, "TryHandleResizeKey"), Is.True);
        Assert.That(GetProperty(player.GravityInteract, "IsHoldingBlock"), Is.False);
        Assert.That(GetProperty(airAnchor, "IsAnchored"), Is.True);

        Rigidbody body = block.GetComponent<Rigidbody>();
        Assert.That(body.isKinematic, Is.True);
        Assert.That(body.useGravity, Is.False);
        Component effect = (Component)GetProperty(airAnchor, "ActiveParticleEffect");
        Assert.That(effect, Is.Not.Null);
        createdObjects.Add(effect.gameObject);
        Assert.That(((ParticleSystem)GetProperty(effect, "Particles")).isPlaying, Is.True);

        Assert.That(player.ClickBlock(), Is.True);
        Assert.That(GetProperty(airAnchor, "IsAnchored"), Is.False);
        Assert.That(GetProperty(airAnchor, "ActiveParticleEffect"), Is.Null);
        Assert.That(GetProperty(player.GravityInteract, "IsHoldingBlock"), Is.True);
        Assert.That(GetProperty(player.GravityInteract, "HeldBlock"), Is.EqualTo(block.transform));
    }

    [UnityTest]
    public IEnumerator ContextualF_DoesNotAnchorNormalMathBlock()
    {
        PlayerHarness player = CreatePlayerHarness();
        Component unusedAnchor;
        CreateMathBlock(false, out unusedAnchor);
        Texture2D starTexture = CreateWhiteTexture();
        SetField(player.Controller, "resizeParticleTexture", starTexture);
        yield return null;

        Assert.That(player.ClickBlock(), Is.True);
        Assert.That(GetProperty(player.GravityInteract, "IsHoldingBlock"), Is.True);
        Assert.That((bool)Invoke(player.Controller, "TryHandleResizeKey"), Is.False);
        Assert.That(GetProperty(player.GravityInteract, "IsHoldingBlock"), Is.True);
        Assert.That(Resources.FindObjectsOfTypeAll(RequireType("BlockResizeParticleEffect")), Is.Empty);
    }

    private PlayerHarness CreatePlayerHarness()
    {
        GameObject playerObject = Track(new GameObject("AirAnchorTestPlayer"));
        PlayerInput playerInput = playerObject.AddComponent<PlayerInput>();
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

        GameObject cameraObject = Track(new GameObject("AirAnchorTestCamera"));
        cameraObject.transform.SetParent(playerObject.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Component gravityInteract = playerObject.AddComponent(RequireType("GravityInteract"));
        SetField(gravityInteract, "interactionCamera", camera.transform);
        SetField(gravityInteract, "grabDistance", 10f);

        Component controller = playerObject.AddComponent(RequireType("BlockResizeController"));
        SetField(controller, "playerCamera", camera);
        SetField(controller, "playerInput", playerInput);
        SetField(controller, "gravityInteract", gravityInteract);
        return new PlayerHarness(controller, gravityInteract, camera);
    }

    private Component CreateMathBlock(bool resizable, out Component airAnchor)
    {
        GameObject root = Track(new GameObject(resizable ? "ResizableAirAnchorBlock" : "NormalMathBlock"));
        root.tag = "MathBlock";
        root.transform.position = new Vector3(0f, 0f, 3f);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.useGravity = false;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        Component mathValue = root.AddComponent(RequireType("MathBlockValue"));
        Invoke(mathValue, "SetValue", 9);

        airAnchor = null;
        if (!resizable)
        {
            Physics.SyncTransforms();
            return mathValue;
        }

        GameObject visual = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        Component block = root.AddComponent(RequireType("ResizableBlock"));
        SetField(block, "mathBlockValue", mathValue);
        SetField(block, "visualRoot", visual.transform);
        SetField(block, "blockCollider", collider);
        SetField(block, "blockRigidbody", body);
        SetField(block, "obstacleMask", (LayerMask)0);
        InvokeNonPublic(block, "ApplyCurrentDimensions");
        airAnchor = root.AddComponent(RequireType("ResizableBlockAirAnchor"));
        Physics.SyncTransforms();
        return block;
    }

    private Texture2D CreateWhiteTexture()
    {
        Texture2D texture = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        return texture;
    }

    private T Track<T>(T target) where T : UnityEngine.Object
    {
        createdObjects.Add(target);
        return target;
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

    private sealed class PlayerHarness
    {
        public Component Controller { get; }
        public Component GravityInteract { get; }
        private Camera Camera { get; }

        public PlayerHarness(Component controller, Component gravityInteract, Camera camera)
        {
            Controller = controller;
            GravityInteract = gravityInteract;
            Camera = camera;
        }

        public bool ClickBlock()
        {
            Physics.SyncTransforms();
            Invoke(GravityInteract, "TryHandleGrabOrDrop");
            return (bool)GetProperty(GravityInteract, "IsHoldingBlock");
        }
    }
}
