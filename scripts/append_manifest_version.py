#!/usr/bin/env python3
"""Append a release entry to the static Jellyfin plugin repository manifest."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

PLUGIN_GUID = "65e3f94b-29d8-4d3b-a348-2343784b1db8"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", default="manifest.json")
    parser.add_argument("--version", required=True)
    parser.add_argument("--target-abi", required=True)
    parser.add_argument("--source-url", required=True)
    parser.add_argument("--checksum", required=True)
    parser.add_argument("--changelog", required=True)
    args = parser.parse_args()

    path = Path(args.manifest)
    manifest = json.loads(path.read_text(encoding="utf-8"))
    plugin = next(item for item in manifest if item["guid"] == PLUGIN_GUID)
    versions = plugin.setdefault("versions", [])
    versions[:] = [item for item in versions if item.get("version") != args.version]
    versions.insert(0, {
        "version": args.version,
        "changelog": args.changelog,
        "targetAbi": args.target_abi,
        "sourceUrl": args.source_url,
        "checksum": args.checksum,
    })
    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
