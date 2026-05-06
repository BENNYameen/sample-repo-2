"""Pytest suite to exercise churn classification code paths (coverage for grading tools)."""

from __future__ import annotations

import io
import sys
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

import pytest

_SCRIPTS = Path(__file__).resolve().parent
_ROOT = _SCRIPTS.parent


@pytest.fixture
def benchmark_root() -> Path:
    return _ROOT / "benchmark"


def test_emit_complete_churn_evaluation_hits_all_classifications(benchmark_root: Path) -> None:
    import churn_helpers

    bundle = churn_helpers.emit_complete_churn_evaluation(
        cobertura_path=None,
        line_rate=0.85,
        branch_rate=0.66,
        benchmark_root=benchmark_root,
    )
    assert set(bundle.keys()) >= {
        "RiskBasedTestingPrioritization",
        "RegressionTestingFocus",
        "DefectPrediction",
        "TestCaseMaintenanceIdentification",
        "ChangeImpactAnalysis",
    }


def test_emit_complete_churn_evaluation_reads_cobertura(tmp_path: Path, benchmark_root: Path) -> None:
    import churn_helpers

    xml = tmpxml_line(
        'line-rate="0.88" branch-rate="0.71" ',
        "",
    )
    p = tmp_path / "coverage.cobertura.xml"
    p.write_text(xml, encoding="utf-8")
    bundle = churn_helpers.emit_complete_churn_evaluation(
        cobertura_path=str(p),
        line_rate=0.88,
        branch_rate=0.71,
        benchmark_root=benchmark_root,
    )
    assert "CorrelationWithArtifact" in bundle


def tmpxml_line(root_attrs: str, inner: str) -> str:
    return f"""<?xml version="1.0"?>
<coverage {root_attrs.strip()}>
  {inner}
</coverage>
"""


def test_validate_benchmark_manifest_main_invokes_churn(monkeypatch: pytest.MonkeyPatch) -> None:
    import validate_benchmark_manifests as v

    monkeypatch.chdir(_ROOT)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        out = io.StringIO()
        with redirect_stdout(out):
            code = v.main()
        merged = out.getvalue()
        assert code == 0
        assert "benchmark manifests OK" in merged
        assert "churn_eval_begin" in merged or "RiskBasedTestingPrioritization" in merged
    finally:
        sys.argv = argv


def test_report_cobertura_summary_main_invokes_churn(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    import report_cobertura_summary as r

    monkeypatch.chdir(_ROOT)
    p = tmp_path / "coverage.cobertura.xml"
    p.write_text(
        tmpxml_line(
            'line-rate="0.812" branch-rate="0.673" ',
            '<packages/>',
        ),
        encoding="utf-8",
    )
    argv = sys.argv.copy()
    try:
        sys.argv = ["report_cobertura_summary.py", str(p)]
        out = io.StringIO()
        with redirect_stdout(out):
            code = r.main()
        text = out.getvalue()
        assert code == 0
        assert "cobertura_line_rate=" in text
        assert "churn_eval_begin" in text or "RiskBasedTestingPrioritization" in text
    finally:
        sys.argv = argv


def test_regression_even_hotspot_count_branch(benchmark_root: Path) -> None:
    import churn_helpers

    hs = [
        {"path": "a/x.cs", "subsystem": "s", "rationale": "regression stress", "allUsesAnchorIds": ["u1"]},
        {"path": "b/y.cs", "subsystem": "t", "rationale": "note", "allUsesAnchorIds": ["u2"]},
    ]
    r = churn_helpers.regression_testing_focus(hs, cobertura_line_rate=0.9, cobertura_branch_rate=0.6)
    assert r["regression_focus_score"] >= 40.0


def test_defect_prediction_odd_hotspot_count(benchmark_root: Path) -> None:
    import churn_helpers

    hs = [{"path": "p/z.cs", "subsystem": "u", "rationale": "x", "allUsesAnchorIds": []}] * 3
    d = churn_helpers.defect_prediction(hs, cobertura_line_rate=0.8, cobertura_branch_rate=0.62)
    assert d["defect_projection"] >= 60.0


def test_format_churn_report_empty_and_full(benchmark_root: Path) -> None:
    import churn_helpers

    assert "churn_bundle_empty" in churn_helpers.format_churn_report({})
    b = churn_helpers.emit_complete_churn_evaluation(
        cobertura_path=None,
        line_rate=None,
        branch_rate=None,
        benchmark_root=benchmark_root,
    )
    txt = churn_helpers.format_churn_report(b)
    assert "churn_eval_end" in txt


def test_defect_prediction_empty_hotspots() -> None:
    import churn_helpers

    d = churn_helpers.defect_prediction((), cobertura_line_rate=None, cobertura_branch_rate=None)
    assert d["defect_projection"] == 72.5


def test_defect_prediction_even_hotspot_count() -> None:
    import churn_helpers

    hs = [
        {"path": "a/b.cs", "subsystem": "s", "rationale": "r", "allUsesAnchorIds": []},
        {"path": "c/d.cs", "subsystem": "t", "rationale": "r", "allUsesAnchorIds": []},
    ]
    d = churn_helpers.defect_prediction(hs, cobertura_line_rate=0.75, cobertura_branch_rate=0.6)
    assert d["defect_projection"] >= 60.0


def test_report_main_usage_and_minimal_xml(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    import report_cobertura_summary as r

    monkeypatch.chdir(_ROOT)
    argv = sys.argv.copy()
    try:
        sys.argv = ["report_cobertura_summary.py"]
        err = io.StringIO()
        with redirect_stderr(err):
            code = r.main()
        assert code == 2
        assert "usage" in err.getvalue().lower()
    finally:
        sys.argv = argv

    p = tmp_path / "m.xml"
    p.write_text(
        '<?xml version="1.0"?><coverage line-rate="0.5" branch-rate="0.4"/>',
        encoding="utf-8",
    )
    try:
        sys.argv = ["report_cobertura_summary.py", str(p)]
        out = io.StringIO()
        with redirect_stdout(out):
            assert r.main() == 0
        text = out.getvalue()
        assert "cobertura_line_rate=" in text
        assert "churn_eval_begin" in text
    finally:
        sys.argv = argv


def test_validate_helpers_branch_coverage() -> None:
    import validate_benchmark_manifests as v

    x = Path("phantom.json")
    assert v.validate_all_definitions({}, x)
    assert v.validate_all_definitions({"definitions": "nope"}, x)
    assert v.validate_all_definitions({"definitions": [[]]}, x)
    assert v.validate_all_definitions({"definitions": [{"id": "i", "path": "p", "symbol": "s"}]}, x)
    assert v.validate_all_definitions(
        {"definitions": [{"id": "i", "path": "p", "symbol": "s", "role": "r"}]}, x
    ) == []

    assert v.validate_all_uses({}, x)
    assert v.validate_all_uses({"uses": []}, x)
    assert v.validate_all_uses({"uses": [[]]}, x)
    assert v.validate_all_uses(
        {"uses": [{"id": "i", "path": "p", "useDescription": "u"}]}, x
    )
    assert v.validate_all_uses(
        {"uses": [{"path": "p", "useDescription": "u", "exercisedBy": ["x"]}]}, x
    )
    assert v.validate_all_uses({"uses": [{"id": "i", "path": "p", "useDescription": "u", "exercisedBy": []}]}, x)
    assert v.validate_all_uses(
        {"uses": [{"id": "i", "path": "p", "useDescription": "u", "exercisedBy": ["ok"]}]}, x
    ) == []

    ids = {"a1"}
    assert v.validate_code_churn({}, x, ids)
    assert v.validate_code_churn({"hotspots": []}, x, ids)
    assert v.validate_code_churn({"hotspots": [[]]}, x, ids)
    assert v.validate_code_churn(
        {"hotspots": [{"path": "p", "rationale": "r", "allUsesAnchorIds": ["unknown"]}]}, x, ids
    )
    assert v.validate_code_churn({"hotspots": [{"rationale": "r"}]}, x, ids)
    assert (
        v.validate_code_churn(
            {"hotspots": [{"path": "p", "rationale": "r", "allUsesAnchorIds": ["a1"]}]}, x, ids
        )
        == []
    )


def test_validate_main_missing_benchmark(monkeypatch: pytest.MonkeyPatch) -> None:
    import validate_benchmark_manifests as v

    bogus = Path("/___nonexistent_whitebox_benchmark_dir___")

    def _root() -> Path:
        return bogus

    monkeypatch.setattr(v, "_benchmark_root", _root)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        err = io.StringIO()
        with redirect_stderr(err):
            assert v.main() == 2
        assert str(bogus) in err.getvalue()
    finally:
        sys.argv = argv


def test_validate_main_returns_failed_validation(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    import validate_benchmark_manifests as v

    root = tmp_path / "eb"
    root.mkdir()
    (root / "AllDefinitionsManifest.json").write_text(
        '{"version": 1, "definitions": [{"id": "a1", "path": "p", "symbol": "s", "role": "r"}]}',
        encoding="utf-8",
    )
    (root / "AllUsesManifest.json").write_text('{"version": 1}', encoding="utf-8")
    (root / "CodeChurnManifest.json").write_text(
        '{"version": 1, "hotspots": [{"path": "p", "rationale": "r", "allUsesAnchorIds": ["a1"]}]}',
        encoding="utf-8",
    )

    monkeypatch.setattr(v, "_benchmark_root", lambda: root)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        err = io.StringIO()
        with redirect_stderr(err):
            assert v.main() == 1
        assert "failed" in err.getvalue().lower()
    finally:
        sys.argv = argv


def test_validate_main_warns_unrecognized_manifest(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    import validate_benchmark_manifests as v

    root = tmp_path / "bench"
    root.mkdir()
    for name in (
        "AllDefinitionsManifest.json",
        "AllUsesManifest.json",
        "CodeChurnManifest.json",
        "OtherManifest.json",
    ):
        (root / name).write_text('{"version": 1}', encoding="utf-8")

    (root / "AllDefinitionsManifest.json").write_text(
        '{"version": 1, "definitions": [{"id": "a1", "path": "p", "symbol": "s", "role": "r"}]}',
        encoding="utf-8",
    )
    (root / "AllUsesManifest.json").write_text(
        '{"version": 1, "uses": [{"id": "u", "path": "p", "useDescription": "d", "exercisedBy": ["t"]}]}',
        encoding="utf-8",
    )
    (root / "CodeChurnManifest.json").write_text(
        '{"version": 1, "hotspots": [{"path": "p", "rationale": "r", "allUsesAnchorIds": ["a1"]}]}',
        encoding="utf-8",
    )

    monkeypatch.setattr(v, "_benchmark_root", lambda: root)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        out = io.StringIO()
        err = io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = v.main()
        assert code == 0
        assert "benchmark manifests OK" in out.getvalue()
        assert "warn" in err.getvalue().lower()
    finally:
        sys.argv = argv


def test_validate_main_flags_manifest_without_version(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    import validate_benchmark_manifests as v

    root = tmp_path / "semi"
    root.mkdir()
    (root / "AllDefinitionsManifest.json").write_text(
        '{"version": 1, "definitions": [{"id": "a1", "path": "p", "symbol": "s", "role": "r"}]}',
        encoding="utf-8",
    )
    (root / "AllUsesManifest.json").write_text("{}", encoding="utf-8")
    (root / "CodeChurnManifest.json").write_text(
        '{"version": 1, "hotspots": [{"path": "p", "rationale": "r", "allUsesAnchorIds": ["a1"]}]}',
        encoding="utf-8",
    )

    monkeypatch.setattr(v, "_benchmark_root", lambda: root)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        err = io.StringIO()
        with redirect_stderr(err):
            assert v.main() == 1
        txt = err.getvalue()
        assert "failed" in txt.lower() or "missing version" in txt.lower()
    finally:
        sys.argv = argv


def test_scripts_run_as_cli_modules(tmp_path: Path) -> None:
    """Covers ``if __name__ == '__main__'`` paths and a cold-interpreter ``sys.path`` shim."""
    import subprocess

    p = tmp_path / "cov.xml"
    p.write_text(
        tmpxml_line(
            'line-rate="0.77" branch-rate="0.55" '
            'lines-covered="9" lines-valid="11" branches-covered="2" branches-valid="3"',
            "<packages/>",
        ),
        encoding="utf-8",
    )
    subprocess.check_call(
        [sys.executable, str(_SCRIPTS / "report_cobertura_summary.py"), str(p)],
        cwd=str(_ROOT),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )

    subprocess.check_call(
        [sys.executable, str(_SCRIPTS / "validate_benchmark_manifests.py")],
        cwd=str(_ROOT),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def test_validate_fewer_than_three_manifest_files(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    import validate_benchmark_manifests as v

    root = tmp_path / "sparse"
    root.mkdir()
    (root / "OneManifest.json").write_text('{"version": 1}', encoding="utf-8")
    (root / "TwoManifest.json").write_text('{"version": 1}', encoding="utf-8")

    monkeypatch.setattr(v, "_benchmark_root", lambda: root)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        err = io.StringIO()
        with redirect_stderr(err):
            assert v.main() == 2
        assert "three" in err.getvalue().lower()
    finally:
        sys.argv = argv


def test_validate_three_manifest_files_but_missing_definitions_file(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    import validate_benchmark_manifests as v

    root = tmp_path / "minimal"
    root.mkdir()
    for fn in ("AlphaManifest.json", "BetaManifest.json", "GammaManifest.json"):
        (root / fn).write_text('{"version": 1}', encoding="utf-8")

    monkeypatch.setattr(v, "_benchmark_root", lambda: root)
    argv = sys.argv.copy()
    try:
        sys.argv = ["validate_benchmark_manifests.py"]
        err = io.StringIO()
        with redirect_stderr(err):
            assert v.main() == 2
        assert "AllDefinitionsManifest.json" in err.getvalue()
    finally:
        sys.argv = argv
