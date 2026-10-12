"""Combine measured BenchmarkDotNet and rate-controlled load results; reject missing cases."""
import csv
import json
import re
import sys
from pathlib import Path

root = Path(sys.argv[1])
metadata = json.loads((root / "metadata.json").read_text(encoding="utf-8-sig"))
if isinstance(metadata, dict):
    metadata = [metadata]
rows = []
loads = []
for profile in metadata:
    name = profile["Profile"]
    folder = root / name
    reports = list((folder / "results").glob("*report.csv"))
    if len(reports) != 1:
        raise ValueError(f"Missing or ambiguous BenchmarkDotNet report for {name}")
    with reports[0].open(encoding="utf-8-sig", newline="") as f:
        measurements = list(csv.DictReader(f))
    if len(measurements) != 5:
        raise ValueError(f"Expected five workload measurements for {name}")
    for measurement in measurements:
        mean = measurement["Mean"]
        match = re.fullmatch(r"([0-9.,]+)\s*(ns|us|\u00b5s|\u03bcs|ms|s)", mean)
        if not match:
            raise ValueError(f"Invalid measurement for {name}: {mean}")
        value = float(match[1].replace(",", ""))
        factor = {"ns": 0.000001, "us": .001, "\u00b5s": .001, "\u03bcs": .001, "ms": 1, "s": 1000}[match[2]]
        rows.append({"Profile": name, "Method": measurement["Method"], "MeanMs": value * factor,
                     "Error": measurement["Error"], "StdDev": measurement["StdDev"], "Allocated": measurement["Allocated"]})
    load_files = sorted(folder.glob("postgres-plan-business-load-*.json"))
    if not load_files:
        raise ValueError(f"Missing load results for {name}")
    latest = json.loads(load_files[-1].read_text(encoding="utf-8-sig"))
    expected_modes = profile.get("SustainedModes", ["BusinessOnly", "PlansAndBusiness"])
    if [item["Mode"] for item in latest["Results"]] != expected_modes:
        raise ValueError(f"Incomplete load modes for {name}")
    loads.append({"Profile": name, "Results": latest["Results"]})
baselines = {r["Method"]: r["MeanMs"] for r in rows if r["Profile"] == "AtomicDurable"}
for row in rows:
    row["ChangePercentVsDurable"] = (row["MeanMs"] / baselines[row["Method"]] - 1) * 100 if row["Method"] in baselines else None
(root / "comparison.json").write_text(json.dumps({"Measurements": rows, "Load": loads}, indent=2))
with (root / "comparison.csv").open("w", newline="") as f:
    writer = csv.DictWriter(f, fieldnames=rows[0].keys())
    writer.writeheader()
    writer.writerows(rows)
print("| Profile | Event ms | Projection ms | Plan ms | Ledger ms | Concurrent ms |")
print("| --- | ---: | ---: | ---: | ---: | ---: |")
for profile in metadata:
    values = {r["Method"]: r["MeanMs"] for r in rows if r["Profile"] == profile["Profile"]}
    print(f"| {profile['Profile']} | {values['EventAppendAndCommit']:.3f} | {values['ProjectionWriteAndCommit']:.3f} | {values['PlanWrite']:.3f} | {values['BusinessLedgerPosting']:.3f} | {values['ConcurrentPlanAndBusiness']:.3f} |")
