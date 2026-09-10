#!/usr/bin/env python3
"""Summarize observed workflow latency; uses only the Python standard library.

New JSONL evidence is preferred. Legacy TRX rows are accepted for inspection, but
their polling/read timing boundary is intentionally in a different metric.
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
import random
import statistics
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict


TIMING_METRICS = (
    "AuthorizedMilliseconds",
    "QueryVisibleMilliseconds",
    "VerificationMilliseconds",
    "WorkflowMilliseconds",
)
PROCESS_METRICS = ("AllocatedBytes", "CpuMilliseconds", "Gen0Collections", "Gen1Collections",
                   "Gen2Collections", "WorkingSetBytes", "PrivateMemoryBytes", "ManagedHeapBytes")
METRICS = (*TIMING_METRICS, *(f"Process{name}" for name in PROCESS_METRICS))
PHASES = ("cold", "warmup", "warm", "trace", "legacy", "runner", "unspecified")


def field(row, key, default=None):
    if key in row:
        return row[key]
    target = key.casefold()
    return next((v for k, v in row.items() if k.casefold() == target), default)


def finite(value):
    if value is None or isinstance(value, bool):
        return None
    try:
        result = float(value)
        return result if math.isfinite(result) and result >= 0 else None
    except (ValueError, TypeError, OverflowError):
        return None


def json_safe(value):
    """Keep raw evidence serializable even when JSON numeric overflow occurs."""
    if isinstance(value, float) and not math.isfinite(value):
        return None
    if isinstance(value, dict):
        return {key: json_safe(item) for key, item in value.items()}
    if isinstance(value, list):
        return [json_safe(item) for item in value]
    return value


def percentile(values, probability):
    """Linear interpolation at (n-1)*p, including n=1."""
    ordered = sorted(values)
    if not ordered:
        return None
    index = (len(ordered) - 1) * probability
    lo, hi = math.floor(index), math.ceil(index)
    return ordered[lo] + (ordered[hi] - ordered[lo]) * (index - lo)


def stats(values):
    if not values:
        return dict(Count=0, Median=None, P95=None, Mean=None, Minimum=None,
                    Maximum=None, StandardDeviation=None)
    return dict(Count=len(values), Median=statistics.median(values),
                P95=percentile(values, .95), Mean=statistics.mean(values),
                Minimum=min(values), Maximum=max(values),
                StandardDeviation=statistics.stdev(values) if len(values) > 1 else None)


def parse_timestamp(value):
    if not isinstance(value, str):
        return None
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
        # Activity.StartTimeUtc is UTC. Reject missing zone rather than using a
        # workstation's local timezone and silently shifting trace windows.
        if parsed.tzinfo is None:
            return None
        return parsed.timestamp() * 1000
    except (ValueError, OverflowError):
        return None


def merge_intervals(intervals):
    result = []
    for start, end in sorted(intervals):
        if end <= start:
            continue
        if result and start <= result[-1][1]:
            result[-1] = (result[-1][0], max(end, result[-1][1]))
        else:
            result.append((start, end))
    return result


def union_duration(intervals):
    return sum(end - start for start, end in merge_intervals(intervals))


def clipped(interval, window):
    return (max(interval[0], window[0]), min(interval[1], window[1]))


def load_file(path, label, issues):
    rows = []
    if path.suffix.lower() == ".trx":
        legacy_timings = False
        try:
            tree = ET.parse(path)
        except (OSError, ET.ParseError) as exc:
            issues.append(f"{path}: unreadable TRX: {exc}")
            return rows
        for test in tree.iter():
            if test.tag.rsplit("}", 1)[-1] != "UnitTestResult":
                continue
            sample_count_before = sum(str(field(row, "RecordType", "")).lower() == "sample" for row in rows)
            for node in test.iter():
                if node.tag.rsplit("}", 1)[-1] != "StdOut":
                    continue
                for line in (node.text or "").splitlines():
                    if not line.strip().startswith("{"):
                        continue
                    try:
                        row = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if not isinstance(row, dict):
                        continue
                    if field(row, "RecordType") is None:
                        row["RecordType"] = "span" if field(row, "Operation") else "sample"
                        if field(row, "RecordType") == "sample":
                            legacy_timings = True
                            row["Phase"] = "legacy"
                            row["Success"] = test.attrib.get("outcome") == "Passed"
                            row["SampleId"] = test.attrib.get("executionId", test.attrib.get("testName"))
                    row.setdefault("RunId", str(path.resolve()))
                    rows.append(row)
            if test.attrib.get("outcome", "").casefold() in ("failed", "error", "timeout", "aborted"):
                errors = [(node.text or "").strip() for node in test.iter()
                          if node.tag.rsplit("}", 1)[-1] in ("Message", "StackTrace") and (node.text or "").strip()]
                error = "\n".join(errors) or f"Test runner outcome: {test.attrib.get('outcome')}"
                sample_id = test.attrib.get("executionId", test.attrib.get("testName", "runner"))
                run_id = str(path.resolve())
                if sample_count_before == sum(str(field(row, "RecordType", "")).lower() == "sample" for row in rows):
                    rows.append(dict(RecordType="sample", RunId=run_id, SampleId=sample_id,
                                     Scenario=test.attrib.get("testName", "test runner"),
                                     Phase="runner", Success=False, Error=error,
                                     Endpoint="Test runner / no timing emitted", StageCount=None))
                rows.append(dict(RecordType="run_failed", RunId=run_id,
                                 Scenario=test.attrib.get("testName", "test runner"),
                                 Phase="runner", Error=error))
        if legacy_timings:
            issues.append(f"{path}: legacy TRX timings include their original boundaries; do not compare WorkflowMilliseconds to AuthorizedMilliseconds.")
        manifest_path = path.with_name("run.json")
        if manifest_path.exists() and not any(str(field(row, "RecordType", "")).lower() == "metadata" for row in rows):
            try:
                manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
                if not isinstance(manifest, dict):
                    raise ValueError("run manifest must be a JSON object")
                manifest.update(RecordType="metadata", RunId=str(path.resolve()), SourceManifest=str(manifest_path))
                if field(manifest, "Conditions") is None and field(manifest, "DatabasePolicy") is not None:
                    manifest["Conditions"] = field(manifest, "DatabasePolicy")
                rows.append(manifest)
            except (OSError, ValueError) as exc:
                issues.append(f"{manifest_path}: could not read companion metadata: {exc}")
    else:
        try:
            with path.open(encoding="utf-8-sig") as source:
                for number, line in enumerate(source, 1):
                    if not line.strip():
                        continue
                    try:
                        row = json.loads(line)
                        if not isinstance(row, dict):
                            raise ValueError("record must be a JSON object")
                        rows.append(row)
                    except (json.JSONDecodeError, ValueError) as exc:
                        issues.append(f"{path}:{number}: invalid JSONL row: {exc}")
        except OSError as exc:
            issues.append(f"{path}: unreadable input: {exc}")
    for row in rows:
        row["_File"] = str(path)
        row["_Label"] = label
    return rows


def normalize_sample(row, index, issues):
    phase = str(field(row, "Phase", "unspecified")).lower()
    success = field(row, "Success")
    if not isinstance(success, bool):
        success = False
        issues.append(f"{row['_File']}: sample {index} lacks boolean Success; treated as unsuccessful.")
    sample = dict(
        Label=row["_Label"], RunId=str(field(row, "RunId", row["_File"])),
        SampleId=str(field(row, "SampleId", index)),
        Scenario=str(field(row, "Scenario", "unspecified")), Phase=phase,
        Iteration=field(row, "Iteration"), StageCount=field(row, "StageCount", 5),
        Endpoint=str(field(row, "Endpoint", "Risk Manager / Authorized intent")),
        Success=success, Error=field(row, "Error"), TraceId=field(row, "TraceId"),
        StartedAtUtc=field(row, "StartedAtUtc"),
        StageAcceptedMilliseconds=field(row, "StageAcceptedMilliseconds", []),
        SourceFile=row["_File"],
    )
    if phase not in PHASES:
        issues.append(f"{row['_File']}: sample {index} has unknown phase {phase!r}; kept separate.")
    for name in TIMING_METRICS:
        value = field(row, name)
        sample[name] = finite(value)
        if value is not None and sample[name] is None:
            issues.append(f"{row['_File']}: sample {index} has invalid {name}={value!r}; excluded from that metric.")
    process = field(row, "ProcessMetrics", {})
    process = process if isinstance(process, dict) else {}
    for name in PROCESS_METRICS:
        value = field(process, name)
        sample[f"Process{name}"] = finite(value)
        if value is not None and sample[f"Process{name}"] is None:
            issues.append(f"{row['_File']}: sample {index} has invalid process {name}={value!r}; excluded from that metric.")
    if success and all(sample[name] is None for name in TIMING_METRICS):
        issues.append(f"{row['_File']}: successful sample {index} has no valid latency measurement.")
    return sample


def group_samples(samples):
    groups = defaultdict(list)
    for sample in samples:
        key = tuple(sample[k] for k in ("Label", "Scenario", "Phase", "StageCount", "Endpoint"))
        groups[key].append(sample)
    summaries, populations = [], {}
    for key, rows in sorted(groups.items(), key=lambda pair: str(pair[0])):
        shared = dict(zip(("Label", "Scenario", "Phase", "StageCount", "Endpoint"), key))
        shared.update(Attempted=len(rows), Successful=sum(r["Success"] for r in rows),
                      Failed=sum(not r["Success"] for r in rows))
        for metric in METRICS:
            values = [r[metric] for r in rows if r["Success"] and r[metric] is not None]
            if not values and all(r[metric] is None for r in rows):
                continue
            summaries.append({**shared, "Metric": metric, **stats(values),
                              "MissingOrInvalidSuccessful": shared["Successful"] - len(values)})
            populations[(*key, metric)] = values
        # Failure-only groups must remain visible even if no timing was emitted.
        if all(r[metric] is None for r in rows for metric in METRICS):
            summaries.append({**shared, "Metric": "No valid latency", **stats([]),
                              "MissingOrInvalidSuccessful": shared["Successful"]})
        stage_values = defaultdict(list)
        for sample in rows:
            if not sample["Success"] or not isinstance(sample["StageAcceptedMilliseconds"], list):
                continue
            seen_stages = set()
            for stage in sample["StageAcceptedMilliseconds"]:
                if not isinstance(stage, dict):
                    continue
                value = finite(field(stage, "Milliseconds"))
                stage_key = (str(field(stage, "StageCount")), str(field(stage, "Endpoint", "unspecified")))
                if value is not None and stage_key not in seen_stages:
                    seen_stages.add(stage_key)
                    stage_values[stage_key].append(value)
        for (stage_count, endpoint), values in stage_values.items():
            metric = f"CumulativeStage{stage_count}Milliseconds"
            summaries.append({**shared, "Endpoint": endpoint, "Metric": metric,
                              **stats(values), "MissingOrInvalidSuccessful": shared["Successful"] - len(values)})
            populations[(*key, metric)] = values
    return summaries, populations


def bootstrap_reduction(baseline, candidate, iterations, seed):
    if len(baseline) < 2 or len(candidate) < 2 or statistics.median(baseline) <= 0:
        return None, None
    generator = random.Random(seed)
    reductions = []
    for _ in range(iterations):
        before = statistics.median(generator.choices(baseline, k=len(baseline)))
        after = statistics.median(generator.choices(candidate, k=len(candidate)))
        if before > 0:
            reductions.append(100 * (before - after) / before)
    return percentile(reductions, .025), percentile(reductions, .975)


def compare(populations, iterations, seed):
    comparisons = []
    for key, baseline in populations.items():
        if key[0] != "baseline":
            continue
        candidate = populations.get(("candidate", *key[1:]), [])
        if not baseline or not candidate:
            continue
        baseline_median, candidate_median = statistics.median(baseline), statistics.median(candidate)
        baseline_p95, candidate_p95 = percentile(baseline, .95), percentile(candidate, .95)
        stable_seed = seed + int(hashlib.sha256(repr(key).encode()).hexdigest()[:16], 16)
        low, high = bootstrap_reduction(baseline, candidate, iterations, stable_seed)
        comparisons.append(dict(
            Scenario=key[1], Phase=key[2], StageCount=key[3], Endpoint=key[4], Metric=key[5],
            BaselineCount=len(baseline), CandidateCount=len(candidate),
            BaselineMedian=baseline_median, CandidateMedian=candidate_median,
            MedianReductionPercent=100 * (baseline_median - candidate_median) / baseline_median if baseline_median > 0 else None,
            BaselineP95=baseline_p95, CandidateP95=candidate_p95,
            P95ReductionPercent=100 * (baseline_p95 - candidate_p95) / baseline_p95 if baseline_p95 > 0 else None,
            BootstrapMedianReduction95Low=low, BootstrapMedianReduction95High=high,
            Evidence="interval above zero" if low is not None and low > 0 else
                     "interval below zero" if high is not None and high < 0 else
                     "inconclusive" if low is not None else "insufficient samples",
        ))
    return comparisons


def analyze_traces(rows, samples, issues):
    traces = defaultdict(dict)
    sample_index = {(s["Label"], s["RunId"], s["SampleId"]): s for s in samples}
    trace_index = {(s["Label"], s["RunId"], s["TraceId"]): s for s in samples if s["TraceId"]}
    for row in rows:
        trace_id, span_id = field(row, "TraceId"), field(row, "SpanId")
        duration, start = finite(field(row, "Milliseconds")), parse_timestamp(field(row, "StartTimeUtc"))
        if not trace_id or not span_id or duration is None or start is None:
            issues.append(f"{row['_File']}: invalid trace span {span_id!r}; requires IDs, finite duration and UTC/offset StartTimeUtc.")
            continue
        run_id = str(field(row, "RunId", row["_File"]))
        key = (row["_Label"], run_id, str(trace_id))
        spans = traces[key]
        if span_id in spans:
            issues.append(f"{row['_File']}: duplicate span {span_id}; later copy ignored.")
            continue
        spans[span_id] = dict(Operation=str(field(row, "Operation", "unknown")),
                             ParentSpanId=field(row, "ParentSpanId"),
                             SampleId=str(field(row, "SampleId", "")),
                             Interval=(start, start + duration), Milliseconds=duration,
                             Tags=field(row, "Tags", {}))
    trace_summaries, operations = [], []
    for (label, run_id, trace_id), spans in traces.items():
        identified = next((sample_index[(label, run_id, s["SampleId"])] for s in spans.values()
                           if (label, run_id, s["SampleId"]) in sample_index), None)
        sample = identified or trace_index.get((label, run_id, trace_id), {})
        trace_window = (min(s["Interval"][0] for s in spans.values()), max(s["Interval"][1] for s in spans.values()))
        sample_start = parse_timestamp(sample.get("StartedAtUtc"))
        sample_duration = sample.get("AuthorizedMilliseconds")
        if sample_duration is None:
            sample_duration = sample.get("WorkflowMilliseconds")
        window = (sample_start, sample_start + sample_duration) if sample_start is not None and sample_duration is not None else trace_window
        boundary = "start to durable authorization" if sample_start is not None and sample.get("AuthorizedMilliseconds") is not None else "start to legacy endpoint" if sample_start is not None and sample_duration is not None else "first to last span (not workflow latency)"
        shared = dict(Label=label, RunId=run_id, TraceId=trace_id,
                      SampleId=sample.get("SampleId"), Scenario=sample.get("Scenario", "unspecified"),
                      Phase=sample.get("Phase", "trace"))
        children = defaultdict(list)
        missing_parents = 0
        for span in spans.values():
            parent = span["ParentSpanId"]
            if parent in spans:
                children[parent].append(span["Interval"])
            elif parent and set(str(parent)) != {"0"}:
                missing_parents += 1
        by_operation = defaultdict(list)
        for span_id, span in spans.items():
            interval = clipped(span["Interval"], window)
            inclusive = max(0, interval[1] - interval[0])
            exclusive = max(0, inclusive - union_duration(clipped(child, interval) for child in children[span_id]))
            by_operation[span["Operation"]].append((interval, inclusive, exclusive))
        covered = union_duration(clipped(s["Interval"], window) for s in spans.values())
        window_duration = max(0, window[1] - window[0])
        # Removing observed outer roots shows whether detailed child spans cover
        # the window. It still cannot distinguish CPU time from I/O waits.
        nested = [s["Interval"] for s in spans.values() if s["ParentSpanId"] in spans]
        nested_coverage = union_duration(clipped(interval, window) for interval in nested)
        trace_summaries.append({**shared, "SpanCount": len(spans), "WindowBoundary": boundary,
                                "WindowMilliseconds": window_duration, "CoveredMilliseconds": covered,
                                "UncoveredMilliseconds": max(0, window_duration - covered),
                                "NestedSpanCoverageMilliseconds": nested_coverage,
                                "MissingParentCount": missing_parents})
        for operation, values in by_operation.items():
            operations.append({**shared, "Operation": operation, "Calls": len(values),
                               "InclusiveMilliseconds": sum(v[1] for v in values),
                               "ExclusiveOfDirectChildrenMilliseconds": sum(v[2] for v in values),
                               "WallCoverageMilliseconds": union_duration(v[0] for v in values),
                               "WindowMilliseconds": window_duration})
    return trace_summaries, operations


def metadata_warnings(metadata, issues):
    if not metadata:
        issues.append("No metadata records: build, environment and benchmark conditions cannot be verified.")
        return
    by_label = defaultdict(list)
    for row in metadata:
        by_label[row["_Label"]].append(row)
    # An optimization is expected to change commit. Other conditions should
    # match or be explained by the reviewer. Missing metadata is not equality.
    comparable = ("BuildConfiguration", "Machine", "MachineName", "Runtime", "FrameworkDescription",
                  "ProcessorCount", "Conditions", "Scenarios", "TracingEnabled", "CaptureTrace",
                  "QueryVisiblePollingMilliseconds", "Warmups", "Samples", "ServerGC", "GCLatencyMode",
                  "OS", "Architecture", "ColdDefinition")
    for key in comparable:
        values = {label: {json.dumps(field(row, key), sort_keys=True) for row in rows
                          if field(row, key) is not None} for label, rows in by_label.items()}
        for label, observed in values.items():
            if len(observed) > 1:
                issues.append(f"{label}: metadata {key} varies within the evidence set; aggregate with caution.")
        if values.get("baseline") and values.get("candidate") and values["baseline"] != values["candidate"]:
            issues.append(f"Baseline/candidate metadata differs for {key}: {values['baseline']} vs {values['candidate']}.")
    for label, rows in by_label.items():
        if not any(field(row, "Commit") for row in rows):
            issues.append(f"{label}: commit metadata missing.")
        if not any(field(row, "BuildConfiguration") for row in rows):
            issues.append(f"{label}: build configuration metadata missing.")
        if not any(field(row, "Conditions") for row in rows):
            issues.append(f"{label}: benchmark conditions metadata missing; infrastructure and fixture equivalence cannot be checked.")
    harnesses = defaultdict(set)
    for row in metadata:
        assemblies = field(row, "Assemblies", [])
        if not isinstance(assemblies, list):
            continue
        for assembly in assemblies:
            if isinstance(assembly, dict) and str(field(assembly, "Name", "")).endswith(".IntegratedTests"):
                digest = field(assembly, "Sha256")
                if digest:
                    harnesses[row["_Label"]].add(str(digest))
    for label, hashes in harnesses.items():
        if len(hashes) > 1:
            issues.append(f"{label}: integration benchmark assembly fingerprints differ within the evidence set; verify identical fixtures and timing boundaries.")
    if harnesses.get("baseline") and harnesses.get("candidate") and harnesses["baseline"] != harnesses["candidate"]:
        issues.append("Baseline/candidate integration benchmark assembly fingerprints differ; explain harness-only binary differences and verify identical fixtures and timing boundaries before attributing latency changes to production code.")


def audit_runs(samples, run_outcomes, issues):
    """A successfully timed iteration cannot establish a completed benchmark."""
    grouped = defaultdict(list)
    for sample in samples:
        grouped[(sample["Label"], sample["RunId"])].append(sample)
    outcomes = defaultdict(list)
    for row in run_outcomes:
        outcomes[(row["_Label"], str(field(row, "RunId", "unspecified")))].append(row)
    eligible = []
    for key, rows in grouped.items():
        statuses = {str(field(row, "RecordType", "")).lower() for row in outcomes[key]}
        is_legacy = all(row["Phase"] in ("legacy", "runner") for row in rows)
        completed = "run_completed" in statuses
        failed = bool(statuses.intersection(("run_failed", "run_incomplete")))
        if completed and any(not row["Success"] for row in rows) and not failed:
            record = dict(RecordType="run_incomplete", RunId=key[1], _Label=key[0],
                          _File=rows[0]["SourceFile"], CompletedIterations=len(rows),
                          Error="A run_completed marker accompanies failed sample records; the run is excluded from comparisons.")
            run_outcomes.append(record)
            issues.append(f"{key[0]} run {key[1]} declares completion but contains failed samples.")
            failed = True
        if not is_legacy and not completed and not failed:
            record = dict(RecordType="run_incomplete", RunId=key[1], _Label=key[0],
                          _File=rows[0]["SourceFile"], CompletedIterations=len(rows),
                          Error="No run_completed or run_failed marker: the process may still be running or may have terminated before recording its outcome.")
            run_outcomes.append(record)
            issues.append(f"{key[0]} run {key[1]} has no completion marker and is excluded from comparisons.")
            failed = True
        if completed:
            declared = [field(row, "CompletedIterations") for row in outcomes[key]
                        if str(field(row, "RecordType", "")).lower() == "run_completed"]
            if any(not isinstance(count, int) or isinstance(count, bool) or count != len(rows) for count in declared):
                record = dict(RecordType="run_incomplete", RunId=key[1], _Label=key[0],
                              _File=rows[0]["SourceFile"], CompletedIterations=len(rows),
                              Error=f"Completion marker iteration count {declared} does not match {len(rows)} captured sample records.")
                run_outcomes.append(record)
                issues.append(f"{key[0]} run {key[1]} has a completion/sample-count mismatch and is excluded from comparisons.")
                failed = True
        for sample in rows:
            sample["ComparisonEligible"] = (completed or is_legacy) and not failed
            if sample["ComparisonEligible"]:
                eligible.append(sample)
    return eligible


def write_csv(path, rows):
    with path.open("w", newline="", encoding="utf-8") as destination:
        if not rows:
            return
        columns = list(dict.fromkeys(key for row in rows for key in row))
        writer = csv.DictWriter(destination, fieldnames=columns)
        writer.writeheader()
        for row in rows:
            writer.writerow({k: json.dumps(v, ensure_ascii=False) if isinstance(v, (dict, list)) else v
                             for k, v in row.items()})


def display(value):
    if value is None:
        return "—"
    if isinstance(value, float):
        return f"{value:.3f}"
    return str(value).replace("|", "\\|").replace("\n", " ").replace("\r", " ")


def table(headers, rows):
    return ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |",
            *("| " + " | ".join(display(value) for value in row) + " |" for row in rows)]


def markdown_report(report):
    lines = ["# Strategy workflow performance observations", "",
             "Workflow timings are elapsed milliseconds. Process metric units are given by their names. Latency is observational; this report imposes no latency qualification limit.", "",
             "## Evidence and conditions", ""]
    lines += table(["Label", "Run", "Commit", "Build", "Machine", "Runtime"], [
        (r["_Label"], field(r, "RunId"), field(r, "Commit"), field(r, "BuildConfiguration"),
         field(r, "Machine", field(r, "MachineName")), field(r, "Runtime", field(r, "FrameworkDescription")))
        for r in report["Metadata"]])
    lines += ["", "Complete metadata and conditions are preserved in report.json. Fresh identities and equivalent initial capital do not erase accumulated database history.", "",
              "## Latency distributions", "",
              "Only successful samples with finite, nonnegative latency enter percentiles. Failures and invalid measurements are retained. Cold, warmup, warm, traced and legacy observations are separate groups. p95 uses linear interpolation at (n−1)×0.95; small samples give an unstable tail estimate.", ""]
    lines += table(["Label", "Scenario", "Phase", "Metric", "Attempts", "Failed", "Valid n", "Missing", "Median", "p95", "Min", "Max", "Mean", "SD"], [
        (s["Label"], s["Scenario"], s["Phase"], s["Metric"], s["Attempted"], s["Failed"], s["Count"],
         s["MissingOrInvalidSuccessful"], s["Median"], s["P95"], s["Minimum"], s["Maximum"], s["Mean"], s["StandardDeviation"])
        for s in report["Summaries"]])
    lines += ["", "AuthorizedMilliseconds ends at the durable terminal commit. QueryVisibleMilliseconds observes removal from the active projection cache, with the polling granularity recorded in run conditions (10 ms in the current harness). It does not establish dedicated history-query visibility. VerificationMilliseconds is outside workflow latency. Cumulative stage times come from the same workflow; they must not be interpreted as individual stage processing durations. Legacy WorkflowMilliseconds includes the historical polling/read boundary.", "",
              "Process allocation, CPU and collection counts are process-wide deltas through observed visibility and can include concurrent/background activity. Working set, private memory and managed heap are sampled sizes, not per-workflow allocations. These counters do not isolate CPU or allocation inside individual actors.", ""]
    if report["Comparisons"]:
        lines += ["## Baseline versus candidate", "",
                  "Positive reduction means lower candidate latency. Comparisons exclude failed/incomplete runs and new JSONL runs without a matching completion marker; their samples remain visible in raw summaries. The deterministic percentile bootstrap resamples each population independently; it does not correct for host drift, serial correlation, differing data history or measurement bias. Prefer alternating baseline/candidate batches and inspect metadata before attributing a change to code.", ""]
        lines += table(["Scenario", "Phase", "Metric", "Baseline n", "Candidate n", "Baseline median", "Candidate median", "Reduction %", "95% interval low %", "95% interval high %", "Evidence"], [
            (c["Scenario"], c["Phase"], c["Metric"], c["BaselineCount"], c["CandidateCount"], c["BaselineMedian"], c["CandidateMedian"],
             c["MedianReductionPercent"], c["BootstrapMedianReduction95Low"], c["BootstrapMedianReduction95High"], c["Evidence"])
            for c in report["Comparisons"]])
        lines += [""]
        workflow_comparisons = [c for c in report["Comparisons"] if c["Phase"] == "warm" and c["Metric"] == "AuthorizedMilliseconds"]
        if workflow_comparisons:
            lines += ["### Warm durable-authorization comparison", "",
                      "The interval below describes the median reduction. p95 reduction is an observed estimate without a tail confidence interval; it is particularly sensitive to sample size.", ""]
            lines += table(["Scenario", "Baseline n", "Candidate n", "Baseline p50 ms", "Candidate p50 ms", "p50 reduction %", "p50 reduction 95% interval %", "Baseline p95 ms", "Candidate p95 ms", "p95 reduction %"], [
                (c["Scenario"], c["BaselineCount"], c["CandidateCount"], c["BaselineMedian"], c["CandidateMedian"],
                 c["MedianReductionPercent"], f"{display(c['BootstrapMedianReduction95Low'])} to {display(c['BootstrapMedianReduction95High'])}",
                 c["BaselineP95"], c["CandidateP95"], c["P95ReductionPercent"])
                for c in workflow_comparisons])
            lines += [""]
    if report["TraceSummaries"]:
        lines += ["## Trace windows", "",
                  "Spans are clipped to the workflow interval when its UTC start is available; otherwise the window is first-to-last observed span and is not workflow latency. Coverage is an interval union. An outer workflow span can cover the entire window while revealing little about its children.", ""]
        lines += table(["Scenario", "Sample", "TraceId", "Spans", "Window boundary", "Window ms", "Covered ms", "Nested coverage ms", "Uncovered ms", "Missing parents"], [
            (t["Scenario"], t["SampleId"], t["TraceId"], t["SpanCount"], t["WindowBoundary"], t["WindowMilliseconds"], t["CoveredMilliseconds"],
             t["NestedSpanCoverageMilliseconds"], t["UncoveredMilliseconds"], t["MissingParentCount"])
            for t in report["TraceSummaries"]])
        lines += ["", "Largest operations by wall coverage in each sample follow. Inclusive totals contain child spans; exclusive totals subtract the union of observed direct children intersected with the parent window. Missing instrumentation remains in exclusive time, and asynchronous descendants may outlive their parent. Operation rows can overlap and **must not be added together**. These are elapsed intervals, not CPU time or a proven critical path; establish critical-path membership from the causal span tree and the terminal commit before choosing an optimization.", ""]
        operations = defaultdict(list)
        for row in report["Operations"]:
            operations[(row["Label"], row["RunId"], row["TraceId"])].append(row)
        top = [r for values in operations.values() for r in sorted(values, key=lambda v: v["WallCoverageMilliseconds"], reverse=True)[:20]]
        lines += table(["Sample", "Operation", "Calls", "Inclusive ms", "Exclusive ms", "Wall coverage ms"], [
            (r["SampleId"], r["Operation"], r["Calls"], r["InclusiveMilliseconds"], r["ExclusiveOfDirectChildrenMilliseconds"], r["WallCoverageMilliseconds"])
            for r in top])
        lines += ["", "All operations, including ones outside the terminal window, are in operations.csv; outside-window contributions are zero.", ""]
    lines += ["## Failures and evidence limits", ""]
    failed_runs = [r for r in report["RunOutcomes"] if str(field(r, "RecordType", "")).lower() in ("run_failed", "run_incomplete")]
    completed_runs = [r for r in report["RunOutcomes"] if str(field(r, "RecordType", "")).lower() == "run_completed"]
    if failed_runs:
        lines += [f"**Incomplete or failed runs: {len(failed_runs)}.** Successful samples before a setup/batch failure do not establish a completed benchmark.", ""]
        lines += table(["Label", "Run", "Completed iterations", "Scenario", "Phase", "Error"], [
            (r["_Label"], field(r, "RunId"), field(r, "CompletedIterations"), field(r, "Scenario"), field(r, "Phase"), field(r, "Error"))
            for r in failed_runs])
        lines += [""]
    if completed_runs:
        lines += [f"Explicit successful run-completion records: {len(completed_runs)}.", ""]
    failures = [s for s in report["Samples"] if not s["Success"]]
    if failures:
        lines += table(["Label", "Scenario", "Phase", "Sample", "Error"], [
            (s["Label"], s["Scenario"], s["Phase"], s["SampleId"], s["Error"]) for s in failures])
        lines += [""]
    else:
        lines += ["No failed sample records were found. Run failures above can still make the batch incomplete. Check the test runner result for failures that prevented sample emission.", ""]
    lines += [f"- {display(issue)}" for issue in report["Issues"]] or ["- No input validation issues found."]
    lines += ["", "No production optimization claim follows from a single run or an unmatched legacy timing. Synthetic market inputs remain a fixture boundary; actual Portfolio/storage services are described by the run metadata.", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, nargs="+", default=[], help="JSONL/TRX evidence to summarize")
    parser.add_argument("--baseline", type=Path, nargs="+", default=[], help="baseline evidence for comparison")
    parser.add_argument("--candidate", type=Path, nargs="+", default=[], help="candidate evidence for comparison")
    parser.add_argument("--output", type=Path, required=True, help="directory for CSV, JSON and Markdown reports")
    parser.add_argument("--bootstrap", type=int, default=2000, help="bootstrap repetitions (default 2000)")
    parser.add_argument("--seed", type=int, default=20260909, help="deterministic bootstrap seed")
    args = parser.parse_args()
    if not (args.input or args.baseline or args.candidate):
        parser.error("provide --input or --baseline/--candidate")
    if bool(args.baseline) != bool(args.candidate):
        parser.error("--baseline and --candidate must be provided together")
    if args.bootstrap < 100:
        parser.error("--bootstrap must be at least 100")
    issues, rows = [], []
    for label, paths in (("observed", args.input), ("baseline", args.baseline), ("candidate", args.candidate)):
        seen = set()
        for path in paths:
            absolute = str(path.resolve()).casefold()
            if absolute in seen:
                issues.append(f"{label}: repeated input path {path}; ignored duplicate file.")
                continue
            seen.add(absolute)
            rows.extend(load_file(path, label, issues))
    metadata, samples, spans, run_outcomes = [], [], [], []
    seen_samples = set()
    for row in rows:
        kind = str(field(row, "RecordType", "")).lower()
        if kind == "metadata":
            metadata.append(row)
        elif kind == "sample":
            sample = normalize_sample(row, len(samples) + 1, issues)
            key = (sample["Label"], sample["RunId"], sample["SampleId"])
            if key in seen_samples:
                issues.append(f"Duplicate sample {key}; later record ignored.")
                continue
            seen_samples.add(key)
            samples.append(sample)
        elif kind == "span":
            spans.append(row)
        elif kind in ("run_failed", "run_completed", "run_incomplete"):
            run_outcomes.append(row)
            if kind in ("run_failed", "run_incomplete"):
                issues.append(f"{row['_File']}: benchmark run {field(row, 'RunId', 'unspecified')} failed or was incomplete.")
        else:
            issues.append(f"{row['_File']}: unknown RecordType {kind!r}; ignored.")
    metadata_warnings(metadata, issues)
    if not samples:
        issues.append("No sample records found; no workflow distribution can be established.")
    eligible_samples = audit_runs(samples, run_outcomes, issues)
    summaries, _ = group_samples(samples)
    _, populations = group_samples(eligible_samples)
    for summary in summaries:
        if summary["Phase"] == "warm" and summary["Metric"] == "AuthorizedMilliseconds" and summary["Count"] < 30:
            issues.append(f"{summary['Label']} {summary['Scenario']}: only {summary['Count']} valid warm authorization samples; median and especially p95 remain preliminary.")
    comparisons = compare(populations, args.bootstrap, args.seed)
    if args.baseline and not comparisons:
        issues.append("No matched baseline/candidate metric groups found; no reduction can be estimated.")
    trace_summaries, operations = analyze_traces(spans, samples, issues)
    report = dict(SchemaVersion=1, GeneratedAtUtc=dt.datetime.now(dt.timezone.utc).isoformat(),
                  BootstrapIterations=args.bootstrap, BootstrapSeed=args.seed,
                  Metadata=metadata, Samples=samples, Summaries=summaries,
                  Comparisons=comparisons, TraceSummaries=trace_summaries,
                  Operations=operations, RunOutcomes=run_outcomes, Issues=issues)
    args.output.mkdir(parents=True, exist_ok=True)
    for name, data in (("samples", samples), ("summary", summaries), ("comparisons", comparisons),
                       ("traces", trace_summaries), ("operations", operations), ("runs", run_outcomes)):
        write_csv(args.output / f"{name}.csv", data)
    (args.output / "report.json").write_text(json.dumps(json_safe(report), indent=2, ensure_ascii=False, allow_nan=False) + "\n", encoding="utf-8")
    (args.output / "report.md").write_text(markdown_report(report), encoding="utf-8")
    print(f"Wrote {args.output / 'report.md'}: {len(samples)} samples, {len(spans)} spans, {len(issues)} evidence notices.")
    if not samples:
        return 2
    return 1 if any(not s["Success"] for s in samples) or any(str(field(r, "RecordType", "")).lower() in ("run_failed", "run_incomplete") for r in run_outcomes) else 0


if __name__ == "__main__":
    sys.exit(main())
