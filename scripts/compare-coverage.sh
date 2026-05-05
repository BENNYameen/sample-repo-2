#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/compare-coverage.sh baseline.cobertura.xml current.cobertura.xml
# Requires: dotnet tool restore (ReportGenerator).

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

dotnet tool restore

BASE="${1:?first arg: baseline cobertura path}"
CUR="${2:?second arg: current cobertura path}"
REPORT_DIR="artifacts/coverage/delta-report"

mkdir -p "$REPORT_DIR"

dotnet tool run reportgenerator -- \
    -reports:"$BASE;$CUR" \
    -targetdir:"$REPORT_DIR" \
    -reporttypes:'TextDelta;Html' \
    -verbosity:Info

echo "Open $REPORT_DIR/index.html (Html) or see TextDelta in console output above."
