using System.IO;
using System.Text.Json;
using SmoothAnnotate.Models;

namespace SmoothAnnotate.Services;

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmoothAnnotate");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AnnotationSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AnnotationSettings>(json, JsonOptions)
                       ?? new AnnotationSettings();
            }
        }
        catch
        {
            // Corrupt file - fall back to defaults
        }

        return new AnnotationSettings();
    }

    public static void Save(AnnotationSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently fail on write errors
        }
    }
}
