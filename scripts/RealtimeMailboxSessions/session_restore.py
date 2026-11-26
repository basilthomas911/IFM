"""Restore owner artifacts only into fresh, labelled, isolated Docker fixtures.

This restores data; it never starts an API, opens a broker connection or certifies a session.
"""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import session_tool as common

LABEL = "ifm.session-qualification"
NAME = re.compile(r"ifm_session_[a-f0-9]{12}_[a-z0-9_]+\Z")
CQL_ID = re.compile(r"[a-z][a-z0-9_]{0,127}\Z")


def run(args, *, incoming=None, timeout=600):
    result = subprocess.run(args, stdin=incoming, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, timeout=timeout, check=False)
    if result.returncode:
        raise RuntimeError("Isolated restore command failed; provider output withheld")
    if len(result.stdout) > 8 * 1024 * 1024:
        raise ValueError("Restore provider output exceeds metadata bound")
    return result.stdout.decode("utf-8")


def identifier(value):
    if not isinstance(value, str) or not CQL_ID.fullmatch(value):
        raise ValueError("Restore requires a plain unquoted database/table identifier")
    return value


def published_endpoint(info, container, container_port):
    bindings = info.get("NetworkSettings", {}).get("Ports", {}).get(str(container_port) + "/tcp") or []
    if len(bindings) != 1 or bindings[0].get("HostIp") != "127.0.0.1":
        raise ValueError("Replay stores require one inspected loopback port binding")
    port = int(bindings[0]["HostPort"])
    if not 1024 <= port <= 65535 or port in (4222, 5432, 9042, 22543, 22544):
        raise ValueError("Replay endpoint overlaps a standard development service")
    return {"host": "127.0.0.1", "port": port, "container": container}


def verify_target(target, run_id, kind):
    name = target["container"]
    if not NAME.fullmatch(name) or not name.startswith("ifm_session_" + run_id + "_"):
        raise ValueError("Restore target is not a run-scoped disposable container")
    data = json.loads(run(["docker", "inspect", name]))[0]
    if data["Config"].get("Labels", {}).get(LABEL) != run_id:
        raise ValueError("Restore target lacks its exact disposable ownership label")
    for mount in data.get("Mounts", []):
        if mount.get("Type") == "tmpfs":
            continue
        if mount.get("Type") != "volume":
            raise ValueError("Restore targets cannot mount host directories")
        volume = json.loads(run(["docker", "volume", "inspect", mount["Name"]]))[0]
        if volume.get("Labels", {}).get(LABEL) != run_id:
            raise ValueError("Restore volume is not exclusively owned by this disposable run")
    networks = data.get("NetworkSettings", {}).get("Networks", {})
    expected_network = "ifm_session_" + run_id
    if set(networks) != {expected_network}:
        raise ValueError("Restore target is not on its exclusive run network")
    network = json.loads(run(["docker", "network", "inspect", expected_network]))[0]
    if network.get("Labels", {}).get(LABEL) != run_id or network.get("Driver") not in (None, "bridge"):
        raise ValueError("Restore network is not an owned isolated bridge")
    for peer in network.get("Containers", {}).values():
        if not peer.get("Name", "").startswith("ifm_session_" + run_id + "_"):
            raise ValueError("Restore network contains an unowned peer")
    if target.get("kind") != kind:
        raise ValueError("Restore target kind mismatch")
    return data


def checked_artifacts(snapshot_path):
    root = Path(snapshot_path).resolve()
    manifest_path = root / "snapshot.json"
    manifest = common.load(manifest_path)
    if manifest.get("version") != 1 or manifest.get("state") != "CapturedUnderHeldFence":
        raise ValueError("A captured owner snapshot is required")
    artifacts = manifest.get("artifacts", [])
    if not {"postgres", "scylla", "scylla-schema", "owner-file"} <= {item.get("kind") for item in artifacts}:
        raise ValueError("Snapshot lacks required owner stores")
    seen = set()
    for item in artifacts:
        path = Path(item["path"]).resolve()
        if path in seen or root not in path.parents or path.is_symlink():
            raise ValueError("Snapshot artifact path overlaps or escapes its root")
        seen.add(path)
        if path.stat().st_size != item["bytes"] or common.digest(path) != item["sha256"]:
            raise ValueError("Snapshot artifact size/hash changed")
    return manifest, common.digest(manifest_path)


def restore_owner_files(manifest, root):
    """Unique physical roots preserve category-repeated receipts without renaming business keys."""
    restored=[]; root=Path(root)
    for item in manifest['artifacts']:
        if item['kind']!='owner-file':continue
        owner=common.identifier(item['owner']);restore_owner=common.identifier(item.get('restoreOwner',owner))
        relative=Path(item['relativePath']);target=root/'owners'/restore_owner/relative
        if relative.is_absolute() or '..' in relative.parts or not target.resolve().is_relative_to(root/'owners'/restore_owner):raise ValueError('Owner restore path escapes its root')
        target.parent.mkdir(parents=True,exist_ok=True)
        with Path(item['path']).open('rb') as incoming,target.open('xb') as outgoing:
            shutil.copyfileobj(incoming,outgoing,256*1024);outgoing.flush();os.fsync(outgoing.fileno())
        if common.digest(target)!=item['sha256']:raise ValueError('Restored owner bytes differ')
        restored.append({'owner':owner,'restoreOwner':restore_owner,'path':str(target),'sha256':item['sha256']})
    return restored


def json_rows(text):
    return [json.loads(line.strip()) for line in text.splitlines() if line.strip().startswith("{")]


def cql(container, statement):
    return run(["docker", "exec", container, "cqlsh", "--execute", statement])


def extract_sstables(artifact, output, keyspace):
    """Never extract links, arbitrary paths or files outside one original table snapshot."""
    groups = {}
    total = 0
    with tarfile.open(artifact["path"], "r:") as archive:
        for member in archive:
            parts = Path(member.name.replace("\\", "/")).parts
            if member.isdir():
                continue
            if not member.isfile() or len(parts) != 5 or parts[0] != keyspace or parts[2] != "snapshots" or parts[3] != artifact["tag"]:
                raise ValueError("Scylla snapshot contains an unexpected path or link")
            match = re.fullmatch(r"([a-z][a-z0-9_]{0,127})-([a-f0-9]{32})", parts[1])
            if not match or parts[4] in (".", "..") or "/" in parts[4] or ":" in parts[4]:
                raise ValueError("Scylla snapshot table/component identity is invalid")
            total += member.size
            if total > artifact["bytes"]:
                raise ValueError("Scylla expanded snapshot exceeds original byte budget")
            if parts[4] in ("manifest.json", "schema.cql"):
                continue
            table = match[1]
            directory = groups.setdefault(table, output / table)
            directory.mkdir(parents=True, exist_ok=True)
            path = directory / parts[4]
            with archive.extractfile(member) as incoming, path.open("xb") as outgoing:
                shutil.copyfileobj(incoming, outgoing, 256 * 1024)
    return groups


def restore(snapshot_path, targets_path, output, execute=False):
    manifest, snapshot_hash = checked_artifacts(snapshot_path)
    targets = common.load(targets_path)
    run_id = targets.get("runId", "")
    if targets.get("version") != 1 or not re.fullmatch(r"[a-f0-9]{12}", run_id):
        raise ValueError("A twelve-digit disposable run ID is required")
    root = Path(output).resolve()
    source = Path(snapshot_path).resolve()
    if root.exists() or root == source or root in source.parents or source in root.parents:
        raise ValueError("Use a new restore output outside the snapshot")
    pg = [item for item in manifest["artifacts"] if item["kind"] == "postgres"]
    schemas = {item["keyspace"]: item for item in manifest["artifacts"] if item["kind"] == "scylla-schema"}
    scylla = [item for item in manifest["artifacts"] if item["kind"] == "scylla"]
    if len(pg) != 1 or len(targets.get("postgres", [])) != 1 or len(targets.get("scylla", [])) != 1:
        raise ValueError("Restore currently supports the actual one-physical-PG/one-Scylla topology only")
    pg_target, scylla_target = targets["postgres"][0], targets["scylla"][0]
    pg_info = verify_target(pg_target, run_id, "postgres")
    scylla_info = verify_target(scylla_target, run_id, "scylla")
    database = identifier(pg_target["database"])
    endpoints = {"postgres": {**published_endpoint(pg_info, pg_target["container"], 5432), "database": database},
                 "scylla": published_endpoint(scylla_info, scylla_target["container"], 9042)}
    if targets.get("nats"):
        nats_target = targets["nats"]
        nats_info = verify_target(nats_target, run_id, "nats")
        broker = published_endpoint(nats_info, nats_target["container"], 4222)
        endpoints["nats"] = {**broker, "url": "nats://127.0.0.1:" + str(broker["port"])}
    for artifact, info in [(pg[0], pg_info), *[(item, scylla_info) for item in scylla]]:
        if artifact.get("providerImageId") != info["Image"]:
            raise ValueError("Snapshot source image is absent or differs from the isolated restore image")
    for artifact in scylla:
        identifier(artifact["keyspace"])
        if artifact["keyspace"] not in schemas:
            raise ValueError("Missing matching Scylla schema")
    pg_exec = ["docker", "exec", "--user", "postgres", pg_target["container"]]
    count = run([*pg_exec, "psql", "-XAt", "--dbname", database, "--command",
                 "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema');"]).strip()
    if count != "0":
        raise ValueError("PostgreSQL restore target is not empty")
    keyspaces = json_rows(cql(scylla_target["container"], "SELECT JSON keyspace_name FROM system_schema.keyspaces;"))
    if any(not row["keyspace_name"].startswith("system") for row in keyspaces):
        raise ValueError("Scylla restore target is not empty")
    result = {"version": 1, "state": "ValidatedNotRestored", "initialSnapshotSha256": snapshot_hash,
              "runId": run_id, "qualified": False, "apiStarted": False, "stores": [], "ownerFiles": [], "endpoints": endpoints}
    if not execute:
        return result
    root.mkdir(parents=True)
    result["state"] = "Incomplete"
    try:
        with Path(pg[0]["path"]).open("rb") as incoming:
            run(["docker", "exec", "-i", "--user", "postgres", pg_target["container"], "pg_restore",
                 "--exit-on-error", "--no-owner", "--no-privileges", "--dbname", database], incoming=incoming)
        result["stores"].append({"kind": "postgres", "container": pg_target["container"], "database": database, "artifactSha256": pg[0]["sha256"]})
        for index, artifact in enumerate(scylla):
            keyspace = artifact["keyspace"]
            with Path(schemas[keyspace]["path"]).open("rb") as incoming:
                run(["docker", "exec", "-i", scylla_target["container"], "cqlsh"], incoming=incoming)
            tables = json_rows(cql(scylla_target["container"], f"SELECT JSON table_name, id FROM system_schema.tables WHERE keyspace_name='{keyspace}';"))
            views = json_rows(cql(scylla_target["container"], f"SELECT JSON view_name FROM system_schema.views WHERE keyspace_name='{keyspace}';"))
            table_ids = {row["table_name"]: str(row["id"]).replace("-", "") for row in tables}
            derived = {row["view_name"] for row in views}
            groups = extract_sstables(artifact, root / "sstables" / str(index), keyspace)
            restored, rebuilt = [], []
            for table, directory in groups.items():
                if table in derived:
                    rebuilt.append(table)
                    continue
                if table not in table_ids or not re.fullmatch(r"[a-f0-9]{32}", table_ids[table]):
                    raise ValueError("Snapshot table has no exact restored-schema mapping")
                upload = f"/var/lib/scylla/data/{keyspace}/{identifier(table)}-{table_ids[table]}/upload"
                run(["docker", "exec", scylla_target["container"], "mkdir", "-p", upload])
                run(["docker", "cp", str(directory) + "/.", scylla_target["container"] + ":" + upload])
                run(["docker", "exec", scylla_target["container"], "chown", "-R", "scylla:scylla", upload])
                run(["docker", "exec", scylla_target["container"], "nodetool", "refresh", "--load-and-stream", keyspace, table])
                restored.append(table)
            result["stores"].append({"kind": "scylla", "container": scylla_target["container"], "keyspace": keyspace,
                                      "artifactSha256": artifact["sha256"], "restoredTables": restored, "rebuiltViews": rebuilt})
        result['ownerFiles'].extend(restore_owner_files(manifest,root))
        checked_artifacts(snapshot_path)
        result.update(state="RestoredNotReplayed", completedUtc=common.utc())
        return result
    finally:
        common.write(root / "restore.json", result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ("snapshot", "targets", "output"):
        parser.add_argument("--" + arg, required=True)
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()
    result = restore(args.snapshot, args.targets, args.output, args.execute)
    print(json.dumps({"state": result["state"], "qualified": False}))


if __name__ == "__main__":
    main()
