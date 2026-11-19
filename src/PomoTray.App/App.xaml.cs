using System.Windows;
using System.Windows.Threading;
using PomoTray.App.Services;
using Application = System.Windows.Application;

namespace PomoTray.App;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\PomoTray.SingleInstance";
    private const string ShowSignalName = @"Local\PomoTray.ShowWindow";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private TrayController? _trayController;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                ErrorLog.Write(exception, "app domain");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLog.Write(args.Exception, "unobserved task");
            args.SetObserved();
        };

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);

        if (!createdNew)
        {
            // A second copy asks the first to surface its settings window, then leaves.
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var running))
            {
                running.Set();
                running.Dispose();
            }

            Shutdown();
            return;
        }

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        StartShowSignalListener();

        _trayController = new TrayController();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayController?.Dispose();
        _showSignal?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void StartShowSignalListener()
    {
        var listener = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    _showSignal!.WaitOne();
                    Dispatcher.Invoke(() => _trayController?.ShowSettings());
                }
                catch (Exception ex) when (ex is ObjectDisposedException or TaskCanceledException)
                {
                    return; // Shutting down.
                }
            }
        })
        {
            IsBackground = true,
            Name = "PomoTray show-signal listener",
        };

        listener.Start();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Write(e.Exception);

        System.Windows.MessageBox.Show(
            $"PomoTray hit an unexpected error and kept running.\n\n{e.Exception.Message}\n\nDetails: {ErrorLog.LogPath}",
            "PomoTray",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);

        e.Handled = true;
    }
}
