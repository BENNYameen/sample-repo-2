#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/run-coverage.sh [label]
# Runs Release tests with Coverlet MSBuild (see WhiteboxMetrix.Tests.csproj) and copies
# the deterministic Cobertura file to artifacts/coverage/baselines/ for delta baselines.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

LABEL="${1:-snapshot}"
OUT_DIR="artifacts/coverage/baselines"
COV="${ROOT}/TestResults/coverage/coverage.cobertura.xml"

mkdir -p "$OUT_DIR"
dotnet build WhiteboxMetrix.sln -c Release --nologo
dotnet test WhiteboxMetrix.sln -c Release --no-build --nologo -v minimal

if [[ ! -f "$COV" ]]; then
  echo "Expected Cobertura missing: $COV (Coverlet MSBuild must run on the test project)." >&2
  exit 1
fi

DEST="${OUT_DIR}/coverage-${LABEL}.cobertura.xml"
cp "$COV" "$DEST"
echo "Saved: $DEST"
python3 "${ROOT}/scripts/report_cobertura_summary.py" "$DEST"
