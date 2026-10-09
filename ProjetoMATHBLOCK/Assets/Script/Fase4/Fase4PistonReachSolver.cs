using UnityEngine;

/// <summary>Geometry-only calibration helper that finds the first radial-boundary crossing of projected mesh triangles.</summary>
internal static class Fase4PistonReachSolver
{
    public static bool TryGetMaximumTravel(Vector3[] worldVertices, int[] triangles, Vector3 center, Vector3 inward, float boundaryRadius, out float maximumTravel)
    {
        maximumTravel = 0f;
        if (worldVertices == null || worldVertices.Length == 0 || triangles == null || triangles.Length < 3 || triangles.Length % 3 != 0 || float.IsNaN(boundaryRadius) ||
            float.IsInfinity(boundaryRadius) || boundaryRadius <= 0f)
            return false;

        inward.y = 0f;
        if (inward.sqrMagnitude <= 0.000001f)
            return false;
        inward.Normalize();

        float firstCrossing = float.PositiveInfinity;
        Vector3 tangent = Vector3.Cross(Vector3.up, inward).normalized;
        for (int triangle = 0; triangle < triangles.Length; triangle += 3)
        {
            int first = triangles[triangle];
            int second = triangles[triangle + 1];
            int third = triangles[triangle + 2];
            if (first < 0 || first >= worldVertices.Length || second < 0 || second >= worldVertices.Length || third < 0 || third >= worldVertices.Length)
                return false;

            Vector3 a = ProjectToPlane(worldVertices[first] - center);
            Vector3 b = ProjectToPlane(worldVertices[second] - center);
            Vector3 c = ProjectToPlane(worldVertices[third] - center);
            if (ContainsOrigin(a, b, c))
                return false;

            if (!AccumulateEdge(a, b, inward, tangent, boundaryRadius, ref firstCrossing) ||
                !AccumulateEdge(b, c, inward, tangent, boundaryRadius, ref firstCrossing) ||
                !AccumulateEdge(c, a, inward, tangent, boundaryRadius, ref firstCrossing))
                return false;
        }

        if (float.IsInfinity(firstCrossing))
            return false;

        maximumTravel = firstCrossing;
        return true;
    }

    public static bool TryGetMinimumRadius(Vector3[] worldVertices, int[] triangles, Vector3 center, out float minimumRadius)
    {
        minimumRadius = float.PositiveInfinity;
        if (worldVertices == null || worldVertices.Length == 0 || triangles == null || triangles.Length < 3 || triangles.Length % 3 != 0)
            return false;

        for (int triangle = 0; triangle < triangles.Length; triangle += 3)
        {
            int first = triangles[triangle];
            int second = triangles[triangle + 1];
            int third = triangles[triangle + 2];
            if (first < 0 || first >= worldVertices.Length || second < 0 || second >= worldVertices.Length || third < 0 || third >= worldVertices.Length)
                return false;

            Vector3 a = ProjectToPlane(worldVertices[first] - center);
            Vector3 b = ProjectToPlane(worldVertices[second] - center);
            Vector3 c = ProjectToPlane(worldVertices[third] - center);
            if (ContainsOrigin(a, b, c))
            {
                minimumRadius = 0f;
                return true;
            }

            minimumRadius = Mathf.Min(minimumRadius, DistanceToSegment(a, b), DistanceToSegment(b, c), DistanceToSegment(c, a));
        }

        return !float.IsInfinity(minimumRadius);
    }

    private static bool AccumulateEdge(Vector3 a, Vector3 b, Vector3 inward, Vector3 tangent, float radius, ref float firstCrossing)
    {
        if (DistanceToSegment(a, b) <= radius)
            return false;

        float outwardA = Vector3.Dot(a, -inward);
        float outwardDelta = Vector3.Dot(b - a, -inward);
        float tangentA = Vector3.Dot(a, tangent);
        float tangentDelta = Vector3.Dot(b - a, tangent);
        float lower = 0f;
        float upper = 1f;

        if (Mathf.Abs(tangentDelta) <= 0.000001f)
        {
            if (Mathf.Abs(tangentA) > radius)
                return true;
        }
        else
        {
            float t0 = (-radius - tangentA) / tangentDelta;
            float t1 = (radius - tangentA) / tangentDelta;
            lower = Mathf.Max(lower, Mathf.Min(t0, t1));
            upper = Mathf.Min(upper, Mathf.Max(t0, t1));
            if (lower > upper)
                return true;
        }

        EvaluateEdgePoint(lower, outwardA, outwardDelta, tangentA, tangentDelta, radius, ref firstCrossing);
        EvaluateEdgePoint(upper, outwardA, outwardDelta, tangentA, tangentDelta, radius, ref firstCrossing);

        float denominator = Mathf.Sqrt(outwardDelta * outwardDelta + tangentDelta * tangentDelta);
        if (denominator > 0.000001f && Mathf.Abs(tangentDelta) > 0.000001f)
        {
            float sign = Mathf.Sign(-outwardDelta * tangentDelta);
            float stationaryTangent = sign * Mathf.Abs(outwardDelta) * radius / denominator;
            float stationaryT = (stationaryTangent - tangentA) / tangentDelta;
            if (stationaryT > lower && stationaryT < upper)
                EvaluateEdgePoint(stationaryT, outwardA, outwardDelta, tangentA, tangentDelta, radius, ref firstCrossing);
        }

        return true;
    }

    private static void EvaluateEdgePoint(float t, float outwardA, float outwardDelta, float tangentA, float tangentDelta, float radius, ref float firstCrossing)
    {
        float lateral = tangentA + tangentDelta * t;
        float radialBoundary = Mathf.Sqrt(Mathf.Max(0f, radius * radius - lateral * lateral));
        float travel = outwardA + outwardDelta * t - radialBoundary;
        if (travel >= 0f && travel < firstCrossing)
            firstCrossing = travel;
    }

    private static float DistanceToSegment(Vector3 a, Vector3 b)
    {
        Vector3 edge = b - a;
        float t = edge.sqrMagnitude <= 0.000001f ? 0f : Mathf.Clamp01(-Vector3.Dot(a, edge) / edge.sqrMagnitude);
        return (a + edge * t).magnitude;
    }

    private static bool ContainsOrigin(Vector3 a, Vector3 b, Vector3 c)
    {
        float ab = Cross2D(b - a, -a);
        float bc = Cross2D(c - b, -b);
        float ca = Cross2D(a - c, -c);
        bool hasNegative = ab < -0.000001f || bc < -0.000001f || ca < -0.000001f;
        bool hasPositive = ab > 0.000001f || bc > 0.000001f || ca > 0.000001f;
        return !(hasNegative && hasPositive);
    }

    private static float Cross2D(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;

    private static Vector3 ProjectToPlane(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
