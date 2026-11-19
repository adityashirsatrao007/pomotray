using System.IO;
using System.Text;

namespace PomoTray.App.Services;

/// <summary>
/// Appends unexpected failures to %AppData%\PomoTray\crash.log. Without this a crashing
/// tray app disappears with no evidence, which is impossible to support remotely.
/// </summary>
public static class ErrorLog
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomoTray");

    public static string LogPath => Path.Combine(Folder, "crash.log");

    public static void Write(Exception exception, string context = "unhandled")
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var entry = new StringBuilder()
                .AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {context}")
                .AppendLine($"{exception.GetType().FullName}: {exception.Message}")
                .AppendLine(exception.StackTrace)
                .AppendLine()
                .ToString();

            File.AppendAllText(LogPath, entry);
        }
        catch (Exception)
        {
            // Logging must never be the reason the app dies.
        }
    }
}
