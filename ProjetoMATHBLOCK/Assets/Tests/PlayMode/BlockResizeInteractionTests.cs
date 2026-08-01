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
    public IEnumerator EnterMode_SelectsValidBlock()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(GetProperty(h.Controller, "SelectedBlock"), Is.EqualTo(h.Block));
    }

    [UnityTest]
    public IEnumerator WallOcclusion_PreventsSelection()
    {
        Harness h = CreateHarness(9);
        CreateObstacle("OccludingWall", new Vector3(0f, 0f, 1.5f), new Vector3(2f, 2f, 0.2f));
        Physics.SyncTransforms(); yield return null;
        Assert.That(h.BeginFromCenter(), Is.False);
    }

    [UnityTest]
    public IEnumerator Gizmo_AppearsOnConfiguredPlaneFace()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Component gizmo = (Component)GetProperty(h.Controller, "ActiveGizmo");
        Vector3 normal = (Vector3)GetProperty(gizmo, "FaceNormalWorld");
        Assert.That(Vector3.Distance(normal, Vector3.back), Is.LessThan(0.001f));
        Assert.That(gizmo.gameObject.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator Rigidbody_IsStableDuringSession()
    {
        Harness h = CreateHarness(9);
        h.Rigidbody.isKinematic = false; h.Rigidbody.useGravity = true; h.Rigidbody.linearVelocity = Vector3.right * 3f;
        yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(h.Rigidbody.isKinematic, Is.True);
        Assert.That(h.Rigidbody.useGravity, Is.False);
        Assert.That(h.Rigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
    }

    [UnityTest]
    public IEnumerator ArrowDrag_ChangesOneDimensionByOneStep()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(h.Drag("Right", new Vector3(1.1f, 0f, 0f)), Is.True);
        AssertDimensions(h.Block, 2, 1, 1);
    }

    [UnityTest]
    public IEnumerator CenterDrag_ChangesTwoDimensions()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(h.Drag("Center", new Vector3(1.1f, 0f, 0f)), Is.True);
        AssertDimensions(h.Block, 2, 2, 1);
    }

    [UnityTest]
    public IEnumerator AreaLimit_RejectsOversizedProposal()
    {
        Harness h = CreateHarness(1); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(h.Drag("Right", new Vector3(1.1f, 0f, 0f)), Is.False);
        AssertDimensions(h.Block, 1, 1, 1);
    }

    [UnityTest]
    public IEnumerator Obstacle_BlocksGrowth()
    {
        Harness h = CreateHarness(9);
        CreateObstacle("GrowthWall", new Vector3(1.45f, 0f, 3f), new Vector3(0.2f, 2f, 2f));
        Physics.SyncTransforms(); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(h.Drag("Right", new Vector3(1.1f, 0f, 0f)), Is.False);
        AssertDimensions(h.Block, 1, 1, 1);
    }

    [UnityTest]
    public IEnumerator MouseRelease_EndsOnlyCurrentDrag()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True); h.Drag("Right", new Vector3(1.1f, 0f, 0f));
        Invoke(h.Controller, "EndHandleDrag");
        Assert.That(GetProperty(h.Controller, "State").ToString(), Is.EqualTo("ResizeMode"));
        AssertDimensions(h.Block, 2, 1, 1);
    }

    [UnityTest]
    public IEnumerator Confirm_KeepsCurrentSize()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True); h.Drag("Right", new Vector3(1.1f, 0f, 0f));
        Invoke(h.Controller, "ConfirmResizeSession");
        AssertDimensions(h.Block, 2, 1, 1);
        Assert.That(GetProperty(h.Controller, "State").ToString(), Is.EqualTo("Idle"));
    }

    [UnityTest]
    public IEnumerator Cancel_RestoresSessionState()
    {
        Harness h = CreateHarness(9); yield return null;
        Assert.That(h.BeginFromCenter(), Is.True); Vector3 initial = h.Block.transform.position; h.Drag("Right", new Vector3(1.1f, 0f, 0f));
        Invoke(h.Controller, "CancelResizeSession");
        AssertDimensions(h.Block, 1, 1, 1);
        Assert.That(Vector3.Distance(h.Block.transform.position, initial), Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator Exit_RestoresControlsCursorRigidbodyAndActionMap()
    {
        Harness h = CreateHarness(9, true); yield return null;
        bool previousLook = h.Look.enabled, previousMovement = h.Movement.enabled;
        CursorLockMode previousLock = Cursor.lockState; bool previousVisible = Cursor.visible;
        Assert.That(h.BeginFromCenter(), Is.True);
        Assert.That(h.Look.enabled, Is.False); Assert.That(h.Movement.enabled, Is.False);
        Invoke(h.Controller, "ConfirmResizeSession");
        Assert.That(h.Look.enabled, Is.EqualTo(previousLook)); Assert.That(h.Movement.enabled, Is.EqualTo(previousMovement));
        Assert.That(Cursor.lockState, Is.EqualTo(previousLock)); Assert.That(Cursor.visible, Is.EqualTo(previousVisible));
        Assert.That(h.PlayerInput.currentActionMap.name, Is.EqualTo("Player"));
        Assert.That(h.Rigidbody.isKinematic, Is.False); Assert.That(h.Rigidbody.useGravity, Is.True);
    }

    private Harness CreateHarness(int value, bool includePlayerBehaviours = false)
    {
        GameObject player = Track(new GameObject("ResizeTestPlayer"));
        PlayerInput playerInput = player.AddComponent<PlayerInput>();
        InputActionAsset actions = Track(ScriptableObject.CreateInstance<InputActionAsset>());
        InputActionMap playerMap = actions.AddActionMap("Player"); playerMap.AddAction("EnterResize", InputActionType.Button);
        InputActionMap resizeMap = actions.AddActionMap("Resize");
        resizeMap.AddAction("Point", InputActionType.PassThrough);
        resizeMap.AddAction("Click", InputActionType.Button); resizeMap.AddAction("Cancel", InputActionType.Button); resizeMap.AddAction("ExitResize", InputActionType.Button);
        playerInput.actions = actions; playerInput.defaultActionMap = "Player"; playerInput.ActivateInput(); playerInput.SwitchCurrentActionMap("Player");

        GameObject cameraObject = Track(new GameObject("TestCamera")); cameraObject.transform.SetParent(player.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        Component look = null, movement = null;
        if (includePlayerBehaviours)
        {
            player.AddComponent<CharacterController>();
            movement = player.AddComponent(RequireType("PlayerMovement")); look = player.AddComponent(RequireType("Look"));
            SetField(movement, "controller", player.GetComponent<CharacterController>()); SetField(look, "cameraTransform", camera.transform);
        }

        Component controller = player.AddComponent(RequireType("BlockResizeController"));
        Component gizmoPrefab = CreateGizmoPrefab();
        SetField(controller, "playerCamera", camera); SetField(controller, "playerInput", playerInput); SetField(controller, "resizeGizmoPrefab", gizmoPrefab);
        SetField(controller, "interactionDistance", 5f); SetField(controller, "targetingMask", (LayerMask)~(1 << 8)); SetField(controller, "resizeHandleMask", (LayerMask)(1 << 8));
        if (look != null) SetField(controller, "playerLook", look); if (movement != null) SetField(controller, "playerMovement", movement);
        Component block = CreateResizableBlock(value);
        return new Harness(controller, block, block.GetComponent<Rigidbody>(), playerInput, camera, look, movement);
    }

    private Component CreateResizableBlock(int value)
    {
        GameObject root = Track(new GameObject("TestResizableBlock")); root.transform.position = new Vector3(0f, 0f, 3f);
        Rigidbody body = root.AddComponent<Rigidbody>(); body.useGravity = true; BoxCollider collider = root.AddComponent<BoxCollider>();
        GameObject visual = Track(GameObject.CreatePrimitive(PrimitiveType.Cube)); UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.name = "BlockVisual"; visual.transform.SetParent(root.transform, false);
        Component mathValue = root.AddComponent(RequireType("MathBlockValue")); Invoke(mathValue, "SetValue", value);
        Component block = root.AddComponent(RequireType("ResizableBlock"));
        SetField(block, "mathBlockValue", mathValue); SetField(block, "visualRoot", visual.transform); SetField(block, "blockCollider", collider);
        SetField(block, "blockRigidbody", body); SetField(block, "obstacleMask", (LayerMask)~(1 << 8)); Invoke(block, "ApplyCurrentDimensions");
        Physics.SyncTransforms(); return block;
    }

    private Component CreateGizmoPrefab()
    {
        GameObject root = Track(new GameObject("TestBlockResizeGizmo")); root.SetActive(false);
        Type handleType = RequireType("BlockResizeHandle"), positionType = RequireType("ResizeHandlePosition");
        string[] positions = { "Top", "Bottom", "Left", "Right", "Center" };
        foreach (string position in positions)
        {
            GameObject handleObject = new GameObject(position + "Handle"); handleObject.layer = 8; handleObject.transform.SetParent(root.transform, false);
            GameObject interaction = new GameObject("InteractionCollider"); interaction.layer = 8; interaction.transform.SetParent(handleObject.transform, false);
            BoxCollider interactionCollider = interaction.AddComponent<BoxCollider>(); interactionCollider.isTrigger = true; interactionCollider.size = Vector3.one * 0.45f;
            GameObject visualRoot = new GameObject("VisualRoot"); visualRoot.transform.SetParent(handleObject.transform, false);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.transform.SetParent(visualRoot.transform, false);
            Component handle = handleObject.AddComponent(handleType);
            SetField(handle, "position", Enum.Parse(positionType, position)); SetField(handle, "interactionCollider", interactionCollider);
            SetField(handle, "visualRoot", visualRoot.transform); SetField(handle, "visualRenderers", visual.GetComponents<Renderer>());
        }
        return root.AddComponent(RequireType("BlockResizeGizmo"));
    }

    private GameObject CreateObstacle(string name, Vector3 position, Vector3 scale)
    {
        GameObject obstacle = Track(GameObject.CreatePrimitive(PrimitiveType.Cube)); obstacle.name = name; obstacle.transform.position = position; obstacle.transform.localScale = scale; return obstacle;
    }

    private static void AssertDimensions(Component block, int width, int height, int depth)
    {
        Assert.That(GetProperty(block, "Width"), Is.EqualTo(width)); Assert.That(GetProperty(block, "Height"), Is.EqualTo(height)); Assert.That(GetProperty(block, "Depth"), Is.EqualTo(depth));
    }

    private T Track<T>(T target) where T : UnityEngine.Object { createdObjects.Add(target); return target; }
    private static Type RequireType(string name) { Type type = Type.GetType(name + ", Assembly-CSharp"); Assert.That(type, Is.Not.Null, $"Tipo {name} nao encontrado."); return type; }
    private static object Invoke(object target, string methodName, params object[] arguments) { MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags); Assert.That(method, Is.Not.Null); return method.Invoke(target, arguments); }
    private static object GetProperty(object target, string propertyName) { PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags); Assert.That(property, Is.Not.Null); return property.GetValue(target); }
    private static void SetField(object target, string fieldName, object value) { FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags); Assert.That(field, Is.Not.Null, $"Campo {fieldName} nao encontrado."); field.SetValue(target, value); }

    private sealed class Harness
    {
        public Component Controller { get; } public Component Block { get; } public Rigidbody Rigidbody { get; }
        public PlayerInput PlayerInput { get; } public Camera Camera { get; } public Behaviour Look { get; } public Behaviour Movement { get; }
        public Harness(Component controller, Component block, Rigidbody rigidbody, PlayerInput input, Camera camera, Component look, Component movement)
        { Controller = controller; Block = block; Rigidbody = rigidbody; PlayerInput = input; Camera = camera; Look = look as Behaviour; Movement = movement as Behaviour; }
        public bool BeginFromCenter() => (bool)Invoke(Controller, "TryBeginResizeFromRay", new Ray(Camera.transform.position, Camera.transform.forward));
        public bool Drag(string handlePosition, Vector3 planeDelta)
        {
            Component gizmo = (Component)GetProperty(Controller, "ActiveGizmo"); Type handleType = RequireType("BlockResizeHandle"); Component selected = null;
            foreach (Component handle in gizmo.GetComponentsInChildren(handleType, true))
                if (GetProperty(handle, "Position").ToString() == handlePosition) { selected = handle; break; }
            Assert.That(selected, Is.Not.Null);
            Vector3 planePoint = (Vector3)GetProperty(gizmo, "FacePlanePoint");
            Assert.That((bool)Invoke(Controller, "BeginHandleDrag", selected, new Ray(Camera.transform.position, planePoint - Camera.transform.position)), Is.True);
            Vector3 targetPoint = planePoint + planeDelta;
            return (bool)Invoke(Controller, "UpdateHandleDrag", new Ray(Camera.transform.position, targetPoint - Camera.transform.position));
        }
    }
}
