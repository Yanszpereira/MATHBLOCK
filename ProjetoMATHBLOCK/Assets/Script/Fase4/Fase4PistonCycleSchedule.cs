using System;

/// <summary>
/// Pure, deterministic schedule for synchronized Fase 4 pistons. It owns timing only;
/// the MonoBehaviour adapter applies the resulting extension fraction to scene transforms.
/// </summary>
public sealed class Fase4PistonCycleSchedule
{
    public enum Phase
    {
        Stopped,
        Warning,
        Extending,
        Holding,
        Retracting,
        Resting
    }

    public const float WarningSeconds = 2f;
    public const float ExtensionSeconds = 1.5f;
    public const float HoldSeconds = 0.5f;
    public const float RetractionSeconds = 2f;
    public const float CycleStartIntervalSeconds = 10f;
    public const float FullCycleSeconds = WarningSeconds + ExtensionSeconds + HoldSeconds + RetractionSeconds;
    public const float RestSeconds = CycleStartIntervalSeconds - FullCycleSeconds;

    private float phaseElapsed;
    private float retractionStartAmount;

    public Phase CurrentPhase { get; private set; } = Phase.Stopped;
    public float ExtensionAmount { get; private set; }
    public int CycleIndex { get; private set; }
    public bool IsRepeating { get; private set; }
    public float SecondsUntilPhaseChange => Math.Max(0f, GetDuration(CurrentPhase) - phaseElapsed);

    public void Activate()
    {
        if (IsRepeating || CurrentPhase != Phase.Stopped)
            return;

        IsRepeating = true;
        BeginWarning();
    }

    /// <summary>Stops future cycles and starts a smooth return if a piston is extended.</summary>
    public void Stop()
    {
        IsRepeating = false;
        if (ExtensionAmount <= 0f)
        {
            SetPhase(Phase.Stopped);
            ExtensionAmount = 0f;
            return;
        }

        retractionStartAmount = ExtensionAmount;
        SetPhase(Phase.Retracting);
    }

    /// <summary>Resets the schedule at a new round boundary after the previous attack has retracted.</summary>
    public void Reset()
    {
        IsRepeating = false;
        CycleIndex = 0;
        ExtensionAmount = 0f;
        retractionStartAmount = 0f;
        SetPhase(Phase.Stopped);
    }

    public void Advance(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f ||
            CurrentPhase == Phase.Stopped)
            return;

        float remaining = deltaTime;
        int transitions = 0;
        while (remaining > 0f && CurrentPhase != Phase.Stopped && transitions < 128)
        {
            float duration = GetDuration(CurrentPhase);
            float phaseRemaining = Math.Max(0f, duration - phaseElapsed);
            if (remaining + 0.000001f < phaseRemaining)
            {
                phaseElapsed += remaining;
                remaining = 0f;
                UpdateExtensionAmount();
                break;
            }

            remaining = Math.Max(0f, remaining - phaseRemaining);
            phaseElapsed = duration;
            UpdateExtensionAmount();
            AdvancePhase();
            transitions++;
        }
    }

    private void AdvancePhase()
    {
        switch (CurrentPhase)
        {
            case Phase.Warning:
                SetPhase(Phase.Extending);
                break;
            case Phase.Extending:
                ExtensionAmount = 1f;
                SetPhase(Phase.Holding);
                break;
            case Phase.Holding:
                retractionStartAmount = ExtensionAmount;
                SetPhase(Phase.Retracting);
                break;
            case Phase.Retracting:
                ExtensionAmount = 0f;
                if (IsRepeating)
                    SetPhase(Phase.Resting);
                else
                    SetPhase(Phase.Stopped);
                break;
            case Phase.Resting:
                BeginWarning();
                break;
            default:
                SetPhase(Phase.Stopped);
                break;
        }
    }

    private void BeginWarning()
    {
        CycleIndex++;
        SetPhase(Phase.Warning);
    }

    private void SetPhase(Phase next)
    {
        CurrentPhase = next;
        phaseElapsed = 0f;
    }

    private void UpdateExtensionAmount()
    {
        if (CurrentPhase == Phase.Extending)
            ExtensionAmount = SmoothStep01(phaseElapsed / ExtensionSeconds);
        else if (CurrentPhase == Phase.Retracting)
            ExtensionAmount = Math.Max(0f, retractionStartAmount * (1f - SmoothStep01(phaseElapsed / RetractionSeconds)));
    }

    private static float SmoothStep01(float value)
    {
        float t = Math.Max(0f, Math.Min(1f, value));
        return t * t * (3f - 2f * t);
    }

    private static float GetDuration(Phase phase)
    {
        switch (phase)
        {
            case Phase.Warning: return WarningSeconds;
            case Phase.Extending: return ExtensionSeconds;
            case Phase.Holding: return HoldSeconds;
            case Phase.Retracting: return RetractionSeconds;
            case Phase.Resting: return RestSeconds;
            default: return 0f;
        }
    }
}
