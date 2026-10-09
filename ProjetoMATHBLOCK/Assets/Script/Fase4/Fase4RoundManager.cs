using System;

/// <summary>
/// Owns the logical progression of the three-expression challenge in Fase 4.
/// </summary>
public sealed class Fase4RoundManager
{
    public enum RoundState
    {
        AwaitingInteraction,
        Preparing,
        Active,
        InterRoundTransition,
        FailureAndRespawn,
        ChallengeCompleted
    }

    public const int TotalRounds = 3;
    public const float InitialRoundSeconds = 60f;
    public const float CorrectAnswerBonusSeconds = 35f;
    public const float FirstWrongPenaltySeconds = 5f;
    public const float InterRoundTransitionSeconds = 5f;

    private readonly Fase4ExpressionDeck deck;
    private float transitionRemaining;
    private bool isPublishingEvents;

    public RoundState State { get; private set; } = RoundState.AwaitingInteraction;
    public int CompletedRounds { get; private set; }
    public int CurrentRound => Math.Min(CompletedRounds + 1, TotalRounds);
    public int ErrorsThisRound { get; private set; }
    public float RemainingSeconds { get; private set; }
    public Fase4ExpressionDefinition CurrentExpression { get; private set; }
    public bool PistonsLogicallyActive { get; private set; }

    public event Action<RoundState> StateChanged;
    public event Action<Fase4ExpressionDefinition> ExpressionSelected;
    public event Action<float> TimeChanged;
    public event Action<int> WrongAnswerRecorded;
    public event Action RoundAttemptFailed;
    public event Action PistonsActivated;
    public event Action ChallengeCompleted;

    public Fase4RoundManager(Fase4ExpressionDeck expressionDeck)
    {
        deck = expressionDeck ?? throw new ArgumentNullException(nameof(expressionDeck));
        RemainingSeconds = InitialRoundSeconds;
    }

    public bool RequestAttempt()
    {
        if (isPublishingEvents || State != RoundState.AwaitingInteraction) return false;
        BeginCurrentRoundAttempt();
        return true;
    }

    public bool NotifySpawnCompleted()
    {
        if (isPublishingEvents || State != RoundState.Preparing) return false;
        bool stateChanged = SetState(RoundState.Active);
        if (stateChanged) Publish(() => StateChanged?.Invoke(State));
        return true;
    }

    /// <summary>
    /// Submits the numeric result produced by combining the MathBlocks. The
    /// operation sequence is deliberately irrelevant; only the final value is
    /// compared with the selected expression's result.
    /// </summary>
    public bool SubmitAnswer(int finalBlockValue)
    {
        if (State != RoundState.Active || isPublishingEvents) return false;
        bool correct = Fase4ExpressionEvaluator.TryEvaluate(CurrentExpression, out int expectedValue) &&
                       finalBlockValue == expectedValue;
        return SubmitAnswer(correct);
    }

    /// <summary>
    /// Compatibility entry point for callers that already evaluate the answer.
    /// New receptacle integrations should submit the MathBlock's numeric value.
    /// </summary>
    public bool SubmitAnswer(bool correct)
    {
        if (State != RoundState.Active || isPublishingEvents) return false;

        RoundState oldState = State;
        float oldTime = RemainingSeconds;
        int recordedErrorCount = 0;
        bool activatePistons = false;
        bool failAttempt = false;
        bool challengeCompleted = false;

        if (correct)
        {
            CompletedRounds++;
            RemainingSeconds += CorrectAnswerBonusSeconds;
            if (CompletedRounds >= TotalRounds)
            {
                SetState(RoundState.ChallengeCompleted);
                challengeCompleted = true;
            }
            else
            {
                transitionRemaining = InterRoundTransitionSeconds;
                SetState(RoundState.InterRoundTransition);
            }
        }
        else
        {
            ErrorsThisRound++;
            recordedErrorCount = ErrorsThisRound;

            if (ErrorsThisRound == 1)
            {
                RemainingSeconds = Math.Max(0f, RemainingSeconds - FirstWrongPenaltySeconds);
                if (RemainingSeconds <= 0f)
                    failAttempt = EnterFailureState();
            }
            else if (ErrorsThisRound == 2)
            {
                PistonsLogicallyActive = true;
                activatePistons = true;
            }

            if (ErrorsThisRound >= 3)
                failAttempt = EnterFailureState();
        }

        RoundState newState = State;
        float newTime = RemainingSeconds;
        Publish(() =>
        {
            if (!ApproximatelyEqual(oldTime, newTime)) TimeChanged?.Invoke(newTime);
            if (recordedErrorCount > 0) WrongAnswerRecorded?.Invoke(recordedErrorCount);
            if (activatePistons) PistonsActivated?.Invoke();
            if (oldState != newState) StateChanged?.Invoke(newState);
            if (failAttempt) RoundAttemptFailed?.Invoke();
            if (challengeCompleted) ChallengeCompleted?.Invoke();
        });

        return true;
    }

    /// <summary>
    /// Advances an active timer or inter-round transition. NaN, either
    /// infinity, zero, and negative deltas are ignored. A finite delta larger
    /// than the current phase is capped at that phase's remaining duration.
    /// </summary>
    public void Tick(float elapsedSeconds)
    {
        if (isPublishingEvents || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) ||
            elapsedSeconds <= 0f)
            return;

        if (State == RoundState.Active)
        {
            float oldTime = RemainingSeconds;
            RoundState oldState = State;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - Math.Min(elapsedSeconds, RemainingSeconds));
            bool failed = RemainingSeconds <= 0f && EnterFailureState();
            float newTime = RemainingSeconds;
            RoundState newState = State;
            Publish(() =>
            {
                if (!ApproximatelyEqual(oldTime, newTime)) TimeChanged?.Invoke(newTime);
                if (oldState != newState) StateChanged?.Invoke(newState);
                if (failed) RoundAttemptFailed?.Invoke();
            });
        }
        else if (State == RoundState.InterRoundTransition)
        {
            transitionRemaining = Math.Max(0f, transitionRemaining - Math.Min(elapsedSeconds, transitionRemaining));
            if (transitionRemaining <= 0f)
                BeginCurrentRoundAttempt();
        }
    }

    public bool NotifyRespawnCompleted()
    {
        if (isPublishingEvents || State != RoundState.FailureAndRespawn) return false;
        bool stateChanged = SetState(RoundState.AwaitingInteraction);
        if (stateChanged) Publish(() => StateChanged?.Invoke(State));
        return true;
    }

    /// <summary>
    /// Ends an active attempt because of an external arena failure (for example,
    /// the player falling into the Void). This is not a wrong answer and does
    /// not apply a time penalty or change the completed-round count.
    /// </summary>
    public bool AbortAttemptWithoutPenalty()
    {
        if (isPublishingEvents || State != RoundState.Active)
            return false;

        float oldTime = RemainingSeconds;
        RoundState oldState = State;
        if (!EnterFailureState())
            return false;

        float newTime = RemainingSeconds;
        RoundState newState = State;
        Publish(() =>
        {
            if (!ApproximatelyEqual(oldTime, newTime)) TimeChanged?.Invoke(newTime);
            if (oldState != newState) StateChanged?.Invoke(newState);
            RoundAttemptFailed?.Invoke();
        });
        return true;
    }

    private void BeginCurrentRoundAttempt()
    {
        float oldTime = RemainingSeconds;
        RoundState oldState = State;

        ErrorsThisRound = 0;
        PistonsLogicallyActive = false;
        if ((CurrentRound == 1 && CompletedRounds == 0) || oldState == RoundState.AwaitingInteraction)
            RemainingSeconds = InitialRoundSeconds;

        CurrentExpression = deck.Draw();
        SetState(RoundState.Preparing);

        float newTime = RemainingSeconds;
        RoundState newState = State;
        Publish(() =>
        {
            if (!ApproximatelyEqual(oldTime, newTime)) TimeChanged?.Invoke(newTime);
            if (oldState != newState) StateChanged?.Invoke(newState);
            ExpressionSelected?.Invoke(CurrentExpression);
        });
    }

    private bool EnterFailureState()
    {
        if (State != RoundState.Active) return false;
        PistonsLogicallyActive = false;
        RemainingSeconds = 0f;
        SetState(RoundState.FailureAndRespawn);
        return true;
    }

    private bool SetState(RoundState next)
    {
        if (State == next) return false;
        State = next;
        return true;
    }

    private void Publish(Action callbacks)
    {
        isPublishingEvents = true;
        try
        {
            callbacks?.Invoke();
        }
        finally
        {
            isPublishingEvents = false;
        }
    }

    private static bool ApproximatelyEqual(float first, float second)
        => Math.Abs(first - second) < 0.0001f;
}
