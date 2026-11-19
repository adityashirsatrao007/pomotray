namespace PomoTray.Core;

public sealed class PhaseCompletedEventArgs(Phase completed, Phase next) : EventArgs
{
    public Phase Completed { get; } = completed;

    public Phase Next { get; } = next;
}

/// <summary>
/// A two-phase pomodoro timer with no clock of its own. Callers drive it with
/// <see cref="Advance"/>, which is what makes the transitions testable without waiting.
/// </summary>
public sealed class PomodoroEngine
{
    private EngineSettings _settings;

    public PomodoroEngine(EngineSettings? settings = null)
    {
        _settings = settings ?? EngineSettings.Default;
        Remaining = _settings.WorkDuration;
    }

    public event EventHandler<PhaseCompletedEventArgs>? PhaseCompleted;

    public Phase Phase { get; private set; } = Phase.Idle;

    public TimeSpan Remaining { get; private set; } = TimeSpan.Zero;

    public bool IsRunning { get; private set; }

    public int CompletedWorkSessions { get; private set; }

    public EngineSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            // A running phase keeps its remaining time; the new duration applies from the next phase.
            if (Phase == Phase.Idle)
                Remaining = value.WorkDuration;
        }
    }

    public TimeSpan CurrentPhaseDuration =>
        Phase == Phase.Break ? _settings.BreakDuration : _settings.WorkDuration;

    public void Start()
    {
        if (Phase == Phase.Idle)
        {
            Phase = Phase.Work;
            Remaining = _settings.WorkDuration;
        }

        IsRunning = true;
    }

    public void Pause() => IsRunning = false;

    public void Toggle()
    {
        if (IsRunning)
            Pause();
        else
            Start();
    }

    /// <summary>Ends the current phase immediately. Starting from idle begins a work session.</summary>
    public void Skip()
    {
        if (Phase == Phase.Idle)
        {
            Start();
            return;
        }

        MoveToNext();
    }

    /// <summary>
    /// Consumes elapsed time and returns the resulting phase. Leftover time carries into
    /// the next phase, so a blocked UI thread does not silently lose seconds.
    /// </summary>
    public Phase Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time cannot be negative.");

        if (!IsRunning || Phase == Phase.Idle)
            return Phase;

        var remaining = Remaining - elapsed;

        // Loop, not a single check: a long elapsed span can span more than one phase.
        while (remaining <= TimeSpan.Zero)
        {
            var overflow = -remaining;
            MoveToNext();
            remaining = CurrentPhaseDuration - overflow;
        }

        Remaining = remaining;
        return Phase;
    }

    public void Reset()
    {
        Phase = Phase.Idle;
        Remaining = _settings.WorkDuration;
        IsRunning = false;
        CompletedWorkSessions = 0;
    }

    /// <summary>Human-readable countdown for the tray tooltip: mm:ss, or h:mm:ss past an hour.</summary>
    public string FormatRemaining()
    {
        var total = Remaining < TimeSpan.Zero ? TimeSpan.Zero : Remaining;
        return total.TotalHours >= 1
            ? $"{(int)total.TotalHours}:{total.Minutes:00}:{total.Seconds:00}"
            : $"{total.Minutes:00}:{total.Seconds:00}";
    }

    private void MoveToNext()
    {
        var completed = Phase;

        if (completed == Phase.Work)
        {
            CompletedWorkSessions++;
            Phase = Phase.Break;
        }
        else
        {
            Phase = Phase.Work;
        }

        Remaining = CurrentPhaseDuration;
        PhaseCompleted?.Invoke(this, new PhaseCompletedEventArgs(completed, Phase));
    }
}
