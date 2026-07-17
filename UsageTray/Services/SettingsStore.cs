using System.Text.Json;

namespace UsageTray.Services;

internal sealed class AppSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ProtectedApiKey { get; set; } = string.Empty;
    public int RefreshMinutes { get; set; } = 5;
    public bool StartWithWindows { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) &&
                                !string.IsNullOrWhiteSpace(ProtectedApiKey);

    public string GetApiKey() => string.IsNullOrWhiteSpace(ProtectedApiKey)
        ? string.Empty
        : SecretProtector.Unprotect(ProtectedApiKey);

    public void SetApiKey(string apiKey) => ProtectedApiKey = SecretProtector.Protect(apiKey.Trim());
}

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UsageTray",
        "settings.json");

    public AppSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.RefreshMinutes = Math.Clamp(settings.RefreshMinutes, 1, 1440);
            return settings;
        }
        catch (Exception ex)
        {
            warning = $"无法读取配置：{ex.Message}";
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }
}
