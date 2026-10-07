namespace AudioRouter.Tests;

/// <summary>
/// 录制的真实样本数据。
///
/// 为什么要把样本写进代码：Linux 后端在当前机器上跑不起来，
/// 但解析逻辑是纯函数 —— 用真实形态的 `pactl -f json` 输出做验证，
/// 比"看起来解析对了"可靠得多。
/// </summary>
internal static class SampleData
{
    /// <summary>`pactl -f json list sinks` 的真实形态（2 个 sink，第二个体积不同声道、且静音）。</summary>
    public const string Sinks = """
    [
      {
        "index": 0,
        "state": "RUNNING",
        "name": "alsa_output.pci-0000_00_1f.3.analog-stereo",
        "description": "Built-in Audio Analog Stereo",
        "driver": "PipeWire",
        "owner_module": 6,
        "mute": false,
        "sample_spec": { "format": "s16le", "rate": 48000, "channels": 2 },
        "volume": {
          "front-left": { "value": 65536, "value_percent": "100%", "db": "0.00 dB" },
          "front-right": { "value": 65536, "value_percent": "100%", "db": "0.00 dB" }
        },
        "monitor_source": "alsa_output.pci-0000_00_1f.3.analog-stereo.monitor"
      },
      {
        "index": 5,
        "state": "SUSPENDED",
        "name": "bluez_output.AC_80_0A_1B_2C_3D.1",
        "description": "WH-1000XM5",
        "driver": "PipeWire",
        "owner_module": 23,
        "mute": true,
        "sample_spec": { "format": "float32le", "rate": 48000, "channels": 2 },
        "volume": {
          "front-left": { "value": 32768, "value_percent": "50%", "db": "-6.02 dB" },
          "front-right": { "value": 65536, "value_percent": "100%", "db": "0.00 dB" }
        }
      }
    ]
    """;

    /// <summary>
    /// `pactl -f json list sink-inputs` 的真实形态。
    /// 刻意覆盖两个易错点：`application.process.id` 有时是字符串有时是数字；
    /// 以及没有 pid 的系统流必须被跳过。
    /// </summary>
    public const string SinkInputs = """
    [
      {
        "index": 42,
        "driver": "PipeWire",
        "owner_module": 23,
        "client": 15,
        "sink": 0,
        "mute": false,
        "corked": false,
        "volume": {
          "front-left": { "value": 65536, "value_percent": "100%" },
          "front-right": { "value": 65536, "value_percent": "100%" }
        },
        "properties": {
          "application.name": "Firefox",
          "application.process.binary": "firefox",
          "application.process.id": "12345",
          "media.name": "YouTube"
        }
      },
      {
        "index": 43,
        "driver": "PipeWire",
        "sink": 5,
        "mute": true,
        "volume": {
          "front-left": { "value": 32768, "value_percent": "50%" },
          "front-right": { "value": 32768, "value_percent": "50%" }
        },
        "properties": {
          "application.name": "Spotify",
          "application.process.binary": "spotify",
          "application.process.id": 6789
        }
      },
      {
        "index": 44,
        "sink": 0,
        "mute": false,
        "volume": { "front-left": { "value": 65536 } },
        "properties": {
          "application.name": "System Sounds"
        }
      }
    ]
    """;

    /// <summary>一个合法的语言包（2 条键）。</summary>
    public const string ValidLanguage = """
    {
      "code": "xx-XX",
      "name": "Test Language",
      "version": 1,
      "strings": {
        "section.apps": "Applications",
        "section.devices": "Devices"
      }
    }
    """;

    /// <summary>
    /// `{"code":"xx","strings":{"a":"<0xC3 0x28>"}}` —— 字符串里含非法 UTF-8 字节序列，
    /// 用来模拟「用记事本存成 ANSI」的语言文件。
    /// （不用 Encoding.GetEncoding(936)：.NET Core 默认不带 GBK 代码页。）
    /// </summary>
    public static readonly byte[] InvalidUtf8LanguageFile =
    {
        0x7B, 0x22, 0x63, 0x6F, 0x64, 0x65, 0x22, 0x3A, 0x22, 0x78, 0x78, 0x22, 0x2C,
        0x22, 0x73, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x73, 0x22, 0x3A, 0x7B, 0x22, 0x61, 0x22,
        0x3A, 0x22, 0xC3, 0x28, 0x22, 0x7D, 0x7D,
    };
}
