using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using SmoothZoom.Models;

namespace SmoothZoom.Services;

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmoothZoom");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                // Reset to defaults if settings are from an older version
                if (settings.Version < 2)
                    return new AppSettings();
                // v3: the ring no longer turns on by itself when OBS records. Once only, and
                // written back, so setting it to true again by hand sticks.
                if (settings.Version < 3)
                {
                    settings.AutoRingWhileRecording = false;
                    settings.Version = 3;
                    try { File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, JsonOptions)); }
                    catch { /* applies this run anyway; written on the next start */ }
                }
                return settings;
            }
        }
        catch
        {
            // Corrupt file — fall back to defaults
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFile, json);

        UpdateStartWithWindows(settings.StartWithWindows);
    }

    private static void UpdateStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue("SmoothZoom", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("SmoothZoom", false);
            }
        }
        catch
        {
            // Registry access failed — silently ignore
        }
    }
}
