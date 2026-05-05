#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/run-coverage.sh [label]
# Produces artifacts/coverage/baselines/coverage-<label>.cobertura.xml for delta tooling.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

LABEL="${1:-snapshot}"
RAW="artifacts/coverage-raw/${LABEL}"
OUT_DIR="artifacts/coverage/baselines"
SETTINGS="${ROOT}/CodeCoverage.runsettings"

mkdir -p "$OUT_DIR"
dotnet build WhiteboxMetrix.sln -c Release --nologo
dotnet test WhiteboxMetrix.sln -c Release --no-build --nologo \
    --settings "$SETTINGS" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$RAW"

shopt -s nullglob
FILES=("$RAW"/*/coverage.cobertura.xml)
if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "No coverage.cobertura.xml under $RAW — check test run output." >&2
  exit 1
fi

DEST="${OUT_DIR}/coverage-${LABEL}.cobertura.xml"
cp "${FILES[0]}" "$DEST"
echo "Saved: $DEST"
