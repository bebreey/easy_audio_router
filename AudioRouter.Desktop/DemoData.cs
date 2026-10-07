using AudioRouter.Core.Models;

namespace AudioRouter.Desktop;

/// <summary>
/// 平台不可用时的演示数据。一旦启用，界面必须显式标注「演示数据」
/// —— 任何情况下都不允许伪装成真实枚举结果。
/// </summary>
internal static class DemoData
{
    public static List<AppSession> Sessions() => new()
    {
        new() { Pid = 9124, ExePath = @"C:\Demo\Spotify.exe", ProcessName = "Spotify", DisplayName = "Spotify", Volume = 0.62f, IsPlaying = true },
        new() { Pid = 3322, ExePath = @"C:\Demo\vlc.exe", ProcessName = "vlc", DisplayName = "VLC media player", Volume = 0.35f, IsMuted = true, IsPlaying = true },
        new() { Pid = 12044, ExePath = @"C:\Demo\chrome.exe", ProcessName = "chrome", DisplayName = "Google Chrome", Volume = 1f },
        new() { Pid = 7740, ExePath = @"C:\Demo\Discord.exe", ProcessName = "Discord", DisplayName = "Discord", Volume = 0.8f, IsPlaying = true },
    };

    public static List<AudioDevice> Devices() => new()
    {
        new() { Id = "demo-speakers", FriendlyName = "扬声器 (Realtek(R) Audio)", Kind = DeviceKind.Speakers, IsActive = true, IsEnabled = true, IsDefault = true, FormatText = "24 bit · 48 kHz · 2ch" },
        new() { Id = "demo-bt", FriendlyName = "耳机 (WH-1000XM5 蓝牙)", Kind = DeviceKind.Bluetooth, IsActive = true, IsEnabled = true, FormatText = "16 bit · 44.1 kHz · 2ch" },
        new() { Id = "demo-hdmi", FriendlyName = "HDMI (显示器音频)", Kind = DeviceKind.Hdmi, IsActive = true, IsEnabled = true, FormatText = "24 bit · 48 kHz · 2ch" },
        new() { Id = "demo-virtual", FriendlyName = "CABLE Input (VB-Audio Virtual Cable)", Kind = DeviceKind.Virtual, IsActive = true, IsEnabled = true, FormatText = "32 bit float · 48 kHz · 2ch" },
        new() { Id = "demo-spdif", FriendlyName = "数字输出 (S/PDIF)", Kind = DeviceKind.Spdif, IsActive = false, IsEnabled = false },
    };
}
