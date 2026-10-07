# Changelog / 变更记录

本文件记录本仓库（上游 [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router) 的修改版）的变更。
双语：英文在前，中文在后。GitHub Release 的说明可**直接粘贴**对应版本的小节。

---

## [0.12.0] — 2026-10-07

### Windows audio redirection actually works now / Windows 上真的能改道了

Earlier text in this file said this build could not redirect audio on Windows. That is no longer true:
the routing intent is now dispatched to the original project's injected native core.

（本文件此前写着"本构建在 Windows 上不能改道"—— 这句话现在不成立了：路由意图已经能真正下发到
上游的注入式原生核心。）

**Verified on real Windows 11 hardware** with the upstream 0.10.2 binaries / **已在真实 Windows 11 上验证**：

| Test / 试验 | Result / 结果 |
| --- | --- |
| Inject with the default device, then start the program | session stays on the default device ✓ |
| Inject with a virtual cable *before* the program opens its stream | session leaves the default device ✓ |
| Route (flag=1) then duplicate (flag=2) | both devices stay attached ✓ |

**Two conditions** (details in the README) / **两个前提**（详见 README）：

1. The upstream native core must sit in a `native\` folder next to the app — this repository ships the
   client for that core, not the core itself.
2. The routed program must (re)create its audio stream after the injection: the core hooks
   `IMMDevice::Activate`, so only streams opened *afterwards* are affected.

Defects found and fixed along the way / 顺带修掉的真缺陷：

- The shared-memory handle was closed before `do.exe` ran, so the injected DLL saw no parameters and the
  load failed with the misleading `1114 DLL initialization routine failed`.
- The upstream `audio-router.dll` collides with this project's CLI assembly name — the native files must
  live in a subfolder (the runtime probe already looks there).
- `duplicate` *appends* to the device list instead of replacing it, so the first dispatch for a process
  has to establish a baseline; otherwise the default device is silently dropped.
- `RoutingOutcome.Failed` used to be printed as "recorded … not redirected", hiding real failures.

Release assets follow the same layout as 0.11.0; see the README for the native-core step.
发布包布局与 0.11.0 相同；原生核心那一步见 README。

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
| `AudioRouter-0.11.0-win-x64.zip` | Windows 10/11 x64 — `AudioRouter.Desktop.exe` (GUI) + `audio-router.exe` (CLI). Self-contained, no .NET runtime needed. |
| `AudioRouter-0.11.0-win-arm64.zip` | Windows on ARM64 — same two programs. Self-contained. |
| `AudioRouter-0.11.0-linux-x64.zip` | Linux x64 (glibc) — same two programs. Self-contained. |
| `AudioRouter-0.11.0-linux-arm64.zip` | Linux ARM64 (glibc) — same two programs. Self-contained. |
| `AudioRouter-0.11.0-linux-musl-x64.zip` | Linux x64 musl (Alpine) — same two programs. Self-contained. |

No macOS build: the macOS backend is a stub, so the app would open with demo data and no audio
subsystem. Shipping that would only generate "it does nothing" reports.
（不提供 macOS 包：该平台没有音频后端，装上也只会看到演示数据。）

```
4159787cbe93a46d4b57c414940f6e560146d08bd16890d664ffb5d7cdb8159b  AudioRouter-0.11.0-linux-arm64.zip
331716bf9ad234905fa18e56d7238585dc3df2022de57a225d7c1b3478802e30  AudioRouter-0.11.0-linux-musl-x64.zip
787623b592fb4f78af976a00d9c221b65afff691009364243cb7074b855bd8e2  AudioRouter-0.11.0-linux-x64.zip
b8c9d70f7621d8aabdb642a79c32567b7c6a83f2ff4cced7b6c1a2143d22f006  AudioRouter-0.11.0-win-arm64.zip
845da4fd6349360b26529bff445ba54c8f5d39dfcdf3bee5d5940032063da1dd  AudioRouter-0.11.0-win-x64.zip
```

Quick smoke test after unzip / 解压后自检：

```text
audio-router doctor        # 会如实告诉你这台机器能不能改道
AudioRouter.Desktop.exe    # 图形界面
```

### Fixed in this build / 本构建修复

- **Minimize button glyph was invisible**: the geometry was a zero-height line, which
  `Stretch="Uniform"` collapses to nothing. Fixed in **both** front-ends (the same geometry had the
  same bug in the WPF one). 最小化图标曾不可见，两个前端都已修。
- **Removed the per-app volume bar and percentage** from both front-ends — no information value, and
  mute state is still shown by the icon. 移除应用行里的音量条与百分比（静音仍由图标表达）。
- **Route storage hardened** against data loss: atomic replace (temp file + move), one valid `.bak`
  generation, recovery from that backup when `routes.json` is damaged, and the damaged file kept as
  `routes.corrupt-<timestamp>.json`. Covered by 8 new tests.
  路由落盘加固：原子替换 + 保留一代有效备份 + 损坏时自动恢复 + 留存现场。

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
