**First public release** · **首个公开发行版**

Audio Router sends one program's audio to a different audio device — and, with a virtual audio
cable, hands it to another program as if it were a microphone.
Audio Router 可以把某个程序的声音送到另一台音频设备；配合一块虚拟声卡，还能把它当作麦克风交给另一个程序。

Based on [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router), rebuilt for modern
Windows (and attempted on Linux), GPL-3.0. 基于上游项目重做，面向现代 Windows（并尝试支持 Linux），GPL-3.0。

## Downloads 下载

| File 文件 | Platform 平台 |
| --- | --- |
| `AudioRouter-1.0.0-win-x64.zip` | Windows 10/11 **x64** — GUI + CLI **+ native core**（解压即用 / unzip and run） |
| `AudioRouter-1.0.0-win-arm64.zip` | Windows on ARM64 — GUI + CLI；**no native core**（上游无 ARM64 工具，无法改道） |
| `AudioRouter-1.0.0-linux-x64.zip` | Linux x64 (glibc) — GUI + CLI（路由走音频服务器 / routing goes through the audio server） |
| `AudioRouter-1.0.0-linux-arm64.zip` | Linux ARM64 (glibc) — same as above / 同上 |
| `AudioRouter-1.0.0-linux-musl-x64.zip` | Linux x64 musl (Alpine) — same as above / 同上 |

**Unzip and run** — no installer, nothing to configure. Windows x64 users want the first file.
**解压即用** —— 不用安装、不用配置。Windows 用户下第一个即可。

## Inside every package 每个包内

- one single folder, so your download folder stays clean / 只解出一层文件夹，不会倒一堆文件到下载目录
- `readme.txt` — the three steps, the usual traps, how to use it as a microphone, and what happens on exit
  / `readme.txt` —— 三步上手、常见坑、当麦克风用的做法、关闭程序时会怎样
- `LICENSE.md` and `NOTICE.md` — license and attribution / 许可与来源说明
- Windows x64 only: `native\` — the redirection core, already in place / 仅 Windows x64：`native\`，改道核心，已就位

## What is fixed in this release 本版修复

1. **Plugging in a headset no longer shrinks the program list.** Sessions are read from every active
   playback device, not only from the default one.
   **插上耳机不再让应用列表骤减。** 会话改为遍历所有在用播放设备，而不是只看默认设备。
2. **Removing a route really unloads it.** The removal path hardcoded pid 0, so the unload was never
   sent and the patch stayed inside the target program.
   **移除路由现在真的会卸载。** 原来硬编码 pid=0，卸载从未下发，补丁一直留在目标程序里。
3. **`audio-router unroute` goes through the routing service** instead of editing the store only.
   **命令行的 `unroute` 改走服务层**，不再只改记录、不下发。
4. **Removing one route no longer kills the others.** Unloading is all-or-nothing, so the remaining
   routes are re-applied right after it.
   **移除其中一条不再连带废掉其余几条。** 卸载是整条撤销，所以随后立刻重新下发剩余路由。
5. **Dragging an existing route onto another device moves it** instead of doing nothing silently.
   **把已有路由标签拖到另一台设备 = 移动这条路由**，不再静默失败。
6. **The same route is no longer injected twice** (two dispatch paths did not share state).
   **同一条路由不再被注入两次**（两条下发路径原本不共享状态）。
7. **Closing the program restores the audio** to where it was; your saved routes are kept and applied
   again next time you start it.
   **关闭程序会把音频还原**到改道之前；已保存的路由保留，下次启动照旧套用。

## Verified 已验证

- The packaged build was extracted into an empty folder and driven by its own CLI: `doctor` reports
  *Can redirect audio: yes*, a real route worked, and the routed program stayed in the list exactly once.
  / 打包成品解压到空文件夹后用它自带的 CLI 实跑：`doctor` 报可改道、真跑路由成功、被路由的程序仍只出现一行。
- Removing a route on a live program: `inject: remove pid=<real> → ok`, and the remaining route is
  re-applied (`reapplied=1`). / 真机移除：真实 pid 的卸载成功，剩余路由被重新下发。
- An identical duplicate is skipped in the same process (`already applied (skipped)`), and a different
  device still injects. / 同进程内完全相同的复制会被跳过，换设备仍会真注入。
- Plugging a headset in and out, dragging a route onto another device, and closing the program were all
  checked by hand. / 插拔耳机、拖动路由、关闭程序三项均经手工实测。

## Not verified 未验证

- The Linux build has never been run against a real PipeWire/PulseAudio server — the code is complete,
  but nobody has proved it works on Linux hardware.
  Linux 版本从未在真实的 PipeWire/PulseAudio 上跑过 —— 代码完成，但**没有人在 Linux 真机上验证过**。
- The ARM64 Windows package cannot redirect audio at all; upstream ships no ARM64 tools.
  ARM64 的 Windows 包**无法改道**（上游不提供 ARM64 工具）。

## License 许可

GPL-3.0, inherited from the original project. As a modified version this must stay GPL-3.0 and cannot be
relicensed. Attribution and the modification statement are in `NOTICE.md`.
GPL-3.0，继承自上游。作为修改版必须保持 GPL-3.0、不可改许可。来源与修改声明见 `NOTICE.md`。
