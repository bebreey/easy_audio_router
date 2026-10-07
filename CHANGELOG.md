# Changelog / 变更记录

本文件记录本仓库（上游 [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router) 的修改版）的变更。
双语：英文在前，中文在后。GitHub Release 的说明可**直接粘贴**对应版本的小节。

---

## [0.11.0] — 2026-10-07

### ⚠️ Read before downloading / 下载前先读

**This build can NOT redirect audio on Windows yet.** It ships the modernized UI, a headless CLI, a
cross-platform core and a routing model — but **not** the audio redirection engine (that needs the
injected native core plus an IPC client this build does not implement). For real redirection on
Windows today, use the [upstream 0.10.2 release](https://github.com/audiorouterdev/audio-router/releases).

**本构建在 Windows 上还**不能**真正改道音频。** 它带来的是现代化界面、无头命令行、跨平台核心与路由模型，
**不含**改道引擎（需要注入式原生核心，以及本构建尚未实现的 IPC 客户端）。现在就要改道的 Windows 用户请用
[上游 0.10.2](https://github.com/audiorouterdev/audio-router/releases)。

### What works / 可用的部分

| | Windows | Linux |
| --- | --- | --- |
| Enumerate output devices with format (`32 bit float · 48 kHz · 2ch`) | ✅ tested | ✅ code complete |
| Enumerate programs with audio sessions (volume / mute / playing) | ✅ tested | ✅ code complete |
| Real per-app **mute** | ✅ tested | ✅ code complete |
| Save routings **keyed by executable path**, auto-applied when the app appears again | ✅ tested | ✅ code complete |
| GUI: dark theme, drag & drop, context menus, duplicate dialog, language switch (EN/中文), real app icons, custom title bar | ✅ tested | ✅ same UI |
| Headless CLI: `doctor / devices / apps / route / unroute / routes / apply / mute / lang`, `--json` | ✅ tested | ✅ code complete |

- 输出设备与音频会话枚举（含格式）；真实静音；按**可执行文件路径**保存路由并在应用再次出现时自动套用。
- 图形界面：深色主题、拖拽路由、右键菜单、批量复制对话框、中英切换、真实应用图标、自绘标题栏。
- 无头命令行：脚本可用（`--json`，退出码 0/1/2），**不需要桌面环境**。

### What does not work yet / 还不能用的部分

- **Windows audio redirection** — not implemented in this build (see the warning above).
- **Linux backend** — code complete, **never run against a real PipeWire/PulseAudio server**. Its
  `pactl` command lines are covered by tests, but end-to-end behaviour is unverified.
- **macOS** — no backend; the app reports the platform as unsupported instead of pretending.
- **Duplicate to several devices** — unsupported on both platforms (needs `module-combine-sink` on
  Linux, the native core on Windows).

### Verification / 验证情况

- 5 projects build with 0 errors / 0 warnings.
- **110 assertions pass** (`AudioRouter.Tests`): routing semantics (never reports `Applied` when it
  cannot dispatch), localStorage/persistence, language-pack encoding edge cases, `pactl` parsing and
  command construction, CJK text width, save/restore notifications.
- Live-tested on Windows: real device/session enumeration, real mute, drag & drop routing, context
  menus, duplicate dialog, language switching, real per-app icons, route persistence + auto-restore,
  window icon.
- **Not verified**: real PipeWire end-to-end; mouse-drag window resizing (only the `WS_THICKFRAME`
  style bit was checked).

### Downloads / 下载

| File | Platform |
| --- | --- |
| `AudioRouter-0.11.0-win-x64.zip` | Windows 10/11 x64 — contains `AudioRouter.Desktop.exe` (GUI) and `audio-router.exe` (CLI). Self-contained, no .NET runtime needed. |
| `AudioRouter-0.11.0-linux-x64.zip` | Linux x64 — same two programs. Self-contained. |

```
a7d270309441fcc25f7599746b20f7be7b2bb29164344be3992d42c4639b7bb5  AudioRouter-0.11.0-linux-x64.zip
7fce32c89a3b519c76ac8a3b5c87c726e9f92b30d5a42ae6e61d70666e76cdff  AudioRouter-0.11.0-win-x64.zip
```

Quick smoke test after unzip:

```text
audio-router doctor        # 会如实告诉你这台机器能不能改道
AudioRouter.Desktop.exe    # 图形界面
```

### Source / 源码

GPL-3.0 requires that binaries come with their source. The complete corresponding source is this
repository (tag `v0.11.0`). 依 GPL-3.0，二进制必须能对应到源码：完整对应源码即本仓库（标签 `v0.11.0`）。

### License / 许可

GPL-3.0 (inherited). This is a **modified version**: attribution and the modification statement are in
[`NOTICE.md`](../NOTICE.md). As a derivative it must remain GPL-3.0 (GPLv3 §5c).
本作品是**修改版**，必须继续以 GPL-3.0 授权，不得改为宽松许可。

Note: `third-party/WTL90_4140_Final/` (Common Public License 1.0, classified by the FSF as
GPL-incompatible) is used **only** by the legacy `audio-router-gui/`; nothing in `AudioRouter.*`
depends on it.

---

## Upstream history / 上游历史

Versions 0.7 – 0.10.2 belong to the original project; its changelog is preserved in
[`docs/UPSTREAM-README.md`](../docs/UPSTREAM-README.md).
0.7 – 0.10.2 属于原项目，变更记录原样保留在上面的文件里。
