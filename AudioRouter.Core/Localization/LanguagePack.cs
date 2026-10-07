using System.IO;
using System.Text;
using System.Text.Json;

namespace AudioRouter.Core.Localization;

/// <summary>
/// 一个语言包，对应 Languages 目录下的一个 .json 文件。
/// 格式：{ "code": "en-US", "name": "English", "strings": { "key": "value" } }
/// </summary>
public sealed class LanguagePack
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public string? Author { get; init; }

    public IReadOnlyDictionary<string, string> Strings { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public string? SourcePath { get; set; }

    public bool TryGet(string key, out string value) => Strings.TryGetValue(key, out value!);

    public static LanguagePack? Parse(string json, string? sourcePath, out string? error)
    {
        error = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "root must be a JSON object";
                return null;
            }

            var code = root.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(code))
            {
                error = "missing \"code\"";
                return null;
            }

            if (!root.TryGetProperty("strings", out var stringsElement) ||
                stringsElement.ValueKind != JsonValueKind.Object)
            {
                error = "missing \"strings\" object";
                return null;
            }

            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in stringsElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    strings[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            if (strings.Count == 0)
            {
                error = "\"strings\" is empty";
                return null;
            }

            var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

            return new LanguagePack
            {
                Code = code!.Trim(),
                Name = string.IsNullOrWhiteSpace(name) ? code!.Trim() : name!.Trim(),
                Version = root.TryGetProperty("version", out var versionElement) &&
                          versionElement.TryGetInt32(out var version)
                    ? version
                    : 1,
                Author = root.TryGetProperty("author", out var authorElement)
                    ? authorElement.GetString()
                    : null,
                Strings = strings,
                SourcePath = sourcePath,
            };
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return null;
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static LanguagePack? Load(string path, out string? error)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            string text;

            try
            {
                // 严格 UTF-8 解码：拦住「记事本存成 ANSI」这类会导致整包乱码的导入
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                error = "file is not valid UTF-8 (save the language file as UTF-8)";
                return null;
            }

            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];

            return Parse(text, path, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
