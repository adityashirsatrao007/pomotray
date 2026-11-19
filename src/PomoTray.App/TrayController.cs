using System.Drawing;
using System.Windows.Threading;
using PomoTray.App.Services;
using PomoTray.Core;
using WinForms = System.Windows.Forms;

namespace PomoTray.App;

/// <summary>
/// Owns the tray icon, the menu and the one-second tick. Deliberately procedural:
/// a tray app is glue code, and the decisions worth testing live in PomoTray.Core.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly SettingsStore _store = new();
    private readonly PomodoroEngine _engine;
    private readonly WinForms.NotifyIcon _trayIcon;
    private readonly WinForms.ToolStripMenuItem _toggleItem;
    private readonly WinForms.ToolStripMenuItem _autoStartItem;
    private readonly DispatcherTimer _timer;
    private GlobalHotKey? _hotKey;
    private MainWindow? _window;
    private bool _disposed;

    public TrayController()
    {
        var saved = _store.Load();
        _engine = new PomodoroEngine(new EngineSettings(saved.WorkMinutes, saved.BreakMinutes));
        _engine.PhaseCompleted += OnPhaseCompleted;

        _toggleItem = new WinForms.ToolStripMenuItem("Start", null, (_, _) => ToggleTimer());
        _autoStartItem = new WinForms.ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutoStart())
        {
            Checked = SafeIsAutoStartEnabled(),
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new WinForms.ToolStripMenuItem("Skip phase", null, (_, _) =>
        {
            _engine.Skip();
            Refresh();
        }));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings()));
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Exit", null, (_, _) => Exit()));

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "PomoTray",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        try
        {
            _hotKey = new GlobalHotKey(ToggleTimer);
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            // Another app owns Ctrl+Alt+P. The tray menu still works, so keep running.
            ErrorLog.Write(ex, "global hotkey");
        }

        Refresh();
    }

    public void ShowSettings()
    {
        _window ??= new MainWindow(_engine, _store, Refresh);

        _window.Show();
        if (_window.WindowState == System.Windows.WindowState.Minimized)
            _window.WindowState = System.Windows.WindowState.Normal;

        _window.Activate();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _timer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        _hotKey?.Dispose();
        _hotKey = null;

        if (_window is not null)
        {
            _window.AllowClose = true;
            _window.Close();
            _window = null;
        }
    }

    private void Tick()
    {
        if (!_engine.IsRunning)
            return;

        _engine.Advance(TimeSpan.FromSeconds(1));
        Refresh();
    }

    private void Refresh()
    {
        var phase = _engine.Phase switch
        {
            Phase.Work => "work",
            Phase.Break => "break",
            _ => "ready",
        };

        _trayIcon.Text = Truncate($"PomoTray — {_engine.FormatRemaining()} · {phase}", 63);
        _toggleItem.Text = _engine.IsRunning ? "Pause" : "Start";
    }

    private void ToggleTimer()
    {
        _engine.Toggle();
        Refresh();
    }

    private void OnPhaseCompleted(object? sender, PhaseCompletedEventArgs e)
    {
        _trayIcon.BalloonTipTitle = e.Completed == Phase.Work ? "Work session complete" : "Break over";
        _trayIcon.BalloonTipText = e.Completed == Phase.Work
            ? $"Take {_engine.Settings.BreakMinutes} minutes. {_engine.CompletedWorkSessions} session(s) done."
            : "Back to focus.";
        _trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(5_000);

        Refresh();
    }

    private void ToggleAutoStart()
    {
        try
        {
            var enable = !AutoStart.IsEnabled();
            AutoStart.SetEnabled(enable);
            _autoStartItem.Checked = enable;
        }
        catch (Exception ex)
        {
            _autoStartItem.Checked = SafeIsAutoStartEnabled();
            _trayIcon.BalloonTipTitle = "Could not change startup";
            _trayIcon.BalloonTipText = ex.Message;
            _trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Warning;
            _trayIcon.ShowBalloonTip(4_000);
        }
    }

    private static bool SafeIsAutoStartEnabled()
    {
        try
        {
            return AutoStart.IsEnabled();
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "autostart query");
            return false;
        }
    }

    private void Exit()
    {
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/assets/app.ico", UriKind.Absolute));

        return new Icon(resource.Stream, 16, 16);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
