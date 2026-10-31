"""Summarize retained Criterion estimates without modifying any baseline.

Use --baseline NAME for unchanged production IDs, or before_json/after_typed
pairs from the same run. Estimates and confidence intervals are copied into the
report so subsequent Criterion runs cannot silently replace the evidence.
"""
import argparse
import hashlib
from datetime import datetime,timezone
import json
from pathlib import Path
import platform
import subprocess


def estimate(path):
    values = json.loads(path.read_text())
    key = "slope" if values.get("slope") else "mean"
    return {"estimator": key, "artifact_modified_utc": datetime.fromtimestamp(path.stat().st_mtime,timezone.utc).isoformat(), **values[key]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--criterion", type=Path, default=Path(__file__).resolve().parents[1] / "target/criterion")
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--include-chart", action="store_true", help="Include the earlier chart-only migration.")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--portfolio-before", type=Path)
    parser.add_argument("--portfolio-after", type=Path)
    args = parser.parse_args()
    rows = []
    for metadata in sorted(args.criterion.glob("**/new/benchmark.json")):
        case = metadata.parent.parent
        identifier = json.loads(metadata.read_text())["full_id"]
        if not identifier.startswith("ui_models/"):
            continue
        if "/chart_decode/" in identifier and not args.include_chart:
            continue
        if "after_typed" in case.parts:
            parts = list(case.parts)
            parts[parts.index("after_typed")] = "before_json"
            before = Path(*parts) / "new/estimates.json"
        elif case.parent.name in ("ui_models_backend_to_display", "ui_models_publication_prepare"):
            before = case / args.baseline / "estimates.json"
        else:
            continue
        after = case / "new/estimates.json"
        if not before.exists() or not after.exists():
            continue
        old, new = estimate(before), estimate(after)
        rows.append({"benchmark": identifier, "before": old, "after": new,
                     "speedup": old["point_estimate"] / new["point_estimate"],
                     "confidence_intervals_overlap": not (
                         old["confidence_interval"]["upper_bound"] < new["confidence_interval"]["lower_bound"]
                         or new["confidence_interval"]["upper_bound"] < old["confidence_interval"]["lower_bound"]),
                     "before_source": str(before.resolve()), "after_source": str(after.resolve())})
    root = Path(__file__).resolve().parents[1]
    fixtures = root / "tests/fixtures/backend"
    names = ["monitor-plan.msgpack", "monitor-realtime.msgpack", "strategy-view.msgpack", "actor-health-http.json", "risk-observation.msgpack", "risk-completed.msgpack", "portfolio-create-input.msgpack", "portfolio-fund.msgpack", "backup-operation.wire.msgpack", "backup-policy.wire.msgpack", "broker-market-input.msgpack", "scheduled-run.wire.msgpack", "contracts.json", "portfolio-request-defaults.json"]
    report = {"platform": platform.platform(), "rustc": subprocess.run(["rustc", "-Vv"], capture_output=True, text=True, check=True).stdout,
              "build_configuration_sha256": {name: hashlib.sha256((root / name).read_bytes()).hexdigest()
                                              for name in ("Cargo.toml", "Cargo.lock")},
              "profile": "development", "sample_size": 30, "warmup_seconds": 1, "measurement_seconds": 3,
              "original_baseline": args.baseline,
              "oracle_source_sha256": {str(path.relative_to(root)).replace(chr(92), "/"): hashlib.sha256(path.read_bytes()).hexdigest()
                                       for path in sorted((root / "tools/ContractFixtures").rglob("*.cs"))
                                       if "obj" not in path.parts and "bin" not in path.parts},
              "source_sha256": {str(path.relative_to(root)).replace(chr(92), "/"): hashlib.sha256(path.read_bytes()).hexdigest()
                                for directory in ("src", "benches", "tests")
                                for path in sorted((root / directory).rglob("*.rs"))},
              "fixtures_sha256": {name: hashlib.sha256((fixtures / name).read_bytes()).hexdigest() for name in names},
              "benchmarks": rows}
    if args.portfolio_before or args.portfolio_after:
        if not args.portfolio_before or not args.portfolio_after:
            parser.error("Supply both portfolio timing files.")
        old=json.loads(args.portfolio_before.read_text(encoding="utf-8-sig"))
        new=json.loads(args.portfolio_after.read_text(encoding="utf-8-sig"))
        if len(old)!=len(new):
            raise ValueError("Portfolio timing cases differ")
        def equivalent(a,b):
            # JSON numeric widths may serialize as integral floats after typing.
            # Booleans and all other scalar types must remain distinct.
            if isinstance(a,bool) or isinstance(b,bool):
                return type(a) is type(b) and a==b
            if isinstance(a,(int,float)) and isinstance(b,(int,float)):
                return a==b
            if type(a) is not type(b):return False
            if isinstance(a,dict):return a.keys()==b.keys() and all(equivalent(a[k],b[k]) for k in a)
            if isinstance(a,list):return len(a)==len(b) and all(equivalent(x,y) for x,y in zip(a,b))
            return a==b
        report["portfolio_publication"]=[]
        for a,b in zip(old,new):
            if a["fundRows"]!=b["fundRows"] or not equivalent(a["document"],b["document"]):
                raise ValueError("Portfolio publication display fields changed")
            report["portfolio_publication"].append({"fundRows":a["fundRows"],"before_p50_ms":a["p50Ms"],"after_p50_ms":b["p50Ms"],"before_p95_ms":a["p95Ms"],"after_p95_ms":b["p95Ms"],"speedup":a["p50Ms"]/b["p50Ms"],"display_parity":True})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n")
    for row in rows:
        print(f'{row["benchmark"]}: {row["before"]["point_estimate"]/1000:.3f} -> {row["after"]["point_estimate"]/1000:.3f} us ({row["speedup"]:.2f}x)')


if __name__ == "__main__":
    main()
