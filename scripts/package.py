#!/usr/bin/env python3
import json
from pathlib import Path
from subprocess import run
import sys
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parent.parent
manifest = json.loads((ROOT / "package/manifest.json").read_text(encoding="utf-8"))
dll = ROOT / "src/bin/Release/netstandard2.1/RagnavikServerBridge.dll"
if not dll.is_file():
    raise SystemExit("Build the plugin before packaging.")

archive = ROOT / "artifacts" / f"LostKode-Ragnavik_Server_Bridge-{manifest['version_number']}.zip"
archive.parent.mkdir(parents=True, exist_ok=True)
files = {
    "CHANGELOG.md": ROOT / "CHANGELOG.md",
    "README.md": ROOT / "README.md",
    "config/lostkode.ragnavik.serverbridge.cfg": ROOT / "package/config/lostkode.ragnavik.serverbridge.cfg",
    "icon.png": ROOT / "package/icon.png",
    "manifest.json": ROOT / "package/manifest.json",
    "plugins/RagnavikServerBridge/RagnavikServerBridge.dll": dll,
}
with ZipFile(archive, "w", ZIP_DEFLATED) as output:
    for name, source in files.items():
        output.write(source, name)
run([sys.executable, str(ROOT / "scripts/validate_package.py"), str(archive)], check=True)
print(archive)
