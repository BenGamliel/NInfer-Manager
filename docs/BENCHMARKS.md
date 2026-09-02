# Benchmarks and QA

The figures below document one functional QA run, not guaranteed performance.

## Test environment

- Windows 11 x64 (`10.0.26200`)
- AMD Ryzen 9 9950X3D
- NVIDIA GeForce RTX 5090, 32,607 MiB, driver 610.62
- Qwen3.8-27B NVFP4; 150,000-token context; INT8 KV; Vision enabled
- NInfer Manager 1.0.0

| Check | Result |
|---|---|
| API and Web UI while unloaded | HTTP 200; no `ninfer-serve` process |
| Text request | Completed successfully |
| Image request | Vision media accepted and processed |
| Context allocation | 150,016 KV tokens resolved by the engine |
| Idle unload QA setting | 0.1 minute; completed successfully |
| Hidden steady-state working set | 7.7 MiB |
| Cold-start working set at five seconds | 29.7 MiB |
| GPU memory while loaded | Approximately 28 GB total observed use |
| GPU memory after unload | Approximately 1.4 GB system baseline |
| Process cleanup | Manager exit terminated the backend job |
| Package model count | 0 |
| Installer test | Silent install, launch, API check and uninstall passed |

GPU values are total device memory observed through `nvidia-smi`, not an isolated Manager allocation. Working-set values vary with Windows and runtime activity. Reproduction must use models outside the repository and must not commit settings, logs, model files or identifying paths.

