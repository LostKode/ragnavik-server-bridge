#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$root_dir/scripts/validate_package.py" "${1:?Usage: scripts/validate-package.sh PATH_TO_ZIP}"
