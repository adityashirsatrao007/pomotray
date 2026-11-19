using System.IO;
using System.Text.Json;

namespace PomoTray.App.Services;

public sealed class AppSettings
{
    public int WorkMinutes { get; set; } = 25;

    public int BreakMinutes { get; set; } = 5;
}

/// <summary>
/// Settings live in %AppData%\PomoTray\settings.json so they survive upgrades and are
/// hand-editable. A corrupt file falls back to defaults instead of blocking startup.
/// </summary>
public sealed class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomoTray");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(Folder, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex, "settings load");
            return new AppSettings();
        }
    }

    /// <summary>Throws on failure so the settings dialog can tell the user nothing was saved.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}
