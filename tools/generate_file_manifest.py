#!/usr/bin/env python3
"""Generate a files.json for the SWG Unified Launchpad.

Server operators run this against their patch directory (the files that
differ from a stock SWG 14.1 client) every time they release a patch:

    python generate_file_manifest.py "C:\\path\\to\\patch\\files" > files.json

Then upload files.json next to the patch files. That's the whole release.
"""
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

EXCLUDE_SUFFIXES = {".lp-tmp"}
EXCLUDE_NAMES = {"files.json", "manifest.json", "thumbs.db", "desktop.ini"}


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__, file=sys.stderr)
        return 2
    root = Path(sys.argv[1]).resolve()
    if not root.is_dir():
        print(f"error: '{root}' is not a directory", file=sys.stderr)
        return 2

    files = []
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        if path.suffix.lower() in EXCLUDE_SUFFIXES or path.name.lower() in EXCLUDE_NAMES:
            continue
        rel = path.relative_to(root).as_posix()
        files.append({"path": rel, "size": path.stat().st_size, "sha256": sha256_of(path)})
        print(f"  {rel}", file=sys.stderr)

    manifest = {
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "files": files,
    }
    json.dump(manifest, sys.stdout, indent=2)
    print(file=sys.stdout)
    print(f"{len(files)} file(s) listed.", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
