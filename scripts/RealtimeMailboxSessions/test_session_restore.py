import io
import json
import os
from pathlib import Path
import tarfile
import tempfile
import time
import unittest
from unittest.mock import patch
import uuid
import session_restore as restore
import session_tool as common


class RestoreTests(unittest.TestCase):
    def test_repeated_owner_categories_restore_distinct_original_receipts_without_collision(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);artifacts=[]
            for index,body in enumerate((b'original-A',b'original-B')):
                source=root/f'source-{index}';source.write_bytes(body)
                artifacts.append({'kind':'owner-file','owner':'domain-journals','restoreOwner':f'domain-journals-{index}','relativePath':'receipts.bin','path':str(source),'sha256':common.digest(source)})
            restored=restore.restore_owner_files({'artifacts':artifacts},root/'target')
            self.assertEqual(['domain-journals','domain-journals'],[item['owner'] for item in restored])
            self.assertEqual([b'original-A',b'original-B'],[Path(item['path']).read_bytes() for item in restored])
            self.assertNotEqual(restored[0]['path'],restored[1]['path'])

    def test_live_container_name_is_rejected_before_docker_access(self):
        with patch.object(restore, "run") as command:
            with self.assertRaises(ValueError):
                restore.verify_target({"container": "ifm_db", "kind": "postgres"}, "a" * 12, "postgres")
            command.assert_not_called()

    def test_unowned_volume_and_foreign_network_peer_are_rejected(self):
        run_id = "a" * 12
        target = {"container": f"ifm_session_{run_id}_pg", "kind": "postgres"}
        data = {"Config": {"Labels": {restore.LABEL: run_id}}, "Mounts": [{"Type": "bind"}],
                "NetworkSettings": {"Networks": {f"ifm_session_{run_id}": {}}}}
        with patch.object(restore, "run", return_value=json.dumps([data])):
            with self.assertRaisesRegex(ValueError, "host directories"):
                restore.verify_target(target, run_id, "postgres")
        data["Mounts"] = []
        with patch.object(restore, "run", side_effect=[json.dumps([data]), json.dumps([{"Labels": {restore.LABEL: run_id}, "Driver": "bridge", "Containers": {"foreign": {"Name": "live-peer"}}}])]):
            with self.assertRaisesRegex(ValueError, "unowned peer"):
                restore.verify_target(target, run_id, "postgres")

    def test_tar_traversal_and_links_are_not_extracted(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for kind in (tarfile.REGTYPE, tarfile.SYMTYPE):
                path = root / "bad.tar"
                with tarfile.open(path, "w") as archive:
                    member = tarfile.TarInfo("../outside")
                    member.type = kind
                    member.linkname = "outside"
                    member.size = 1 if kind == tarfile.REGTYPE else 0
                    archive.addfile(member, io.BytesIO(b"x") if member.size else None)
                with self.assertRaises(ValueError):
                    restore.extract_sstables({"path": str(path), "tag": "tag", "bytes": path.stat().st_size}, root / "extract", "ticks")
            self.assertFalse((root / "outside").exists())

    def test_sstable_component_bytes_are_preserved_and_metadata_is_excluded(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); path = root / "data.tar"
            with tarfile.open(path, "w") as archive:
                for name, data in [("me-1-big-Data.db", b"original"), ("manifest.json", b"metadata")]:
                    member = tarfile.TarInfo("ticks/trades-" + "a" * 32 + "/snapshots/tag/" + name)
                    member.size = len(data); archive.addfile(member, io.BytesIO(data))
            groups = restore.extract_sstables({"path": str(path), "tag": "tag", "bytes": path.stat().st_size}, root / "extract", "ticks")
            self.assertEqual(b"original", (groups["trades"] / "me-1-big-Data.db").read_bytes())
            self.assertFalse((groups["trades"] / "manifest.json").exists())


@unittest.skipUnless(os.environ.get("IFM_TEST_SESSION_RESTORE_DOCKER") == "1", "Requires explicitly enabled disposable Docker fixtures")
class DockerRestoreTests(unittest.TestCase):
    def test_real_postgres_scylla_and_owner_files_restore_without_starting_application(self):
        run_id = uuid.uuid4().hex[:12]
        network = "ifm_session_" + run_id
        label = restore.LABEL + "=" + run_id
        containers, volumes = [], []

        def command(args, **kwargs):
            return restore.run(args, **kwargs)

        def create(kind, suffix):
            name = network + "_" + suffix
            volume = name + "_data"
            command(["docker", "volume", "create", "--label", label, volume]); volumes.append(volume)
            image = "postgres:17.2" if kind == "postgres" else "scylladb/scylla:6.2.2"
            mount = "/var/lib/postgresql/data" if kind == "postgres" else "/var/lib/scylla"
            args = ["docker", "run", "-d", "--name", name, "--label", label, "--network", network, "-v", volume + ":" + mount,
                    "-p", "127.0.0.1::" + ("5432" if kind == "postgres" else "9042")]
            if kind == "postgres":
                args += ["-e", "POSTGRES_HOST_AUTH_METHOD=trust", "-e", "POSTGRES_DB=session_fixture", image]
            else:
                args += [image, "--smp", "1", "--memory", "768M", "--overprovisioned", "1", "--developer-mode", "1"]
            command(args); containers.append(name)
            deadline = time.monotonic() + 150
            while True:
                try:
                    if kind == "postgres":
                        command(["docker", "exec", "--user", "postgres", name, "psql", "-XAt", "--dbname", "session_fixture", "--command", "SELECT 1;"])
                    else:
                        restore.cql(name, "SELECT key FROM system.local;")
                    break
                except RuntimeError:
                    if time.monotonic() > deadline:
                        raise TimeoutError("Disposable fixture did not become ready")
                    time.sleep(2)
            image_id = json.loads(command(["docker", "inspect", name]))[0]["Image"]
            return {"container": name, "kind": kind, "database": "session_fixture"}, image_id

        def artifact(path, **fields):
            return {"path": str(path), "bytes": path.stat().st_size, "sha256": common.digest(path), **fields}

        try:
            command(["docker", "network", "create", "--label", label, network])
            with tempfile.TemporaryDirectory() as temp:
                root = Path(temp); snapshot = root / "snapshot"; snapshot.mkdir()
                pg, pg_image = create("postgres", "pg_source")
                scylla, scylla_image = create("scylla", "scylla_source")
                command(["docker", "exec", "--user", "postgres", pg["container"], "psql", "-X", "-v", "ON_ERROR_STOP=1", "--dbname", "session_fixture", "--command",
                         "CREATE TABLE owner_state(id bigint PRIMARY KEY, cash numeric(20,8), payload bytea); INSERT INTO owner_state VALUES(7,12345.67890123,decode('000102ff','hex')); "])
                restore.cql(scylla["container"], "CREATE KEYSPACE replay_fixture WITH replication={'class':'SimpleStrategy','replication_factor':1}; CREATE TABLE replay_fixture.trades(id int PRIMARY KEY, sequence bigint, price decimal, payload blob); CREATE INDEX price_idx ON replay_fixture.trades(price); INSERT INTO replay_fixture.trades(id,sequence,price,payload) VALUES(7,123456789012345,6123.25,0x000102ff);")
                original_table = restore.json_rows(restore.cql(scylla["container"], "SELECT JSON id FROM system_schema.tables WHERE keyspace_name='replay_fixture' AND table_name='trades';"))[0]["id"]
                pg_dump = common.bounded_export(["docker", "exec", "--user", "postgres", pg["container"], "pg_dump", "--format=custom", "--no-owner", "--dbname", "session_fixture"], snapshot / "postgres.dump", 16 * 1024 * 1024)
                pg_dump.update(kind="postgres", database="session_fixture", providerImageId=pg_image)
                schema = common.bounded_export(["docker", "exec", scylla["container"], "cqlsh", "--execute", "DESCRIBE KEYSPACE replay_fixture;"], snapshot / "scylla.cql", 1024 * 1024)
                schema.update(kind="scylla-schema", keyspace="replay_fixture")
                command(["docker", "exec", scylla["container"], "nodetool", "snapshot", "-t", "restore_fixture", "replay_fixture"])
                directories = command(["docker", "exec", scylla["container"], "find", "/var/lib/scylla/data/replay_fixture", "-type", "d", "-path", "*/snapshots/restore_fixture"]).splitlines()
                data = common.bounded_export(["docker", "exec", scylla["container"], "tar", "-cf", "-", "-C", "/var/lib/scylla/data", *[value.removeprefix("/var/lib/scylla/data/") for value in directories]], snapshot / "scylla.tar", 32 * 1024 * 1024)
                data.update(kind="scylla", keyspace="replay_fixture", tag="restore_fixture", providerImageId=scylla_image)
                owner = snapshot / "owner.bin"; owner.write_bytes(b"original-owner-checkpoint")
                common.write(snapshot / "snapshot.json", {"version": 1, "state": "CapturedUnderHeldFence", "qualified": False,
                    "boundary": {"kind": "DisposableComponentFixture_NotActualSession"}, "artifacts": [pg_dump, schema, data,
                        artifact(owner, kind="owner-file", owner="emulator-ledger", relativePath="ledger.bin")]})
                for target in (pg, scylla):
                    command(["docker", "rm", "-f", target["container"]]); containers.remove(target["container"])
                pg_target, _ = create("postgres", "pg_target")
                scylla_target, _ = create("scylla", "scylla_target")
                common.write(root / "targets.json", {"version": 1, "runId": run_id, "postgres": [pg_target], "scylla": [scylla_target]})
                result = restore.restore(snapshot, root / "targets.json", root / "restored", execute=True)
                self.assertEqual("RestoredNotReplayed", result["state"])
                self.assertFalse(result["apiStarted"]); self.assertFalse(result["qualified"])
                pg_rows = command(["docker", "exec", "--user", "postgres", pg_target["container"], "psql", "-XAt", "--dbname", "session_fixture", "--command", "SELECT id,cash,encode(payload,'hex') FROM owner_state;"])
                self.assertIn("7|12345.67890123|000102ff", pg_rows)
                scylla_rows = restore.json_rows(restore.cql(scylla_target["container"], "SELECT JSON id,sequence,price,payload FROM replay_fixture.trades;"))
                self.assertEqual(7, scylla_rows[0]["id"])
                self.assertEqual(123456789012345, scylla_rows[0]["sequence"])
                self.assertEqual(6123.25, scylla_rows[0]["price"])
                self.assertEqual("0x000102ff", scylla_rows[0]["payload"])
                restored_table = restore.json_rows(restore.cql(scylla_target["container"], "SELECT JSON id FROM system_schema.tables WHERE keyspace_name='replay_fixture' AND table_name='trades';"))[0]["id"]
                self.assertNotEqual(original_table, restored_table)
                indexed = restore.json_rows(restore.cql(scylla_target["container"], "SELECT JSON id FROM replay_fixture.trades WHERE price=6123.25;"))
                self.assertEqual([{"id": 7}], indexed)
                self.assertEqual(owner.read_bytes(), (root / "restored/owners/emulator-ledger/ledger.bin").read_bytes())
                with self.assertRaises(ValueError):
                    restore.restore(snapshot, root / "targets.json", root / "second", execute=True)
        finally:
            for name in containers:
                command(["docker", "rm", "-f", name])
            for volume in volumes:
                command(["docker", "volume", "rm", volume])
            command(["docker", "network", "rm", network])


if __name__ == "__main__":
    unittest.main()
