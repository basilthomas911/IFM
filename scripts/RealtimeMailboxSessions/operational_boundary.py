"""Real stopped-writer boundary coordinator. No forced termination or synthetic owner checkpoint."""
from __future__ import annotations
import contextlib
import copy
import datetime as dt
import hashlib
import http.server
import json
import os
from pathlib import Path
import subprocess
import threading
import time
import uuid
import urllib.request
import urllib.error
import ctypes
import session_tool as tools
import cutover_tool


def field(value, name, default=None):
    return value.get(name, value.get(name[0].lower() + name[1:], default))


def request(base, route, token, post=False, timeout=15, allow_degraded_health=False):
    url = base.rstrip('/') + route; tools.local_url(url)
    req = urllib.request.Request(url, data=b'' if post else None,
        headers={'X-IFM-Session-Capture': token, 'Accept': 'application/json'}, method='POST' if post else 'GET')
    try:reply=urllib.request.urlopen(req, timeout=timeout)
    except urllib.error.HTTPError as error:
        if not (allow_degraded_health and not post and route=='/health/ready' and error.code==503):raise
        reply=error
    with reply:
        body = reply.read(4 * 1024 * 1024 + 1)
        if len(body) > 4 * 1024 * 1024: raise ValueError('Operational response exceeds bound')
        return json.loads(body)


def wait_for_settled_market_data(config, token):
    """Let accepted feed startup finish before pausing the callbacks it needs for readiness."""
    until=time.monotonic()+config.get('drainSeconds',120)
    while True:
        health=request(config['apiUrl'],'/health/ready',token,allow_degraded_health=True)
        entry=field(field(health,'Entries',{}),'market_data_runtime',{})
        if field(entry,'Status')=='Healthy':return
        if time.monotonic()>=until:raise TimeoutError('Original feed owners have not settled; no snapshot pause was acquired')
        time.sleep(.5)


def command(argv, timeout=120):
    if not isinstance(argv, list) or not argv or any(not isinstance(x, str) or len(x) > 32768 for x in argv):
        raise ValueError('Explicit bounded argument vector required; no shell commands')
    result = subprocess.run(argv, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            timeout=timeout, check=False, shell=False)
    if result.returncode: raise RuntimeError('Operational command failed; provider output withheld')
    if len(result.stdout) > 4 * 1024 * 1024: raise ValueError('Operational command response exceeds bound')
    return json.loads(result.stdout.decode('utf-8-sig')) if result.stdout.strip() else None


def manager(config, action, lease=None):
    return command([config['powershell'], '-NoProfile', '-File', config['managerControl'],
                    '-Command', action, *(['-Lease', lease] if lease else [])], 310)


@contextlib.contextmanager
def existing_file_lease(path):
    """Open the authentic existing file exclusively; never create a replacement lock."""
    path=Path(path).resolve(strict=True)
    if path.is_symlink() or not path.is_file(): raise ValueError('Actual owner lease file is absent')
    if os.name=='nt':
        import msvcrt
        kernel=ctypes.WinDLL('kernel32',use_last_error=True)
        kernel.CreateFileW.restype=ctypes.c_void_p
        kernel.CreateFileW.argtypes=[ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
        handle=kernel.CreateFileW(str(path),0x80000000,0,None,3,0x80,None)
        if handle==ctypes.c_void_p(-1).value: raise ValueError('An original writer still owns the exact lease/file')
        fd=msvcrt.open_osfhandle(handle,os.O_RDONLY|os.O_BINARY)
        with os.fdopen(fd,'rb') as reader: yield reader
    else:
        import fcntl
        with path.open('rb') as reader:
            fcntl.flock(reader,fcntl.LOCK_EX|fcntl.LOCK_NB)
            try: yield reader
            finally: fcntl.flock(reader,fcntl.LOCK_UN)


def discovery(config):
    rows = manager(config, 'discover')
    if isinstance(rows, dict): rows = [rows]
    if not isinstance(rows, list) or len(rows) > 4096: raise ValueError('Writer discovery is unavailable or exceeds bound')
    if any(field(row, 'Ownership') != 'Owned' for row in rows): raise ValueError('An IFM process is unowned or ambiguous')
    return rows


def drained(value, lease, boundary):
    iti=field(value,'Iti')
    iti_drained=(iti is None or isinstance(iti,dict) and field(iti,'Drained') is True and field(iti,'Failed') is False
                 and all(type(field(iti,key)) is int and field(iti,key)==0 for key in ('Pending','InFlightCaptures','PreparedSources','UnflushedSources')))
    return (field(value, 'State') == 'LocallyDrained' and field(value, 'LocallyDrained') is True
        and field(value, 'LeaseId') == lease and field(value, 'BoundaryId') == boundary
        and field(value, 'RootProducersPaused') is True
        and all(type(field(value, key)) is int and field(value, key) == 0 for key in ('ActiveRootPublications', 'ActorQueued', 'ActorProcessing', 'RawPending', 'RawUnacknowledged', 'ArchivePending'))
        and not field(value, 'ArchiveFailure') and iti_drained)


def native_held(config, token, lease):
    status = field(request(config['nativeUrl'], '/internal/session-capture/native/status', token), 'Status', {})
    if field(status, 'Supported') is not True or field(status, 'Lease') != lease or field(status, 'ActiveSends') != 0:
        raise ValueError('Original native forwarding lease is not held and drained')


def pause_and_drain_native(config,token,lease):
    """Park new native mutations without retiring owner state; drain accepted originals."""
    until=time.monotonic()+config.get('drainSeconds',120)
    while True:
        try:
            paused=request(config['nativeUrl'],f'/internal/session-capture/native/pause-production/{lease:x}',token,True,min(30,max(.1,until-time.monotonic())))
            status=field(paused,'Status',{})
            if field(status,'Supported') is not True or field(status,'Lease')!=lease or field(status,'ActiveCallbacks')!=0:
                raise ValueError('Native production pause did not establish original mutation ownership')
            if field(paused,'RawRetentionLease')!=lease:raise ValueError('Raw native checkpoint prefixes are not retained through shutdown')
            break
        except urllib.error.HTTPError as error:
            if error.code!=409:raise
            status=field(request(config['nativeUrl'],'/internal/session-capture/native/status',token),'Production',{})
            if field(status,'Lease')!=lease:raise ValueError('Another native production pause owns this boundary')from error
            if time.monotonic()>=until:raise TimeoutError('Original native mutation callbacks did not drain')from error
            time.sleep(.1)
    while True:
        if time.monotonic()>=until:raise TimeoutError('Accepted original native publications did not drain')
        request(config['nativeUrl'],f'/internal/session-capture/native/hold/{lease:x}',token,True)
        native_held(config,token,lease)
        prefix=request(config['nativeUrl'],f'/internal/session-capture/native/prefix/{lease:x}',token)
        pending=field(prefix,'PendingCount')
        if type(pending)is not int or pending<0:raise ValueError('Native source pending ownership is unknown')
        if pending==0:return prefix
        request(config['nativeUrl'],f'/internal/session-capture/native/release/{lease:x}',token,True)
        time.sleep(.1)


def prepare_profile_shutdown(config,token,output):
    """No snapshot: leave providers parked, accepted ownership drained and real leases held for process shutdown."""
    root=Path(output);root.mkdir(parents=True,exist_ok=False)
    wait_for_settled_market_data(config,token)
    lease=uuid.uuid4().hex;boundary=uuid.uuid4().hex;native_lease=int(lease[:16],16)
    hold=manager(config,'hold',lease)
    if field(hold,'State')!='Held' or field(hold,'LeaseId')!=lease or field(hold,'ForcedStop')is not False:
        raise ValueError('Actual manager capture hold was refused before shutdown')
    prefix=pause_and_drain_native(config,token,native_lease)
    request(config['apiUrl'],f'/internal/session-capture/quiesce/{lease}/{boundary}',token,True)
    until=time.monotonic()+config.get('drainSeconds',120);consecutive=0
    while consecutive<3:
        if time.monotonic()>=until:raise TimeoutError('Original API ownership did not drain before profile shutdown')
        value=request(config['apiUrl'],'/internal/session-capture/boundary',token)
        consecutive=consecutive+1 if drained(value,lease,boundary) else 0;time.sleep(.1)
    current=request(config['nativeUrl'],f'/internal/session-capture/native/prefix/{native_lease:x}',token)
    if field(current,'PendingCount')!=0 or field(current,'NextOrdinal')!=field(prefix,'NextOrdinal'):
        raise ValueError('Native source progressed after production was parked')
    receipt={'version':1,'state':'OriginalSourcesParkedAndDrainedForShutdown','qualified':False,'snapshotCreated':False,
             'leaseId':lease,'nativeLease':native_lease,'apiDrain':value,'nativePrefix':current}
    tools.write(root/'shutdown-preparation.json',receipt);return receipt


def stopped_native_prefix_unchanged(prefix):
    """A parked source must not append a tail after the atomic image was exported."""
    groups=[{'SourceRoot':field(prefix,'SourceRoot'),'Files':field(prefix,'Files',[])}]+field(prefix,'RawSources',[])
    for group in groups:
        root=Path(field(group,'SourceRoot')).resolve(strict=True)
        for item in field(group,'Files',[]):
            path=(root/field(item,'RelativePath')).resolve(strict=True);length=field(item,'Length')
            if not path.is_relative_to(root) or type(length)is not int or path.stat().st_size!=length:
                raise ValueError('Native owner changed after its frozen original checkpoint prefix')


def certificate(directory, response, boundary):
    root = Path(directory).resolve(strict=True); path = root / boundary / 'checkpoint.json'
    value = tools.load(path)
    def schema_fields(document):
        if not isinstance(document,dict):raise ValueError('Native certificate must be an object')
        result={key.casefold():item for key,item in document.items()}
        if len(result)!=len(document):raise ValueError('Native certificate has ambiguous schema keys')
        return result
    # Minimal API uses camelCase; the on-disk certificate uses CLR property names.
    # Compare every schema field/value, without changing any artifact or business bytes.
    if schema_fields(value) != schema_fields(response) or field(value, 'Version') != 1 or field(value, 'Kind') != 'FrozenNativeAndBackendSnapshot':
        raise ValueError('Actual native certificate readback differs')
    for prefix in ('Checkpoint', 'BackendSnapshot', 'SourcePrefix'):
        name = field(value, prefix + 'File'); expected = field(value, prefix + 'Sha256'); length = field(value, prefix + 'Bytes')
        child = (path.parent / name).resolve(strict=True)
        if child.parent != path.parent or child.stat().st_size != length or tools.digest(child) != expected:
            raise ValueError('Original native certificate artifact changed')
    return path


class HeldBoundary:
    """Holds actual existing file leases and continuously checks stopped writers/native ownership."""
    def __init__(self, config, plan, token, output, copy_native_prefixes=True):
        self.config=config; self.plan=copy.deepcopy(plan); self.token=token; self.output=Path(output)
        self.lease=uuid.uuid4().hex; self.boundary=uuid.uuid4().hex; self.native_lease=int(self.lease[:16],16)
        self.native_boundary=self.boundary[:12]; self.native_stopped=False
        self.stack=contextlib.ExitStack(); self.server=None; self.complete=False; self.stopped=False
        self.held_readers={}
        self.owner_stamps=[]
        self.copy_native_prefixes=copy_native_prefixes
        self.check_gate=threading.Lock();self.last_check=0.0;self.check_failure=None

    def __enter__(self):
        try: return self._enter()
        except Exception as error:
            self.stack.close()
            if self.output.exists(): tools.write(self.output/'blocked.json',{'state':'BlockedRetainedOwnership','leaseId':self.lease,'nativeLease':self.native_lease,'boundaryId':self.boundary,'exception':type(error).__name__,'qualified':False})
            raise

    def _enter(self):
        self.output.mkdir(parents=True,exist_ok=False)
        wait_for_settled_market_data(self.config,self.token)
        before=discovery(self.config); self.writers=[row for row in before if field(row,'Role') in ('api','scheduler','ui')]
        if len([x for x in self.writers if field(x,'Role')=='api'])!=1: raise ValueError('Exactly one owned API is required')
        hold=manager(self.config,'hold',self.lease)
        if field(hold,'State')!='Held' or field(hold,'LeaseId')!=self.lease or field(hold,'ForcedStop') is not False:
            raise ValueError('Manager did not establish its no-force scheduler/recovery hold')
        if any(field(x,'Role') in ('scheduler','ui') for x in discovery(self.config)): raise ValueError('Scheduler/UI writer is still running')
        pause_and_drain_native(self.config,self.token,self.native_lease)
        request(self.config['apiUrl'],f'/internal/session-capture/quiesce/{self.lease}/{self.boundary}',self.token,True)
        until=time.monotonic()+self.config.get('drainSeconds',120); consecutive=0
        while consecutive<3:
            if time.monotonic()>until: raise TimeoutError('Actual API drain did not stabilize')
            value=request(self.config['apiUrl'],'/internal/session-capture/boundary',self.token)
            consecutive=consecutive+1 if drained(value,self.lease,self.boundary) else 0
            time.sleep(.2)
        self.local_drain=value; tools.write(self.output/'api-local-drain.json',value)
        settings=request(self.config['apiUrl'],'/internal/session-capture/configuration',self.token)
        if field(settings,'AllOwnerPathsExplicit') is not True:raise ValueError('Original runtime owner paths are not explicit and complete')
        tools.write(self.output/'effective-configuration.json',settings)
        # Capture original source floor and financial owner while real API is held, never after constructing a replacement.
        financial=request(self.config['apiUrl'],f'/internal/session-capture/emulator/checkpoint/{self.boundary}',self.token,True,120)
        if field(financial,'FinancialValuesMatched') is not True: raise ValueError('Actual emulator/account projection has not converged')
        tools.write(self.output/'emulator-owner.json',financial)
        until=time.monotonic()+self.config.get('drainSeconds',120)
        while True:
            value=request(self.config['apiUrl'],'/internal/session-capture/boundary',self.token)
            if drained(value,self.lease,self.boundary): break
            if time.monotonic()>until: raise TimeoutError('Financial owner publication did not drain')
            time.sleep(.2)
        tools.write(self.output/'api-local-drain.json',value)
        archive=request(self.config['apiUrl'],'/internal/session-capture/prefix',self.token)
        tools.write(self.output/'initial-archive-prefix.json',archive)
        native=request(self.config['nativeUrl'],f'/internal/session-capture/native/checkpoint/export/{self.native_lease:x}/{self.native_boundary}',self.token,True,120)
        tools.write(self.output/'native-certificate-response.json',native)
        self.native_certificate=certificate(self.config['nativeCheckpointDirectory'],native,self.native_boundary)
        tools.write(self.output/'native-certificate-ref.json',{'path':str(self.native_certificate),'sha256':tools.digest(self.native_certificate)})
        if self.copy_native_prefixes:tools.copy_prefix(self.plan,self.native_certificate.parent/'source-prefix.json',self.output/'native-prefixes')
        request(self.config['apiUrl'],f'/internal/session-capture/stop/{self.lease}',self.token,True)
        manager(self.config,'stop-native',self.lease); self.native_stopped=True
        until=time.monotonic()+self.config.get('drainSeconds',120)
        while any(field(x,'Role') in ('api','scheduler','ui','market-data','market-data-worker') for x in discovery(self.config)):
            if time.monotonic()>until: raise TimeoutError('Graceful API/scheduler process exit was not proved; no forced stop attempted')
            time.sleep(.2)
        self.stopped=True
        native_prefix=tools.load(self.native_certificate.parent/'source-prefix.json')
        stopped_native_prefix_unchanged(native_prefix)
        source_root=Path(native_prefix['SourceRoot']).resolve(strict=True)
        if not tools.same_directory(source_root,self.plan['nativeSourceRoot']):raise ValueError('Stopped native source root differs from its original prefix')
        native_leases=[source_root/'owner.lock']
        raw_root=Path(self.plan['nativeRawRoot']).resolve(strict=True)
        native_leases.append(raw_root/'.publisher.lock')
        for raw in native_prefix.get('RawSources',[]):
            if not tools.same_directory(raw['SourceRoot'],raw_root):raise ValueError('Stopped raw source root differs')
            native_leases.append(raw_root/(raw['CurrentEpoch']+'.owner'))
        for path in dict.fromkeys(native_leases):self.stack.enter_context(existing_file_lease(path))
        # Every listed owner must have an actual existing exclusive writer lease.
        lease_roots=self.config.get('ownerLeaseRoots',[])
        required={'source-outbox','domain-journals','indicator-state','workflow-state','emulator-ledger','registrations-config'}
        if {x['owner'] for x in lease_roots}!=required: raise ValueError('Complete actual file-owner lease providers are required')
        seen={}; owners=[]
        for item in lease_roots:
            root=Path(item['path']).resolve(strict=True)
            explicit=item.get('leaseFiles',[])
            exact=item.get('exactFiles',[])
            if item.get('exactAllFiles'):
                exact=[str(path.relative_to(root)) for path in sorted(root.rglob('*')) if path.is_file()]
            if not explicit and not exact: raise ValueError('Actual lease files or exact stopped-writer configuration files are required')
            for relative in explicit+exact:
                path=(root/relative).resolve(strict=True)
                if not path.is_relative_to(root): raise ValueError('Actual lease escapes its owner root')
                if path not in seen:
                    reader=self.stack.enter_context(existing_file_lease(path));seen[path]=True
                    self.held_readers[str(path)]=reader
            files=[]
            selected=[root/relative for relative in item.get('files',[])] if item.get('files') else sorted(root.rglob('*'))
            for path in selected:
                if path.is_symlink(): raise ValueError('Owner root contains a link')
                if path.is_file() and str(path.relative_to(root)) not in explicit:
                    reader=self.held_readers.get(str(path.resolve()))
                    if reader is not None: reader.seek(0);digest=hashlib.file_digest(reader,'sha256').hexdigest();reader.seek(0)
                    else:digest=tools.digest(path)
                    files.append([str(path.relative_to(root)),path.stat().st_size,digest])
            checksum=hashlib.sha256(json.dumps(files,separators=(',',':')).encode()).hexdigest()
            # The full per-file witnesses are the snapshot artifacts. Keep the
            # live fence bounded even when a journal has 100,000 retained keys.
            evidence={'path':str(root),'fileCount':len(files),'fileSetSha256':checksum,
                'leaseFiles':explicit,'exactFileLeaseCount':len(exact),'writersExited':self.writers}
            if item['owner']=='source-outbox': evidence.update(sessionArchivePrefixSha256=tools.digest(self.output/'initial-archive-prefix.json'),sessionArchiveSessionId=self.config['archiveSessionId'])
            owners.append({'owner':item['owner'],'state':'Frozen','checkpointSha256':checksum,'evidence':evidence})
            self.owner_stamps.append((root,[(str(path.relative_to(root)),path.stat().st_size,path.stat().st_mtime_ns) for path in selected if path.is_file()]))
        self.plan['snapshot']['sourceArchive']={'prefixEvidencePath':str((self.output/'initial-archive-prefix.json').resolve()),'prefixEvidenceSha256':tools.digest(self.output/'initial-archive-prefix.json'),'sessionId':self.config['archiveSessionId'],'directory':self.config['archiveDirectory']}
        financial_owner=field(financial,'Owner',{});ledger=Path(field(financial_owner,'EffectivePath',''))
        if not ledger.is_file() or ledger.stat().st_size!=field(financial_owner,'Length') or tools.digest(ledger).lower()!=str(field(financial_owner,'Sha256')).lower():
            raise ValueError('Actual stopped emulator ledger differs from the owner-issued projection checkpoint')
        self.proof={'version':1,'state':'Held','leaseId':self.lease,'boundaryId':self.boundary,'forcedStop':False,
            'sourceContinuity':'RetainedBeforeForwardPause','owners':owners}
        for name in ('postgres','scylla','actor-drain','native-source','native-mapper'):
            evidence={'nativeCertificateSha256':tools.digest(self.native_certificate),'apiLocalDrainSha256':tools.digest(self.output/'api-local-drain.json'),'writersExited':self.writers}
            self.proof['owners'].append({'owner':name,'state':'Frozen','checkpointSha256':hashlib.sha256(json.dumps(evidence,sort_keys=True).encode()).hexdigest(),'evidence':evidence})
        # Consolidate multiple actual journals in the same owner category.
        grouped={}
        for owner in self.proof['owners']:grouped.setdefault(owner['owner'],[]).append(owner)
        self.proof['owners']=[items[0] if len(items)==1 else {'owner':name,'state':'Frozen','checkpointSha256':hashlib.sha256(json.dumps(items,sort_keys=True).encode()).hexdigest(),'evidence':{'roots':items}} for name,items in grouped.items()]
        participants=[]
        for kind in ('postgres','scylla'):
            for provider in self.plan['snapshot'][kind]:
                result=subprocess.run(['docker','inspect','--format','{{.Id}}',provider['container']],capture_output=True,check=True,text=True,timeout=30)
                participants.append({'kind':kind,'container':provider['container'],'containerId':result.stdout.strip(),
                    **({'database':provider['database']} if kind=='postgres' else {'keyspace':provider['keyspace']})})
        self.proof.update(participants=participants,nativeCheckpoint={'path':str(self.native_certificate),'sha256':tools.digest(self.native_certificate)},
            apiLocalDrainPath=str((self.output/'api-local-drain.json').resolve()),apiLocalDrainSha256=tools.digest(self.output/'api-local-drain.json'),
            nativeLeaseFiles=[str(path) for path in native_leases])
        parent=self
        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                try:
                    parent.check(); result=copy.deepcopy(parent.proof)
                    result['expiresUtc']=(dt.datetime.now(tools.UTC)+dt.timedelta(minutes=2)).isoformat()
                    body=json.dumps(result).encode(); self.send_response(200); self.end_headers(); self.wfile.write(body)
                except Exception:
                    self.send_response(409); self.end_headers()
            def log_message(self,*_): pass
        self.server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler)
        threading.Thread(target=self.server.serve_forever,daemon=True).start()
        self.plan['boundary']={'statusUrl':f'http://127.0.0.1:{self.server.server_port}/boundary'}
        self.proof['statusUrl']=self.plan['boundary']['statusUrl']
        tools.write(self.output/'held-evidence.json',self.proof)
        return self

    def check(self, force=False):
        # Exact file/owner handles remain held continuously. Avoid O(N²) scans
        # and a subprocess pair per small receipt file during a large snapshot.
        with self.check_gate:
            if self.check_failure is not None:raise ValueError('Original owner fence previously failed') from self.check_failure
            if not force and time.monotonic()-self.last_check<1:return
            try:self._check();self.last_check=time.monotonic()
            except Exception as error:self.check_failure=error;raise

    def _check(self):
        if not self.native_stopped: native_held(self.config,self.token,self.native_lease)
        state=manager(self.config,'status')
        if field(state,'State')!='Held' or field(state,'LeaseId')!=self.lease: raise ValueError('Manager lifecycle ownership changed')
        if any(field(x,'Role') in ('api','scheduler','ui','market-data','market-data-worker') for x in discovery(self.config)): raise ValueError('An independent API/scheduler/UI/native writer restarted')
        for root,original in self.owner_stamps:
            for relative,length,mtime in original:
                current=(root/relative).stat()
                if (current.st_size,current.st_mtime_ns)!=(length,mtime):raise ValueError('Stopped original owner state changed under its actual file lease')

    def snapshot(self):
        self.check(force=True); result=tools.snapshot(self.plan,self.output/'owners',True,self.held_readers); self.check(force=True)
        # Persist the actual native image and coherent backend snapshot alongside
        # the store baseline; immutable prefix copies retain original source IDs.
        target=self.output/'owners'/'owners'/'native-checkpoint'; target.mkdir(parents=True)
        for original in self.native_certificate.parent.iterdir():
            if not original.is_file(): raise ValueError('Unexpected native certificate directory entry')
            destination=target/original.name
            with original.open('rb') as incoming,destination.open('xb') as outgoing:
                for block in iter(lambda:incoming.read(256*1024),b''): outgoing.write(block)
                outgoing.flush();os.fsync(outgoing.fileno())
            if tools.digest(original)!=tools.digest(destination): raise ValueError('Native certificate changed while copying')
            result['artifacts'].append({'kind':'owner-file','owner':'native-checkpoint','relativePath':original.name,'path':str(destination),'bytes':destination.stat().st_size,'sha256':tools.digest(destination)})
        # Same-volume rename avoids retaining a second full 136+GiB raw prefix.
        os.replace(self.output/'native-prefixes',target/'prefixes')
        for original in (target/'prefixes').rglob('*'):
            if original.is_file(): result['artifacts'].append({'kind':'owner-file','owner':'native-checkpoint','relativePath':str(original.relative_to(target)),'path':str(original),'bytes':original.stat().st_size,'sha256':tools.digest(original)})
        financial=self.output/'emulator-owner.json'; copied=target/'emulator-owner.json'; copied.write_bytes(financial.read_bytes())
        result['artifacts'].append({'kind':'owner-file','owner':'native-checkpoint','relativePath':copied.name,'path':str(copied),'bytes':copied.stat().st_size,'sha256':tools.digest(copied)})
        result['ownerRoots'].append({'owner':'native-checkpoint','restoreOwner':'native-checkpoint','path':self.plan['nativeRawRoot'],'restoreRelativePath':'prefixes/raw'})
        tools.write(self.output/'owners'/'snapshot.json',result)
        self.complete=True; return result

    def __exit__(self,kind,error,trace):
        if self.server: self.server.shutdown(); self.server.server_close()
        self.stack.close()
        # Failure retains holds deliberately. Resume requires the operator to inspect the persisted real evidence.
        if kind is None and self.complete:
            manager(self.config,'release',self.lease)
            # The original native owner exited after the hash-bound prefix export.
            # Its replacement owns a new forwarding lifetime; never reuse the retired lease.
        else: tools.write(self.output/'blocked.json',{'state':'BlockedRetainedOwnership','leaseId':self.lease,'nativeLease':self.native_lease,'boundaryId':self.boundary,'exception':kind.__name__ if kind else 'SnapshotIncomplete','qualified':False})
