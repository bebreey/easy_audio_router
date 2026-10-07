# Audio Router

> English | [简体中文](README.zh-CN.md)

Route one program's audio to a different output device — a modernized fork of
[audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router).

![Audio Router](docs/desktop-en.png)

## Why I made this

It started with a game night. I was playing music for my friends in-game to keep the mood going, and I
was using Soundpad — which is fine for sound effects, but it only plays local audio files, so putting
on actual music was a hassle.

Then I found [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router) and was glad
it existed, but it wasn't quite convenient enough for what I wanted. So I took it as the base and
vibe-coded this version on top of it, mainly to make things easier for me and my friends.

If it happens to help you too, that's great. And if it turns out useful, don't forget to give the
original project a star — this wouldn't exist without it.

## Use it

1. Download the **Windows x64** package from [Releases](../../releases)
   (Linux: `linux-x64` / `linux-arm64` / `linux-musl-x64`. macOS is not supported.)
2. Unzip and run **`AudioRouter.Desktop.exe`** — self-contained, no .NET install needed.
3. Drag a program from the left onto a device on the right.

**The one thing to know:** a program has to (re)create its audio stream after being routed. If it is
already playing, **restart it** — that is when the routing takes effect.

The zip also contains `先看这里-怎么用.txt` with these steps and the usual traps
(including how to get past the SmartScreen prompt on a downloaded zip).

## Sending one program's audio to another program

Routing to a different **speaker** works on its own. The reason most people want this, though, is
different: you want *another program* (a voice chat, a streaming or recording tool) to receive that
audio as if it were a microphone. That needs a **virtual audio cable**.

A virtual cable is one device that Windows shows twice — as a playback device and as a recording device:

1. Install one, for example [VB-CABLE](https://vb-audio.com/Cable/) (free) or the cable bundled with
   VoiceMeeter. You will then see **`CABLE Input`** (playback) and **`CABLE Output`** (recording).
2. In Audio Router, route the program that plays the audio → **`CABLE Input`**.
3. In the other program (Discord, OBS, a game…), set its **microphone / input** to **`CABLE Output`**.
4. If that program was already playing audio, restart it — see the note in *Use it* above.

Now the other program hears the music, and nobody else does.

**Want to hear it yourself as well?** Route the program to `CABLE Input` and then **duplicate** it to
your speakers: in the app, drag it onto the speakers while holding nothing — the second route is a
duplicate, so it plays on both. (Command line: `audio-router route <pid> <cableId>` followed by
`audio-router route <pid> <speakerId> --duplicate`.)
## What works

| | Windows x64 | Linux | macOS |
| --- | --- | --- | --- |
| List devices, programs, volume, mute | ✅ | ✅ code complete | — |
| **Redirect one program's audio** | ✅ | ⚠️ not verified on real hardware | ❌ |
| Duplicate to several devices | ✅ (route first, then duplicate) | ❌ | ❌ |
| Saved routings, reapplied automatically | ✅ | ✅ | ✅ |
| Dark UI, drag & drop, English / 中文 | ✅ | ✅ | ✅ |
| Headless CLI | ✅ | ✅ | ✅ |

- The Windows x64 package includes the native core it needs. The **ARM64** package does not: upstream
  only ships x86/x64 tools, so redirection is unavailable there.
- Routing a program that runs elevated needs Audio Router itself to run as administrator.
- Nothing is fabricated — when something is unavailable, the app says so.

## Languages

The interface ships in **English** and **简体中文**. Switch any time from the 🌐 button in the top-right
corner — the same menu has **Open language folder**.

Language packs are plain JSON. Drop one into that folder and it appears in the list, no rebuild needed:

```json
{
  "code": "ja-JP",
  "name": "日本語",
  "version": 1,
  "author": "your name",
  "strings": {
    "section.apps": "アプリケーション",
    "device.kind.virtual": "仮想ケーブル"
  }
}
```

The easiest way to write one: copy [`AudioRouter.Core/Languages/en-US.json`](AudioRouter.Core/Languages/en-US.json)
and translate the values — that file lists every key (`code`, `name`, `version`, `author` and `strings`
are the only required fields).

From the CLI: `audio-router lang list` · `audio-router lang set ja-JP` · `audio-router lang import <file>`.
## Command line (optional)

```text
audio-router doctor                      can this machine redirect?
audio-router devices                     output devices
audio-router apps                        programs playing audio
audio-router route <pid> <deviceId>      route (restart the app to take effect)
audio-router unroute <pid> <deviceId>
audio-router routes                      saved routings
audio-router apply                       reapply saved routings now
audio-router mute <pid> [--off]
audio-router lang [list|set|import]
```

`--json` for scripts, `--lang <code>` to change the output language. Exit codes: `0` ok, `1` error,
`2` usage.

## Build from source

```powershell
dotnet build AudioRouter.Managed.slnx
dotnet run   --project AudioRouter.Tests     # 138 assertions
```

## License

**GPL-3.0**, inherited from the original project. As a modified version this work must stay GPL-3.0
and cannot be relicensed. Attribution and the modification statement are in [`NOTICE.md`](NOTICE.md).
