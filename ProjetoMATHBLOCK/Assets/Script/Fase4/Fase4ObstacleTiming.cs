using System;

/// <summary>Deterministic timing values shared by the Fase 4 obstacle controller and tests.</summary>
public static class Fase4ObstacleTiming
{
    public static float GetSecondsPerRevolution(int round)
    {
        switch (round)
        {
            case 1: return 15f;
            case 2: return 12f;
            case 3: return 9f;
            default: throw new ArgumentOutOfRangeException(nameof(round), round, "A Fase 4 round must be between 1 and 3.");
        }
    }

    public static float GetDegreesPerSecond(int round)
    {
        return 360f / GetSecondsPerRevolution(round);
    }
}
