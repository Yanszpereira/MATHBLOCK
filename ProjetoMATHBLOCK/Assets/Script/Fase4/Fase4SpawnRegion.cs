using System;
using UnityEngine;

/// <summary>One pedestal's authored set of physical spawn points.</summary>
[DisallowMultipleComponent]
public sealed class Fase4SpawnRegion : MonoBehaviour
{
    public const int RequiredPointCount = 8;

    [SerializeField] private Transform[] spawnPoints = new Transform[RequiredPointCount];

    public Transform[] SpawnPoints => (Transform[])spawnPoints.Clone();

    public bool IsValid
    {
        get
        {
            if (spawnPoints == null || spawnPoints.Length != RequiredPointCount)
                return false;

            for (int i = 0; i < spawnPoints.Length; i++)
                if (spawnPoints[i] == null)
                    return false;

            return true;
        }
    }

    public void Configure(Transform[] points)
    {
        if (points == null || points.Length != RequiredPointCount)
            throw new ArgumentException($"A região requer exatamente {RequiredPointCount} pontos.", nameof(points));

        for (int i = 0; i < points.Length; i++)
            if (points[i] == null)
                throw new ArgumentException("A região não pode conter pontos nulos.", nameof(points));

        spawnPoints = (Transform[])points.Clone();
    }

    private void OnValidate()
    {
        if (spawnPoints == null || spawnPoints.Length != RequiredPointCount)
            Array.Resize(ref spawnPoints, RequiredPointCount);
    }
}
