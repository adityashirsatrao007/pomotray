namespace PomoTray.Core;

/// <summary>Which phase the timer is in.</summary>
public enum Phase
{
    Idle,
    Work,
    Break,
}

/// <summary>
/// Durations for a working session. Values are clamped rather than rejected: these
/// arrive from a hand-editable JSON file, so a bad number must not stop the app starting.
/// </summary>
public sealed record EngineSettings
{
    public const int MinMinutes = 1;
    public const int MaxWorkMinutes = 180;
    public const int MaxBreakMinutes = 60;

    public EngineSettings(int workMinutes, int breakMinutes)
    {
        WorkMinutes = Math.Clamp(workMinutes, MinMinutes, MaxWorkMinutes);
        BreakMinutes = Math.Clamp(breakMinutes, MinMinutes, MaxBreakMinutes);
    }

    public int WorkMinutes { get; }

    public int BreakMinutes { get; }

    public static EngineSettings Default => new(25, 5);

    public TimeSpan WorkDuration => TimeSpan.FromMinutes(WorkMinutes);

    public TimeSpan BreakDuration => TimeSpan.FromMinutes(BreakMinutes);
}
