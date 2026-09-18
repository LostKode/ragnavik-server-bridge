#!/usr/bin/env bash
set -euo pipefail

: "${VALHEIM_MANAGED_DIR:?Set VALHEIM_MANAGED_DIR to valheim_server_Data/Managed}"
: "${BEPINEX_CORE_DIR:?Set BEPINEX_CORE_DIR to BepInEx/core}"

dotnet build src/RagnavikServerBridge.csproj --configuration Release \
  -p:ValheimManagedDir="$VALHEIM_MANAGED_DIR" \
  -p:BepInExCoreDir="$BEPINEX_CORE_DIR"
dotnet run --project tests/RagnavikServerBridge.Tests.csproj --configuration Release
python3 scripts/validate.py
