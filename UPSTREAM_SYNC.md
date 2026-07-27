# Guarded upstream synchronization

FModel-Recreate checks `4sval/FModel`'s `dev` branch every day at 06:00 UTC (09:00 Türkiye time) and can also be started manually from GitHub Actions.

## Automatic path

An upstream update is automatically integrated only when all of the following are true:

1. The upstream merge has no unresolved conflicts.
2. Upstream does not modify a protected FModel-Recreate path.
3. The change set does not exceed 150 files.
4. Every FModel-Recreate invariant remains present.
5. Recursive submodules are valid.
6. NuGet restore and the vulnerability audit complete.
7. C# and XAML compile in Release mode.
8. A self-contained Windows x64 executable is published.
9. The packaged `FModel-Recreate.exe --verify-runtime` process succeeds on a Windows runner.
10. The `dev` branch has not changed while validation was running.

When all gates pass, the workflow opens an integration pull request, merges it, increments the FModel-Recreate patch version, and publishes **only** an automated upstream prerelease tagged `upstream-vX.Y.Z`. The resulting ZIP and checksum are downloaded again, SHA-256 and archive integrity are verified, and the archive must contain `FModel-Recreate.exe`.

The upstream workflow cannot call the stable release workflow and cannot create a `vX.Y.Z` tag. Automated releases use separate `FModel-Recreate-upstream-X.Y.Z-win-x64` asset names, are marked as prereleases, and never replace GitHub's latest stable release pointer.

## Stable promotion

The repository owner can promote a verified `upstream-vX.Y.Z` prerelease through **Actions → Promote Upstream Release**. The promotion workflow:

1. accepts only tags in the `upstream-vX.Y.Z` namespace;
2. permits only the `apexlions16` GitHub account;
3. verifies the source prerelease and its SHA-256 file;
4. preserves the exact ZIP bytes;
5. creates the corresponding stable `vX.Y.Z` release at the same source commit;
6. downloads and verifies the promoted stable assets again.

The automated prerelease remains available after promotion as a traceable record of the upstream integration.

## Upstream transport isolation

The fork-scoped GitHub token is removed after checkout before contacting the public upstream repository. Only `4sval/FModel:dev` is fetched with `--no-tags`, preventing upstream release tags such as `qa` from colliding with FModel-Recreate's own tags. The workflow authenticates again only immediately before pushing its integration branch back to FModel-Recreate.

Every workflow change also performs a real read-only fetch of the public upstream branch, so credential or tag regressions fail before the synchronization workflow is merged.

## Manual-review path

The workflow creates a draft pull request and does not publish a release when any protected surface is touched, a conflict occurs, an invariant disappears, a build/runtime check fails, the change set is unusually large, or `dev` changes concurrently.

Merge conflicts are staged with the FModel-Recreate side preserved. This prevents upstream from silently replacing branding, localization, startup handling, settings migration, release infrastructure, or other custom behavior. A developer can then review and manually combine the conflicting upstream implementation.

## Protected surfaces

The editable policy is stored at `tools/upstream-sync/protected-paths.txt`. It currently protects:

- application identity and repository URLs;
- startup lifecycle and first-run language selection;
- Turkish localization;
- independent application-data and settings handling;
- invalid game-directory recovery;
- local ImGui settings fallback;
- FModel-Recreate update checks;
- About/branding content;
- QA, stable, automated-upstream, promotion, and synchronization workflows.

## Invariant guard

`tools/upstream-sync/Assert-RecreateInvariants.ps1` checks the actual source tree rather than trusting a clean Git merge. This catches non-conflicting upstream edits that could still remove or redirect FModel-Recreate features. It also prevents stable publishing from becoming push-triggered or automation-callable and verifies that upstream releases remain prereleases in their own tag namespace.

## Versioning

Safe automatic integrations increment only the patch component. For example, `0.2.1` becomes `0.2.2`, which is published as `upstream-v0.2.2`. The corresponding stable `v0.2.2` exists only after an owner-approved promotion or a separate owner-started stable build.

Risky/manual integrations do not change the version until a maintainer resolves and approves them.

## Manual execution

Open **Actions → Upstream FModel Sync → Run workflow**. Enabling `force_manual_review` creates a draft integration PR even when every automatic safety gate would otherwise pass.
