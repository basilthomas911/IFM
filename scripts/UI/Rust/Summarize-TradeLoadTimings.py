"""Summarize standalone trade-load JSONL records; nested durations overlap."""
import argparse
import glob
import json
import os
import statistics
from pathlib import Path


def summarize(records):
    groups = {}
    for row in records:
        if row.get("event") not in {"trade_load_timing", "trade_load_background_timing"} or row.get("source") == "isolated-test":
            continue
        path = "already-open" if any(s["stage"] == "already_open" for s in row["stages"]) else "full-load"
        if row.get("event") == "trade_load_background_timing":
            path = "background / " + row.get("selection", {}).get("task", "unknown")
        if path == "full-load":
            path += " / first-in-process" if row.get("process_load_number") == 1 else " / subsequent"
        groups.setdefault((path, row["outcome"]), []).append(row)
    lines = []
    for (path, outcome), rows in sorted(groups.items()):
        totals = [r["total_ms"] for r in rows]
        lines.append(f"{path} / {outcome}: {len(rows)} loads; total mean {statistics.mean(totals):.2f} ms, median {statistics.median(totals):.2f} ms, max {max(totals):.2f} ms")
        stages = {}
        for row in rows:
            for stage in row["stages"]:
                stages.setdefault((stage["stage"], stage["outcome"]), []).append(stage["duration_ms"])
        for (name, status), durations in sorted(stages.items(), key=lambda pair: statistics.mean(pair[1]), reverse=True):
            lines.append(f"  {name} ({status}): {len(durations)} calls, mean {statistics.mean(durations):.2f} ms, max {max(durations):.2f} ms")
    return lines


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="*", help="JSONL paths or glob patterns; defaults to the local standalone UI logs")
    args = parser.parse_args()
    patterns = args.paths or [str(Path(os.environ["LOCALAPPDATA"]) / "TomasAI/IFM/UI.Rust.eGui/Logs/trade-load-*.jsonl")]
    records = []
    seen = set()
    for pattern in patterns:
        for name in glob.glob(pattern):
            if name in seen:
                continue
            seen.add(name)
            for number, line in enumerate(Path(name).read_text(encoding="utf-8-sig").splitlines(), 1):
                try:
                    records.append(json.loads(line))
                except json.JSONDecodeError:
                    print(f"Skipped incomplete record: {name}:{number}")
    print("\n".join(summarize(records)) or "No trade-load measurements found.")
    print("Nested stages and parallel requests overlap; do not sum their durations. Full-load does not prove a cold backend/cache.")


if __name__ == "__main__":
    main()
