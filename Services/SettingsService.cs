using System;
using System.IO;
using System.Text.Json;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Services;

public sealed class SettingsService
{
    private readonly UiLogger _logger;
    private AppSettings _settings;

    public SettingsService(UiLogger logger)
    {
        _logger = logger;
        _settings = Load();
    }

    public string ApiToken
    {
        get => _settings.ApiToken;
        set
        {
            _settings.ApiToken = value;
            Save();
        }
    }

    public int BackfillDays
    {
        get => _settings.BackfillDays;
        set
        {
            _settings.BackfillDays = value;
            Save();
        }
    }

    public bool AutoSyncEnabled
    {
        get => _settings.AutoSyncEnabled;
        set
        {
            _settings.AutoSyncEnabled = value;
            Save();
        }
    }

    public string TimeZoneId
    {
        get => _settings.TimeZoneId;
        set
        {
            _settings.TimeZoneId = value;
            Save();
        }
    }

    public string DatabasePath => Path.Combine(AppDataPath, "stepik-analytics.db");

    private string SettingsPath => Path.Combine(AppDataPath, "settings.json");

    private static string AppDataPath
    {
        get
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(basePath, "StepikAnalyticsDesktop");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return AppSettings.Default;
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load settings. {ex.Message}");
            return AppSettings.Default;
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to save settings. {ex.Message}");
        }
    }
}

public sealed record AppSettings
{
    public string ApiToken { get; set; } = string.Empty;
    public int BackfillDays { get; set; } = 365;
    public bool AutoSyncEnabled { get; set; } = true;
    public string TimeZoneId { get; set; } = "Europe/Riga";

    public static AppSettings Default => new();
}
