# Release channels

FModel-Recreate keeps stable, automated upstream, and QA publications separate.

## Stable channel

- Tag format: `vX.Y.Z`
- Asset format: `FModel-Recreate-X.Y.Z-win-x64.zip`
- GitHub status: normal release and latest stable candidate
- Entry point: **Actions → FModel-Recreate Stable Release**
- Authorization: only the `apexlions16` GitHub account
- Trigger: manual `workflow_dispatch` only

The stable workflow has no push trigger and no reusable `workflow_call` entry point. A scheduled workflow, merge, version-file change, or GitHub Actions bot cannot publish a stable release.

## Automated upstream channel

- Tag format: `upstream-vX.Y.Z`
- Asset format: `FModel-Recreate-upstream-X.Y.Z-win-x64.zip`
- GitHub status: prerelease
- Latest stable pointer: never changed
- Trigger: a safe, fully verified automatic integration from `4sval/FModel:dev`

The automated channel is intended for reviewing and testing upstream-derived updates without changing the application's main release channel.

## QA channel

- Tag: `qa`
- Status: rolling prerelease
- Source: latest successful `dev` build

QA is independent from both semantic-versioned channels.

## Promoting an automated upstream release

1. Open **Actions → Promote Upstream Release**.
2. Select **Run workflow**.
3. Enter a source tag such as `upstream-v0.2.2`.
4. Start the workflow while signed in as `apexlions16`.

Promotion accepts only an existing, non-draft prerelease in the `upstream-vX.Y.Z` namespace. It verifies the source ZIP and SHA-256, resolves the exact source commit, refuses to overwrite an existing stable tag, and creates `vX.Y.Z` with standard stable asset names.

The ZIP is copied byte-for-byte from the verified automated release. A new checksum file is generated only because the stable ZIP filename differs. The promoted release is then downloaded and verified again.

## Publishing a separate manual stable release

The owner may instead open **Actions → FModel-Recreate Stable Release** and build a stable release from a chosen Git ref. This path performs a fresh dependency audit, build, self-contained publish, runtime execution test, ZIP creation, and checksum generation.
