using AudioRouter.Core.Models;

namespace AudioRouter.Gui.Services;

/// <summary>
/// WASAPI 不可用时的演示数据。
/// 重要：一旦启用，界面必须显式标注「演示数据」，绝不允许伪装成真实数据。
/// </summary>
internal static class MockAudioService
{
    public static List<AppSession> Sessions()
    {
        return new List<AppSession>
        {
            Make("Spotify.exe", "Spotify", 9124, 0.62f, false, true),
            Make("vlc.exe", "VLC media player", 3322, 0.35f, true, true),
            Make("chrome.exe", "Google Chrome", 12044, 1.00f, false, false),
            Make("Discord.exe", "Discord", 7740, 0.80f, false, true),
        };
    }

    private static AppSession Make(string processName, string displayName, int pid,
                                   float volume, bool muted, bool playing)
    {
        return new AppSession
        {
            Pid = pid,
            ExePath = string.Empty,
            ProcessName = processName,
            DisplayName = displayName,
            Icon = null,
            AvatarBrush = AvatarPalette.BrushFor(displayName),
            Volume = volume,
            IsMuted = muted,
            IsPlaying = playing,
        };
    }

    public static List<AudioDevice> Devices()
    {
        return new List<AudioDevice>
        {
            new()
            {
                Id = "demo-speakers",
                FriendlyName = "扬声器 (Realtek(R) Audio)",
                Kind = DeviceKind.Speakers,
                IsActive = true,
                IsEnabled = true,
                IsDefault = true,
                FormatText = "24 bit · 48 kHz · 2ch",
            },
            new()
            {
                Id = "demo-bt",
                FriendlyName = "耳机 (WH-1000XM5 蓝牙)",
                Kind = DeviceKind.Bluetooth,
                IsActive = true,
                IsEnabled = true,
                FormatText = "16 bit · 44.1 kHz · 2ch",
            },
            new()
            {
                Id = "demo-hdmi",
                FriendlyName = "HDMI (显示器音频)",
                Kind = DeviceKind.Hdmi,
                IsActive = true,
                IsEnabled = true,
                FormatText = "24 bit · 48 kHz · 2ch",
            },
            new()
            {
                Id = "demo-virtual",
                FriendlyName = "CABLE Input (VB-Audio Virtual Cable)",
                Kind = DeviceKind.Virtual,
                IsActive = true,
                IsEnabled = true,
                FormatText = "32 bit float · 48 kHz · 2ch",
            },
            new()
            {
                Id = "demo-spdif",
                FriendlyName = "数字输出 (S/PDIF)",
                Kind = DeviceKind.Spdif,
                IsActive = false,
                IsEnabled = false,
            },
        };
    }
}
