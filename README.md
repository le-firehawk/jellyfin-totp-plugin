# Jellyfin TOTP Plugin

Adds server-side TOTP enforcement to Jellyfin login responses and exposes API endpoints for user setup, confirmation, and administrator reset.

## Build locally

```bash
dotnet publish --configuration Release --output bin
```

## GitHub CI

The repository includes a GitHub Actions workflow at `.github/workflows/build.yml` that restores, builds, publishes, zips, and uploads the plugin. Tag pushes such as `v0.1.0.0` also create a GitHub release containing:

- `Jellyfin.Plugin.Totp.zip`
- `manifest.json`

## Jellyfin repository URL

After pushing a tag such as `v0.1.0.0`, import the generated release manifest into Jellyfin with this URL, replacing the owner and repository with your real GitHub location:

```text
https://github.com/<owner>/<repo>/releases/latest/download/manifest.json
```

For this repository name, the expected release URL shape is:

```text
https://github.com/<owner>/jellyfin-totp-plugin/releases/latest/download/manifest.json
```

For branch builds, the workflow summary also prints a raw GitHub manifest URL in this shape:

```text
https://raw.githubusercontent.com/<owner>/<repo>/<branch>/manifest.json
```

Use the release manifest URL for real Jellyfin imports because the CI-generated release manifest contains the release ZIP URL and SHA-256 checksum for the exact build.

## Web client

The plugin ships an embedded `totpclient` script that prompts for a second factor on login retries and exposes helpers for profile setup and admin reset UI integrations.
