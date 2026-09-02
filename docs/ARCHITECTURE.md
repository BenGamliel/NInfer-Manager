# Architecture

NInfer Manager uses WinForms and a small Kestrel loopback proxy. There is no
Electron runtime and no embedded browser. The Web UI is served as static files
and opened in the user's default browser.

## Runtime flow

1. Startup tests the configured public loopback port. Automatic mode selects a
   free Windows dynamic port when necessary; Locked mode requires a user choice.
2. The public loopback API starts without loading a model.
3. A supported API request enters `ApiProxy`.
4. `EngineController` starts one `ninfer-serve` process for the active artifact.
5. Readiness is confirmed through the internal `/v1/models` endpoint.
6. The request is streamed between client and backend.
7. A one-shot idle timer unloads the engine when no request is active.

The child process is assigned to a Windows Job Object with
`KILL_ON_JOB_CLOSE`, preventing an invisible orphaned backend after a crash.

## Data boundaries

- `AppSettings` contains application/API settings and per-model profiles.
- `ModelCatalogService` starts from an embedded verified catalog, merges a
  cached catalog and optionally discovers official upstream model cards.
- `ModelDownloadService` owns resumable downloads, size checks, SHA-256,
  imports and recoverable deletion.
- Models and user data are never embedded in the executable or source tree.
- A new settings file has no active model. The catalog remains browseable, while
  inference endpoints fail clearly until an installed model is explicitly selected.

## Update boundary

`UpdateService` reads the latest release from the project's GitHub Releases API,
selects the package matching the current install mode, and requires its published
SHA-256 digest to match the downloaded file. Installed updates are delegated to
the release Installer. Portable updates run from a temporary copy of the current
Manager executable, replace only application files, and exclude `Data` and
`Models` from extraction. Checks are periodic rather than continuous and can be
disabled in Settings.

## Resource behavior

NInfer and all fixed GPU allocations exist only while `ninfer-serve` is alive.
GPU information is queried only when the visible UI requests it. Catalog checks
are bounded, cached and disabled by configuration. No database, telemetry,
indexer, background WebView or continuous filesystem scanner is used.
