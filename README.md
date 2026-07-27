# FModel-Recreate

FModel-Recreate is a Windows Unreal Engine archive explorer and the foundation for a future modding workspace. It is derived from the GPL-3.0 licensed [FModel](https://github.com/4sval/FModel) project and uses [CUE4Parse](https://github.com/FabianFG/CUE4Parse) for UE4 and UE5 package parsing.

## Current capabilities

- Browse Unreal Engine PAK and IoStore archives supported by CUE4Parse.
- Supply static and dynamic AES keys.
- Search packages and inspect references.
- Preview and export textures, audio, meshes, animations, materials and package data.
- Use English or Turkish interface localization.
- Receive stable application updates from this repository's owner-approved GitHub Releases channel.

## Release channels

- **Stable (`vX.Y.Z`):** owner-approved main releases. They can only be started manually by the `apexlions16` GitHub account.
- **Automated upstream (`upstream-vX.Y.Z`):** prereleases created only after a safe, verified integration from `4sval/FModel:dev`. They never replace the stable latest release.
- **QA (`qa`):** a rolling prerelease built from the latest successful `dev` commit.

An automated upstream prerelease can later be promoted to the stable channel by the repository owner through **Actions → Promote Upstream Release**. Promotion retains the exact verified ZIP bytes and publishes standard stable asset names.

Every release includes a Windows x64 ZIP and a SHA-256 checksum file. The packaged executable is self-contained; users do not need to install .NET separately. See `RELEASE_CHANNELS.md` for the publication and promotion rules.

## Startup diagnostics

When the application cannot finish its startup sequence, it writes a diagnostic file to:

```text
%APPDATA%\FModel-Recreate\startup-crash.log
```

## Building

Requirements for source builds:

- Windows
- .NET 10 SDK
- Git with submodule support

```powershell
git clone --recursive https://github.com/apexlions16/FModel-Recreate.git
cd FModel-Recreate
dotnet restore .\FModel\FModel.csproj -r win-x64
dotnet build .\FModel\FModel.csproj -c Release -r win-x64 --no-restore
```

## Attribution and license

FModel-Recreate remains licensed under GPL-3.0. The original FModel copyright, license and third-party notices are preserved in `LICENSE` and `NOTICE`. This repository is an independent derivative and is not the official FModel distribution.

## Support

Use this repository's Discussions and Releases pages for project-specific support and downloads.
