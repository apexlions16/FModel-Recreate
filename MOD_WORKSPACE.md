# FModel-Recreate Mod Workspace

The Mod Workspace is the first write path in FModel-Recreate. It keeps browsing and extraction separate from writing, validates every staged file, builds a replacement PAK, inspects the generated archive, and installs only files managed by the current project.

## Supported first release scope

- Unreal Engine 4 game profiles.
- Windows `Content/Paks` installations.
- Legacy `.pak` writing through verified repak 0.2.3 or a user-provided matching `UnrealPak.exe`.
- Existing cooked replacements: `.uasset`, `.umap`, `.uexp`, `.ubulk`, `.uptnl`, `.locres`, `.wem`, `.bnk`, and other loose files.
- Automatic package sidecar collection when an asset is added from the FModel explorer.
- `_P.pak` build naming, post-build archive listing, SHA-256 recording, managed installation, and tamper-safe uninstall.
- LocRes editing for Legacy, Compact, Optimized CRC32, and Optimized CityHash64 formats.
- Texture and WAV import/cook through a locally installed matching Unreal Engine 4 editor.

## Explicitly blocked or conditional

- UE5 and pure IoStore (`.utoc/.ucas`) writing are not presented as supported.
- Signed-PAK enforcement is not bypassed.
- Custom game PAK formats are blocked.
- Encrypted source indexes are reported; generated mod PAKs remain unencrypted and acceptance is game-specific.
- Anti-cheat or executable patching is outside this feature.

## Workflow

1. Open **Mods → Mod Workspace**.
2. Select the game directory and exact UE4 minor version, then run **Analyze Game**.
3. Create a project.
4. Add an asset from the explorer with **Add to Mod Workspace**, add cooked files manually, edit a LocRes, or run texture/audio conversion.
5. Run **Validate**.
6. Run **Build _P.pak**.
7. Install the verified output into the detected `Content/Paks` directory.
8. Use **Uninstall** to remove only the PAK tracked by the project installation manifest.

Every project stores `ModProject.json`, source copies, staging files, build logs, checksums, and installation ownership records in its own directory.
