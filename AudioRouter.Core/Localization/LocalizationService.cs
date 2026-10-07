using System.ComponentModel;
using System.Globalization;
using System.IO;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Settings;

namespace AudioRouter.Core.Localization;

/// <summary>
/// 界面/命令行文案的单一来源（平台无关）。
///
/// 语言包是外置 JSON：内置只读目录 + 用户可写目录（导入落点）。
/// 缺键时回退到 en-US，再回退到键名本身。
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string FallbackCode = "en-US";

    private readonly Dictionary<string, LanguagePack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private LanguagePack? _current;

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    /// <summary>随程序发布的只读语言目录。</summary>
    public static string BuiltInDirectory => Path.Combine(AppContext.BaseDirectory, "Languages");

    /// <summary>用户可写语言目录（跨平台：Windows=%APPDATA%，Linux=~/.config）。</summary>
    public static string UserDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioRouter", "Languages");

    public IReadOnlyList<LanguagePack> AvailableLanguages =>
        _packs.Values.OrderBy(p => p.Name, StringComparer.CurrentCulture).ToList();

    public LanguagePack? Current => _current;

    public string this[string key] => Translate(key);

    public string Translate(string key)
    {
        if (_current is not null && _current.TryGet(key, out var value)) return value;

        if (_packs.TryGetValue(FallbackCode, out var fallback) && fallback.TryGet(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    public string Format(string key, params object?[] args)
        => args.Length == 0 ? Translate(key) : string.Format(Translate(key), args);

    public void Initialize()
    {
        ReloadPacks();
        SelectInitialLanguage();
    }

    public void ReloadPacks()
    {
        _packs.Clear();
        LoadDirectory(BuiltInDirectory);
        LoadDirectory(UserDirectory);
        StartupLog.Write($"language: {_packs.Count} 个语言包（内置 + 用户目录）");
    }

    private void LoadDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;

            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                var pack = LanguagePack.Load(file, out var error);
                if (pack is null)
                {
                    StartupLog.Write($"language: 跳过 {file}（{error}）");
                    continue;
                }

                _packs[pack.Code] = pack;
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write($"language: 扫描 {directory} 失败：{ex.Message}");
        }
    }

    private void SelectInitialLanguage()
    {
        var saved = SettingsStore.Language;
        if (!string.IsNullOrWhiteSpace(saved) && ChangeLanguage(saved!, persist: false)) return;

        var ui = CultureInfo.CurrentUICulture;

        foreach (var code in new[] { ui.Name, ui.TwoLetterISOLanguageName })
        {
            var match = _packs.Keys.FirstOrDefault(k =>
                k.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase));

            if (match is not null && ChangeLanguage(match, persist: false)) return;
        }

        if (ChangeLanguage(FallbackCode, persist: false)) return;

        var first = AvailableLanguages.FirstOrDefault();
        if (first is not null) ChangeLanguage(first.Code, persist: false);
    }

    public bool ChangeLanguage(string code, bool persist = true)
    {
        if (!_packs.TryGetValue(code, out var pack)) return false;

        _current = pack;
        if (persist) SettingsStore.Language = pack.Code;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);

        StartupLog.Write($"language: 切换为 {pack.Code}（{pack.Name}）");
        return true;
    }

    /// <summary>导入外部语言文件：校验 → 复制到用户目录 → 立即生效。</summary>
    public bool Import(string sourcePath, out string code, out string? error)
    {
        code = string.Empty;
        error = null;

        var pack = LanguagePack.Load(sourcePath, out error);
        if (pack is null)
        {
            error ??= "invalid language file";
            return false;
        }

        try
        {
            Directory.CreateDirectory(UserDirectory);
            var target = Path.Combine(UserDirectory, pack.Code + ".json");

            if (!Path.GetFullPath(target).Equals(Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, target, overwrite: true);
            }

            pack.SourcePath = target;
            _packs[pack.Code] = pack;
            code = pack.Code;
            StartupLog.Write($"language: 导入 {pack.Code}（{pack.Name}，{pack.Strings.Count} 条）");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>代码侧取文案的简写入口。</summary>
public static class Loc
{
    public static string T(string key) => LocalizationService.Instance.Translate(key);

    public static string F(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
