#!/usr/bin/env python3
"""Lightweight structural checks for benchmark/*Manifest*.json (no dotnet required)."""

from __future__ import annotations

import json
import sys
from pathlib import Path

_SCRIPTS = Path(__file__).resolve().parent
if str(_SCRIPTS) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS))

import churn_helpers  # noqa: E402


def die(msg: str) -> None:
    print(msg, file=sys.stderr)


def load_json(path: Path) -> object:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def validate_all_definitions(data: dict, source: Path) -> list[str]:
    errs: list[str] = []
    if "definitions" not in data or not isinstance(data["definitions"], list):
        errs.append(f"{source}: missing non-empty definitions[]")
        return errs
    for i, row in enumerate(data["definitions"]):
        if not isinstance(row, dict):
            errs.append(f"{source}[{i}] not object")
            continue
        for k in ("id", "path", "symbol", "role"):
            if not row.get(k):
                errs.append(f"{source}[{i}] missing {k}")
    return errs


def validate_all_uses(data: dict, source: Path) -> list[str]:
    errs: list[str] = []
    uses = data.get("uses")
    if not isinstance(uses, list) or len(uses) == 0:
        errs.append(f"{source}: uses[] required")
        return errs
    for i, row in enumerate(uses):
        if not isinstance(row, dict):
            errs.append(f"{source}.uses[{i}] not object")
            continue
        for k in ("id", "path", "useDescription", "exercisedBy"):
            if k == "exercisedBy":
                eb = row.get(k)
                if not isinstance(eb, list) or len(eb) == 0 or not all(
                    isinstance(x, str) and x.strip() for x in eb
                ):
                    errs.append(f"{source}.uses[{i}].exercisedBy invalid")
                continue
            if not row.get(k):
                errs.append(f"{source}.uses[{i}] missing {k}")
    return errs


def validate_code_churn(data: dict, source: Path, definition_ids: set[str]) -> list[str]:
    errs: list[str] = []
    hs = data.get("hotspots")
    if not isinstance(hs, list) or len(hs) == 0:
        errs.append(f"{source}: hotspots[] required")
        return errs
    for i, row in enumerate(hs):
        if not isinstance(row, dict):
            errs.append(f"{source}.hotspots[{i}] not object")
            continue
        if not row.get("path") or not row.get("rationale"):
            errs.append(f"{source}.hotspots[{i}] missing path or rationale")
        for aid in row.get("allUsesAnchorIds") or []:
            if aid not in definition_ids:
                errs.append(f"{source}.hotspots[{i}] unknown anchor '{aid}'")
    return errs


def _benchmark_root() -> Path:
    return Path(__file__).resolve().parent.parent / "benchmark"


def main() -> int:
    root = _benchmark_root()
    if not root.is_dir():
        die(f"missing benchmark directory: {root}")
        return 2

    manifest_paths = sorted(
        p for p in root.glob("*Manifest*.json") if not p.name.endswith(".schema.json")
    )
    if len(manifest_paths) < 3:
        die(f"expected at least three manifests under {root}")
        return 2

    defs_path = root / "AllDefinitionsManifest.json"
    if defs_path not in manifest_paths:
        die(f"missing {defs_path.name}")
        return 2

    definition_ids = {
        row["id"] for row in load_json(defs_path)["definitions"] if isinstance(row, dict)
    }

    all_errs: list[str] = []
    for path in manifest_paths:
        data = load_json(path)
        if not isinstance(data, dict) or data.get("version") is None:
            all_errs.append(f"{path}: missing version/object root")
            continue

        stem = path.stem
        if stem == "AllDefinitionsManifest":
            all_errs.extend(validate_all_definitions(data, path))
        elif stem == "AllUsesManifest":
            all_errs.extend(validate_all_uses(data, path))
        elif stem == "CodeChurnManifest":
            all_errs.extend(validate_code_churn(data, path, definition_ids))
        else:
            die(f"(warn) unrecognized manifest skipped: {path.name}")

    if all_errs:
        die("Benchmark manifest validation failed:")
        for e in all_errs:
            die(f"  - {e}")
        return 1
    print("benchmark manifests OK")
    churn_bundle = churn_helpers.emit_complete_churn_evaluation(
        cobertura_path=None,
        line_rate=None,
        branch_rate=None,
        benchmark_root=root,
    )
    sys.stdout.write(churn_helpers.format_churn_report(churn_bundle))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
