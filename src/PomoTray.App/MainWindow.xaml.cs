using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using PomoTray.App.Services;
using PomoTray.Core;

namespace PomoTray.App;

public partial class MainWindow : Window
{
    private readonly PomodoroEngine _engine;
    private readonly SettingsStore _store;
    private readonly Action _onSaved;

    public MainWindow(PomodoroEngine engine, SettingsStore store, Action onSaved)
    {
        InitializeComponent();

        _engine = engine;
        _store = store;
        _onSaved = onSaved;

        WorkMinutesBox.Text = engine.Settings.WorkMinutes.ToString(CultureInfo.InvariantCulture);
        BreakMinutesBox.Text = engine.Settings.BreakMinutes.ToString(CultureInfo.InvariantCulture);
        UpdateStatus();
    }

    /// <summary>Set by the tray before shutdown; otherwise closing only hides the window.</summary>
    public bool AllowClose { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WorkMinutesBox.Text, out var workMinutes)
            || !int.TryParse(BreakMinutesBox.Text, out var breakMinutes))
        {
            MessageText.Text = "Enter whole minutes for both fields.";
            return;
        }

        var settings = new EngineSettings(workMinutes, breakMinutes);
        _engine.Settings = settings;

        try
        {
            _store.Save(new AppSettings
            {
                WorkMinutes = settings.WorkMinutes,
                BreakMinutes = settings.BreakMinutes,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageText.Text = $"Could not save settings: {ex.Message}";
            return;
        }

        // Values are clamped, so tell the user what was actually stored.
        WorkMinutesBox.Text = settings.WorkMinutes.ToString(CultureInfo.InvariantCulture);
        BreakMinutesBox.Text = settings.BreakMinutes.ToString(CultureInfo.InvariantCulture);
        MessageText.Text = string.Empty;
        UpdateStatus();
        _onSaved();
    }

    private void OnHide(object sender, RoutedEventArgs e) => Hide();

    private void UpdateStatus()
    {
        StatusText.Text = $"Settings file: {SettingsStore.FilePath}\n" +
                          $"Work sessions completed this run: {_engine.CompletedWorkSessions}";
    }
}
