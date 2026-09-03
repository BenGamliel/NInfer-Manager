<p align="center"><img src="src/NInferManager/Assets/ninfer-manager.png" width="128" alt="NInfer Manager icon"></p>
<h1 align="center">NInfer Manager</h1>
<p align="center">A lightweight Windows control center for local NInfer models.<br>Modern Light/Dark interface, no terminal, no permanently loaded model, and no model files bundled.</p>
<p align="center">
  <img alt="Windows" src="https://img.shields.io/badge/Windows-11_x64-0078D4?logo=windows11">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet">
  <img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue">
  <img alt="Status" src="https://img.shields.io/badge/status-v1.0.0_ready-22a06b">
</p>

> [!IMPORTANT]
> NInfer Manager is an independent, unofficial community application. It is not affiliated with or endorsed by NInfer, NVIDIA, Qwen, Hugging Face or llama.cpp.

![NInfer Manager dashboard](docs/images/dashboard.png)

The modern dashboard keeps engine state, VRAM, GPU use, context capacity and the
active profile visible at a glance. Complete warm Light and Dark themes cover
inputs, menus, scrollbars and advanced controls, and the selected mode is remembered.

![NInfer Manager dashboard in Dark mode](docs/images/dashboard-dark.png)

## Start clean

NInfer Manager ships without models and does not pretend that a missing model is
active. A lightweight first-run Wizard explains the API port, helps choose a
verified model, and can be skipped at any time.

## Interface tour

Everyday controls stay visible while the complete engine configuration remains
available under Advanced settings.

| Model Manager | Essentials and Advanced settings |
|---|---|
| ![NInfer Manager model catalog](docs/images/models.png) | ![NInfer Manager settings](docs/images/settings.png) |

## Why NInfer Manager?

NInfer is fast, but a command line and long launch arguments should not be a requirement for using a local model. NInfer Manager keeps a stable local API available, loads the selected model only when a request arrives, and releases VRAM after a configurable idle period.

| Capability | Raw NInfer CLI / BAT | NInfer Manager |
|---|:---:|:---:|
| OpenAI-compatible endpoint | Yes | Yes |
| API remains reachable while the model is unloaded | No | Yes |
| Load on first inference request | Manual restart | Automatic |
| Configurable idle VRAM unload | Manual stop | Automatic |
| Notification-area controls | No | Yes |
| Official model discovery and verified downloads | Manual | Built in |
| Resumable downloads and SHA-256 verification | Manual | Built in |
| Per-model visual profiles | No | Yes |
| Vision, context, KV cache and speculative controls | CLI flags | Visual settings |
| Portable and installed modes | Runtime only | Both |
| Redacted diagnostics package | No | Built in |
| Verified application updates | Manual | Automatic and on demand |
| Busy API port recovery | Startup failure | Automatic or locked |
| Guided first setup | No | Built in and skippable |
| Context-aware model actions | No | Shows only actions valid for the selected model |

## Highlights

- **On-demand inference:** the Web UI and API can stay online without consuming model VRAM. The first inference request loads NInfer automatically.
- **Full lifecycle control:** load, unload, restart, change the idle timer or disable automatic unloading from the app or tray icon.
- **Model Manager:** discover official model cards, download with resume, validate size and SHA-256, import, activate, verify or move a model to the Recycle Bin. The active or installed model is selected automatically, and unavailable actions stay hidden.
- **Detailed profiles:** context size, Vision/video, shared K/V precision, KV capacity, CUDA graphs, MTP/DFlash, media budgets, queues, caches, thinking and sampling controls.
- **Safe local defaults:** loopback-only API, optional bearer token, one running instance and child-process cleanup through a Windows Job Object.
- **Small idle footprint:** native WinForms UI; no Electron runtime and no embedded browser process.
- **Built-in updates:** optional automatic checks, a manual **Check for updates** button and SHA-256 verification before an Installer or Portable update is launched.
- **Predictable ports:** Automatic mode moves to a free Windows dynamic port and reports the change; Locked mode stops and asks instead. Settings also support one-session or saved changes.
- **Clean first run:** no active model is selected until an artifact is installed and explicitly activated.

## Verified default profile

| Setting | Default |
|---|---:|
| Context | 150,000 tokens |
| Vision and video | Enabled |
| KV cache | INT8, shared K/V precision |
| Speculative decoding | MTP, 3 draft tokens |
| CUDA graphs / prefix reuse | Enabled |
| Automatic unload | 3 minutes, configurable |
| Public API | `http://127.0.0.1:48173/v1`, automatic fallback when unlocked |

NInfer currently exposes one shared KV precision setting; separate K and V precisions are therefore not presented as independent controls.

## Measured QA snapshot

These are functional QA measurements, not universal performance claims. They were recorded on Windows 11 with an RTX 5090 32 GB using Qwen3.8-27B NVFP4, 150K context, INT8 KV and Vision enabled.

| State | Manager working set | NInfer process | Total GPU memory observed |
|---|---:|:---:|---:|
| Hidden in tray, steady state | 7.7 MiB | No | Existing system use only |
| Five seconds after cold start | 29.7 MiB | No | Existing system use only |
| Model active during QA | — | Yes | ~28 GB |
| After automatic unload | — | No | ~1.4 GB system baseline |

Text generation, image input, 150K KV allocation, automatic unload, process cleanup, Portable startup, silent install and uninstall were tested. See the [benchmark and QA notes](docs/BENCHMARKS.md) for scope and methodology.

## Install

Releases provide two model-free packages:

- **Installer:** per-user installation, Start Menu entry and optional startup shortcut. Models and settings remain under `%LOCALAPPDATA%\NInfer Manager`.
- **Portable ZIP:** extract anywhere and run `NInfer Manager.exe`. Settings and models stay beside the executable.

Follow the optional Setup Wizard, or open **Models**, select an official artifact,
choose **Install**, then **Set active**. No model download starts without explicit confirmation.

## Build from source

Requirements: .NET SDK 10, Inno Setup 6, and an official `ninfer-windows` portable release directory.

```powershell
./scripts/build.ps1 -EngineSource "D:\path\to\ninfer-windows-release"
```

The build script explicitly excludes `.ninfer` and partial model files. Run `./scripts/audit-repository.ps1` before publishing.

## Documentation

- [User guide](docs/USER_GUIDE.md)
- [Architecture and security boundaries](docs/ARCHITECTURE.md)
- [Benchmarks and QA methodology](docs/BENCHMARKS.md)
- [Publishing checklist](docs/PUBLISHING.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)

## Credits and license

NInfer Manager was created and is maintained by **Ben Gamliel**. The Manager source is licensed under [Apache License 2.0](LICENSE).

NInfer, NInfer-windows, the bundled Web UI and runtime libraries remain the work of their respective authors and retain their original licenses and notices. See [Third-Party Notices](THIRD-PARTY-NOTICES.txt) for project links and full attribution. Model licenses are shown through their official model cards.
