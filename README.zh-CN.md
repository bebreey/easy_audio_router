# Audio Router · 音频路由器

把某个程序的音频**单独送到指定输出设备**——这是
[audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router) 的现代化改造版。

> [English](README.md) | 简体中文

![Audio Router](docs/desktop.png)

**本仓库是原项目的修改版**，来源与修改声明见 [NOTICE.md](NOTICE.md)。

---

## 为什么做这个

起因是和朋友一起玩游戏。我一开始是为了在游戏里给朋友们放点音乐、活跃活跃气氛。
当时用的是 Soundpad —— 放放音效还行，但它只能播放本地音频文件，想放点音乐就比较麻烦。

后来了解到 [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router)
这个项目，我很高兴，但用起来还不够方便。于是我以它为基础 vibe coding 了现在这个版本，
主要是想让我和朋友用起来更省事。

它的使用很简单。如果能帮到你，那就最好了。要是觉得有用，别忘了给原项目一个 star ——
没有它就没有这个项目：**[audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router)** ⭐

---

## 先看这里：什么能用，什么还不能

这次改造动的是**界面与工具链**，**没有**在 Windows 上补齐音频改道引擎。
把这件事写在最前面，比列一堆功能有用：

| 能力 | Windows | Linux | macOS |
| --- | --- | --- | --- |
| 列出输出设备（含格式 `32 bit float · 48 kHz · 2ch`） | ✅ 已验证 | ✅ 代码完成 | — |
| 列出正在发声的程序（音量 / 静音 / 播放中） | ✅ 已验证 | ✅ 代码完成 | — |
| 真实的按程序**静音** | ✅ 已验证 | ✅ 代码完成 | — |
| **把某个程序的音频改道到指定设备** | ❌ **本构建未实现**（需要注入式原生核心，**并且**需要一套本构建尚未实现的 IPC 客户端） | ⚠️ 已实现（`pactl move-sink-input`），**未在真实 PipeWire 上验证** | ❌ 需要虚拟音频设备 |
| 把一个程序复制到多个设备 | ❌ 同上 | ❌ 需要 `module-combine-sink` | ❌ |
| 保存路由，应用再次出现时自动套用（按**可执行文件路径**存，因此能跨重启） | ✅ 已验证 | ✅ 代码完成 | ✅ |
| 深色界面、拖拽、右键菜单、真实图标、中英文 | ✅ 已验证 | ✅ 同一套界面 | ✅ 同一套界面 |
| 无头命令行（不需要桌面环境） | ✅ 已验证 | ✅ 代码完成 | ✅ |

**为什么还没有改道**：Windows 上要把某个程序的音频换设备，必须往那个进程里**注入原生代码**（上游就是这么做的）——
本构建既没有编译/携带那部分原生核心（`audio-router.dll` + `do.exe`），也没有实现与它通信的客户端
（往共享内存 `Local\audio-router-file` 写路由表 + 调用 `do.exe` 完成注入）。Linux 后端则是**已经实现**、只是未在真机验证。
几条必须说清的：

- **上游 0.10.2 在 Windows 上可以真正改道，本构建还不行。** 如果你现在就要改道，请用上游的发布包。
  本仓库当下的价值在界面、命令行、跨平台核心，以及路由模型本身。
- “代码完成但未验证”就是字面意思：Linux 后端的**命令行**有测试覆盖，但**从没在真实 PipeWire/PulseAudio 上跑过**。
- 程序不编造数据：拿不到就如实说——所以状态栏会写「仅记录路由（当前平台未下发）」。

## 工程结构

```
AudioRouter.Core        跨平台核心（net10.0，零 NuGet、零 UI 框架依赖）
  Backends              IAudioBackend + WASAPI（Windows）/ PipeWire-PulseAudio（Linux）
  Routing               路由按可执行文件路径持久化 + 应用出现时自动恢复
  Localization          外置 JSON 语言包（Languages/ 是唯一真源）
AudioRouter.Cli         无头命令行——服务器 / SSH / 脚本
AudioRouter.Desktop     跨平台图形界面（Avalonia）
AudioRouter.Tests       零依赖测试运行器（110 项断言，退出码 0/1）
AudioRouter.Gui         【已归档】早期自研 WPF 界面，见该目录 ARCHIVED.md
```

依赖只有一个方向：前端依赖 `Core`，前端之间互不依赖。

## 构建与运行

```powershell
dotnet build AudioRouter.Managed.slnx -c Debug      # 全部（不含归档工程）
dotnet run   --project AudioRouter.Desktop          # 图形界面
dotnet run   --project AudioRouter.Cli -- doctor    # 能力自检
dotnet run   --project AudioRouter.Tests            # 测试
```

自包含发布（目标机器不需要装 .NET 运行时）：

```powershell
dotnet publish AudioRouter.Desktop -c Release -r win-x64   --self-contained true
dotnet publish AudioRouter.Cli     -c Release -r linux-x64 --self-contained true
```

上游 C++ 源码（`audio-router/`、`do/`、`bootstrapper/`、`audio-router.sln`）仍可用 Visual Studio 构建，**未作改动**。

## 命令行

```text
audio-router doctor                        平台与能力自检（含"能不能改道"）
audio-router devices [filter]              输出设备（含格式）
audio-router apps                          正在发声的程序
audio-router route <pid> <deviceId>        保存路由（按可执行文件路径存，不是 PID）
audio-router unroute <pid|exePath> <deviceId>
audio-router routes                        已保存的路由
audio-router apply                         立刻把已保存的路由套用到在跑的程序（无头场景）
audio-router mute <pid> [--off]            真实静音
audio-router lang [list|set|import]        语言包管理
```

`--json` 便于脚本，`--lang <code>` 覆盖输出语言。
退出码：`0` 成功 / `1` 运行错误 / `2` 用法错误。

## 设计约定

1. **不编造数据**。格式、图标、音量表拿不到就留空，界面自动降级。
2. **区分"已生效"与"仅记录"**。只有真正下发成功才报 `Applied`；有专门的测试会在后端虚报时失败。
3. **单一真源**。语言包、模型、路由逻辑、MVVM 基类各自只有一份。
4. **核心层不出现 UI 类型**。图标与画刷是 `object?` 槽位，由各前端自己填充。

## 文档

- [`docs/UI-DESIGN.md`](docs/UI-DESIGN.md) —— 设计 Token、交互规格，以及附录 A.1–A.21
  逐轮记录交付内容、缺陷根因与验证证据。
- [`docs/UPSTREAM-README.md`](docs/UPSTREAM-README.md) —— 上游项目原始 README（原样保留）。
- [`NOTICE.md`](NOTICE.md) —— 来源、修改声明与第三方许可。

## 许可

**GPL-3.0** —— 继承自原项目（`LICENSE.md` 原样保留）。
作为修改版，本作品**必须继续以 GPL-3.0 授权**，不得改为宽松许可后分发（GPLv3 §5c）。
来源与修改声明见 [`NOTICE.md`](NOTICE.md)。

有一点需要你知道：`third-party/WTL90_4140_Final/` 是 **Common Public License 1.0**，
FSF 把它归类为**与 GPL 不兼容**（[依据](https://directory.fsf.org/wiki/License:CPL-1.0)）。
这个组合来自上游仓库，不是本次修改引入的。WTL **只**被遗留的 `audio-router-gui/` 使用；
路由核心、注入器，以及本仓库 `AudioRouter.*` 下的代码都不依赖它。
