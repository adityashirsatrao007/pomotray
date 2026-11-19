# PomoTray

A pomodoro timer that lives in the Windows system tray. Start or pause it from anywhere with
**Ctrl+Alt+P**, and it tells you when to stop working.

[![build](https://github.com/adityashirsatrao007/pomotray/actions/workflows/build.yml/badge.svg)](https://github.com/adityashirsatrao007/pomotray/actions/workflows/build.yml)

WPF and WinForms on .NET 8. The timer logic is a separate library with no UI or clock dependency,
so every transition is tested in milliseconds instead of real minutes.

## What it does

- Tray icon showing the countdown and current phase; tooltip updates every second
- **Ctrl+Alt+P** starts or pauses from any application
- Skip phase from the tray menu
- Notification when work ends and when a break ends
- Work/break lengths set in a small settings window, persisted to `%AppData%\PomoTray\settings.json`
- Optional **Start with Windows**
- Closing the settings window hides it; the timer keeps running
- Launching a second copy just focuses the running one

## The Windows integration

This app is mostly a tour of the desktop APIs that a "Windows application developer" is expected
to know, so each one is called out here:

| Capability | How | Why it is interesting |
| --- | --- | --- |
| Tray icon and menu | `System.Windows.Forms.NotifyIcon` + `ContextMenuStrip` | WPF has no tray control; hosting the WinForms one is the zero-dependency option |
| Global hotkey | `RegisterHotKey` via P/Invoke on a **message-only window** (`HwndSource`, `HWND_MESSAGE`) | `RegisterHotKey` needs an HWND even with no visible window. `MOD_NOREPEAT` prevents key-repeat floods |
| Single instance | Named `Mutex`, plus a named `EventWaitHandle` to signal the running copy | A mutex alone can only refuse to start; the event lets the second launch hand off to the first |
| Start with Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Per-user, so no elevation prompt and trivial to remove |
| Settings | `System.Text.Json` in `%AppData%`, written to a temp file and swapped in | A corrupt or hand-edited file falls back to defaults instead of blocking startup, and a crash mid-write cannot truncate it |
| Crash evidence | `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` → `crash.log` | A tray app that dies silently is unsupportable |

## Layout

```
src/
  PomoTray.Core/          timer state machine — net8.0, no clock, no UI
    PomodoroEngine.cs     Start/Pause/Skip/Advance, phase transitions, session count
    EngineSettings.cs     durations, clamped because they come from a JSON file
  PomoTray.App/           Windows host — net8.0-windows
    TrayController.cs     tray icon, menu, one-second tick
    Services/             global hotkey, autostart, settings store, crash log
    MainWindow.xaml       settings window
tests/
  PomoTray.Core.Tests/    17 xUnit tests
```

### Why the engine takes elapsed time instead of reading a clock

`Advance(TimeSpan)` means a test can simulate 150 seconds in a single call and assert that the
timer crossed two phase boundaries and landed 30 seconds into the next work phase. A `Timer`-driven
design would need a fake clock and still be slower to test.

That design also fixes a real bug: if the UI thread is blocked for two seconds, the tick passes the
whole interval rather than dropping it, so the countdown never drifts.

### Why this app is procedural, not MVVM

DupFind uses MVVM because it has a data grid, selection state and commands. PomoTray is a tray icon
and five menu items: an MVVM layer here would be ceremony around glue code. The part with actual
decisions — the phase machine — is isolated and tested instead. Same judgement, different size.

## Build and run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build
dotnet run --project src/PomoTray.App
```

The app starts in the tray with no window. Double-click the tray icon (or right-click → Settings)
to change the durations.

```bash
dotnet test
```

CI builds, tests and publishes a `win-x64` artifact on every push.

## Tests

| Test | What it guards |
| --- | --- |
| `StartsIdleWithTheWorkDurationLoaded` | Tooltip shows 25:00 before the first start, not 00:00 |
| `CompletingWorkRecordsASessionAndEntersBreak` | Work → break transition and session counting |
| `CompletingBreakReturnsToWork` | Break → work transition |
| `LeftoverTimeCarriesIntoTheNextPhase` | 70s into a 60s phase leaves 50s of break, not 60s |
| `OneLongAdvanceCanCrossSeveralPhases` | A blocked UI thread does not desync the timer |
| `PausedEngineIgnoresElapsedTime` | Pause actually stops the countdown |
| `SkipMovesToTheNextPhaseImmediately` | Manual skip behaves like a natural transition |
| `PhaseCompletedFiresOncePerTransition` | No duplicate notifications |
| `SettingsAreClampedToUsableRanges` | Bad JSON cannot stop the app starting |
| `ResetReturnsToIdleAndClearsTheSessionCount` | Reset clears state completely |

## What I'd do next

- Replace balloon tips with `AppNotificationManager` (Windows App SDK) for actionable toasts
- Configurable hotkey, stored in settings
- Long break after four sessions, which is the part of the technique this app omits
- MSIX packaging and code signing, so installs don't trigger a SmartScreen warning
- Pause automatically when the machine is locked or idle

## License

MIT — see [LICENSE](LICENSE).
