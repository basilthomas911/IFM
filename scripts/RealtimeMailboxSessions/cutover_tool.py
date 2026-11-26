"""Stopped-owner inventory and disposable config-switch/rollback rehearsal. Never activates an application."""
from __future__ import annotations
import argparse
import base64
import contextlib
import copy
import ctypes
import hashlib
import json
import os
import secrets
import ipaddress
from pathlib import Path
import struct
import uuid
import urllib.parse
import urllib.request

MAX_FRAME = 2 * 1024 * 1024
MAX_PENDING = 262144
MAX_BYTES = 8 * 1024 ** 3

def sha(value): return hashlib.sha256(value).hexdigest()

def exact(stream, count):
    value = stream.read(count)
    if len(value) != count: raise ValueError("Incomplete authoritative journal; recover its original owner first")
    return value

def text(stream):
    length = 0
    for shift in range(0, 35, 7):
        value = exact(stream, 1)[0]; length |= (value & 127) << shift
        if value < 128: break
    else: raise ValueError("Invalid original string length")
    if length > 65536: raise ValueError("Original metadata exceeds bound")
    return exact(stream, length).decode("utf-8")

def source_identity(body):
    import io
    reader=io.BytesIO(body)
    if struct.unpack("<i",exact(reader,4))[0]!=2: raise ValueError("Archive-only or unknown source is not handler ownership")
    key,source,manifest=text(reader),text(reader),text(reader)
    start=reader.tell()
    def subject():
        kind=struct.unpack("<i",exact(reader,4))[0];name,verb,entity=text(reader),text(reader),text(reader)
        if kind!=7 or not name or not verb or not entity or len(name)>256 or len(verb)>256 or len(entity)>16384: raise ValueError("Invalid realtime subject")
        return verb
    verb=subject();exact(reader,16);text(reader);exact(reader,16);header=body[start:reader.tell()]
    if len(text(reader))>256 or len(text(reader))>1024: raise ValueError("Trace metadata exceeds bounds")
    flag=exact(reader,1)[0]
    if flag not in (0,1): raise ValueError("Invalid origin presence")
    origin=text(reader) if flag else "unknown";clock=b""
    if flag:
        encoded=base64.b64decode(origin,validate=True)
        if len(encoded)<15 or encoded[:4]!=b"IMO\x02": raise ValueError("Invalid original origin metadata")
        length=struct.unpack("<H",encoded[12:14])[0];flags=encoded[14]
        if not 1<=length<=512 or flags&~7 or len(encoded)!=15+length+16*flags.bit_count(): raise ValueError("Invalid original origin shape")
        domain=encoded[15:15+length].decode("utf-8");run=str(uuid.UUID(bytes_le=encoded[15+length:31+length])) if flags&1 else ""
        clock=(domain+"|"+run).encode()
    count=struct.unpack("<i",exact(reader,4))[0]
    if not 0<=count<=256: raise ValueError("Destination count exceeds bound")
    start_manifest=reader.tell()
    for _ in range(count): subject()
    manifest_bytes=body[start_manifest:reader.tell()]
    length=struct.unpack("<i",exact(reader,4))[0]
    if not 0<length<=MAX_FRAME or reader.tell()>65536: raise ValueError("Source payload exceeds bound")
    payload=exact(reader,length)
    if reader.read(1): raise ValueError("Trailing original source bytes")
    actual=sha(header+origin.encode()+payload)
    actualkey=sha(header+clock+(bytes.fromhex(actual) if verb=="EvaluateFuturesExitClock" else b""))
    if (key,source,manifest)!=(actualkey,actual,sha(manifest_bytes)): raise ValueError("Original source identity/hash mismatch")
    return key,source,manifest

@contextlib.contextmanager
def stopped_owner(root):
    """Require the real owner's existing lease; do not create a lookalike lock."""
    root = Path(root).resolve(strict=True); path = root / "owner.lock"
    if not path.is_file() or path.is_symlink(): raise ValueError("Original owner lease is absent")
    if os.name == "nt":
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateFileW.restype = ctypes.c_void_p
        kernel.CreateFileW.argtypes = [ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
        handle = kernel.CreateFileW(str(path), 0xC0000000, 0, None, 3, 0x80, None)
        if handle == ctypes.c_void_p(-1).value: raise ValueError("Owner is active or its lease cannot be acquired")
        try: yield root
        finally: kernel.CloseHandle(ctypes.c_void_p(handle))
    else:
        import fcntl
        with path.open("r+b") as lease:
            try: fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except OSError as error: raise ValueError("Owner lease is active") from error
            try: yield root
            finally: fcntl.flock(lease, fcntl.LOCK_UN)

def journal_files(root, suffix):
    files = sorted(root.glob(suffix))
    if len(files) > MAX_PENDING: raise ValueError("Journal file count exceeds bound")
    if any(path.is_symlink() for path in files): raise ValueError("Journal links are refused")
    if sum(path.stat().st_size for path in files) > MAX_BYTES: raise ValueError("Journal inventory exceeds finite byte bound")
    return files

def backend_inventory(root):
    if (root.parent/"archive-only.protocol").exists() or (root.parent/"ownership.protocol").read_text()!="2:source-until-handler-proof":
        raise ValueError("Original handler ownership protocol is unavailable")
    required = {}; rows = 0; artifacts = []
    for path in journal_files(root, "*.rto"):
        staged = []
        with path.open("rb") as stream:
            while raw := stream.read(4):
                if len(raw) != 4: raise ValueError("Torn backend source length")
                length, = struct.unpack("<I", raw)
                if length == 0:
                    for identity in staged:
                        old = required.setdefault(identity[0], identity[1:])
                        if old != identity[1:]: raise ValueError("Conflicting original source identity")
                    staged = []; continue
                if length > MAX_FRAME or length < 4: raise ValueError("Backend source frame outside bound")
                checksum = exact(stream, 32); body = exact(stream, length)
                if hashlib.sha256(body).digest() != checksum: raise ValueError("Backend source checksum mismatch")
                key,source,manifest=source_identity(body)
                staged.append((key, source, manifest)); rows += 1
                if rows > MAX_PENDING: raise ValueError("Backend retained source count exceeds bound")
            if staged: raise ValueError("Uncommitted backend source group; recover owner before cutover")
        artifacts.append(file_proof(path))
    receipt = root / "receipts.bin"
    if not receipt.is_file() or receipt.is_symlink() or receipt.stat().st_size > MAX_BYTES: raise ValueError("Full-source receipt authority unavailable or over bound")
    original=dict(required)
    with receipt.open("rb") as stream:
        while row := stream.read(148):
            if len(row) != 148 or hashlib.sha256(row[:116]).digest() != row[116:]: raise ValueError("Full-source receipt checksum/tail mismatch")
            key = row[:32].hex()
            if key in original:
                if original[key] != (row[32:64].hex(), row[80:112].hex()): raise ValueError("Full-source receipt conflicts with original")
                required.pop(key,None)
    artifacts.append(file_proof(receipt))
    delivery=root/"delivery-receipts.bin"
    if not delivery.is_file() or delivery.stat().st_size>MAX_BYTES: raise ValueError("Destination receipt authority unavailable")
    with delivery.open("rb") as stream:
        while row:=stream.read(128):
            if len(row)!=128 or hashlib.sha256(row[:96]).digest()!=row[96:]: raise ValueError("Destination receipt checksum/tail mismatch")
    artifacts.extend([file_proof(delivery),file_proof(root.parent/"ownership.protocol")])
    return {"kind":"BackendFullHandlerOwnership", "pending":len(required),"retainedSources":rows,"artifacts":artifacts}

def native_inventory(root):
    namespace=root/"namespace.sha256"
    if not namespace.is_file() or namespace.is_symlink() or namespace.stat().st_size!=32: raise ValueError("Original native broker namespace authority unavailable")
    required = {}; rows = 0; artifacts = []
    for path in journal_files(root, "*.pubwal"):
        with path.open("rb") as stream:
            if exact(stream,8) != b"IFMSRC02": raise ValueError("Unknown native source journal")
            while raw := stream.read(4):
                if len(raw) != 4: raise ValueError("Torn native source length")
                length, = struct.unpack("<I", raw)
                if not 52 <= length <= MAX_FRAME: raise ValueError("Native frame outside bounds")
                body = exact(stream,length); checksum=exact(stream,32)
                if hashlib.sha256(body).digest() != checksum: raise ValueError("Native source checksum mismatch")
                ordinal, = struct.unpack("<Q",body[:8]); identity=sha(body)
                offset=40
                for bound in (512,32768,MAX_FRAME):
                    if offset+4>len(body): raise ValueError("Native original field is truncated")
                    size=struct.unpack("<I",body[offset:offset+4])[0];offset+=4
                    if size>bound or offset+size>len(body): raise ValueError("Native original field exceeds bound")
                    offset+=size
                if offset!=len(body) or ordinal==0: raise ValueError("Native original tail/ordinal invalid")
                if ordinal in required and required[ordinal] != identity: raise ValueError("Native ordinal conflicts")
                required[ordinal]=identity; rows += 1
                if rows > MAX_PENDING: raise ValueError("Native source count exceeds bounds")
        artifacts.append(file_proof(path))
    receipt=root/"confirmed.bin"
    if receipt.exists():
        if receipt.is_symlink() or receipt.stat().st_size > MAX_BYTES: raise ValueError("Native receipt authority outside bound")
        with receipt.open("rb") as stream:
            while row:=stream.read(40):
                if len(row)!=40 or hashlib.sha256(row[:8]).digest()!=row[8:]: raise ValueError("Native receipt checksum/tail mismatch")
                required.pop(struct.unpack("<Q",row[:8])[0],None)
        artifacts.append(file_proof(receipt))
    artifacts.append(file_proof(namespace))
    return {"kind":"NativeApiCaptureOwnershipOnly","pending":len(required),"retainedSources":rows,"artifacts":artifacts}

def file_proof(path):
    digest=hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda:stream.read(262144),b""): digest.update(block)
    return {"path":str(path.resolve()),"bytes":path.stat().st_size,"sha256":digest.hexdigest()}

def atomic(path, content):
    temporary=path.with_suffix(path.suffix+".tmp")
    with temporary.open("xb") as stream: stream.write(content);stream.flush();os.fsync(stream.fileno())
    os.replace(temporary,path)

def rehearse(backend, native, baseline, output, epoch):
    """Switch only a newly created disposable config, then restore its exact bytes. No process is launched."""
    if not epoch or len(epoch)>64 or any(char not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for char in epoch): raise ValueError("Bounded explicit target epoch required")
    baseline=Path(baseline).resolve(strict=True);output=Path(output).resolve()
    for source in (Path(backend).resolve(),Path(native).resolve(),baseline.parent):
        if source==output or source in output.parents or output in source.parents: raise ValueError("Disposable output overlaps original owner/config")
    if output.exists(): raise ValueError("Rehearsal requires a new disposable directory")
    original=baseline.read_bytes()
    if len(original)>1024*1024: raise ValueError("Configuration exceeds finite bound")
    config=json.loads(original);current=config.get("Actors",{}).get("RealtimeMailboxes",{})
    if current.get("Enabled",False): raise ValueError("Baseline must leave mailbox activation disabled")
    if current.get("Epoch")==epoch: raise ValueError("New epoch must differ from original")
    with stopped_owner(backend) as backend_root,stopped_owner(native) as native_root:
        before=[backend_inventory(backend_root),native_inventory(native_root)]
        if any(item["pending"] for item in before): raise ValueError("Pending original ownership prevents drained cutover/rollback")
        output.mkdir(parents=True);atomic(output/"disposable-owner.json",json.dumps({"kind":"CutoverRehearsalOnly","id":str(uuid.uuid4())}).encode())
        active=output/"appsettings.rehearsal.json";atomic(active,original)
        candidate=copy.deepcopy(config);actors=candidate.setdefault("Actors",{})
        actors.setdefault("SessionCapture",{})["Enabled"]=False
        target=actors.setdefault("RealtimeMailboxes",{});target.update(Enabled=True,Epoch=epoch,Directory=str(output/"isolated-owner"))
        encoded=json.dumps(candidate,sort_keys=True,indent=2).encode();atomic(output/"candidate.review-only.json",encoded)
        try:
            atomic(active,encoded)
            if active.read_bytes()!=encoded: raise ValueError("Isolated switch readback differs")
            check=json.loads(active.read_bytes())
            if check["Actors"]["SessionCapture"]["Enabled"] or not check["Actors"]["RealtimeMailboxes"]["Enabled"]: raise ValueError("Double-route exclusion failed")
        finally:
            atomic(active,original)
        if active.read_bytes()!=original or baseline.read_bytes()!=original: raise ValueError("Rollback failed exact original-byte verification")
        after=[backend_inventory(backend_root),native_inventory(native_root)]
        if before!=after: raise ValueError("Original ownership journals changed during rehearsal")
        result={"version":1,"kind":"DisposableConfigCutoverRollbackRehearsal","state":"Passed","activationPerformed":False,
            "liveCutoverQualified":False,"sourceOwnersStoppedAndLocked":True,"sourceInventory":before,
            "originalConfigSha256":sha(original),"candidateConfigSha256":sha(encoded),"rollbackConfigSha256":sha(active.read_bytes()),
            "limits":["Native capture receipts are not backend handler completion","No broker/database/emulator processes launched",
                "Live cutover still requires retained source forwarding hold, actor drain and matched-session owner qualification; this rehearsal does not attest them"]}
        atomic(output/"rehearsal.json",json.dumps(result,indent=2).encode());return result

def native_forwarding(base_url,token,lease,hold):
    """Explicit development-only native quiesce/resume; caller must separately drain backend owners."""
    address=urllib.parse.urlparse(base_url)
    if address.scheme!='http' or address.username or address.password or address.query or address.fragment or address.path not in ('','/'):
        raise ValueError('Explicit loopback HTTP origin required')
    if address.hostname!='localhost' and not ipaddress.ip_address(address.hostname or '').is_loopback: raise ValueError('Non-loopback source control refused')
    if not token or len(token)<32: raise ValueError('Protected source-control access token is unavailable')
    if not isinstance(lease,int) or not 0<lease<2**64: raise ValueError('Original nonzero native lease required')
    def request(suffix,method='GET'):
        message=urllib.request.Request(base_url.rstrip('/')+'/internal/session-capture/native/'+suffix,
            headers={'X-IFM-Session-Capture':token},method=method)
        with urllib.request.urlopen(message,timeout=15) as reply:
            body=reply.read(1024*1024+1)
            if len(body)>1024*1024: raise ValueError('Native lease response exceeds bound')
            return json.loads(body)
    before=request('status');status=before.get('status',{})
    if before.get('kind')!='NativeForwardingOnly' or before.get('crossOwnerSnapshot') is not False or not status.get('supported'):
        raise ValueError('Original native no-loss forwarding control is unavailable')
    current=status.get('lease',0)
    if hold and current not in (0,lease) or not hold and current!=lease: raise ValueError('Another owner holds the forwarding lease')
    request(('hold/' if hold else 'release/')+format(lease,'x'),'POST')
    after=request('status');observed=after.get('status',{})
    if observed.get('lease')!=(lease if hold else 0) or hold and observed.get('activeSends')!=0:
        raise ValueError('Native source lease readback did not confirm requested boundary; inspect original lease before retry')
    return {'version':1,'kind':'NativeForwardingOnly','crossOwnerSnapshot':False,'lease':lease,'leaseHex':format(lease,'x'),'held':hold,'readback':after,
        'backendDrained':False,'activationPerformed':False,'limitations':['Raw/source capture continues while forwarding is held',
            'Backend late requests, actors and financial owners require their own independent drain proof']}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native-control',choices=('hold','release'));parser.add_argument('--native-url');parser.add_argument('--token-env',default='IFM_SESSION_CAPTURE_ACCESS_TOKEN')
    parser.add_argument('--lease',type=lambda value:int(value,16));parser.add_argument('--execute-native-control',action='store_true')
    parser.add_argument("--backend-sources");parser.add_argument("--native-sources")
    parser.add_argument("--baseline-config");parser.add_argument("--output");parser.add_argument("--epoch")
    args=parser.parse_args()
    try:
        if args.native_control:
            if not args.execute_native_control: raise ValueError('Native control requires explicit --execute-native-control; no source changed')
            if not args.native_url: raise ValueError('Native loopback origin required')
            if args.native_control=='release' and not args.lease: raise ValueError('Resume requires the original held lease')
            result=native_forwarding(args.native_url,os.environ.get(args.token_env),args.lease or secrets.randbits(64) or 1,args.native_control=='hold')
        else:
            if not all((args.backend_sources,args.native_sources,args.baseline_config,args.output,args.epoch)): raise ValueError('Rehearsal requires source owners, baseline config, new output and epoch')
            result=rehearse(args.backend_sources,args.native_sources,args.baseline_config,args.output,args.epoch)
        print(json.dumps(result,indent=2))
    except (ValueError,OSError,KeyError) as error: parser.exit(2,f"Cutover rehearsal refused: {error}\n")
if __name__=="__main__": main()
