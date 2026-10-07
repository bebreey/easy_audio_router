# Audio Router

Route audio from individual programs to different output devices — a modernized fork of
[audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router).

> English | [简体中文](README.zh-CN.md)

![Audio Router](docs/desktop.png)

**This is a modified version of the original project** — see [NOTICE.md](NOTICE.md).

---

## Why this exists

It started with a game night. I was playing music for my friends in-game to keep the mood going, and
I was using Soundpad — which is fine for sound effects, but it only plays local audio files, so
putting on actual music was a hassle.

Then I found [audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router) and was
glad it existed, but it wasn't quite convenient enough for what I wanted. So I took it as the base and
vibe-coded this version on top of it, mainly to make things easier for me and my friends.

It's easy to use. If it happens to help you too, that's great. And if it turns out useful, don't
forget to give the original project a star — this wouldn't exist without it:
**[audiorouterdev/audio-router](https://github.com/audiorouterdev/audio-router)** ⭐

---

## Read this first: what works, and what does not

This fork modernizes the **interface and tooling**, and it **really redirects audio on Windows**
through the original project's injected native core.

| Capability | Windows | Linux | macOS |
| --- | --- | --- | --- |
| List output devices, including format (`32 bit float · 48 kHz · 2ch`) | ✅ tested | ✅ code complete | — |
| List programs playing audio (volume / mute / playing) | ✅ tested | ✅ code complete | — |
| Real per-app **mute** | ✅ tested | ✅ code complete | — |
| **Redirect one program's audio to a chosen device** | ✅ **tested on real hardware** — see the two conditions below | ⚠️ implemented (`pactl move-sink-input`), **not verified on real PipeWire** | ❌ needs a virtual audio device |
| Duplicate one program to several devices | ✅ tested (route first, then duplicate) | ❌ needs `module-combine-sink` | ❌ |
| Saved routings, auto-applied when the app appears again (keyed by **executable path**, so it survives restarts) | ✅ tested | ✅ code complete | ✅ |
| Dark UI, drag & drop, context menus, per-app icons, English / 中文 | ✅ tested | ✅ same UI | ✅ same UI |
| Headless CLI (no desktop environment required) | ✅ tested | ✅ code complete | ✅ |

### Redirection on Windows: two conditions

1. **The native core must sit next to the app.** Windows cannot move another program's audio without
   injecting code into that program. This repository ships the *client* for that core, not the core
   itself: the **Windows x64 release package already contains them** in `native\` — nothing to copy.
   For any other setup, take `do.exe`, `do64.exe`, `audio-router.dll` and `audio-router64.dll` from the
   upstream 0.10.2 release and put them in a `native\` folder next to `AudioRouter.Desktop.exe`.
   The app probes for them at runtime and says so honestly when they are missing
   (`audio-router doctor` → *Can redirect audio: no*).
2. **The program must (re)create its audio stream after being routed.** The core hooks
   `IMMDevice::Activate`, so it only affects streams opened *after* the injection. Route it first and
   then start it — or restart it if it is already playing. The CLI says this after every dispatch.

Notes that matter:

- "Code complete, not verified" is meant literally: the Linux backend's *command lines* are covered by
  tests, but they have never run against a real PipeWire/PulseAudio server.
- Injecting into an app that runs elevated needs Audio Router itself to run as administrator.
- The app never fabricates data: when something is unavailable it says so instead of guessing.
## Projects

```
AudioRouter.Core        Cross-platform core (net10.0, zero NuGet/UI dependencies)
  Backends              IAudioBackend + WASAPI (Windows) / PipeWire-PulseAudio (Linux)
  Routing               Route persistence keyed by executable path + auto-restore on app launch
  Localization          External JSON language packs (Languages/ is the single source of truth)
AudioRouter.Cli         Headless command line — servers, SSH, scripts
AudioRouter.Desktop     Cross-platform GUI (Avalonia)
AudioRouter.Tests       Zero-dependency test runner (110 assertions, exit code 0/1)
AudioRouter.Gui         [ARCHIVED] earlier WPF front-end — see its ARCHIVED.md
```

Dependencies point one way: front-ends depend on `Core`; front-ends never depend on each other.

## Build & run

```powershell
dotnet build AudioRouter.Managed.slnx -c Debug      # everything (excluding the archived project)
dotnet run   --project AudioRouter.Desktop          # GUI
dotnet run   --project AudioRouter.Cli -- doctor    # capability self-check
dotnet run   --project AudioRouter.Tests            # tests
```

Self-contained publish (no .NET runtime needed on the target machine):

```powershell
dotnet publish AudioRouter.Desktop -c Release -r win-x64   --self-contained true
dotnet publish AudioRouter.Cli     -c Release -r linux-x64 --self-contained true
```

The upstream C++ sources (`audio-router/`, `do/`, `bootstrapper/`, `audio-router.sln`) still build
with Visual Studio; they are unchanged.

## Command line

```text
audio-router doctor                        platform & capability report (including "can it redirect?")
audio-router devices [filter]              output devices, with format
audio-router apps                          programs with audio sessions
audio-router route <pid> <deviceId>        save a routing (stored per executable path, not PID)
audio-router unroute <pid|exePath> <deviceId>
audio-router routes                        saved routings
audio-router apply                         apply saved routings now (headless)
audio-router mute <pid> [--off]            real mute
audio-router lang [list|set|import]        language packs
```

`--json` for scripting, `--lang <code>` to override the output language.
Exit code `0` success / `1` runtime error / `2` usage error.

## Design conventions

1. **Never fabricate data.** Missing format, icon or meter stays empty and the UI degrades gracefully.
2. **Distinguish "applied" from "recorded".** A routing only reports `Applied` when it was really
   dispatched; a dedicated test fails if a backend claims success it cannot deliver.
3. **Single source of truth.** Language packs, models, routing logic and MVVM helpers exist once.
4. **No UI types in the core.** Icons and brushes are `object?` slots filled by each front-end.

## Documentation

- [`docs/UI-DESIGN.md`](docs/UI-DESIGN.md) — design tokens, interaction spec, and appendices A.1–A.21
  recording every delivery round, defect root-cause and verification evidence.
- [`docs/UPSTREAM-README.md`](docs/UPSTREAM-README.md) — the original project README (preserved).
- [`NOTICE.md`](NOTICE.md) — attribution, modification statement, third-party licenses.

## License

**GPL-3.0** — inherited from the original project (`LICENSE.md` is kept as-is).
As a modified version, this work **must remain GPL-3.0** and may not be relicensed to a permissive
license (GPLv3 §5c). Attribution and modification statements are in [`NOTICE.md`](NOTICE.md).

One caveat worth knowing: `third-party/WTL90_4140_Final/` is under **Common Public License 1.0**,
which the FSF classifies as *GPL-incompatible* ([reference](https://directory.fsf.org/wiki/License:CPL-1.0)).
That combination comes from the upstream repository, not from this fork. WTL is used **only** by the
legacy `audio-router-gui/`; the routing core, the injector and everything in this fork's `AudioRouter.*`
projects do not depend on it.
