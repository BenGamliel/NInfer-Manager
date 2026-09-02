# Publishing Checklist

Before pushing or creating a release:

1. Run `scripts/audit-repository.ps1`.
2. Confirm no `.ninfer`, `.part`, settings, logs, diagnostics or runtime binaries
   are tracked.
3. Search for local drive paths, user names, API keys and bearer tokens.
4. Build from a clean checkout with `scripts/build.ps1 -EngineSource ...`.
5. Test the Portable EXE with a model outside the repository.
6. Test install, upgrade and uninstall. Confirm models remain unless the user
   deliberately removes them.
7. Verify the SHA-256 files in `dist` and confirm GitHub exposes a digest for
   each uploaded update asset.
8. Include `LICENSE`, `THIRD-PARTY-NOTICES.txt` and the packaged `Licenses`
   directory.
9. Test both the Installer and Portable update flows from the previous release.
10. Describe the project as an unofficial GUI. Do not imply endorsement by the
   NInfer authors or model publishers.

Model files are intentionally never attached to this repository's releases.
