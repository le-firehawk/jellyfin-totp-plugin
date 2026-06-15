#!/usr/bin/env python3
"""Render a Jellyfin plugin repository manifest with release-specific artifact data."""
from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True)
    parser.add_argument("--target-abi", required=True)
    parser.add_argument("--source-url", required=True)
    parser.add_argument("--checksum", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    manifest = [
        {
            "guid": "65e3f94b-29d8-4d3b-a348-2343784b1db8",
            "name": "TOTP Two-Factor Authentication",
            "description": "Time-based one-time-password two-factor authentication for Jellyfin users.",
            "overview": "Adds TOTP setup, login enforcement, administrator reset, and an optional mandate for all users.",
            "owner": "jellyfin-totp-plugin",
            "category": "Authentication",
            "versions": [
                {
                    "version": args.version,
                    "changelog": "Automated build from GitHub Actions.",
                    "targetAbi": args.target_abi,
                    "sourceUrl": args.source_url,
                    "checksum": args.checksum,
                }
            ],
        }
    ]

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
