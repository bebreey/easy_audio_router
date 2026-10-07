namespace AudioRouter.Core.Formatting;

/// <summary>
/// 音频格式参数 → 人看得懂的一行（纯函数，因此可以在任何平台上被测试）。
///
/// 设计文档承诺设备卡要显示格式信息（如「24 bit · 48 kHz · 2ch」），这里集中处理，
/// 避免 GUI 与 CLI 各自拼一套、日后不一致。
/// </summary>
public static class AudioFormat
{
    public static string Describe(int bits, int sampleRate, int channels, bool isFloat = false)    {
        var parts = new List<string>(3);

        if (bits > 0) parts.Add($"{bits} bit{(isFloat ? " float" : string.Empty)}");
        if (sampleRate > 0) parts.Add(DescribeSampleRate(sampleRate));
        if (channels > 0) parts.Add($"{channels}ch");

        return string.Join(" · ", parts);
    }

    /// <summary>48000 → "48 kHz"；44100 → "44.1 kHz"。</summary>
    public static string DescribeSampleRate(int hz)
    {
        if (hz <= 0) return string.Empty;
        if (hz % 1000 == 0) return $"{hz / 1000} kHz";
        return $"{hz / 1000.0:0.#} kHz";
    }

    /// <summary>
    /// 把 PulseAudio/PipeWire 的 sample format 名（如 s16le、s24_3le、float32le）折算成位深。
    /// 认不出来返回 0（调用方就不显示），而不是猜一个数字。
    /// </summary>
    public static int BitsFromSampleFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return 0;

        var text = format.Trim().ToLowerInvariant();

        return text switch
        {
            "u8" => 8,
            "s16le" or "s16be" => 16,
            "s24le" or "s24be" or "s24_3le" or "s24_3be" => 24,
            "s24_32le" or "s24_32be" => 24,
            "s32le" or "s32be" => 32,
            "float32le" or "float32be" or "f32le" => 32,
            "float64le" or "float64be" => 64,
            _ => 0,
        };
    }

    public static bool IsFloatSampleFormat(string? format)
        => !string.IsNullOrWhiteSpace(format) &&
           format.Contains("float", StringComparison.OrdinalIgnoreCase);
}
