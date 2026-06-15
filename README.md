# Jellyfin TOTP Plugin

Adds server-side TOTP enforcement to Jellyfin login responses and exposes API endpoints for user setup, confirmation, and administrator reset.

## Build locally

```bash
dotnet publish --configuration Release --output bin
```

## GitHub CI and releases

The repository includes a GitHub Actions workflow at `.github/workflows/build.yml` that restores, builds, publishes, zips, and uploads the plugin. Release builds also update the checked-in static `manifest.json` so the Jellyfin repository document always points at immutable versioned release assets.

`manifest.json` is the Jellyfin plugin repository document and is intentionally committed so it keeps a complete version history. When a `v*` tag or GitHub release is published, CI builds the ZIP, calculates Jellyfin's MD5 checksum, derives the plugin version from the tag, appends or replaces that version entry, commits `manifest.json` back to the default branch, and uploads the ZIP plus updated manifest to the release.

You can still update the manifest locally with the same helper script when testing the release process manually:

```bash
dotnet publish --configuration Release --output dist/plugin
(cd dist/plugin && zip -r ../Jellyfin.Plugin.Totp.zip .)
CHECKSUM=$(md5sum dist/Jellyfin.Plugin.Totp.zip | awk '{print $1}')
python3 scripts/append_manifest_version.py \
  --version 1.0.0.0 \
  --target-abi 10.10.0.0 \
  --source-url https://github.com/le-firehawk/jellyfin-totp-plugin/releases/download/v1.0.0.0/Jellyfin.Plugin.Totp.zip \
  --checksum "$CHECKSUM" \
  --changelog "Release v1.0.0.0."
```

New entries are inserted at the top of the `versions` array while older entries remain in the file for historical installs.

## Pull request test builds

Pull requests opened from branches in this repository publish a prerelease whose tag matches the pull request number, for example `pr-12`. The prerelease contains the built `Jellyfin.Plugin.Totp.zip` and the current checked-in static manifest for validation, but the static manifest is only updated for intentional release entries.

Pull requests from forks still build and upload workflow artifacts, but they do not publish prereleases because GitHub does not grant write credentials to untrusted fork builds.

## Jellyfin repository URL

After pushing a version tag such as `v0.1.0.0`, or a dated test tag such as `test-release-2026-06-15-02`, import the rolling latest release manifest into Jellyfin with the repository release URL:

```text
https://github.com/le-firehawk/jellyfin-totp-plugin/raw/main/manifest.json
```

For this repository, that URL is:

```text
https://github.com/le-firehawk/jellyfin-totp-plugin/raw/main/manifest.json
```

Use the static manifest URL for Jellyfin imports because the committed manifest accumulates every published release entry and each entry points at its immutable versioned ZIP asset.

## Web client

The plugin ships an embedded `totpclient` script that prompts for a second factor on login retries and exposes helpers for profile setup and admin reset UI integrations.
