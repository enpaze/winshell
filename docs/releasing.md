# Releasing

Winshell and Winland cut, tag, and stamp a version the same way. What a user installs is Winshell's, and that is the rest of this page.

## The version

The released version is `.release-please-manifest.json`. A build stamps that into the assembly unless `-p:Version=` overrides it, so `winshell --version` and `Shell.Version` report that release. The printed version can also carry a `+` build id. A build that cannot read the manifest is `0.0.0-dev`.

## From a merge to a published release

release-please keeps a release PR open that collects the changes merged into `main`, with the next version and its changelog. Merging that PR tags the release and creates a **draft**. `draft` and `force-tag-creation` in `release-please-config.json` keep the release unpublished while making its tag available for packaging.

Winland publishes its own assets from that tag. Winshell's release workflow then runs the Package workflow (`.github/workflows/package.yml`) on the tagged commit:

1. It runs `scripts/package.ps1`.
2. `scripts/publish-release.ps1` attaches both portable zips, `SHA256SUMS.txt`, `version.json`, `winget-manifests.zip`, `install.ps1` and `uninstall.ps1` to the draft. Every file must exist locally, and every remote asset must have the expected SHA-256 before the draft is published. An existing asset is never replaced: matching bytes are skipped and a different digest stops the upload.
3. Signing notes are also added before publication.

This order supports [GitHub immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases), whose assets are frozen when published.

The same workflow builds and checks the packages, without publishing, on the PRs and pushes to `main` the [`package` job](development.md#jobs) runs for, and keeps them as the run's `packages` artifact. The Store bundle from that run is kept for 14 days.

## Dev releases

`.github/workflows/dev-release.yml` runs after the CI workflow succeeds on a push to `main`. It publishes `win-x64` and `win-arm64`, then attaches both zips, `version.json`, and `SHA256SUMS.txt` to the prerelease tag `dev`.

The version is the newest published `vX.Y.Z`, then `-dev.` and the number of commits since that tag, then `+` and the seven-character commit. With no stable tag, the base is `0.0.0`. The workflow moves the `dev` tag onto that commit. It edits the prerelease, or creates it with `--latest=false`, and uploads with `--clobber`.

Immutable releases stay off. This workflow moves the `dev` tag and replaces its assets. Stable releases stay the Release Please package workflow.

## winget

`winget install DanteSparras.Winshell` is not available yet: the first submission is not done.

With a `WINGET_TOKEN` secret, the release workflow sets `WINGET_CREATE_GITHUB_TOKEN` for a pinned `wingetcreate` and submits the manifests to [winget-pkgs](https://github.com/microsoft/winget-pkgs). That secret is not set.

- Before it is added, it has to be a token from a dedicated bot account whose only repository is that account's fork of winget-pkgs. A token from the maintainer account is not used: a classic `public_repo` personal access token can write every public repository that account can access, including this one.
- Without the secret, submit the manifests by hand: unzip `winget-manifests.zip`, set `WINGET_CREATE_GITHUB_TOKEN`, and run `wingetcreate submit <folder>`.
- winget's moderators review every PR, and the first one for `DanteSparras.Winshell` before any other.

## Signing

A release's own binaries are signed through [SignPath Foundation](https://signpath.org) once its project is set up. That project is not set up, so published zips are unsigned.

Once it is, the Package workflow uploads the published folders as the `unsigned` artifact, asks SignPath to sign them with the `parameters` version the binaries carry, waits up to 110 minutes for an approver, and packs and checks the signed files (`scripts/package.ps1 -Verify -Signed`). It needs:

- the repository variable `SIGNPATH_ORGANIZATION_ID`, which turns signing on. Without it, releases are not signed.
- the secret `SIGNPATH_API_TOKEN`, from a SignPath CI user that may submit to the project.
- `SIGNPATH_PROJECT_SLUG` and `SIGNPATH_SIGNING_POLICY_SLUG` when they are not `winshell` and `release-signing`.
- the signing policy's allowed refs to include `refs/heads/main` and `refs/tags/v*`, since a repair runs from the tag.
- the project's artifact configuration, [packaging/signpath/artifact-configuration.xml](https://github.com/enpaze/winshell/blob/main/packaging/signpath/artifact-configuration.xml), pasted into SignPath, and GitHub set up as its trusted build system.

PRs and pushes to `main` are never signed. Every binary already carries the product name `Winshell` and the release's version, which `-Verify` checks with or without signing.

## A draft with missing files

- **Packaging or the upload failed:** open the Release Please run and choose **Re-run failed jobs**. The release stays a draft until every asset verifies.
- **Packaging never ran**, because the whole Release Please run was re-run after the draft existed: run Package by hand (**Actions > Package > Run workflow**). Under **Use workflow from**, choose **Tags** and the draft's tag, and give the same tag. The tagged commit must contain this draft-publication workflow and its scripts. A signed release must be built from the commit the run is for, so starting its repair from `main` stops. The workflow checks the manifest version, resumes matching draft assets and publishes only after verification.

A **published** release with missing files is not repaired by this workflow. In particular, v0.6.0 was published immutable before its assets were uploaded ([#166](https://github.com/enpaze/winshell/issues/166)). Rerunning its workflow cannot add those assets, and disabling repository immutability does not unlock that release. Leave its tag and assets alone. Recovery requires a later approved release, created as a draft, packaged, populated and verified before publication.

## Microsoft Store

The Store package is not published. Publishing waits on Partner Center setup: the identity variables below. Until then it is not an install option.

The Package workflow also builds `winshell-<version>.msixbundle` (x64 and arm64) from `packaging/msix/AppxManifest.xml` and keeps it as the run's `store-package` artifact. It is not attached to the release: it is unsigned, and the Store signs it. Each run also test-signs the x64 package with a certificate only that runner trusts, installs it there and runs the `winshell` alias.

About the Store build:

- **Files.** It reads and writes the same `%APPDATA%\winshell` config folder as every other build. The manifest turns off file and registry write virtualization (`unvirtualizedResources`), so those writes are the real ones.
- **Sign-in** is the package's startup task, declared off, not the user's `Run` key: Windows starts `winshellw.exe --no-build --no-duplicate`, which keeps working after a Store update.
- **Capabilities.** The package declares `userNotificationListener`, `wiFiControl`, `bluetooth`, `location` and `radios`.
- **Start** runs `winshellw.exe --no-duplicate`, so a second click starts no second shell. `winshellw.exe` is a copy of `winshell.exe` that opens no console, made when the build is published, and the portable zips have it too. `winshell` in a terminal is the command line.
- **Version.** The Store takes no 0 as the first part of a package version and installs the highest, so Winshell x.y.z is package version (x+1).y.z.0.
- **Identity.** The repository variables `MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER` and `MSIX_PUBLISHER_DISPLAY_NAME` are the identity Partner Center shows under **Product identity**. Without them, the package is `Winshell.Test` by `CN=Winshell Test`, which the Store refuses.

To submit a release:

1. Download `store-package` from the release's Package run and upload the `.msixbundle` in Partner Center.
2. Paste the listing from `packaging/store/listing.md`.

## Other release tasks

- **Pick the version yourself:** when merging a PR, add `Release-As: x.y.z` to the end of the squash commit message.
- **The release PR's checks don't start:** without the `RELEASE_PLEASE_TOKEN` secret, close and reopen the release PR right before merging it.
