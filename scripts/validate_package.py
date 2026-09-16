#!/usr/bin/env python3
import json
import sys
from pathlib import PurePosixPath
from zipfile import ZipFile

archive = sys.argv[1]
required = {
    "CHANGELOG.md",
    "README.md",
    "config/lostkode.ragnavik.progress.cfg",
    "icon.png",
    "manifest.json",
    "plugins/RagnavikProgress/RagnavikProgress.dll",
}
with ZipFile(archive) as package:
    names = set(package.namelist())
    missing = required - names
    if missing:
        raise SystemExit(f"Missing package entries: {', '.join(sorted(missing))}")
    for name in names:
        path = PurePosixPath(name)
        forbidden_dir = {"bin", "obj", "node_modules"} & set(path.parts)
        forbidden_file = path.suffix in {".pdb", ".zip"} or name.endswith(".deps.json")
        if forbidden_dir or forbidden_file:
            raise SystemExit(f"Forbidden generated or nested artifact: {name}")
    json.loads(package.read("manifest.json"))
print("Package validation passed.")
