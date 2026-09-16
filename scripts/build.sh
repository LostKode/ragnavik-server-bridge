#!/usr/bin/env bash
set -euo pipefail

: "${VALHEIM_MANAGED_DIR:?Set VALHEIM_MANAGED_DIR to valheim_server_Data/Managed}"
: "${BEPINEX_CORE_DIR:?Set BEPINEX_CORE_DIR to BepInEx/core}"

dotnet build src/RagnavikProgress.csproj --configuration Release \
  -p:ValheimManagedDir="$VALHEIM_MANAGED_DIR" \
  -p:BepInExCoreDir="$BEPINEX_CORE_DIR"
