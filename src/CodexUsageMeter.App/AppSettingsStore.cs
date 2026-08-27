using System.IO;
using System.Text.Json;

namespace CodexUsageMeter.App;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public AppSettingsStore()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsageMeter",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.SchemaVersion), out _))
            {
                // 0.6.x persisted noisy reset notifications and an invisible usage bar by
                // default. Apply the corrected defaults once while retaining all other choices.
                settings.NotifyOnPercentChange = false;
                settings.NotifyAt50Percent = false;
                settings.NotifyAt75Percent = false;
                settings.NotifyAt90Percent = false;
                settings.NotifyOnReset = false;
                settings.UsageBarEnabled = true;
                settings.UsageBarThickness = Math.Max(3, settings.UsageBarThickness);
                settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
            }
            return settings;
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch (IOException)
        {
            // A settings failure must not stop the tray application.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the in-memory choice for the current session.
        }
    }
}
