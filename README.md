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

The release manifest is generated during the workflow instead of being stored as a static file. For tag releases, the generated manifest uses a Jellyfin-compatible `System.Version` value derived from the pushed tag and points `sourceUrl` at the matching release ZIP. For non-tag builds, the uploaded workflow artifact manifest uses `0.0.0.0` so Jellyfin can parse the manifest during diagnostics, but that artifact is not intended to be imported as a plugin repository because it points at the workflow run rather than a downloadable ZIP asset.

## Jellyfin repository URL

After pushing a version tag such as `v0.1.0.0`, or a dated test tag such as `test-release-2026-06-15-02`, import the rolling latest release manifest into Jellyfin with the repository release URL:

```text
https://github.com/${GITHUB_REPOSITORY}/releases/download/latest/manifest.json
```

For this repository, that URL is:

```text
https://github.com/<owner>/jellyfin-totp-plugin/releases/download/latest/manifest.json
```

Use the rolling latest manifest URL for Jellyfin imports because CI updates both `manifest.json` and `Jellyfin.Plugin.Totp.zip` on the `latest` release whenever a versioned release is published. If you need an immutable manifest for a specific tag, use `https://github.com/<owner>/jellyfin-totp-plugin/releases/download/<tag>/manifest.json`.

## Web client

The plugin ships an embedded `totpclient` script that prompts for a second factor on login retries and exposes helpers for profile setup and admin reset UI integrations.
