<p align="center"><img src="src/NInferManager/Assets/ninfer-manager.png" width="128" alt="NInfer Manager icon"></p>
<h1 align="center">NInfer Manager</h1>
<p align="center">A lightweight Windows control center for running local NInfer models without a terminal.</p>
<p align="center">
  <a href="https://github.com/BenGamliel/NInfer-Manager/actions/workflows/ci.yml"><img alt="Build" src="https://github.com/BenGamliel/NInfer-Manager/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/BenGamliel/NInfer-Manager/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/BenGamliel/NInfer-Manager"></a>
  <img alt="Windows 11 x64" src="https://img.shields.io/badge/Windows-11_x64-0078D4?logo=windows11">
  <a href="LICENSE"><img alt="Apache 2.0 license" src="https://img.shields.io/badge/license-Apache--2.0-blue"></a>
</p>

> [!IMPORTANT]
> NInfer Manager is an independent, unofficial community application. It is not affiliated with or endorsed by NInfer, NVIDIA, Qwen, Hugging Face or llama.cpp.

![NInfer Manager dashboard](docs/images/dashboard.png)

NInfer Manager keeps a local API available while the model is unloaded, starts
NInfer when an inference request arrives, and releases VRAM after a configurable
idle period. The native WinForms interface provides model downloads, engine
profiles, diagnostics, updates and tray controls without an Electron runtime.

## Compatibility

The current `v1.0.0` packages have a deliberately narrow, verified target:

| Component | Current release |
|---|---|
| Operating system | Windows 11 x64 |
| GPU | NVIDIA GeForce RTX 5090, 32 GB (`sm_120a`) |
| Bundled engine | NInfer 0.5.0 native Windows build, CUDA 13.1 baseline |
| Driver | NVIDIA driver compatible with CUDA 13.1 or later |
| Model storage | Models are downloaded separately and are not part of either package |

Other GPUs and community NInfer forks are **not supported by this release**.
The CUDA Toolkit itself is not required. See the bundled engine provenance and
third-party project links in [Third-Party Notices](THIRD-PARTY-NOTICES.txt).

## Download and quick start

[Download the latest Installer or Portable ZIP](https://github.com/BenGamliel/NInfer-Manager/releases/latest).
Both packages are model-free.

1. Install the application, or extract the Portable ZIP and run `NInfer Manager.exe`.
2. Follow the optional first-run wizard, then open **Models** and install an artifact.
3. Select **Set active**, review the recommended profile and start the API.

No model download starts without confirmation. The Installer stores models and
settings under `%LOCALAPPDATA%\NInfer Manager`; Portable mode keeps them beside
the executable.

> [!NOTE]
> The current binaries are not code-signed, so Windows SmartScreen may display a
> warning. Release checksums are published in `SHA256SUMS.txt` with each release.

## What the Manager adds

- **On-demand inference:** the API remains reachable while the model is unloaded;
  the first inference request loads it automatically.
- **Lifecycle and tray controls:** load, unload, restart, close, and configure or
  disable the automatic idle unload timer.
- **Model management:** discover model cards before download, resume downloads,
  validate size and SHA-256, import existing artifacts, activate, verify or move
  installed models to the Recycle Bin.
- **Visual engine profiles:** context, Vision/video, KV precision, KV capacity,
  CUDA graphs, MTP/DFlash, media budgets, queues, caches, thinking and sampling.
- **Safe local operation:** loopback binding by default, optional bearer token,
  one Manager instance and child-process cleanup through a Windows Job Object.
- **Port recovery:** port `8173` is used by default. Automatic mode selects an
  available port from `49152–65535`; Locked mode reports the conflict instead.
- **Maintenance:** update checks, verified package downloads, readable logs and a
  redacted diagnostics bundle.
- **Accessible setup:** a skippable first-run wizard, contextual actions and full
  warm Light and Dark themes.

## API compatibility

The verified OpenAI-compatible inference route is:

```text
POST http://127.0.0.1:8173/v1/chat/completions
```

The bundled NInfer engine also provides the Responses API under `/v1/responses`,
model discovery under `/v1/models`, and Anthropic-compatible Messages support.
The legacy OpenAI `/v1/completions` route is **not provided**. Manager health is
available at `/manager/health`. If a bearer token is enabled, clients must send it
with every protected request.

## Recommended Qwen profile

The tested Qwen3.8-27B NVFP4 preset uses 150,000-token context, Vision/video,
shared INT8 K/V cache precision, MTP with three draft tokens, CUDA graphs and
prefix reuse. Automatic unload defaults to three minutes and remains editable.
NInfer exposes one shared KV precision setting, so K and V cannot be configured
independently.

## Interface

Everyday status and controls remain visible on the dashboard; the full engine
configuration is available under Advanced settings.

| Model Manager | Essentials and Advanced settings |
|---|---|
| ![NInfer Manager model catalog](docs/images/models.png) | ![NInfer Manager settings](docs/images/settings.png) |

<details>
<summary>View the Dark theme</summary>

![NInfer Manager dashboard in Dark mode](docs/images/dashboard-dark.png)

</details>

## Functional QA record

The current application flow was exercised on Windows 11 with a Ryzen 9
9950X3D, RTX 5090 32 GB and Qwen3.8-27B NVFP4 at 150K context with INT8 KV and
Vision enabled. Text and image requests, KV allocation, automatic unload,
process cleanup, Portable startup, silent install and uninstall were verified.

In that run, the Manager settled at 7.7 MiB working set while hidden in the tray,
and total GPU memory observed while the model was active was approximately 28 GB.
These are measurements from one functional QA run—not universal performance or
throughput claims. See [Benchmarks and QA](docs/BENCHMARKS.md) for the complete
record, environment and methodology.

Deeper compatibility, display-scaling, interrupted-download, concurrency,
upgrade and signed-distribution test coverage is still planned. Results will be
documented when those tests have been completed; they are not implied here.

## Documentation

- [User guide](docs/USER_GUIDE.md)
- [Architecture and security boundaries](docs/ARCHITECTURE.md)
- [Benchmarks and QA methodology](docs/BENCHMARKS.md)
- [Publishing checklist](docs/PUBLISHING.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)

## Build from source

Requirements: .NET SDK 10, Inno Setup 6, and an NInfer Windows portable release
directory compatible with the selected target.

```powershell
./scripts/build.ps1 -EngineSource "D:\path\to\ninfer-windows-release"
```

The build excludes `.ninfer` and partial model files. Run
`./scripts/audit-repository.ps1` before publishing.

## Credits and license

NInfer Manager was created and is maintained by **Ben Gamliel**. The Manager
source is licensed under the [Apache License 2.0](LICENSE).

NInfer, NInfer-windows, the bundled Web UI and runtime libraries remain the work
of their respective authors and retain their original licenses and notices. See
[Third-Party Notices](THIRD-PARTY-NOTICES.txt) for attribution. Model licenses
are shown through their official model cards.
