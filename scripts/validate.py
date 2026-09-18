#!/usr/bin/env python3
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
manifest = json.loads((root / "package/manifest.json").read_text(encoding="utf-8"))
source = "\n".join(path.read_text(encoding="utf-8") for path in (root / "src").glob("*.cs"))
readme = (root / "README.md").read_text(encoding="utf-8")
workflow = (root / ".github/workflows/publish-thunderstore.yml").read_text(encoding="utf-8")
hexium_workflow = (root / ".github/workflows/publish-hexium.yml").read_text(encoding="utf-8")
required = {
    "manifest name": manifest["name"] == "Ragnavik_Server_Bridge",
    "manifest version": manifest["version_number"] == "1.0.0",
    "BepInEx dependency": "denikson-BepInExPack_Valheim-5.4.2350" in manifest["dependencies"],
    "Catos dependency": "catosaurluna-CatosAntiCheat-1.0.4" in manifest["dependencies"],
    "plugin GUID": 'ModGuid = "lostkode.ragnavik.serverbridge"' in source,
    "plugin version": 'ModVersion = "1.0.0"' in source,
    "mismatch hook": 'AccessTools.Method(poster, "PostMismatchKick")' in source,
    "timeout hook": 'AccessTools.Method(poster, "PostTimeoutKick")' in source,
    "future adapter disabled": "AzuAntiCheatEnabled = false" in (root / "package/config/lostkode.ragnavik.serverbridge.cfg").read_text(encoding="utf-8"),
    "server-only policy": "CatosAntiCheat_ServerOnly.txt" in readme,
    "workflow package": "LostKode-Ragnavik_Server_Bridge-${{ inputs.expected_version }}.zip" in workflow,
    "Hexium package": "LostKode-Ragnavik_Server_Bridge-${{ inputs.expected_version }}.zip" in hexium_workflow,
    "Hexium repository": "--repository https://hexium.gg" in hexium_workflow,
    "Hexium token": "secrets.HEXIUM_AUTH_TOKEN" in hexium_workflow,
    "Hexium publish gate": "if: inputs.publish" in hexium_workflow,
    "Hexium verification": "Verify public package" in hexium_workflow,
}
failed = [name for name, valid in required.items() if not valid]
if failed: raise SystemExit("Bridge validation failed: " + ", ".join(failed))
print("Bridge identity and adapter validation passed.")
