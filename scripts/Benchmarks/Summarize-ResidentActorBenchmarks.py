"""Summarize raw BDN pilot cases without conflating a measured batch with one command."""
import csv
import json
import re
import sys
from pathlib import Path

root = Path(sys.argv[1]).resolve()
output = Path(sys.argv[2]).resolve()
output.mkdir(parents=True, exist_ok=True)
groups = {}
for report in sorted((root / "paired" / "results").glob("*-report-full*.json")):
    for item in json.loads(report.read_text(encoding="utf-8-sig"))["Benchmarks"]:
        name = item["FullName"]
        if ".ResidentActor" not in name:
            continue
        if not item.get("Statistics"):
            raise RuntimeError(f"Missing measurements: {name}")
        workload = re.search(r"ResidentActor(Cold|Warm|Replay|Churn)Benchmarks", name).group(1)
        parameters = dict(re.findall(r"(Kind|Streams)=([^&, ]+)", item["Parameters"]))
        kind = parameters["Kind"]
        streams = int(parameters.get("Streams", "16" if workload == "Churn" else "1"))
        commands = {"Cold": 1, "Replay": 8, "Churn": 32}.get(workload, 2 * streams)
        side = item["Method"]
        key = (kind, workload, streams, commands)
        if side in groups.setdefault(key, {}):
            raise RuntimeError(f"Duplicate measurement: {key} {side}")
        groups[key][side] = item
expected_pairs = int(sys.argv[3]) if len(sys.argv) > 3 else 35
if len(groups) != expected_pairs:
    raise RuntimeError(f"Expected {expected_pairs} pairs, got {len(groups)}")
rows = []
for (kind, workload, streams, commands), sides in sorted(groups.items()):
    before, after = sides["Before"], sides["After"]
    row = dict(Actor=kind, Workload=workload, Streams=streams, CommandsPerBatch=commands)
    for side, item in [("Before", before), ("After", after)]:
        metrics = {entry["Descriptor"]["Id"]: entry["Value"] for entry in item.get("Metrics", [])}
        row[side + "BatchMs"] = item["Statistics"]["Mean"] / 1e6
        row[side + "StandardErrorMs"] = item["Statistics"]["StandardError"] / 1e6
        row[side + "NormalizedMsPerCommand"] = row[side + "BatchMs"] / commands
        row[side + "BytesPerCommand"] = item["Memory"]["BytesAllocatedPerOperation"] / commands
        for target, metric in [("LockContentionsPerCommand", "LockContentionCount"),
                               ("WorkItemsPerCommand", "CompletedWorkItemCount"),
                               ("Gen0Per1000Commands", "Gen0Collects"),
                               ("Gen1Per1000Commands", "Gen1Collects"), ("Gen2Per1000Commands", "Gen2Collects")]:
            row[side + target] = metrics[metric] / commands
    row["MeanImprovementPercent"] = (1 - row["AfterBatchMs"] / row["BeforeBatchMs"]) * 100
    row["AllocationReductionPercent"] = (1 - row["AfterBytesPerCommand"] / row["BeforeBytesPerCommand"]) * 100
    rows.append(row)
with (output / "resident-pilot-paired.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
lines = ["# First five resident actor pilots — paired timings", "",
         "Before: residency disabled. After: residency enabled. Same binary, actual actor and disposable PostgreSQL paths.",
         "Both cold paths include stream resolution. Fixture setup and NATS transport are excluded from timed batches.",
         "Uncertainty shown is one standard error of the measured batch mean; raw BDN reports retain confidence intervals.",
         "Batch means are elapsed time for all commands in a batch; dividing by count is throughput normalization, not individual latency.", "",
         "| Actor | Workload / streams | Commands | Before batch ms | After batch ms | Improvement | Before / after KiB per command |",
         "|---|---|---:|---:|---:|---:|---:|"]
for row in rows:
    lines.append(f"| {row['Actor']} | {row['Workload']} / {row['Streams']} | {row['CommandsPerBatch']} | "
                 f"{row['BeforeBatchMs']:.3f} ± {row['BeforeStandardErrorMs']:.3f} | {row['AfterBatchMs']:.3f} ± {row['AfterStandardErrorMs']:.3f} | {row['MeanImprovementPercent']:+.1f}% | "
                 f"{row['BeforeBytesPerCommand']/1024:.2f} / {row['AfterBytesPerCommand']/1024:.2f} |")
(output / "resident-pilot-timings.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Verified and summarized {len(rows)} before/after pairs.")
