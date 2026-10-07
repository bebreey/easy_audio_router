using System.Globalization;
using System.Text.Json;

namespace AudioRouter.Core.Backends.Linux;

/// <summary>pactl 输出的 sink（输出设备）。</summary>
internal sealed record PactlSink(
    int Index,
    string Name,
    string Description,
    string State,
    bool Muted,
    float Volume,
    string? SampleFormat,
    int SampleRate,
    int Channels);

/// <summary>pactl 输出的 sink-input（某个应用的播放流）。</summary>
internal sealed record PactlSinkInput(
    int Index,
    int Sink,
    int Pid,
    string Name,
    string Binary,
    float Volume,
    bool Muted,
    bool Corked);

/// <summary>
/// pactl JSON 解析器。
///
/// 【为什么用 JSON 而不是解析文本】
/// `pactl list sinks` 的人类可读输出**会随语言环境变化**（中文本地化下字段名都不一样），
/// 文本解析等于把程序绑死在某个 locale 上。`pactl -f json`（PulseAudio 15+ / PipeWire 的
/// pulse 兼容层都支持）是稳定的机器接口。
///
/// 该解析器是**纯函数**，因此可以在没有 Linux 的机器上用录制的真实输出做验证 —— 见 AudioRouter.Tests。
/// </summary>
internal static class PactlParser
{
    private const float NormalVolume = 65536f; // pactl 的线性音量满值

    public static IReadOnlyList<PactlSink> ParseSinks(string json, string? defaultSinkName)
    {
        var result = new List<PactlSink>();

        foreach (var element in EnumerateArray(json))
        {
            var name = GetString(element, "name") ?? string.Empty;
            var sampleSpec = element.TryGetProperty("sample_spec", out var spec) &&
                             spec.ValueKind == JsonValueKind.Object
                ? spec
                : default;

            result.Add(new PactlSink(
                Index: GetInt(element, "index") ?? -1,
                Name: name,
                Description: GetString(element, "description") ?? name,
                State: GetString(element, "state") ?? string.Empty,
                Muted: GetBool(element, "mute"),
                Volume: GetVolume(element),
                SampleFormat: sampleSpec.ValueKind == JsonValueKind.Object ? GetString(sampleSpec, "format") : null,
                SampleRate: sampleSpec.ValueKind == JsonValueKind.Object ? GetInt(sampleSpec, "rate") ?? 0 : 0,
                Channels: sampleSpec.ValueKind == JsonValueKind.Object ? GetInt(sampleSpec, "channels") ?? 0 : 0));
        }

        return result;
    }

    public static IReadOnlyList<PactlSinkInput> ParseSinkInputs(string json)
    {
        var result = new List<PactlSinkInput>();

        foreach (var element in EnumerateArray(json))
        {
            if (!element.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var pid = GetPid(properties);
            if (pid <= 0) continue; // 没有 pid 的多为系统声音/内部流

            result.Add(new PactlSinkInput(
                Index: GetInt(element, "index") ?? -1,
                Sink: GetInt(element, "sink") ?? -1,
                Pid: pid,
                Name: GetString(properties, "application.name") ?? string.Empty,
                Binary: GetString(properties, "application.process.binary") ?? string.Empty,
                Volume: GetVolume(element),
                Muted: GetBool(element, "mute"),
                Corked: GetBool(element, "corked")));
        }

        return result;
    }

    /// <summary>取出默认 sink 名（`pactl get-default-sink` 的输出，纯文本）。</summary>
    public static string? ParseDefaultSink(string stdout)
    {
        var text = stdout.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // ======================================================================
    //  防御式 JSON 读取
    //  pactl 在不同版本/后端下字段类型并不稳定
    //  （例如 application.process.id 有时是字符串、有时是数字），
    //  因此这里对每个字段都做宽容处理，拿不到就用默认值而不是抛异常。
    // ======================================================================

    private static IEnumerable<JsonElement> EnumerateArray(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array) yield break;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object) yield return element;
            }
        }
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.TryGetInt32(out var number) ? number : null;
            case JsonValueKind.String:
                return int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null;
            default:
                return null;
        }
    }

    private static bool GetBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int GetPid(JsonElement properties)
    {
        var pid = GetInt(properties, "application.process.id") ?? 0;
        if (pid > 0) return pid;

        // 某些版本只给 application.process.binary，退回按 pid 字段的其他命名
        return GetInt(properties, "application.process.pid") ?? 0;
    }

    /// <summary>把 volume 对象（每声道 {'value': 0..65536}）折算成 0..1 的单值。</summary>
    private static float GetVolume(JsonElement element)
    {
        if (!element.TryGetProperty("volume", out var volume) || volume.ValueKind != JsonValueKind.Object)
        {
            return 1f;
        }

        var sum = 0f;
        var count = 0;

        foreach (var channel in volume.EnumerateObject())
        {
            if (channel.Value.ValueKind != JsonValueKind.Object) continue;
            if (!channel.Value.TryGetProperty("value", out var valueElement)) continue;

            if (valueElement.ValueKind == JsonValueKind.Number &&
                valueElement.TryGetSingle(out var value))
            {
                sum += value;
                count++;
            }
        }

        if (count == 0) return 1f;

        return Math.Clamp(sum / count / NormalVolume, 0f, 1f);
    }
}
