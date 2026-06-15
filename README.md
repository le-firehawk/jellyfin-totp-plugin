# Jellyfin TOTP Plugin

Adds server-side TOTP enforcement to Jellyfin login responses and exposes API endpoints for user setup, confirmation, and administrator reset.

## Build locally

```bash
dotnet publish --configuration Release --output bin
```

## GitHub CI and releases

The repository includes a GitHub Actions workflow at `.github/workflows/build.yml` that restores, builds, publishes, zips, and uploads the plugin. Tag pushes such as `v0.1.0.0` also create a GitHub release containing:

- `Jellyfin.Plugin.Totp.zip`
- `manifest.json`

The release manifest is generated during the workflow instead of being stored as a static file. For tag releases, the generated manifest uses the pushed tag as the plugin version and points `sourceUrl` at the matching release ZIP. For non-tag builds, the uploaded workflow artifact manifest includes the branch/ref name and commit ID in the generated version and changelog.

## Jellyfin repository URL

After pushing a tag such as `v0.1.0.0`, import the generated release manifest into Jellyfin with the repository release URL:

```text
https://github.com/${GITHUB_REPOSITORY}/releases/latest/download/manifest.json
```

For this repository, that URL is:

```text
https://github.com/<owner>/jellyfin-totp-plugin/releases/latest/download/manifest.json
```

Use the release manifest URL for Jellyfin imports because the CI-generated release manifest contains the release ZIP URL and SHA-256 checksum for the exact build.

## Web client

The plugin ships an embedded `totpclient` script that prompts for a second factor on login retries and exposes helpers for profile setup and admin reset UI integrations.
