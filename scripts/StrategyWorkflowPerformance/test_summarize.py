"""Regression checks for evidence math and failure reporting; no external services."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("workflow_report", Path(__file__).with_name("summarize.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)


class ReportTests(unittest.TestCase):
    def row(self, **values):
        return dict(RecordType="sample", RunId="run", SampleId="sample", Scenario="Daily/LongFuture",
                    Phase="warm", Success=True, AuthorizedMilliseconds=100,
                    _File="evidence.jsonl", _Label="observed", **values)

    def sample(self, **values):
        row = self.row()
        row.update(values)
        return reporter.normalize_sample(row, 1, [])

    def run_report(self, directory, records, *extra):
        source = Path(directory) / "samples.jsonl"
        source.write_text("\n".join(json.dumps(row) for row in records) + "\n", encoding="utf-8")
        output = Path(directory) / "report"
        arguments = ["summarize.py", "--input", str(source), "--output", str(output), *extra]
        with patch.object(sys, "argv", arguments), contextlib.redirect_stdout(io.StringIO()):
            status = reporter.main()
        return status, json.loads((output / "report.json").read_text(encoding="utf-8")), (output / "report.md").read_text(encoding="utf-8")

    def test_percentile_uses_documented_interpolation_and_empty_is_missing(self):
        summary = reporter.stats([1, 2, 3, 4])
        self.assertEqual(2.5, summary["Median"])
        self.assertAlmostEqual(3.85, summary["P95"])
        self.assertEqual(2.5, summary["Mean"])
        self.assertEqual(1, summary["Minimum"])
        self.assertEqual(4, summary["Maximum"])
        self.assertEqual(0, reporter.stats([])["Count"])
        self.assertIsNone(reporter.stats([])["Median"])
        self.assertEqual(7, reporter.percentile([7], .95))

    def test_failure_and_missing_measurement_do_not_pollute_success_distribution(self):
        samples = [self.sample(SampleId="one", AuthorizedMilliseconds=10),
                   self.sample(SampleId="two", Success=False, AuthorizedMilliseconds=1000),
                   self.sample(SampleId="three", AuthorizedMilliseconds=None),
                   self.sample(SampleId="four", Phase="cold", AuthorizedMilliseconds=50)]
        summaries, _ = reporter.group_samples(samples)
        warm = next(s for s in summaries if s["Phase"] == "warm")
        cold = next(s for s in summaries if s["Phase"] == "cold")
        self.assertEqual((3, 2, 1, 1, 1, 10), tuple(warm[k] for k in
                         ("Attempted", "Successful", "Failed", "Count", "MissingOrInvalidSuccessful", "Median")))
        self.assertEqual(50, cold["Median"])

    def test_nonfinite_negative_boolean_and_missing_success_are_not_valid_measurements(self):
        for value in (float("nan"), float("inf"), "NaN", -1, True, "garbage"):
            self.assertIsNone(reporter.finite(value))
        issues = []
        row = self.row()
        row.update(Success="true", AuthorizedMilliseconds=float("inf"),
                   ProcessMetrics={"CpuMilliseconds": -1})
        normalized = reporter.normalize_sample(row, 1, issues)
        self.assertFalse(normalized["Success"])
        self.assertIsNone(normalized["AuthorizedMilliseconds"])
        self.assertIsNone(normalized["ProcessCpuMilliseconds"])
        self.assertEqual(3, len(issues))

    def test_bootstrap_is_deterministic_and_does_not_estimate_one_sample_confidence(self):
        first = reporter.bootstrap_reduction([90, 100, 110], [45, 50, 55], 200, 42)
        self.assertEqual(first, reporter.bootstrap_reduction([90, 100, 110], [45, 50, 55], 200, 42))
        self.assertGreater(first[0], 0)
        self.assertEqual((None, None), reporter.bootstrap_reduction([100], [50], 200, 42))
        self.assertEqual((None, None), reporter.bootstrap_reduction([0, 0], [0, 0], 200, 42))

    def test_comparison_matches_phase_and_metric_instead_of_mixing_boundaries(self):
        samples = [self.sample(_Label="baseline", AuthorizedMilliseconds=100),
                   self.sample(_Label="candidate", AuthorizedMilliseconds=80),
                   self.sample(_Label="candidate", Phase="trace", AuthorizedMilliseconds=900),
                   self.sample(_Label="candidate", Phase="cold", AuthorizedMilliseconds=1000)]
        _, populations = reporter.group_samples(samples)
        comparisons = reporter.compare(populations, 100, 42)
        self.assertEqual(1, len(comparisons))
        self.assertEqual(20, comparisons[0]["MedianReductionPercent"])
        self.assertEqual("warm", comparisons[0]["Phase"])
        self.assertEqual("insufficient samples", comparisons[0]["Evidence"])

    def test_stage_duplicates_cannot_inflate_sample_count(self):
        stages = [{"StageCount": 1, "Endpoint": "Regime", "Milliseconds": 30},
                  {"StageCount": 1, "Endpoint": "Regime", "Milliseconds": 30}]
        summaries, _ = reporter.group_samples([self.sample(StageAcceptedMilliseconds=stages)])
        stage = next(s for s in summaries if s["Metric"] == "CumulativeStage1Milliseconds")
        self.assertEqual(1, stage["Count"])
        self.assertEqual(0, stage["MissingOrInvalidSuccessful"])

    def test_overlapping_children_are_unioned_and_clipped_to_authorization(self):
        sample = self.sample(TraceId="trace", StartedAtUtc="2026-09-09T14:00:00Z", AuthorizedMilliseconds=100)
        def span(identity, parent, start_ms, duration, operation):
            return dict(_Label="observed", _File="evidence.jsonl", RunId="run", SampleId="sample",
                        TraceId="trace", SpanId=identity, ParentSpanId=parent,
                        StartTimeUtc=f"2026-09-09T14:00:00.{start_ms:03d}Z", Milliseconds=duration,
                        Operation=operation)
        rows = [span("root", "0000000000000000", 0, 200, "root"),
                span("a", "root", 10, 70, "io"), span("b", "root", 50, 80, "io"),
                span("grand", "a", 30, 30, "decode"), span("outside", "root", 180, 15, "later")]
        issues = []
        traces, operations = reporter.analyze_traces(rows, [sample], issues)
        self.assertEqual([], issues)
        self.assertEqual(100, traces[0]["WindowMilliseconds"])
        self.assertEqual(100, traces[0]["CoveredMilliseconds"])
        self.assertEqual(0, traces[0]["UncoveredMilliseconds"])
        self.assertEqual(90, traces[0]["NestedSpanCoverageMilliseconds"])
        by_operation = {row["Operation"]: row for row in operations}
        self.assertEqual(10, by_operation["root"]["ExclusiveOfDirectChildrenMilliseconds"])
        self.assertEqual(120, by_operation["io"]["InclusiveMilliseconds"])
        self.assertEqual(90, by_operation["io"]["WallCoverageMilliseconds"])
        self.assertEqual(90, by_operation["io"]["ExclusiveOfDirectChildrenMilliseconds"])
        self.assertEqual(0, by_operation["later"]["WallCoverageMilliseconds"])

    def test_missing_trace_parent_is_visible_and_does_not_invent_exclusive_child(self):
        row = dict(_Label="observed", _File="evidence.jsonl", RunId="run", SampleId="sample",
                   TraceId="trace", SpanId="child", ParentSpanId="missing", Operation="work",
                   StartTimeUtc="2026-09-09T14:00:00Z", Milliseconds=20)
        traces, operations = reporter.analyze_traces([row], [], [])
        self.assertEqual(1, traces[0]["MissingParentCount"])
        self.assertEqual("first to last span (not workflow latency)", traces[0]["WindowBoundary"])
        self.assertEqual(20, operations[0]["ExclusiveOfDirectChildrenMilliseconds"])

    def test_run_failed_marks_incomplete_even_when_existing_samples_succeeded(self):
        records = [self.row(), {"RecordType": "run_failed", "RunId": "run", "CompletedIterations": 1,
                               "Scenario": "next", "Phase": "warm", "Error": "Setup disconnected"}]
        with tempfile.TemporaryDirectory() as directory:
            status, report, markdown = self.run_report(directory, records)
        self.assertEqual(1, status)
        self.assertTrue(report["Samples"][0]["Success"])
        self.assertEqual(1, len(report["RunOutcomes"]))
        self.assertIn("Incomplete or failed runs: 1", markdown)
        self.assertIn("Setup disconnected", markdown)

    def test_trx_constructor_failure_survives_without_stdout_or_metadata(self):
        content = '''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>
        <UnitTestResult executionId="failed-constructor" testName="WorkflowPerformance" outcome="Failed">
        <Output><ErrorInfo><Message>Redis unavailable during fixture construction</Message>
        <StackTrace>Fixture.Start()</StackTrace></ErrorInfo></Output></UnitTestResult></Results></TestRun>'''
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "run.trx", Path(directory) / "report"
            source.write_text(content, encoding="utf-8")
            source.with_name("run.json").write_text(json.dumps({
                "Commit": "startup-commit", "BuildConfiguration": "Release", "DatabasePolicy": "retained test database"
            }), encoding="utf-8")
            with patch.object(sys, "argv", ["summarize.py", "--input", str(source), "--output", str(output)]), contextlib.redirect_stdout(io.StringIO()):
                status = reporter.main()
            report = json.loads((output / "report.json").read_text(encoding="utf-8"))
            markdown = (output / "report.md").read_text(encoding="utf-8")
        self.assertEqual(1, status)
        self.assertEqual(1, len(report["Samples"]))
        self.assertFalse(report["Samples"][0]["Success"])
        self.assertEqual("runner", report["Samples"][0]["Phase"])
        self.assertEqual(1, report["Summaries"][0]["Failed"])
        self.assertEqual(0, report["Summaries"][0]["Count"])
        self.assertIn("Redis unavailable", markdown)
        self.assertEqual("startup-commit", report["Metadata"][0]["Commit"])
        self.assertEqual("retained test database", report["Metadata"][0]["Conditions"])

    def test_metadata_differences_and_missing_commit_are_reported(self):
        metadata = [dict(_Label="baseline", BuildConfiguration="Release", Conditions="baseline conditions"),
                    dict(_Label="candidate", Commit="abc", BuildConfiguration="Debug", Conditions="candidate conditions")]
        issues = []
        reporter.metadata_warnings(metadata, issues)
        self.assertTrue(any("differs for BuildConfiguration" in issue for issue in issues))
        self.assertTrue(any("differs for Conditions" in issue for issue in issues))
        self.assertTrue(any("baseline: commit metadata missing" in issue for issue in issues))

    def test_malformed_line_and_duplicate_sample_are_not_silently_counted(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "samples.jsonl", Path(directory) / "report"
            source.write_text(json.dumps(self.row()) + "\n{broken\n" + json.dumps(self.row()) + "\n", encoding="utf-8")
            with patch.object(sys, "argv", ["summarize.py", "--input", str(source), "--output", str(output)]), contextlib.redirect_stdout(io.StringIO()):
                reporter.main()
            report = json.loads((output / "report.json").read_text(encoding="utf-8"))
        self.assertEqual(1, len(report["Samples"]))
        self.assertTrue(any("invalid JSONL row" in issue for issue in report["Issues"]))
        self.assertTrue(any("Duplicate sample" in issue for issue in report["Issues"]))

    def test_zero_sample_run_failure_still_writes_actionable_report(self):
        with tempfile.TemporaryDirectory() as directory:
            status, report, markdown = self.run_report(directory, [
                {"RecordType": "run_failed", "RunId": "empty", "Error": "Host startup failed"}])
        self.assertEqual(2, status)
        self.assertEqual([], report["Samples"])
        self.assertIn("Host startup failed", markdown)
        self.assertIn("Incomplete or failed runs: 1", markdown)

    def test_missing_completion_marker_is_incomplete_and_excluded(self):
        with tempfile.TemporaryDirectory() as directory:
            status, report, markdown = self.run_report(directory, [self.row()])
        self.assertEqual(1, status)
        self.assertFalse(report["Samples"][0]["ComparisonEligible"])
        self.assertEqual("run_incomplete", report["RunOutcomes"][0]["RecordType"])
        self.assertIn("No run_completed", markdown)

    def test_matching_completion_marker_makes_evidence_comparison_eligible(self):
        with tempfile.TemporaryDirectory() as directory:
            status, report, _ = self.run_report(directory, [self.row(),
                {"RecordType": "run_completed", "RunId": "run", "CompletedIterations": 1}])
        self.assertEqual(0, status)
        self.assertTrue(report["Samples"][0]["ComparisonEligible"])

    def test_mismatched_completion_count_is_incomplete(self):
        with tempfile.TemporaryDirectory() as directory:
            status, report, markdown = self.run_report(directory, [self.row(),
                {"RecordType": "run_completed", "RunId": "run", "CompletedIterations": 2}])
        self.assertEqual(1, status)
        self.assertFalse(report["Samples"][0]["ComparisonEligible"])
        self.assertIn("does not match 1 captured sample", markdown)

    def test_failed_batch_successes_never_enter_comparisons(self):
        with tempfile.TemporaryDirectory() as directory:
            baseline, candidate = Path(directory) / "baseline.jsonl", Path(directory) / "candidate.jsonl"
            baseline.write_text(json.dumps(self.row()) + "\n" + json.dumps({
                "RecordType": "run_failed", "RunId": "run", "Error": "Next fixture failed"}) + "\n", encoding="utf-8")
            candidate.write_text(json.dumps(self.row()) + "\n" + json.dumps({
                "RecordType": "run_completed", "RunId": "run", "CompletedIterations": 1}) + "\n", encoding="utf-8")
            output = Path(directory) / "report"
            with patch.object(sys, "argv", ["summarize.py", "--baseline", str(baseline), "--candidate", str(candidate), "--output", str(output)]), contextlib.redirect_stdout(io.StringIO()):
                status = reporter.main()
            report = json.loads((output / "report.json").read_text(encoding="utf-8"))
        self.assertEqual(1, status)
        self.assertEqual([], report["Comparisons"])
        self.assertEqual(2, len(report["Samples"]))
        self.assertTrue(all(s["Success"] for s in report["Samples"]))
        self.assertFalse(next(s for s in report["Samples"] if s["Label"] == "baseline")["ComparisonEligible"])

    def test_completed_marker_cannot_hide_failed_sample(self):
        row = self.row()
        row["Success"] = False
        with tempfile.TemporaryDirectory() as directory:
            status, report, _ = self.run_report(directory, [row,
                {"RecordType": "run_completed", "RunId": "run", "CompletedIterations": 1}])
        self.assertEqual(1, status)
        self.assertFalse(report["Samples"][0]["ComparisonEligible"])


if __name__ == "__main__":
    unittest.main()
