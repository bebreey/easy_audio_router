using System.IO;
using System.Text.Json;

namespace AudioRouter.Core.Settings;

/// <summary>应用设置（目前只有语言，后续可扩）。</summary>
public sealed class AppSettings
{
    public string? Language { get; set; }
}

/// <summary>
/// 极简设置存储（跨平台：Windows=%APPDATA%\AudioRouter，Linux=~/.config/AudioRouter）。
/// </summary>
public static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioRouter", "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static AppSettings _cache = Load();

    public static string? Language
    {
        get => _cache.Language;
        set
        {
            if (string.Equals(_cache.Language, value, StringComparison.OrdinalIgnoreCase)) return;
            _cache.Language = value;
            Save();
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache, Options));
        }
        catch
        {
            // 设置写入失败不能影响主流程
        }
    }
}
