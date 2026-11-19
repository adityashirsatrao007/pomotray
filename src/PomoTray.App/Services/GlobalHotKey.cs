using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PomoTray.App.Services;

/// <summary>
/// System-wide Ctrl+Alt+P registered on a message-only window.
/// <see cref="RegisterHotKey"/> needs an HWND even when there is no visible window,
/// which is why this exists separately from the settings window.
/// </summary>
public sealed class GlobalHotKey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int VirtualKeyP = 0x50;
    private const int HotKeyId = 0xB001;
    private static readonly IntPtr MessageOnlyParent = new(-3); // HWND_MESSAGE

    private readonly Action _onPressed;
    private readonly HwndSource _source;

    public GlobalHotKey(Action onPressed)
    {
        _onPressed = onPressed;

        var parameters = new HwndSourceParameters("PomoTray.HotKeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = MessageOnlyParent,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        if (!RegisterHotKey(_source.Handle, HotKeyId, ModControl | ModAlt | ModNoRepeat, VirtualKeyP))
            throw new InvalidOperationException("Ctrl+Alt+P is already claimed by another application.");
    }

    public void Dispose()
    {
        UnregisterHotKey(_source.Handle, HotKeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            _onPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
