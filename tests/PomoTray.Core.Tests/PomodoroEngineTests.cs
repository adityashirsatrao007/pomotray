using PomoTray.Core;

namespace PomoTray.Core.Tests;

public sealed class PomodoroEngineTests
{
    private static PomodoroEngine NewEngine(int work = 25, int rest = 5)
        => new(new EngineSettings(work, rest));

    [Fact]
    public void StartsIdleWithTheWorkDurationLoaded()
    {
        var engine = NewEngine();

        Assert.Equal(Phase.Idle, engine.Phase);
        Assert.False(engine.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining);
    }

    [Fact]
    public void StartEntersWorkAndCountsDown()
    {
        var engine = NewEngine();
        engine.Start();

        engine.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(Phase.Work, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(90), engine.Remaining);
    }

    [Fact]
    public void PausedEngineIgnoresElapsedTime()
    {
        var engine = NewEngine();
        engine.Start();
        engine.Pause();

        var before = engine.Remaining;
        engine.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(before, engine.Remaining);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void CompletingWorkRecordsASessionAndEntersBreak()
    {
        var engine = NewEngine(work: 1, rest: 5);
        engine.Start();

        var phase = engine.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(Phase.Break, phase);
        Assert.Equal(1, engine.CompletedWorkSessions);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Remaining);
    }

    [Fact]
    public void CompletingBreakReturnsToWork()
    {
        var engine = NewEngine(work: 1, rest: 1);
        engine.Start();
        engine.Advance(TimeSpan.FromMinutes(1));
        engine.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(Phase.Work, engine.Phase);
        Assert.Equal(1, engine.CompletedWorkSessions);
    }

    [Fact]
    public void LeftoverTimeCarriesIntoTheNextPhase()
    {
        var engine = NewEngine(work: 1, rest: 5);
        engine.Start();

        // 70 seconds against a 60 second work phase leaves 10 seconds burnt from the break.
        engine.Advance(TimeSpan.FromSeconds(70));

        Assert.Equal(Phase.Break, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(10), engine.Remaining);
    }

    [Fact]
    public void OneLongAdvanceCanCrossSeveralPhases()
    {
        var engine = NewEngine(work: 1, rest: 1);
        engine.Start();

        // 150s = work(60) + break(60) + 30s into the second work phase.
        engine.Advance(TimeSpan.FromSeconds(150));

        Assert.Equal(Phase.Work, engine.Phase);
        Assert.Equal(1, engine.CompletedWorkSessions);
        Assert.Equal(TimeSpan.FromSeconds(30), engine.Remaining);
    }

    [Fact]
    public void SkipMovesToTheNextPhaseImmediately()
    {
        var engine = NewEngine(work: 25, rest: 5);
        engine.Skip();

        Assert.Equal(Phase.Work, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining);

        engine.Skip();

        Assert.Equal(Phase.Break, engine.Phase);
        Assert.Equal(1, engine.CompletedWorkSessions);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Remaining);
    }

    [Fact]
    public void PhaseCompletedFiresOncePerTransition()
    {
        var engine = NewEngine(work: 1, rest: 1);
        var transitions = new List<PhaseCompletedEventArgs>();
        engine.PhaseCompleted += (_, e) => transitions.Add(e);

        engine.Start();
        engine.Advance(TimeSpan.FromMinutes(1));
        engine.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(2, transitions.Count);
        Assert.Equal(Phase.Work, transitions[0].Completed);
        Assert.Equal(Phase.Break, transitions[0].Next);
        Assert.Equal(Phase.Break, transitions[1].Completed);
        Assert.Equal(Phase.Work, transitions[1].Next);
    }

    [Fact]
    public void ToggleAlternatesBetweenRunningAndPaused()
    {
        var engine = NewEngine();

        engine.Toggle();
        Assert.True(engine.IsRunning);

        engine.Toggle();
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void NegativeElapsedTimeIsRejected()
    {
        var engine = NewEngine();
        engine.Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void ResetReturnsToIdleAndClearsTheSessionCount()
    {
        var engine = NewEngine(work: 1, rest: 1);
        engine.Start();
        engine.Advance(TimeSpan.FromMinutes(1));

        engine.Reset();

        Assert.Equal(Phase.Idle, engine.Phase);
        Assert.Equal(0, engine.CompletedWorkSessions);
        Assert.False(engine.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(1), engine.Remaining);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 900, 1, 60)]
    [InlineData(500, 10, 180, 10)]
    public void SettingsAreClampedToUsableRanges(int work, int rest, int expectedWork, int expectedBreak)
    {
        var settings = new EngineSettings(work, rest);

        Assert.Equal(expectedWork, settings.WorkMinutes);
        Assert.Equal(expectedBreak, settings.BreakMinutes);
    }

    [Fact]
    public void RemainingTimeIsFormattedAsMinutesAndSeconds()
    {
        var engine = NewEngine(work: 25);
        Assert.Equal("25:00", engine.FormatRemaining());

        engine.Start();
        engine.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal("24:59", engine.FormatRemaining());
    }

    [Fact]
    public void RemainingTimeGrowsAnHourFieldPastSixtyMinutes()
    {
        var engine = NewEngine(work: 120);

        Assert.Equal("2:00:00", engine.FormatRemaining());
    }
}
