import copy
import json
from pathlib import Path
import socket
import tempfile
import time
import unittest
from unittest.mock import patch
import session_tool as tool

class SessionToolTests(unittest.TestCase):
    def test_export_wall_clock_kills_a_silent_provider_and_preserves_partial_evidence(self):
        import sys
        with tempfile.TemporaryDirectory() as temporary:
            path=Path(temporary)/'partial.dump';began=time.monotonic()
            with self.assertRaises(TimeoutError):tool.bounded_export([sys.executable,'-c','import time;time.sleep(30)'],path,1024,timeout=.15)
            self.assertLess(time.monotonic()-began,3);self.assertTrue(path.exists());self.assertEqual(0,path.stat().st_size)

    def test_export_hash_is_the_exact_stream_and_byte_refusal_does_not_report_success(self):
        import sys,inspect,hashlib
        self.assertEqual(21600,inspect.signature(tool.bounded_export).parameters['timeout'].default)
        with tempfile.TemporaryDirectory() as temporary:
            path=Path(temporary)/'valid.dump'
            result=tool.bounded_export([sys.executable,'-c','import sys;sys.stdout.buffer.write(b"original"*1024)'],path,8192,timeout=3)
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(),result['sha256']);self.assertEqual(8192,result['bytes'])
            with self.assertRaises(ValueError):tool.bounded_export([sys.executable,'-c','import sys;sys.stdout.buffer.write(b"x"*4096)'],Path(temporary)/'refused.dump',1024,timeout=3)

    def plan(self): return tool.load(Path(__file__).with_name("next-normal-sessions.json"))
    def limits(self): return {"maximumBytes": 2 * 1024 * 1024, "segmentBytes": 1024, "maximumPayloadBytes": 512, "maximumSeconds": 1}
    def tape(self, root):
        writer = tool.TapeWriter(root, self.limits())
        for ordinal in range(12): writer.append({"subject": "Realtime.test.Updated.1", "reply": "_INBOX.original"}, b"NATS/1.0\r\nX-Test: original\r\n\r\n", bytes([ordinal]) * 100)
        tool.write(Path(root) / "capture.json", {"tape": writer.finish(), "kind": "CoreDiagnosticObserver"})
    def test_exact_header_body_order_and_multiple_segments_roundtrip(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "tape"; self.tape(root)
            frames = list(tool.read_tape(root))
            self.assertEqual(12, len(frames)); self.assertGreater(len(tool.load(root / "capture.json")["tape"]["segments"]), 1)
            for ordinal, (meta, headers, body) in enumerate(frames):
                self.assertEqual(ordinal + 1, meta["ordinal"]); self.assertEqual("_INBOX.original", meta["reply"])
                self.assertEqual(b"NATS/1.0\r\nX-Test: original\r\n\r\n", headers); self.assertEqual(bytes([ordinal]) * 100, body)
    def test_corruption_and_missing_segment_fail_closed(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "tape"; self.tape(root)
            segment = next(root.glob("*.tape")); data = bytearray(segment.read_bytes()); data[-1] ^= 1; segment.write_bytes(data)
            with self.assertRaises(ValueError): list(tool.read_tape(root))
            segment.unlink()
            with self.assertRaises(FileNotFoundError): list(tool.read_tape(root))
    def test_capture_budget_and_payload_limit_refuse(self):
        with tempfile.TemporaryDirectory() as temp:
            writer = tool.TapeWriter(Path(temp) / "bounded", self.limits())
            with self.assertRaises(ValueError): writer.append({"subject": "s"}, b"", b"x" * 513)
            writer.finish()
    def test_plan_rejects_holiday_and_nonlocal_inline_credentials(self):
        plan = self.plan(); self.assertTrue(tool.validate_plan(plan))
        plan["sessions"][0]["kind"] = "EarlyClose"
        with self.assertRaises(ValueError): tool.validate_plan(plan)
        for url in ("nats://remote.example:4222", "nats://user:secret@127.0.0.1:4222"):
            with self.assertRaises(ValueError): tool.local_url(url, ("nats",))
    def test_initial_archive_floor_requires_frozen_owner_hash_binding_and_real_receipt_offsets(self):
        import hashlib
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);approved=root/"archive";session="actual-session";epoch=approved/("archive_"+hashlib.sha256(session.encode()).hexdigest()[:24]);(epoch/"sources").mkdir(parents=True)
            (epoch/"archive-only.protocol").write_text("session-archive:v1:ownership-only");(epoch/"sources/receipts.bin").write_bytes(bytes(296));(epoch/"sources/delivery-receipts.bin").write_bytes(b"");(epoch/"sources/000.rto").write_bytes(b"original-owned-body")
            prefix={"Protocol":"session-archive:v1:ownership-only","ArchiveOwnershipOnly":True,"OwnerSnapshot":False,"SessionDirectory":str(epoch),"Records":2,"Files":[{"RelativePath":str(path.relative_to(epoch)),"Length":path.stat().st_size}for path in epoch.rglob("*")if path.is_file()]}
            evidence=root/"prefix.json";tool.write(evidence,prefix);sha=tool.digest(evidence);plan=self.plan();plan["snapshot"]["sourceArchive"]={"prefixEvidencePath":str(evidence),"prefixEvidenceSha256":sha,"sessionId":session,"directory":str(approved)}
            proof={"boundaryId":"held-original","leaseId":"original-lease","owners":[{"owner":"source-outbox","checkpointSha256":"1"*64,"evidence":{"sessionArchivePrefixSha256":sha,"sessionArchiveSessionId":session}}]}
            with patch.object(tool,"same_fence"):
                artifacts,remaining,binding=tool.copy_initial_archive_floor(plan,proof,root/"snapshot",1024*1024)
            self.assertEqual(2,binding["lastCommittedOrdinal"]);self.assertEqual("FrozenOwnerSourceArchiveFloor",tool.load(binding["path"])["Kind"]);self.assertTrue(all(item["owner"]=="session-archive-boundary"for item in artifacts));self.assertLess(remaining,1024*1024)
            broken=copy.deepcopy(proof);broken["owners"][0]["evidence"]["sessionArchivePrefixSha256"]="f"*64
            with self.assertRaisesRegex(ValueError,"not bound"):tool.initial_archive_floor(plan,broken)
            prefix["Records"]=1;tool.write(evidence,prefix);plan["snapshot"]["sourceArchive"]["prefixEvidenceSha256"]=tool.digest(evidence);proof["owners"][0]["evidence"]["sessionArchivePrefixSha256"]=tool.digest(evidence)
            with self.assertRaisesRegex(ValueError,"not derived"):tool.initial_archive_floor(plan,proof)
            prefix["Records"]=2;tool.write(evidence,prefix);plan["snapshot"]["sourceArchive"]["prefixEvidenceSha256"]=tool.digest(evidence);proof["owners"][0]["evidence"]["sessionArchivePrefixSha256"]=tool.digest(evidence)
            with (epoch/"sources/000.rto").open("ab") as growing:growing.write(b"not-quiescent")
            with self.assertRaisesRegex(ValueError,"not quiescent"):tool.initial_archive_floor(plan,proof)
    def test_initial_archive_cannot_be_hand_asserted_without_original_source_prefix(self):
        with self.assertRaisesRegex(ValueError,"Missing hash-certified"):tool.initial_archive_floor(self.plan(),{})

    def test_snapshot_cannot_write_without_actual_fence(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "snapshot"
            with self.assertRaisesRegex(ValueError, "No cooperative"): tool.snapshot(self.plan(), output, True)
            self.assertFalse(output.exists())
    def test_incomplete_owner_attestation_is_not_accepted(self):
        plan = self.plan(); plan["boundary"]["statusUrl"] = "http://127.0.0.1:1/fence"
        proof = {"version": 1, "state": "Held", "leaseId": "lease", "boundaryId": "id", "expiresUtc": "2099-01-01T00:00:00Z", "owners": []}
        with patch.object(tool, "get_json", return_value=proof):
            with self.assertRaisesRegex(ValueError, "not authoritatively frozen"): tool.fence(plan)
    def test_partial_socket_timeout_keeps_protocol_bytes(self):
        class Peer:
            def __init__(self): self.parts = [b"HMS", socket.timeout(), b"G a 1 5 8\r\nheadrpay\r\n"]
            def recv(self, count):
                value = self.parts.pop(0)
                if isinstance(value, Exception): raise value
                return value
        reader = tool.SocketReader(Peer(), time.monotonic() + 1)
        self.assertEqual(b"HMSG a 1 5 8\r\n", reader.readline(100)); self.assertEqual(b"headrpay", reader.read(8)); self.assertEqual(b"\r\n", reader.read(2))
    def test_prefix_copy_copies_only_frozen_length_and_never_claims_mapper_checkpoint(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "source"; source.mkdir(); (source / "sources.pubwal").write_bytes(b"original-prefix-future-tail")
            prefix = {"SourceRoot": str(source), "ForwardingLease": 1, "Files": [{"RelativePath": "sources.pubwal", "Length": 15}], "MapperCheckpoint": False}
            evidence = Path(temp) / "prefix.json"; tool.write(evidence, prefix)
            plan = self.plan(); plan["nativeSourceRoot"] = str(source)
            with patch.object(tool, "check_native_lease"):
                result = tool.copy_prefix(plan, evidence, Path(temp) / "copy")
            self.assertFalse(result["qualified"]); self.assertEqual(b"original-prefix", (Path(temp) / "copy/sources.pubwal").read_bytes())
            prefix["Files"][0]["RelativePath"] = "../escape"; tool.write(evidence, prefix)
            with patch.object(tool, "check_native_lease"):
                with self.assertRaises(ValueError): tool.copy_prefix(plan, evidence, Path(temp) / "bad")
    def test_common_checkpoint_copies_raw_and_source_prefixes_with_full_hashes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "source"; raw = root / "raw"
            source.mkdir(); raw.mkdir()
            (source / "sources.pubwal").write_bytes(b"original-tail")
            (raw / "epoch.wal").write_bytes(b"ticks-tail"); (raw / "epoch.owner").touch()
            prefix = {"SourceRoot": str(source), "ForwardingLease": 1, "CheckpointStateHeld": True,
                      "Files": [{"RelativePath": "sources.pubwal", "Length": 8}],
                      "RawSources": [{"SourceRoot": str(raw), "Files": [{"RelativePath": "epoch.wal", "Length": 5}, {"RelativePath": "epoch.owner", "Length": 0}]}]}
            evidence = root / "prefix.json"; tool.write(evidence, prefix)
            plan = self.plan(); plan.update(nativeSourceRoot=str(source), nativeRawRoot=str(raw))
            with patch.object(tool, "check_native_lease"):
                result = tool.copy_prefix(plan, evidence, root / "copy")
            self.assertEqual(3, len(result["files"])); self.assertEqual(tool.digest(evidence), result["prefixEvidenceSha256"])
            self.assertEqual(b"ticks", (root / "copy/raw/epoch.wal").read_bytes())
            self.assertEqual({"source", "raw"}, {item["ownerKind"] for item in result["files"]})
            self.assertFalse(result["qualified"])
            plan["nativeRawRoot"] = str(root / "foreign")
            with patch.object(tool, "check_native_lease"):
                with self.assertRaisesRegex(ValueError, "reviewed owner"): tool.copy_prefix(plan, evidence, root / "bad")
    def test_archive_prefix_export_retains_exact_prefix_without_owner_claim(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); archive = root / "archive/session"; archive.mkdir(parents=True)
            (archive / "source.frames").write_bytes(b"original-future")
            overlay = {"Actors": {"SessionCapture": {"Directory": str(root / "archive"), "MaximumArchiveBytes": 100, "MaximumReceiptBytes": 100}}}
            tool.write(root / "overlay.json", overlay); tool.write(root / "job.json", {"overlay": "overlay.json"})
            prefix = {"protocol": "archive-v3", "sessionDirectory": str(archive), "archiveOwnershipOnly": True, "ownerSnapshot": False,
                      "mapperCheckpoint": False, "files": [{"relativePath": "source.frames", "length": 8}]}
            tool.write(root / "prefix.json", prefix)
            result = tool.copy_archive_prefix(root / "job.json", root / "export", root / "prefix.json")
            self.assertEqual(b"original", (root / "export/source.frames").read_bytes()); self.assertFalse(result["qualified"])
            self.assertFalse(result["ownerSnapshot"]); self.assertFalse(result["mapperCheckpoint"])
            prefix["files"][0]["relativePath"] = "../escape"; tool.write(root / "prefix.json", prefix)
            with self.assertRaises(ValueError): tool.copy_archive_prefix(root / "job.json", root / "bad", root / "prefix.json")
            prefix["files"][0] = {"relativePath": "source.frames", "length": 100}; tool.write(root / "prefix.json", prefix)
            with self.assertRaises(ValueError): tool.copy_archive_prefix(root / "job.json", root / "truncated", root / "prefix.json")
            self.assertFalse((root / "bad").exists()); self.assertFalse((root / "truncated").exists())
    def test_prepared_job_refuses_missing_owner_snapshot_legacy_floor_and_unhealthy_readiness(self):
        import io
        import urllib.error
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); tool.write(root / "plan.json", self.plan())
            tool.write(root / "overlay.json", {"Actors": {"RealtimeMailboxes": {"Enabled": False, "CapacityQualified": False}, "SessionCapture": {"Enabled": True}}})
            tool.write(root / "floor.json", {"pgSnapshotSourceSequence": 0, "candidateLedgerCommitRevision": 0})
            tool.write(root / "job.json", {"plan": "plan.json", "overlay": "overlay.json", "ownerSnapshotRef": None,
                "ownerFloorEvidenceRef": "floor.json", "readiness": {"api": "http://127.0.0.1:1/health/ready"}})
            body = json.dumps({"entries": {"live_pipeline": {"status": "Unhealthy", "description": "Original source sequence gap"}}}).encode()
            error = urllib.error.HTTPError("http://127.0.0.1:1/health/ready", 503, "Not ready", {}, io.BytesIO(body))
            with patch.object(tool, "get_json", side_effect=error), patch.dict(tool.os.environ, {}, clear=True):
                result = tool.preflight_job(root / "job.json")
            self.assertEqual("Blocked", result["state"]); self.assertFalse(result["eligibleCaptureStarted"])
            for reason in ("owner_snapshot_not_acquired", "live_owner_floor_legacy_zero_or_mismatched", "api_not_ready", "protected_session_access_token_not_available"):
                self.assertIn(reason, result["blockers"])
            self.assertEqual("Original source sequence gap", result["observations"]["api"]["unhealthyReasons"]["live_pipeline"])
    def receipts(self):
        result = {key: "same-original-hash" for key in ("sessionId", "initialSnapshotSha256", "sourceCaptureSha256", "calendarEvidenceSha256")}
        result.update(authority="OriginalRecordedOwnerReceipts", normalSession=True, completeSession=True, pendingRequired=0, unownedRefusals=0, sourceGaps=[],
                      families=[{"family": family, "sourceCoverageVerified": True, "ownerOutcomeVerified": True} for family in tool.FAMILIES])
        for key in ("sources", "destinations", "rawRows", "indicators", "workflows", "orders", "fills", "cash", "holds", "positions", "plans", "eod", "sourceCursors"):
            result[key] = {"count": 1, "originalIdentityLedgerSha256": "exact-identity-ledger"}
        return result
    def test_reconcile_checks_financial_identity_and_missing_family_not_just_counts(self):
        with tempfile.TemporaryDirectory() as temp:
            expected = self.receipts(); actual = copy.deepcopy(expected)
            left = Path(temp) / "expected.json"; right = Path(temp) / "actual.json"; output = Path(temp) / "result.json"
            tool.write(left, expected); tool.write(right, actual); self.assertTrue(tool.reconcile(left, right, output)["passed"])
            actual["cash"]["originalIdentityLedgerSha256"] = "changed"; actual["families"].pop(); tool.write(right, actual)
            result = tool.reconcile(left, right, output); self.assertFalse(result["passed"]); self.assertIn("owner:cash", result["failures"])
            self.assertTrue(any(value.startswith("missing_family_coverage") for value in result["failures"]))

if __name__ == "__main__": unittest.main()
