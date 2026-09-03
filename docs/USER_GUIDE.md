# User Guide

## First run

1. Start `NInfer Manager.exe`.
2. Use the optional Setup Wizard to review the API port and choose a model, or
   select **Skip setup** and configure the application later.
3. The download remains a `.part` file until its size and SHA-256 are verified.
4. The Wizard activates a successfully installed model. When working manually,
   choose **Set active** after installation.
5. Use **Open Web UI**, or connect an OpenAI-compatible client to the address
   shown on the Dashboard.

The API is available immediately, but NInfer is not loaded into VRAM until the
first inference request or until **Load model** is selected. A clean installation
returns an empty `/v1/models` list until a model is activated.

## Dashboard

- Use the **Light / Dark** control in the top-right corner to switch themes. The
  choice is saved immediately for future launches. Inputs, menus, scrollbars,
  setup dialogs and Advanced settings follow the selected theme as well.
- The VRAM and GPU rings update while the window is visible. The Context ring
  shows the configured model capacity rather than tokens used by an individual client.
- Active model settings are summarized as compact profile chips.
- **Load model** starts NInfer with the active profile.
- **Unload from VRAM** stops only the engine; the Manager and public API remain.
- **Restart NInfer** applies profile changes.
- **Open Web UI** opens the bundled UI in the default browser.
- **Run API test** sends a real OpenAI Chat Completions request and expands the compact test panel.
- **Copy generated command** in Settings exposes the complete launch command for transparency and troubleshooting.

## Models

The action area follows the selected model. An unavailable model shows
**Install model** and **Import local file**; a partial download shows
**Resume download** and **Import local file**; an active download shows **Pause**; and installed models
show only the applicable activate, verify and delete actions. **Open model
card** is always available so details and licensing can be reviewed before downloading.

- **Install / Resume** downloads from the model's official Hugging Face
  repository and resumes an interrupted `.part` file.
- **Verify** checks the complete file against the official SHA-256.
- **Import file** accepts a local artifact only when it matches a catalog entry.
- **Delete** unloads the active model if necessary and moves it to Recycle Bin.
- **Check for new models** reads official upstream model cards. Newly discovered
  models may require a newer NInfer engine and are clearly marked.
- Search and filter the catalog by installation state, Vision support or availability.

## Settings

Use **Essentials** for the common choices, with a short explanation below every
control. **Advanced** retains the complete application and selected-model
property editors. NInfer uses one shared KV precision for both K and V; it does
not expose separate K and V types.

Use **Use for this session** to restart the API on a new port without changing
future launches. **Save and restart API** applies the port immediately and saves
it. **Lock this port** prevents automatic fallback: if the port is busy on the
next launch, the Manager stops and asks for another port. When unlocked, a busy
port is replaced automatically with a free port from the Windows dynamic range
and the chosen address is shown to the user.

Model-profile changes require restarting NInfer only when it is already loaded.
**Restore recommended model defaults** affects only the profile selected in the
Settings page. **Open Setup Wizard** starts onboarding again without deleting
models or settings.

## Portable versus Installed

- Portable mode stores `Data` and `Models` beside the EXE.
- Installed mode stores mutable data under `%LOCALAPPDATA%\NInfer Manager` so an
  application upgrade or uninstall does not silently delete downloaded models.
- **Start with Windows** is optional and creates a per-user startup entry.

## Application updates

- When **Automatically check for application updates** is enabled, the Manager
  checks GitHub Releases at startup no more often than the configured interval.
- Use **Check for updates** in Settings, About or the tray menu at any time.
- A download must match the SHA-256 digest published by GitHub before it can run.
- Installed mode launches the release Installer. Portable mode replaces
  only application files and deliberately preserves `Data` and `Models`.
- Updates are never installed silently; the Manager asks before downloading and
  again before closing and applying an update.

## Logs and diagnostics

Logs are capped and rotated. A diagnostics ZIP contains a redacted settings
copy, log tail and basic system information. It never contains models or API
keys.
