"""Bounded session evidence tools. Never activates an application transport or submits orders.

Core observation is diagnostic until reconciled to authoritative source receipts and a held
cross-owner snapshot boundary. A quiet socket, queue count or healthy API is not that boundary.
"""
from __future__ import annotations
import argparse
import contextlib
import base64
import datetime as dt
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import socket
import struct
import subprocess
import sys
import threading
import time
import urllib.parse
import urllib.request

MAGIC = b"IFMSTAPE1\n"
UTC = dt.timezone.utc
FAMILIES = ["native-trades", "native-quotes", "native-market-price", "native-statistics",
            "native-trade-replay", "native-option-trade-evidence", "native-option-observations",
            "bar-clock", "daily-exit-clock", "option-monitoring", "financial-lifecycle", "workflow-lifecycle", "account-observations"]
OWNERS = ["postgres", "scylla", "actor-drain", "native-source", "native-mapper", "source-outbox",
          "domain-journals", "indicator-state", "workflow-state", "emulator-ledger", "registrations-config"]

def utc() -> str:
    return dt.datetime.now(UTC).isoformat()

def load(path):
    if Path(path).stat().st_size > 8 * 1024 * 1024: raise ValueError("Metadata exceeds 8 MiB; use streamed evidence ledgers with indexed digests")
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))

def same_directory(left, right):
    """Bind existing owner directories by filesystem identity, including Windows verbatim paths."""
    try:
        left=Path(left).resolve(strict=True);right=Path(right).resolve(strict=True)
        return left.is_dir() and right.is_dir() and left.samefile(right)
    except OSError:
        return False

def write(path, value):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, indent=2, sort_keys=True); stream.write("\n"); stream.flush(); os.fsync(stream.fileno())
    os.replace(temporary, path)

def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(256 * 1024), b""): result.update(block)
    return result.hexdigest()

def timestamp(value):
    parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None: raise ValueError("UTC offset is required")
    return parsed.astimezone(UTC)

def local_url(value, schemes=("http",)):
    address = urllib.parse.urlparse(value)
    if address.scheme not in schemes or address.username or address.password:
        raise ValueError("Only explicit local endpoints without inline credentials are allowed")
    host = address.hostname
    if host != "localhost" and not ipaddress.ip_address(host or "").is_loopback:
        raise ValueError("Endpoint is outside loopback")
    return address

def get_json(url, headers=None):
    local_url(url)
    request = urllib.request.Request(url, headers={"Accept": "application/json", **(headers or {})})
    with urllib.request.urlopen(request, timeout=5) as reply:
        body = reply.read(1024 * 1024 + 1)
        if len(body) > 1024 * 1024: raise ValueError("Boundary response exceeds 1 MiB")
        return json.loads(body)

def validate_plan(plan):
    if plan.get("version") != 1: raise ValueError("Unsupported session plan")
    local_url(plan["observer"]["url"], ("nats",))
    limits = plan["limits"]
    for field in ("maximumBytes", "segmentBytes", "maximumPayloadBytes", "maximumSeconds"):
        if not isinstance(limits[field], int) or limits[field] <= 0: raise ValueError("Finite positive limits required")
    if limits["maximumPayloadBytes"] > 16 * 1024 * 1024 or limits["segmentBytes"] > 256 * 1024 * 1024:
        raise ValueError("Payload/segment exceeds qualification tool bound")
    if limits["maximumBytes"] < limits["segmentBytes"] + 1024 * 1024:
        raise ValueError("Reserve at least one segment plus metadata headroom")
    for subject in plan["observer"]["subjects"]:
        if not subject or any(c.isspace() for c in subject) or len(subject) > 512:
            raise ValueError("Invalid observer subject")
    sessions = plan.get("sessions", [])
    if len(sessions) != 2: raise ValueError("Exactly two calendar-qualified normal sessions are required")
    for session in sessions:
        if session.get("kind") != "Normal" or not session.get("calendarEvidence"):
            raise ValueError("Normal-session calendar evidence is required; no holiday substitution")
        if timestamp(session["endUtc"]) <= timestamp(session["startUtc"]): raise ValueError("Invalid session range")
    if timestamp(sessions[0]["endUtc"]) > timestamp(sessions[1]["startUtc"]): raise ValueError("Sessions overlap")
    return plan

def fence(plan):
    endpoint = plan.get("boundary", {}).get("statusUrl")
    if not endpoint: raise ValueError("No cooperative cross-owner snapshot fence is configured")
    proof = get_json(endpoint)
    if proof.get("version") != 1 or proof.get("state") != "Held" or not proof.get("leaseId") or not proof.get("boundaryId"):
        raise ValueError("Cross-owner fence is not held")
    if timestamp(proof["expiresUtc"]) < dt.datetime.now(UTC) + dt.timedelta(seconds=30):
        raise ValueError("Snapshot fence expires too soon")
    by_owner = {value["owner"]: value for value in proof.get("owners", [])}
    for name in OWNERS:
        owner = by_owner.get(name, {})
        if owner.get("state") != "Frozen" or not owner.get("checkpointSha256") or not owner.get("evidence"):
            raise ValueError(f"Required owner is not authoritatively frozen: {name}")
    if proof.get("sourceContinuity") != "RetainedBeforeForwardPause" or proof.get("forcedStop"):
        raise ValueError("No-loss source retention and graceful drain are required")
    return proof

def same_fence(plan, original):
    current = fence(plan)
    if (current["boundaryId"], current["leaseId"], current["owners"]) != (original["boundaryId"], original["leaseId"], original["owners"]):
        raise ValueError("Owner boundary changed during snapshot")

class TapeWriter:
    def __init__(self, directory, limits):
        self.root = Path(directory); self.root.mkdir(parents=True, exist_ok=False)
        self.limits = limits; self.total = 0; self.count = 0; self.previous = bytes(32)
        self.files = []; self.stream = None; self.path = None; self.last_flush = time.monotonic()
    def close_segment(self):
        if self.stream:
            self.stream.flush(); os.fsync(self.stream.fileno()); self.stream.close()
            final = self.path.with_suffix(".tape"); self.path.rename(final)
            self.files.append({"path": final.name, "bytes": final.stat().st_size, "sha256": digest(final)})
            self.stream = None
    def append(self, meta, headers, body):
        if len(headers) + len(body) > self.limits["maximumPayloadBytes"]: raise ValueError("Observer payload exceeds bound")
        self.count += 1
        meta = {**meta, "ordinal": self.count, "receivedUtc": utc(), "receivedMonotonicNs": time.monotonic_ns()}
        encoded = json.dumps(meta, separators=(",", ":"), sort_keys=True).encode()
        frame = struct.pack("<III", len(encoded), len(headers), len(body)) + encoded + headers + body
        checksum = hashlib.sha256(self.previous + frame).digest()
        if self.total + len(frame) + 36 > self.limits["maximumBytes"] - 1024 * 1024:
            self.count -= 1; raise ValueError("Observer storage budget reached; capture is incomplete")
        if self.stream and self.stream.tell() + len(frame) + 36 > self.limits["segmentBytes"]: self.close_segment()
        if not self.stream:
            self.path = self.root / f"{len(self.files):06}.partial"
            self.stream = self.path.open("xb"); self.stream.write(MAGIC); self.total += len(MAGIC)
        self.stream.write(struct.pack("<I", len(frame))); self.stream.write(frame); self.stream.write(checksum)
        self.total += len(frame) + 36; self.previous = checksum
        if self.count % 64 == 0 or time.monotonic() - self.last_flush >= 1:
            self.stream.flush(); os.fsync(self.stream.fileno()); self.last_flush = time.monotonic()
    def finish(self):
        self.close_segment()
        return {"segments": self.files, "messages": self.count, "bytes": self.total, "lastChainSha256": self.previous.hex()}

def read_tape(directory):
    root = Path(directory); manifest = load(root / "capture.json"); previous = bytes(32); count = 0
    for item in manifest["tape"]["segments"]:
        path = root / item["path"]
        if path.parent != root or digest(path) != item["sha256"]: raise ValueError("Tape segment checksum/path mismatch")
        with path.open("rb") as stream:
            if stream.read(len(MAGIC)) != MAGIC: raise ValueError("Unsupported tape")
            while size := stream.read(4):
                if len(size) != 4: raise ValueError("Truncated frame")
                length = struct.unpack("<I", size)[0]
                if not 12 <= length <= 17 * 1024 * 1024: raise ValueError("Frame outside bound")
                frame = stream.read(length); checksum = stream.read(32)
                if len(frame) != length or checksum != hashlib.sha256(previous + frame).digest(): raise ValueError("Tape chain/truncation failure")
                ml, hl, bl = struct.unpack("<III", frame[:12])
                if ml > 65536 or ml + hl + bl + 12 != length: raise ValueError("Malformed tape lengths")
                meta = json.loads(frame[12:12 + ml]); count += 1
                if meta["ordinal"] != count: raise ValueError("Tape order gap")
                previous = checksum
                yield meta, frame[12 + ml:12 + ml + hl], frame[12 + ml + hl:]
    if count != manifest["tape"]["messages"] or previous.hex() != manifest["tape"]["lastChainSha256"]:
        raise ValueError("Tape tail mismatch")

def record(plan, output):
    validate_plan(plan)
    address = local_url(plan["observer"]["url"], ("nats",)); started = utc(); gaps = []
    writer = TapeWriter(output, plan["limits"])
    try:
        with socket.create_connection((address.hostname, address.port or 4222), timeout=5) as connection:
            connection.settimeout(0.5); reader = SocketReader(connection, time.monotonic() + plan["limits"]["maximumSeconds"])
            line = reader.readline(65537)
            if not line.startswith(b"INFO ") or len(line) > 65536: raise ValueError("Invalid NATS server greeting")
            info = json.loads(line[5:])
            if info.get("tls_required") or info.get("auth_required"):
                raise ValueError("Authenticated/TLS brokers require a reviewed credential adapter; no inline credentials")
            connection.sendall(b'CONNECT {"name":"ifm-readonly-session-observer","echo":false,"verbose":false,"pedantic":true,"headers":true,"protocol":1}\r\n')
            for sid, subject in enumerate(plan["observer"]["subjects"], 1): connection.sendall(f"SUB {subject} {sid}\r\n".encode())
            connection.sendall(b"PING\r\n"); deadline = time.monotonic() + plan["limits"]["maximumSeconds"]
            while time.monotonic() < deadline:
                try: line = reader.readline(65537)
                except CaptureDeadline: break
                if not line: raise ValueError("Observer connection closed; no gap-masking reconnect")
                if len(line) > 65536: raise ValueError("NATS control line exceeds bound")
                if line == b"PING\r\n": connection.sendall(b"PONG\r\n"); continue
                if line in (b"PONG\r\n", b"+OK\r\n"): continue
                if line.startswith(b"-ERR"): raise ValueError("Broker refused observer/slow consumer")
                fields = line.rstrip(b"\r\n").decode("ascii").split()
                if fields[0] not in ("MSG", "HMSG"): raise ValueError("Unexpected NATS control")
                header_length = int(fields[-2]) if fields[0] == "HMSG" else 0
                total = int(fields[-1]); minimum = 5 if header_length else 4
                if fields[0] == "HMSG" and header_length < 12: raise ValueError("Malformed NATS headers")
                if len(fields) not in (minimum, minimum + 1) or not 0 <= header_length <= total <= plan["limits"]["maximumPayloadBytes"]:
                    raise ValueError("NATS payload/control outside bounds")
                payload = reader.read(total)
                if len(payload) != total or reader.read(2) != b"\r\n": raise ValueError("Truncated NATS payload")
                writer.append({"subject": fields[1], "reply": fields[3] if len(fields) == minimum + 1 else None,
                               "subscription": fields[2], "protocol": fields[0]}, payload[:header_length], payload[header_length:])
    except KeyboardInterrupt: gaps.append("observer_stopped_by_operator")
    except Exception as error: gaps.append(str(error)); raise
    finally:
        write(Path(output) / "capture.json", {"version": 1, "kind": "CoreDiagnosticObserver", "qualified": False,
             "startedUtc": started, "endedUtc": utc(), "sourceUrl": plan["observer"]["url"],
             "subjects": plan["observer"]["subjects"], "gaps": gaps,
             "limitations": ["Core observer is not source admission ownership", "No cross-owner snapshot association",
                             "No complete-session certification from a quiet socket"], "tape": writer.finish()})

def bounded_export(command, path, limit, timeout=21600):
    """No shell; stream to a finite file and never print credentials or child stderr."""
    if not isinstance(timeout,(int,float)) or isinstance(timeout,bool) or not 0<timeout<=21600 or not isinstance(limit,int) or isinstance(limit,bool) or limit<1:
        raise ValueError('Snapshot export requires finite positive byte and at-most-six-hour bounds')
    began = time.monotonic(); size = 0; expired=threading.Event(); content_hash=hashlib.sha256()
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    def expire():
        expired.set()
        try: process.kill()
        except OSError: pass # A simultaneous normal exit does not leave a timer exception.
    watchdog = threading.Timer(timeout, expire); watchdog.daemon = True; watchdog.start()
    try:
        with Path(path).open("xb") as output:
            while block := process.stdout.read(256 * 1024):
                size += len(block)
                if size > limit or time.monotonic() - began > timeout: raise ValueError("Snapshot export exceeds byte/time bound")
                output.write(block);content_hash.update(block)
            if expired.is_set():raise TimeoutError('Snapshot provider exceeded its original wall-clock export budget')
            if process.wait(timeout=max(0.001, timeout - (time.monotonic() - began))) != 0: raise ValueError("Snapshot provider export failed; inspect provider privately")
            output.flush(); os.fsync(output.fileno())
            if expired.is_set():raise TimeoutError('Snapshot provider exceeded its original wall-clock export budget')
    finally:
        watchdog.cancel()
        if process.poll() is None: process.kill(); process.wait(timeout=10)
        if process.stdout is not None: process.stdout.close()
    return {"path": str(path), "bytes": size, "sha256": content_hash.hexdigest()}

def identifier(value):
    if not re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", value): raise ValueError("Unsafe provider identifier")
    return value

class CaptureDeadline(Exception): pass

class SocketReader:
    """Timeout polling retains partial protocol bytes; no makefile timeout poisons a future read."""
    def __init__(self, connection, deadline): self.connection = connection; self.deadline = deadline; self.buffer = bytearray()
    def fill(self):
        while True:
            if time.monotonic() >= self.deadline: raise CaptureDeadline()
            try: block = self.connection.recv(65536)
            except socket.timeout: continue
            if not block: raise ValueError("Observer connection closed")
            self.buffer.extend(block); return
    def readline(self, limit):
        while True:
            end = self.buffer.find(b"\n")
            if end >= 0:
                if end + 1 > limit: raise ValueError("NATS control line exceeds bound")
                result = bytes(self.buffer[:end + 1]); del self.buffer[:end + 1]; return result
            if len(self.buffer) >= limit: raise ValueError("NATS control line exceeds bound")
            self.fill()
    def read(self, size):
        while len(self.buffer) < size: self.fill()
        result = bytes(self.buffer[:size]); del self.buffer[:size]; return result

def copy_prefix(plan, prefix_path, output):
    """Copies native immutable file prefixes while capture appends; this is NOT a mapper restart checkpoint."""
    prefix = load(prefix_path); expected = Path(plan["nativeSourceRoot"]).resolve()
    if not same_directory(prefix["SourceRoot"], expected) or not prefix.get("ForwardingLease"):
        raise ValueError("Native prefix scope/root/lease is not the configured owner")
    check_native_lease(plan, prefix["ForwardingLease"])
    root = Path(output); root.mkdir(parents=True, exist_ok=False); artifacts = []; remaining = plan["snapshot"]["maximumBytes"]
    def copy_files(source_root, items, output_prefix, kind):
        nonlocal remaining
        for item in items:
            check_native_lease(plan, prefix["ForwardingLease"])
            relative = Path(item["RelativePath"])
            if relative.is_absolute() or ".." in relative.parts: raise ValueError("Native prefix path escapes its source root")
            source = source_root / relative; length = item["Length"]
            if source.is_symlink() or not source.resolve().is_relative_to(source_root) or not 0 <= length <= remaining or source.stat().st_size < length:
                raise ValueError("Native prefix changed or exceeds bound")
            exported = output_prefix / relative; target = root / exported; target.parent.mkdir(parents=True, exist_ok=True)
            with source.open("rb") as incoming, target.open("xb") as outgoing:
                left = length
                while left:
                    block = incoming.read(min(256 * 1024, left))
                    if not block: raise ValueError("Native immutable prefix was truncated")
                    outgoing.write(block); left -= len(block)
                outgoing.flush(); os.fsync(outgoing.fileno())
            if source.stat().st_size < length: raise ValueError("Native source prefix was truncated during copy")
            artifacts.append({"relativePath": str(exported), "restoreRelativePath": str(relative), "ownerKind": kind,
                              "bytes": length, "sha256": digest(target)}); remaining -= length
    copy_files(expected, prefix["Files"], Path(), "source")
    raw_sources = prefix.get("RawSources", [])
    if raw_sources:
        raw_root = Path(plan["nativeRawRoot"]).resolve()
        seen = {}
        for raw in raw_sources:
            if not same_directory(raw["SourceRoot"], raw_root): raise ValueError("Raw prefix root is not the reviewed owner")
            unique = []
            for item in raw["Files"]:
                key = item["RelativePath"]
                if key in seen:
                    if seen[key] != item["Length"]: raise ValueError("Raw owners disagree about the frozen prefix")
                    continue
                seen[key] = item["Length"]; unique.append(item)
            copy_files(raw_root, unique, Path("raw"), "raw")
    check_native_lease(plan, prefix["ForwardingLease"])
    result = {"version": 1, "kind": "NativeSourceImmutablePrefixes", "qualified": False,
              "prefixEvidenceSha256": digest(prefix_path), "boundary": prefix, "files": artifacts,
              "limitations": ["Prefix files alone do not replace the separately hash-bound native checkpoint", "Matching API owner snapshot and source-floor reconciliation still required"]}
    write(root / "prefix-copy.json", result); return result

def copy_archive_prefix(job_path_value, output, prefix_path=None):
    """Export committed source archive prefixes, never a financial-owner snapshot."""
    job = load(job_path_value); overlay = load(job_path(job_path_value, job["overlay"]))
    approved = Path(overlay["Actors"]["SessionCapture"]["Directory"]).resolve()
    if prefix_path:
        prefix = load(prefix_path)
    else:
        token = os.environ.get(job.get("accessTokenEnv", "IFM_SESSION_CAPTURE_ACCESS_TOKEN"), "")
        if len(token) < 32: raise ValueError("Protected session access token is unavailable")
        endpoint = job["captureStatusUrl"].rsplit("/", 1)[0] + "/prefix"
        prefix = get_json(endpoint, {"X-IFM-Session-Capture": token})
    # ASP.NET uses camelCase; saved CLR evidence may use PascalCase.
    def field(value, name): return value.get(name, value.get(name[0].lower() + name[1:]))
    if field(prefix, "ArchiveOwnershipOnly") is not True or field(prefix, "OwnerSnapshot") is not False or field(prefix, "MapperCheckpoint") is not False:
        raise ValueError("Prefix does not identify source-only archive ownership")
    source_root = Path(field(prefix, "SessionDirectory")).resolve()
    if not source_root.is_relative_to(approved): raise ValueError("Archive prefix root is outside prepared capture directory")
    items = field(prefix, "Files"); maximum = overlay["Actors"]["SessionCapture"]["MaximumArchiveBytes"] + overlay["Actors"]["SessionCapture"]["MaximumReceiptBytes"]
    if not isinstance(items, list) or not 0 < len(items) <= 100000: raise ValueError("Archive prefix file count is invalid")
    paths = set(); total = 0; checked = []
    for item in items:
        relative = Path(field(item, "RelativePath")); length = field(item, "Length")
        source = source_root / relative
        if relative.is_absolute() or ".." in relative.parts or relative in paths or source.is_symlink() or not source.resolve().is_relative_to(source_root):
            raise ValueError("Archive prefix path escapes or repeats its source")
        if isinstance(length, bool) or not isinstance(length, int) or length < 0 or source.stat().st_size < length: raise ValueError("Archive prefix is truncated or invalid")
        total += length
        if total > maximum: raise ValueError("Archive prefix exceeds configured byte budget")
        paths.add(relative); checked.append((relative, source, length))
    root = Path(output); root.mkdir(parents=True, exist_ok=False); artifacts = []
    for relative, source, length in checked:
        target = root / relative; target.parent.mkdir(parents=True, exist_ok=True)
        with source.open("rb") as incoming, target.open("xb") as outgoing:
            left = length
            while left:
                block = incoming.read(min(256 * 1024, left))
                if not block: raise ValueError("Committed source archive prefix was truncated")
                outgoing.write(block); left -= len(block)
            outgoing.flush(); os.fsync(outgoing.fileno())
        if source.stat().st_size < length: raise ValueError("Source archive prefix was truncated during export")
        artifacts.append({"relativePath": str(relative), "bytes": length, "sha256": digest(target)})
    result = {"version": 1, "kind": "CommittedSourceArchivePrefixes", "qualified": False, "archiveOwnershipOnly": True,
              "ownerSnapshot": False, "mapperCheckpoint": False, "boundary": prefix, "files": artifacts,
              "limitations": ["Source ownership only; no domain-handler or matching initial-owner proof", "Session coverage and owner reconciliation are required separately"]}
    write(root / "archive-prefix-copy.json", result); return result

def check_native_lease(plan, expected):
    endpoint = plan.get("nativeBoundary", {}).get("statusUrl")
    token = os.environ.get(plan.get("nativeBoundary", {}).get("accessTokenEnv", "IFM_SESSION_CAPTURE_ACCESS_TOKEN"), "")
    if not endpoint or len(token) < 32: raise ValueError("Native prefix copying requires an authenticated live forwarding lease")
    response = get_json(endpoint, {"X-IFM-Session-Capture": token}); status = response.get("status", {})
    if not status.get("supported") or status.get("lease") != expected or status.get("activeSends") != 0:
        raise ValueError("Native forwarding lease is not held or sends remain active")


def initial_archive_floor(plan, proof):
    """A source-only prefix is usable here only when the original API owner binds it under the held snapshot fence."""
    config = plan.get("snapshot", {}).get("sourceArchive")
    if not isinstance(config, dict): raise ValueError("Missing hash-certified initial source archive prefix")
    evidence_path = Path(config["prefixEvidencePath"])
    actual_sha = digest(evidence_path)
    if actual_sha != config.get("prefixEvidenceSha256"): raise ValueError("Initial archive prefix evidence changed")
    owner = next((value for value in proof.get("owners", []) if value.get("owner") == "source-outbox"), {})
    association = owner.get("evidence")
    if not isinstance(association, dict) or association.get("sessionArchivePrefixSha256") != actual_sha or association.get("sessionArchiveSessionId") != config.get("sessionId"):
        raise ValueError("Initial archive prefix is not bound to the frozen original API owner")
    if not re.fullmatch(r"[0-9a-fA-F]{64}", owner.get("checkpointSha256", "")): raise ValueError("Original source owner checkpoint digest is invalid")
    prefix = load(evidence_path)
    def field(name): return prefix.get(name, prefix.get(name[0].lower() + name[1:]))
    if field("Protocol") != "session-archive:v1:ownership-only" or field("ArchiveOwnershipOnly") is not True or field("OwnerSnapshot") is not False:
        raise ValueError("Initial archive prefix is not original source-only ownership")
    session = config.get("sessionId")
    if not isinstance(session, str) or not 0 < len(session) <= 256: raise ValueError("Missing original archive session identity")
    epoch = "archive_" + hashlib.sha256(session.encode()).hexdigest()[:24]
    source_root = Path(field("SessionDirectory")).resolve(); approved = Path(config["directory"]).resolve()
    if source_root.name != epoch or not source_root.is_relative_to(approved): raise ValueError("Initial archive belongs to another source/session root")
    files = field("Files"); records = field("Records")
    if not isinstance(files, list) or not 3 <= len(files) <= 100000 or isinstance(records, bool) or not isinstance(records, int) or records < 0:
        raise ValueError("Initial archive prefix count/offset metadata invalid")
    seen = set(); checked = []; receipts = None; body_files = 0
    for item in files:
        relative = Path(item.get("RelativePath", item.get("relativePath", ""))); length = item.get("Length", item.get("length"))
        key = relative.as_posix().replace("\\", "/")
        if relative.is_absolute() or ".." in relative.parts or key in seen or key not in ("archive-only.protocol", "sources/receipts.bin", "sources/delivery-receipts.bin") and not (relative.parent.as_posix() == "sources" and relative.suffix == ".rto"):
            raise ValueError("Initial archive prefix escapes/repeats its authoritative files")
        source = source_root / relative
        if source.is_symlink() or not source.resolve().is_relative_to(source_root) or isinstance(length, bool) or not isinstance(length, int) or length < 0 or source.stat().st_size != length:
            raise ValueError("Initial archive owner changed or is not quiescent under its snapshot fence")
        if key == "sources/receipts.bin": receipts = length
        if relative.suffix == ".rto": body_files += 1
        if key == "sources/delivery-receipts.bin" and length: raise ValueError("Source archive cannot claim handler settlement")
        seen.add(key); checked.append((key, source, length))
    if not {"archive-only.protocol", "sources/receipts.bin", "sources/delivery-receipts.bin"} <= seen or receipts is None or receipts % 148 or receipts // 148 != records:
        raise ValueError("Initial archive ordinal is not derived from original complete receipts")
    return config, prefix, owner, checked, records, actual_sha

def copy_initial_archive_floor(plan, proof, root, remaining):
    config, prefix, owner, files, ordinal, prefix_sha = initial_archive_floor(plan, proof)
    target_root = root / "owners" / "session-archive-boundary"; artifacts = []; copied_files = []
    for relative, source, length in files:
        same_fence(plan, proof)
        if length > remaining: raise ValueError("Initial archive floor exceeds snapshot byte budget")
        target = target_root / "initial-archive" / relative; target.parent.mkdir(parents=True, exist_ok=True)
        before = (source.stat().st_size, source.stat().st_mtime_ns)
        with source.open("rb") as incoming, target.open("xb") as outgoing:
            left = length
            while left:
                block = incoming.read(min(left, 256 * 1024))
                if not block: raise ValueError("Initial source archive was truncated")
                outgoing.write(block); left -= len(block)
            outgoing.flush(); os.fsync(outgoing.fileno())
        if before != (source.stat().st_size, source.stat().st_mtime_ns) or digest(source) != digest(target): raise ValueError("Original API archive changed under its owner fence")
        sha = digest(target); remaining -= length
        copied_files.append({"RelativePath": relative, "Length": length, "Sha256": sha})
        artifacts.append({"kind":"owner-file", "owner":"session-archive-boundary", "relativePath":"initial-archive/" + relative, "path":str(target), "bytes":length, "sha256":sha})
    evidence_source = Path(config["prefixEvidencePath"]); evidence_target = target_root / "initial-archive-prefix.json"
    evidence_bytes = evidence_source.read_bytes()
    if len(evidence_bytes) > remaining or hashlib.sha256(evidence_bytes).hexdigest() != prefix_sha: raise ValueError("Initial archive prefix metadata changed/budget exceeded")
    with evidence_target.open("xb") as output: output.write(evidence_bytes); output.flush(); os.fsync(output.fileno())
    remaining -= len(evidence_bytes); artifacts.append({"kind":"owner-file","owner":"session-archive-boundary","relativePath":evidence_target.name,"path":str(evidence_target),"bytes":len(evidence_bytes),"sha256":prefix_sha})
    boundary = {"Version":1,"Kind":"FrozenOwnerSourceArchiveFloor","ArchiveSessionId":config["sessionId"],"Protocol":"session-archive:v1:ownership-only",
                "BoundaryId":proof["boundaryId"],"LeaseId":proof["leaseId"],"Owner":"source-outbox","OwnerCheckpointSha256":owner["checkpointSha256"],
                "PrefixEvidenceSha256":prefix_sha,"LastCommittedOrdinal":ordinal,"Files":copied_files,"Qualified":False}
    boundary_path = target_root / "initial-archive-boundary.json"; write(boundary_path,boundary)
    length = boundary_path.stat().st_size
    if length > remaining: raise ValueError("Initial archive binding exceeds snapshot budget")
    artifacts.append({"kind":"owner-file","owner":"session-archive-boundary","relativePath":boundary_path.name,"path":str(boundary_path),"bytes":length,"sha256":digest(boundary_path)})
    same_fence(plan,proof)
    return artifacts, remaining - length, {"path":str(boundary_path),"sha256":digest(boundary_path),"lastCommittedOrdinal":ordinal}

def snapshot(plan, output, execute=False, held_readers=None):
    validate_plan(plan); proof = fence(plan); root = Path(output).resolve()
    configured = plan["snapshot"]
    if not configured["postgres"] or not configured["scylla"]:
        raise ValueError("Real PostgreSQL and Scylla snapshot providers must be configured")
    file_owners = {value["owner"] for value in configured["fileRoots"]}
    required_files = {"source-outbox", "domain-journals", "indicator-state", "workflow-state", "emulator-ledger", "registrations-config"}
    if required_files - file_owners: raise ValueError("Missing required snapshot file-owner providers: " + ",".join(sorted(required_files - file_owners)))
    if root.exists(): raise ValueError("Use a new snapshot output directory")
    initial_archive_floor(plan, proof)
    if not execute: return {"state": "ReadyForReviewedSnapshot", "boundaryId": proof["boundaryId"], "writesPerformed": False}
    root.mkdir(parents=True); manifest = {"version": 1, "state": "Incomplete", "boundary": proof, "artifacts": []}
    remaining = plan["snapshot"]["maximumBytes"]
    try:
        archive_artifacts, remaining, archive_boundary = copy_initial_archive_floor(plan, proof, root, remaining)
        manifest["artifacts"].extend(archive_artifacts); manifest["initialArchiveBoundary"] = archive_boundary
        for index, item in enumerate(plan["snapshot"]["postgres"]):
            same_fence(plan, proof)
            command = ["docker", "exec", "--user", "postgres", identifier(item["container"]), "pg_dump",
                       "--username", identifier(item["user"]), "--format=custom", "--no-owner", "--dbname", identifier(item["database"])]
            artifact = bounded_export(command, root / f"postgres-{index}.dump", remaining)
            image = subprocess.check_output(["docker", "inspect", "--format", "{{.Image}}", identifier(item["container"])], timeout=30, text=True).strip()
            artifact.update(kind="postgres", database=item["database"], providerImageId=image); manifest["artifacts"].append(artifact); remaining -= artifact["bytes"]
        for index, item in enumerate(plan["snapshot"]["scylla"]):
            same_fence(plan, proof)
            container = identifier(item["container"]); keyspace = identifier(item["keyspace"])
            tag = "ifm-session-" + identifier(proof["boundaryId"])
            subprocess.run(["docker", "exec", container, "nodetool", "snapshot", "-t", tag, keyspace], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=600)
            directories = subprocess.check_output(["docker", "exec", container, "find", "/var/lib/scylla/data/" + keyspace,
                "-type", "d", "-path", "*/snapshots/" + tag], timeout=30, text=True).splitlines()
            if not directories or any(not value.startswith("/var/lib/scylla/data/" + keyspace + "/") or not value.endswith("/snapshots/" + tag) for value in directories):
                raise ValueError("Scylla snapshot artifacts are missing or outside the requested keyspace")
            schema = bounded_export(["docker", "exec", container, "cqlsh", "--execute", "DESCRIBE KEYSPACE " + keyspace + ";"], root / f"scylla-{index}.cql", min(remaining, 8 * 1024 * 1024),timeout=600)
            schema.update(kind="scylla-schema", keyspace=keyspace); manifest["artifacts"].append(schema); remaining -= schema["bytes"]
            artifact = bounded_export(["docker", "exec", container, "tar", "-cf", "-", "-C", "/var/lib/scylla/data",
                *[value.removeprefix("/var/lib/scylla/data/") for value in directories]], root / f"scylla-{index}.tar", remaining)
            image = subprocess.check_output(["docker", "inspect", "--format", "{{.Image}}", container], timeout=30, text=True).strip()
            artifact.update(kind="scylla", keyspace=keyspace, tag=tag, providerImageId=image); manifest["artifacts"].append(artifact); remaining -= artifact["bytes"]
        for index, item in enumerate(plan["snapshot"]["fileRoots"]):
            source = Path(item["path"]).resolve()
            restore_owner=identifier(item.get("restoreOwner",f"{item['owner']}-{index}"))
            if source == root or source in root.parents or root in source.parents: raise ValueError("Snapshot output overlaps an owner source")
            if not source.is_dir(): raise ValueError("Required owner file root is absent: " + item["owner"])
            selected = [source / relative for relative in item["files"]] if "files" in item else sorted(source.rglob("*"))
            for path in selected:
                if not path.resolve().is_relative_to(source): raise ValueError("Owner file selection escapes its reviewed root")
                if path.is_symlink(): raise ValueError("Owner snapshot contains a symlink/reparse alias")
                if not path.is_file(): continue
                same_fence(plan, proof)
                target = root / "owners" / str(index) / path.relative_to(source); target.parent.mkdir(parents=True, exist_ok=True)
                before = (path.stat().st_size, path.stat().st_mtime_ns); copied = 0
                held = (held_readers or {}).get(str(path.resolve()))
                if held is not None: held.seek(0)
                with (contextlib.nullcontext(held) if held is not None else path.open("rb")) as incoming, target.open("xb") as outgoing:
                    for block in iter(lambda: incoming.read(256 * 1024), b""):
                        copied += len(block)
                        if copied > remaining: raise ValueError("Snapshot file budget exhausted")
                        outgoing.write(block)
                    outgoing.flush(); os.fsync(outgoing.fileno())
                if held is not None:
                    held.seek(0); source_hash=hashlib.file_digest(held,'sha256').hexdigest();held.seek(0)
                else: source_hash=digest(path)
                if before != (path.stat().st_size, path.stat().st_mtime_ns) or source_hash != digest(target): raise ValueError("Owner file changed under snapshot fence")
                manifest["artifacts"].append({"kind": "owner-file", "owner": item["owner"], "restoreOwner": restore_owner, "relativePath": str(path.relative_to(source)), "path": str(target), "bytes": copied, "sha256": digest(target)})
                remaining -= copied
        same_fence(plan, proof)
        if not manifest["artifacts"]: raise ValueError("Empty owner snapshot")
        manifest.update(state="CapturedUnderHeldFence", completedUtc=utc(), qualified=False,
                        ownerRoots=[{**item,"restoreOwner":item.get("restoreOwner",f"{item['owner']}-{index}")} for index,item in enumerate(configured["fileRoots"])],
                        limitations=["Restore/recovery and source cursor reconciliation are required before qualification"])
        return manifest
    finally: write(root / "snapshot.json", manifest)

def replay_plan(plan, capture_path, snapshot_path, output):
    validate_plan(plan); capture = load(Path(capture_path) / "capture.json"); initial = load(Path(snapshot_path) / "snapshot.json")
    if initial.get("state") != "CapturedUnderHeldFence": raise ValueError("Initial owner snapshot is not captured under a verified fence")
    if not {"postgres", "scylla", "scylla-schema", "owner-file"} <= {value.get("kind") for value in initial.get("artifacts", [])}:
        raise ValueError("Initial snapshot lacks required real store artifacts")
    for artifact in initial["artifacts"]:
        if digest(artifact["path"]) != artifact["sha256"]: raise ValueError("Initial snapshot changed")
    result = {"version": 1, "state": "PreparedNotExecuted", "qualified": False,
              "captureSha256": digest(Path(capture_path) / "capture.json"), "initialSnapshotSha256": digest(Path(snapshot_path) / "snapshot.json"),
              "captureKind": capture["kind"], "workloadPolicy": plan.get("workloadPolicy", {}), "runs": [], "blockers": []}
    if capture["kind"] != "AuthoritativeSourceEnvelopeCapture": result["blockers"].append("CoreDiagnosticObserver cannot be promoted without authoritative source-ledger reconciliation")
    if not capture.get("sourceLedgerReconciled"): result["blockers"].append("Missing complete accepted/refused original-source ledger reconciliation")
    for mode in ("Memory", "File"):
        for index, session in enumerate(plan["sessions"]):
            result["runs"].append({"mode": mode, "session": session, "namespace": f"IFM_QUAL_{mode.upper()}_{initial['boundary']['boundaryId']}",
                "initialSnapshotSha256": result["initialSnapshotSha256"], "sourceCaptureSha256": result["captureSha256"], "state": "NotStarted",
                "restoreBaseline": index == 0, "inheritsPriorSession": None if index == 0 else plan["sessions"][index - 1]["id"],
                "requirements": ["fresh disposable PostgreSQL/Scylla/NATS", "file emulator only; no live IBKR",
                    "one baseline restore per mode; carry first-session state into second", "original arrival pacing", "all required owner/recipient reconciliation"]})
    write(output, result); return result

def reconcile(expected_path, actual_path, output):
    """Exact machine-readable owner/source receipts, never P&L or counter-total equivalence alone."""
    expected = load(expected_path); actual = load(actual_path); failures = []
    for field in ("sessionId", "initialSnapshotSha256", "sourceCaptureSha256", "calendarEvidenceSha256"):
        if not expected.get(field) or expected[field] != actual.get(field): failures.append("boundary:" + field)
    if expected.get("authority") != "OriginalRecordedOwnerReceipts": failures.append("missing_original_owner_authority")
    if expected.get("normalSession") is not True or actual.get("completeSession") is not True: failures.append("incomplete_or_non_normal_session")
    if actual.get("pendingRequired", -1) != 0 or actual.get("unownedRefusals", -1) != 0 or actual.get("sourceGaps", ["missing"]): failures.append("unsettled_required_work_or_gap")
    for category in ("sources", "destinations", "rawRows", "indicators", "workflows", "orders", "fills", "cash", "holds", "positions", "plans", "eod", "sourceCursors"):
        left = expected.get(category); right = actual.get(category)
        if left is None or right is None or left != right: failures.append("owner:" + category)
    covered = {value["family"] for value in actual.get("families", []) if value.get("sourceCoverageVerified") is True and value.get("ownerOutcomeVerified") is True}
    if set(FAMILIES) - covered: failures.append("missing_family_coverage:" + ",".join(sorted(set(FAMILIES) - covered)))
    result = {"version": 1, "passed": not failures, "failures": failures,
              "expectedSha256": digest(expected_path), "actualSha256": digest(actual_path), "checkedUtc": utc()}
    write(output, result); return result

def job_path(job_path, value):
    path = Path(value)
    return path if path.is_absolute() else Path(job_path).resolve().parent / path

def preflight_job(job_path_value, output=None, verify_artifacts=True):
    job = load(job_path_value); plan = validate_plan(load(job_path(job_path_value, job["plan"])))
    failures = []; observations = {}; overlay = load(job_path(job_path_value, job["overlay"]))
    actors = overlay.get("Actors", {})
    if actors.get("RealtimeMailboxes", {}).get("Enabled") is not False or actors.get("RealtimeMailboxes", {}).get("CapacityQualified") is not False:
        failures.append("prepared_overlay_would_activate_unqualified_mailbox_backend")
    if actors.get("SessionCapture", {}).get("Enabled") is not True: failures.append("prepared_overlay_has_no_capture_only_archive")
    reference = job.get("ownerSnapshotRef")
    if not reference: failures.append("owner_snapshot_not_acquired")
    else:
        initial = load(job_path(job_path_value, reference)); floor = initial.get("matchingOwnerFloor", {})
        if initial.get("state") != "CapturedUnderHeldFence": failures.append("owner_snapshot_boundary_unproved")
        if floor.get("sourceSequence", 0) <= 0 or floor.get("sourceSequence") != floor.get("emulatorCommitRevision"):
            failures.append("exact_positive_emulator_PG_revision_not_proved")
        if not floor.get("sourceFloorVerified") or not floor.get("clockManifestSha256") or not floor.get("ledgerHeadSha256"):
            failures.append("owner_source_clock_ledger_binding_incomplete")
        if verify_artifacts:
            for item in initial.get("artifacts", []):
                if digest(item["path"]) != item["sha256"]: failures.append("owner_snapshot_artifact_changed")
        observations["ownerSnapshotSha256"] = digest(job_path(job_path_value, reference))
    for role, url in job["readiness"].items():
        local_url(url)
        try:
            state = get_json(url); observations[role] = {"http": 200, "status": state.get("status", state.get("Status"))}
            if state.get("status", state.get("Status")) not in ("Healthy", "Ready"): failures.append(role + "_not_ready")
        except urllib.error.HTTPError as error:
            body = error.read(1024 * 1024)
            try:
                data = json.loads(body); reasons = {name: value.get("description", "") for name, value in data.get("entries", {}).items() if value.get("status") == "Unhealthy"}
            except (ValueError, TypeError): reasons = {"response": "Non-JSON unhealthy response"}
            observations[role] = {"http": error.code, "unhealthyReasons": reasons}; failures.append(role + "_not_ready")
        except Exception as error: observations[role] = {"error": type(error).__name__}; failures.append(role + "_unavailable")
    token = os.environ.get(job.get("accessTokenEnv", "IFM_SESSION_CAPTURE_ACCESS_TOKEN"), "")
    if len(token) < 32: failures.append("protected_session_access_token_not_available")
    else:
        try:
            status = get_json(job["captureStatusUrl"], {"X-IFM-Session-Capture": token}); observations["capture"] = status
            if not status.get("enabled") or status.get("durableMailboxEnabled") is not False or status.get("archiveOwnershipOnly") is not True or status.get("failure"):
                failures.append("independent_capture_only_archive_unavailable")
        except Exception as error: observations["capture"] = {"error": type(error).__name__}; failures.append("capture_only_capability_unavailable")
    floor_ref = job.get("ownerFloorEvidenceRef")
    if not floor_ref: failures.append("live_owner_floor_not_acquired")
    else:
        floor = load(job_path(job_path_value, floor_ref))
        if floor.get("pgSnapshotSourceSequence", 0) <= 0 or floor.get("candidateLedgerCommitRevision", -1) != floor.get("pgSnapshotSourceSequence") or floor.get("matchingOwnerSnapshotProved") is not True:
            failures.append("live_owner_floor_legacy_zero_or_mismatched")
    if not plan.get("captureBudgetsCapacityQualified"): failures.append("capture_storage_budgets_not_capacity_qualified")
    result = {"version": 1, "state": "PreflightPassedNotStarted" if not failures else "Blocked", "eligibleCaptureStarted": False,
              "qualified": False, "checkedUtc": utc(), "jobSha256": digest(job_path_value), "planSha256": digest(job_path(job_path_value, job["plan"])),
              "blockers": sorted(set(failures)), "observations": observations}
    if output: write(output, result)
    return result

def watch_job(job_path_value, output):
    proof = preflight_job(job_path_value)
    if proof["blockers"]: raise ValueError("Capture job is not armed: " + ", ".join(proof["blockers"]))
    job = load(job_path_value); plan = load(job_path(job_path_value, job["plan"])); root = Path(output)
    root.mkdir(parents=True, exist_ok=False)
    end = max(timestamp(session["captureEndUtc"]) for session in plan["sessions"])
    start = min(timestamp(session["captureStartUtc"]) for session in plan["sessions"])
    if end - dt.datetime.now(UTC) > dt.timedelta(days=8): raise ValueError("Prepared job wait exceeds eight-day bound")
    state = {"version": 1, "state": "WaitingForPreparedSessions", "qualified": False, "preflight": proof,
             "ownerSnapshotRef": job["ownerSnapshotRef"], "sessions": plan["sessions"], "samples": 0, "gaps": []}
    if dt.datetime.now(UTC) > start: state["gaps"].append("observer_started_after_capture_boundary")
    try:
        with (root / "capability-witness.jsonl").open("x", encoding="utf-8") as evidence:
            while dt.datetime.now(UTC) < end:
                if dt.datetime.now(UTC) >= start:
                    check = preflight_job(job_path_value, verify_artifacts=False)
                    line = json.dumps(check, separators=(",", ":")) + "\n"
                    if evidence.tell() + len(line.encode()) > 64 * 1024 * 1024: raise ValueError("Capture witness reached its 64 MiB budget")
                    evidence.write(line); evidence.flush(); os.fsync(evidence.fileno()); state["samples"] += 1
                    if check["blockers"]: state["gaps"].append({"utc": utc(), "blockers": check["blockers"]}); raise ValueError("Capture witness lost required readiness/owner evidence")
                write(root / "job-status.json", state)
                time.sleep(min(30, max(0.1, (end - dt.datetime.now(UTC)).total_seconds())))
        state["state"] = "ObservationWindowEndedReconciliationRequired"
    finally: write(root / "job-status.json", state)

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__); commands = parser.add_subparsers(dest="command", required=True)
    for name in ("validate", "record", "snapshot"):
        sub = commands.add_parser(name); sub.add_argument("--plan", required=True)
        if name != "validate": sub.add_argument("--output", required=True)
        if name == "snapshot": sub.add_argument("--execute", action="store_true")
    sub = commands.add_parser("verify-tape"); sub.add_argument("--capture", required=True)
    sub = commands.add_parser("replay-plan")
    for name in ("plan", "capture", "snapshot", "output"): sub.add_argument("--" + name, required=True)
    sub = commands.add_parser("reconcile")
    for name in ("expected", "actual", "output"): sub.add_argument("--" + name, required=True)
    sub = commands.add_parser("copy-prefix")
    for name in ("plan", "prefix", "output"): sub.add_argument("--" + name, required=True)
    sub = commands.add_parser("copy-archive-prefix"); sub.add_argument("--job", required=True); sub.add_argument("--output", required=True); sub.add_argument("--prefix")
    for command in ("preflight-job", "watch-job"):
        sub = commands.add_parser(command); sub.add_argument("--job", required=True); sub.add_argument("--output", required=True)
    args = parser.parse_args(argv)
    if args.command == "validate": result = {"valid": bool(validate_plan(load(args.plan))), "snapshotFenceConfigured": bool(load(args.plan).get("boundary", {}).get("statusUrl"))}
    elif args.command == "verify-tape": result = {"messagesVerified": sum(1 for _ in read_tape(args.capture)), "qualified": False}
    elif args.command == "record": record(load(args.plan), args.output); result = {"recorded": True, "qualified": False}
    elif args.command == "snapshot": result = snapshot(load(args.plan), args.output, args.execute)
    elif args.command == "replay-plan": result = replay_plan(load(args.plan), args.capture, args.snapshot, args.output)
    elif args.command == "copy-prefix": result = copy_prefix(load(args.plan), args.prefix, args.output)
    elif args.command == "copy-archive-prefix": result = copy_archive_prefix(args.job, args.output, args.prefix)
    elif args.command == "preflight-job":
        result = preflight_job(args.job, args.output); print(json.dumps(result, indent=2)); return 0 if not result["blockers"] else 2
    elif args.command == "watch-job": watch_job(args.job, args.output); result = {"qualified": False, "state": "ReconciliationRequired"}
    else: result = reconcile(args.expected, args.actual, args.output)
    print(json.dumps(result, indent=2)); return 0 if result.get("passed", True) else 2

if __name__ == "__main__":
    try: sys.exit(main())
    except Exception as error: print(type(error).__name__ + ": " + str(error), file=sys.stderr); sys.exit(1)
