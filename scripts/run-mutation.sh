#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

dotnet tool restore

CONFIG="${STRYKER_CONFIG:-$ROOT/stryker-config.json}"
dotnet tool run dotnet-stryker -- --config-file "$CONFIG" "$@"
