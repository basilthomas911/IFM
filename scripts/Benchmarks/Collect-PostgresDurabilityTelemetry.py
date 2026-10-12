"""Read telemetry only from containers owned by this benchmark invocation."""
import argparse
import json
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--run-id", required=True)
parser.add_argument("--containers", required=True)
parser.add_argument("--output", required=True)
parser.add_argument("--stop-file", required=True)
args = parser.parse_args()
containers = args.containers.split(",")
for container in containers:
    label = subprocess.check_output(["docker", "inspect", "--format", "{{index .Config.Labels \"ifm.durability.benchmark\"}}", container], text=True).strip()
    if label != args.run_id or not container.startswith("ifm-durability-" + args.run_id + "-"):
        raise ValueError("Telemetry target is not owned by this run.")
sql = """SELECT json_build_object(
'database',(SELECT row_to_json(d) FROM pg_stat_database d WHERE datname='ifm_durability_benchmark'),
'wal',(SELECT row_to_json(w) FROM pg_stat_wal w),
'checkpointer',(SELECT row_to_json(c) FROM pg_stat_checkpointer c),
'io',(SELECT json_agg(i) FROM pg_stat_io i),
'waits',(SELECT json_agg(a) FROM (SELECT state,wait_event_type,wait_event,count(*) FROM pg_stat_activity WHERE datname='ifm_durability_benchmark' GROUP BY state,wait_event_type,wait_event) a),
'locks',(SELECT json_agg(l) FROM (SELECT mode,granted,count(*) FROM pg_locks WHERE database=(SELECT oid FROM pg_database WHERE datname='ifm_durability_benchmark') GROUP BY mode,granted) l));"""
stop = Path(args.stop_file)
successful_samples = 0
with Path(args.output).open("a", encoding="utf-8") as output:
    while not stop.exists():
        sample = {"Utc": datetime.now(timezone.utc).isoformat(), "Containers": []}
        for container in containers:
            entry = {"Name": container}
            try:
                stats = subprocess.check_output(["docker", "stats", "--no-stream", "--format", "{{json .}}", container], text=True, timeout=15)
                entry["Docker"] = json.loads(stats)
                pg = subprocess.check_output(["docker", "exec", container, "psql", "-U", "benchmark", "-d", "ifm_durability_benchmark", "-At", "-c", sql], text=True, timeout=15)
                entry["Postgres"] = json.loads(pg)
                successful_samples += 1
            except (subprocess.SubprocessError, ValueError) as error:
                entry["Error"] = type(error).__name__ + ": " + str(error)
            sample["Containers"].append(entry)
        output.write(json.dumps(sample) + "\n")
        output.flush()
        for _ in range(30):
            if stop.exists():
                break
            time.sleep(1)

if successful_samples == 0:
    raise RuntimeError("No database telemetry sample completed successfully.")
