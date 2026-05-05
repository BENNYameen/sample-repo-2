#!/usr/bin/env python3
"""Emit Cobertura line-rate / branch-rate for gates and baseline records."""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: report_cobertura_summary.py <coverage.cobertura.xml>", file=sys.stderr)
        return 2
    path = sys.argv[1]
    root = ET.parse(path).getroot()
    lr = float(root.attrib.get("line-rate", "0"))
    br = float(root.attrib.get("branch-rate", "0"))
    lines_covered = root.attrib.get("lines-covered", "")
    lines_valid = root.attrib.get("lines-valid", "")
    branches_covered = root.attrib.get("branches-covered", "")
    branches_valid = root.attrib.get("branches-valid", "")
    print(f"cobertura_line_rate={lr:.6f}")
    print(f"cobertura_branch_rate={br:.6f}")
    if lines_valid:
        print(f"cobertura_lines_covered={lines_covered}/{lines_valid}")
    if branches_valid:
        print(f"cobertura_branches_covered={branches_covered}/{branches_valid}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
