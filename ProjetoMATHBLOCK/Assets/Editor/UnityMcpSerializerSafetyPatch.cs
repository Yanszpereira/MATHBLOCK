#if UNITY_EDITOR
using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hardens the Unity AI Assistant MCP serializer against recursive Unity properties.
/// The package's serializer reflects component properties and can otherwise recurse
/// through Matrix4x4 computed properties until the Editor stack overflows.
/// </summary>
[InitializeOnLoad]
internal static class UnityMcpSerializerSafetyPatch
{
    static UnityMcpSerializerSafetyPatch()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        Type serializerType = Type.GetType(
            "Unity.AI.MCP.Editor.Helpers.GameObjectSerializer, Unity.AI.MCP.Editor");
        if (serializerType == null)
        {
            return;
        }

        FieldInfo serializerField = serializerType.GetField(
            "_outputSerializer", BindingFlags.Static | BindingFlags.NonPublic);
        JsonSerializer serializer = serializerField?.GetValue(null) as JsonSerializer;
        if (serializer == null)
        {
            return;
        }

        // Stop unexpected object graphs before they can consume the Editor stack.
        serializer.MaxDepth = 16;

        for (int i = 0; i < serializer.Converters.Count; i++)
        {
            if (serializer.Converters[i] is Matrix4x4McpConverter)
            {
                return;
            }
        }

        serializer.Converters.Insert(0, new Matrix4x4McpConverter());
    }
}

internal sealed class Matrix4x4McpConverter : JsonConverter<Matrix4x4>
{
    public override void WriteJson(JsonWriter writer, Matrix4x4 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("m00"); writer.WriteValue(value.m00);
        writer.WritePropertyName("m01"); writer.WriteValue(value.m01);
        writer.WritePropertyName("m02"); writer.WriteValue(value.m02);
        writer.WritePropertyName("m03"); writer.WriteValue(value.m03);
        writer.WritePropertyName("m10"); writer.WriteValue(value.m10);
        writer.WritePropertyName("m11"); writer.WriteValue(value.m11);
        writer.WritePropertyName("m12"); writer.WriteValue(value.m12);
        writer.WritePropertyName("m13"); writer.WriteValue(value.m13);
        writer.WritePropertyName("m20"); writer.WriteValue(value.m20);
        writer.WritePropertyName("m21"); writer.WriteValue(value.m21);
        writer.WritePropertyName("m22"); writer.WriteValue(value.m22);
        writer.WritePropertyName("m23"); writer.WriteValue(value.m23);
        writer.WritePropertyName("m30"); writer.WriteValue(value.m30);
        writer.WritePropertyName("m31"); writer.WriteValue(value.m31);
        writer.WritePropertyName("m32"); writer.WriteValue(value.m32);
        writer.WritePropertyName("m33"); writer.WriteValue(value.m33);
        writer.WriteEndObject();
    }

    public override Matrix4x4 ReadJson(
        JsonReader reader,
        Type objectType,
        Matrix4x4 existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        JObject json = JObject.Load(reader);
        Matrix4x4 value = new Matrix4x4();
        value.m00 = json["m00"]?.Value<float>() ?? 0f;
        value.m01 = json["m01"]?.Value<float>() ?? 0f;
        value.m02 = json["m02"]?.Value<float>() ?? 0f;
        value.m03 = json["m03"]?.Value<float>() ?? 0f;
        value.m10 = json["m10"]?.Value<float>() ?? 0f;
        value.m11 = json["m11"]?.Value<float>() ?? 0f;
        value.m12 = json["m12"]?.Value<float>() ?? 0f;
        value.m13 = json["m13"]?.Value<float>() ?? 0f;
        value.m20 = json["m20"]?.Value<float>() ?? 0f;
        value.m21 = json["m21"]?.Value<float>() ?? 0f;
        value.m22 = json["m22"]?.Value<float>() ?? 0f;
        value.m23 = json["m23"]?.Value<float>() ?? 0f;
        value.m30 = json["m30"]?.Value<float>() ?? 0f;
        value.m31 = json["m31"]?.Value<float>() ?? 0f;
        value.m32 = json["m32"]?.Value<float>() ?? 0f;
        value.m33 = json["m33"]?.Value<float>() ?? 0f;
        return value;
    }
}
#endif
