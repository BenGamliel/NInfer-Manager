# Contributing

Thank you for improving NInfer Manager. Please open an issue before a large change, keep the application lightweight, and preserve its no-terminal experience.

1. Build with `dotnet build src/NInferManager/NInferManager.csproj -c Release`.
2. Run `dotnet format src/NInferManager/NInferManager.csproj --verify-no-changes`.
3. Run `scripts/audit-repository.ps1`.
4. Test with model files outside the repository.
5. Document visible behavior changes in `CHANGELOG.md`.

Do not submit models, runtime packages, credentials, personal paths, generated logs or diagnostics. Contributions are accepted under Apache License 2.0.

