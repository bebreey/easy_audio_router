# Audio Router · 音频路由器

> [English](README.md) | 简体中文

把某个程序的音频**单独送到指定输出设备** —— 基于
[audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router) 的现代化改造版。

![Audio Router](docs/desktop.png)

## 为什么做这个

起因是和朋友一起玩游戏。我一开始是为了在游戏里给朋友们放点音乐、活跃活跃气氛。
当时用的是 Soundpad —— 放放音效还行，但它只能播放本地音频文件，想放点音乐就比较麻烦。

后来了解到 [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router)
这个项目，我很高兴，但用起来还不够方便。于是我以它为基础 vibe coding 了现在这个版本，
主要是想让我和朋友用起来更省事。

如果能帮到你，那就最好了。要是觉得有用，别忘了给原项目一个 star —— 没有它就没有这个项目。

## 怎么用

1. 从 [Releases](../../releases) 下载 **Windows x64** 那个包
   （Linux 用 `linux-x64` / `linux-arm64` / `linux-musl-x64`；macOS 不支持）
2. 解压，双击 **`AudioRouter.Desktop.exe`** —— 自包含，不需要装 .NET
3. 把左边的应用拖到右边的音频设备上

**唯一需要记住的一件事**：应用要在被路由之后**重新创建音频流**才生效。
如果它已经在播放，**重启它** —— 重启后才真正换设备。

压缩包里还有 `先看这里-怎么用.txt`，写了这三步和最常踩的坑
（包括下载的 zip 被 Windows SmartScreen 拦了怎么过）。

## 什么能用

| | Windows x64 | Linux | macOS |
| --- | --- | --- | --- |
| 设备 / 程序 / 音量 / 静音 | ✅ | ✅ 代码完成 | — |
| **把某程序音频改道到指定设备** | ✅ | ⚠️ 未在真机验证 | ❌ |
| 复制到多个设备 | ✅（先改道、再复制追加）| ❌ | ❌ |
| 保存路由并在应用出现时自动套用 | ✅ | ✅ | ✅ |
| 深色界面、拖拽、中英文 | ✅ | ✅ | ✅ |
| 无头命令行 | ✅ | ✅ | ✅ |

- Windows x64 包**自带**它需要的原生核心；**ARM64** 包不带（上游只有 x86/x64 工具，ARM64 无法改道）。
- 要路由**以管理员身份运行**的程序，本程序也要用管理员身份运行。
- 不编造数据 —— 拿不到就如实说。

## 命令行（可选）

```text
audio-router doctor                      这台机器能不能改道
audio-router devices                     输出设备
audio-router apps                        正在发声的程序
audio-router route <pid> <deviceId>      路由（重启该应用后生效）
audio-router unroute <pid> <deviceId>
audio-router routes                      已保存的路由
audio-router apply                       立刻重新套用已保存的路由
audio-router mute <pid> [--off]
audio-router lang [list|set|import]
```

`--json` 便于脚本，`--lang <code>` 切换输出语言。退出码：`0` 成功 / `1` 错误 / `2` 用法错误。

## 从源码构建

```powershell
dotnet build AudioRouter.Managed.slnx
dotnet run   --project AudioRouter.Tests     # 138 条断言
```

## 许可

**GPL-3.0**，继承自原项目。作为修改版，本作品必须继续以 GPL-3.0 发布，不能改用其他许可。
来源与修改声明见 [`NOTICE.md`](NOTICE.md)。
