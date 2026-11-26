"""Read-only, phase-aware disk admission. A reservation is a fail-closed budget, not preallocated disk or a session certificate."""
from __future__ import annotations
import argparse, datetime as dt, hashlib, json, os, re, shutil, subprocess
from pathlib import Path

GiB=1024**3
def nearest(path):
    result=Path(path).resolve()
    while not result.exists():
        if result.parent==result:raise ValueError('Reservation has no existing volume ancestor')
        result=result.parent
    return result
def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(262144),b''):h.update(block)
    return h.hexdigest()
def volume(path):
    base=nearest(path)
    return (base.drive.upper() if base.drive else str(os.stat(base).st_dev)),base
def inspect_docker(vhd):
    """Do not trust a plan's free-space assertion or infer the Windows volume from /var/lib/docker."""
    settings=Path(os.environ['APPDATA'])/'Docker/settings-store.json'
    config=json.loads(settings.read_text(encoding='utf-8-sig'));base=Path(config['CustomWslDistroDir'])
    actual=(base/'disk/docker_data.vhdx').resolve()
    if actual!=vhd.resolve():raise ValueError('Planned Docker VHD differs from actual Docker Desktop data owner')
    result=subprocess.run(['wsl','-d','docker-desktop','--','df','-B1','/mnt/docker-desktop-disk'],capture_output=True,check=True,timeout=15)
    lines=result.stdout.decode().replace('\x00','').strip().splitlines();fields=lines[-1].split()
    if len(fields)<6 or fields[-1]!='/mnt/docker-desktop-disk':raise ValueError('Cannot inspect actual Docker guest capacity')
    return int(fields[3])
def measured_command(args):
    result=subprocess.run(args,capture_output=True,check=True,timeout=30)
    if len(result.stdout)>1024*1024:raise ValueError('Capacity measurement output exceeded metadata bound')
    return result.stdout.decode().strip()
def root_bytes(path):
    root=Path(path)
    if not root.exists():return {'path':str(root.resolve()),'exists':False,'bytes':0,'files':0}
    total=count=0;pending=[root]
    while pending:
        with os.scandir(pending.pop()) as entries:
            for entry in entries:
                if entry.is_symlink():raise ValueError('Capacity owner tree contains a link')
                if entry.is_dir(follow_symlinks=False):pending.append(Path(entry.path))
                elif entry.is_file(follow_symlinks=False):total+=entry.stat(follow_symlinks=False).st_size;count+=1
                if count+len(pending)>1000000:raise ValueError('Capacity owner file inventory exceeded its bound')
    return {'path':str(root.resolve()),'exists':True,'bytes':total,'files':count}
def refresh_measurements(plan):
    """Re-read real source sizes and ownership paths; updating a timestamp alone is not a refresh."""
    result=json.loads(json.dumps(plan));sources=result['measurementSources'];pg=sources['postgres'];scylla=sources['scylla']
    if not sources.get('fileOwnerPlan'):raise ValueError('Actual initial file-owner copy map is required')
    for item in (pg,scylla):
        if not re.fullmatch('[A-Za-z0-9][A-Za-z0-9_.-]{0,127}',item['container']):raise ValueError('Unsafe measurement container')
    for key in ('database','user'):
        if not re.fullmatch('[a-z_][a-z0-9_]*',pg[key]):raise ValueError('Unsafe measurement database identifier')
    pg_id=measured_command(['docker','inspect','--format','{{.Id}}',pg['container']]);scylla_id=measured_command(['docker','inspect','--format','{{.Id}}',scylla['container']])
    pg_size=int(measured_command(['docker','exec',pg['container'],'psql','-X','-At','-U',pg['user'],'-d',pg['database'],'-c','SELECT pg_database_size(current_database());']))
    scylla_size=int(measured_command(['docker','exec',scylla['container'],'du','-sb','/var/lib/scylla/data']).split()[0])
    guest=inspect_docker(Path(result['dockerVhdPath']));roots=[root_bytes(item['path']) for item in result.get('nativeRoots',[])]+[root_bytes(item['path']) for item in result.get('plannedInitialNativeRoots',[])]+[root_bytes(result['captureRoot'])]
    owner_files=[];owner_total=floor_copy=0
    if sources.get('fileOwnerPlan'):
        owner_plan=json.loads(Path(sources['fileOwnerPlan']).read_text(encoding='utf-8-sig'))
        if not owner_plan['snapshot']['fileRoots']:raise ValueError('Actual initial file-owner copy map is empty')
        for item in owner_plan['snapshot']['fileRoots']:
            path=Path(item['path']).resolve()
            if item.get('files'):
                amount=0
                for name in item['files']:
                    selected=(path/name).resolve()
                    if not selected.is_relative_to(path) or not selected.is_file():raise ValueError('Actual selected owner file is unavailable')
                    amount+=selected.stat().st_size
            else:amount=root_bytes(path)['bytes']
            owner_files.append({'owner':item['owner'],'path':str(path),'bytes':amount});owner_total+=amount
            if item['owner']=='source-outbox':floor_copy+=amount
    native_bytes=sum(item['bytes'] for item in roots[:-1])
    result['measurements']={'postgresBytes':pg_size,'postgresContainerId':pg_id,'scyllaBytes':scylla_size,'scyllaContainerId':scylla_id,'ownerRoots':roots,'fileOwnerCopies':owner_files,'dockerGuestAvailableBytes':guest,
        'initialArchiveFloorAdditionalCopyBytes':floor_copy,'sourceSnapshotMinimumBytes':(pg_size+scylla_size)*120//100+owner_total+floor_copy+native_bytes+9*GiB,
        'sourceRestoreMinimumBytes':pg_size*150//100+scylla_size*2,
        'nativeWorkingMinimumBytes':owner_total+floor_copy+native_bytes*2+80*1024**2}
    result['measuredAtUtc']=dt.datetime.now(dt.timezone.utc).isoformat();return result
def bind_native_prefix(plan,prefix_path):
    result=json.loads(json.dumps(plan));proof=json.loads(Path(prefix_path).read_text(encoding='utf-8-sig'));root=Path(proof['SourceRoot']).resolve()
    if proof.get('Version')!=1 or not proof.get('ForwardingLease') or proof.get('MapperCheckpoint') is not False or proof.get('RequiredIntentCheckpoint') is not False:raise ValueError('Actual held native source prefix evidence is required')
    planned=next((item for item in result.get('plannedInitialNativeRoots',[]) if Path(item['path']).resolve()==root),None)
    if not planned:raise ValueError('Native prefix cannot repoint an unplanned source ownership root')
    namespace=next((item for item in proof['Files'] if item['RelativePath']=='namespace.sha256'),None)
    if not namespace or namespace['Length']!=32 or (root/'namespace.sha256').stat().st_size!=32:raise ValueError('Actual native namespace binding is unavailable')
    result.setdefault('nativeRoots',[]).append({'path':str(root),'role':'ExistingNativeSource','bindings':[{'relativePath':'namespace.sha256','sha256':digest(root/'namespace.sha256')}],
        'prefixEvidencePath':str(Path(prefix_path).resolve()),'prefixEvidenceSha256':digest(prefix_path),'scope':proof['Scope'],'mapperCheckpoint':False})
    result['plannedInitialNativeRoots'].remove(planned);return result
def evaluate(plan):
    if plan.get('version')!=1:raise ValueError('Unsupported capacity plan')
    root=Path(plan['captureRoot'])
    if not root.is_absolute():raise ValueError('Capture root must be absolute')
    root=root.resolve();phases=plan.get('phases',[])
    if not 1<=len(phases)<=16:raise ValueError('Explicit bounded capture/replay phases are required')
    vhd=Path(plan['dockerVhdPath'])
    if not vhd.is_absolute() or not vhd.is_file() or vhd.suffix.lower()!='.vhdx':raise ValueError('Actual Docker VHD must be inspected, not inferred from DockerRootDir')
    minimum=plan.get('minimumFreeBytes',32*GiB)
    if not isinstance(minimum,int) or minimum<GiB:raise ValueError('Explicit positive volume headroom is required')
    failures=[];volumes={};peak={};details=[]
    for phase in phases:
        totals={};names=set()
        for item in phase['reservations']:
            path=Path(item['path']);amount=item['bytes']
            if not path.is_absolute() or not isinstance(amount,int) or amount<0 or item['name'] in names:raise ValueError('Invalid or repeated phase reservation')
            names.add(item['name']);key,base=volume(path);usage=shutil.disk_usage(base)
            volumes[key]={'availableBytes':usage.free,'totalBytes':usage.total,'probePath':str(base)}
            totals[key]=totals.get(key,0)+amount
        for key,amount in totals.items():peak[key]=max(peak.get(key,0),amount)
        details.append({'name':phase['name'],'reservedBytesByVolume':totals})
    for key,amount in peak.items():
        volumes[key].update(requiredBytes=amount,minimumFreeBytes=minimum,remainingAfterReservationBytes=volumes[key]['availableBytes']-amount)
        if amount+minimum>volumes[key]['availableBytes']:failures.append('Insufficient disk reservation on '+key)
    measurement=plan.get('measurements',{})
    if not plan.get('measurementSources'):failures.append('Actual source measurement providers are missing')
    elif not plan['measurementSources'].get('fileOwnerPlan') or not measurement.get('fileOwnerCopies'):failures.append('Actual initial file-owner copy map was not measured')
    if plan.get('measurementSources'):
        if not measurement:failures.append('Actual PostgreSQL/Scylla/root measurements have not been refreshed')
        else:
            reservations={item['name']:item['bytes'] for phase in phases for item in phase['reservations']}
            if measurement['sourceSnapshotMinimumBytes']>reservations.get('original-held-owner-snapshot',0):failures.append('Actual source stores exceed the bounded owner snapshot reservation')
            if measurement['sourceRestoreMinimumBytes']>reservations.get('one-disposable-restored-pg-scylla-plus-wal',0):failures.append('Actual source stores exceed the bounded single restore reservation')
            if 'nativeWorkingMinimumBytes' in measurement and measurement['nativeWorkingMinimumBytes']>reservations.get('working-native-raw-copies',0):failures.append('Actual aggregate restored owner/floor/native/scratch copies exceed the bounded working reservation')
    guest=inspect_docker(vhd);guest_needed=plan.get('dockerGuestRequiredBytes')
    observed=dt.datetime.fromisoformat(plan['measuredAtUtc'])
    if observed.utcoffset()!=dt.timedelta(0) or abs((dt.datetime.now(dt.timezone.utc)-observed).total_seconds())>300:failures.append('Capacity measurements older than five minutes or future-dated')
    if not isinstance(guest,int) or not isinstance(guest_needed,int) or guest_needed<0 or guest_needed+minimum>guest:failures.append('Docker guest filesystem capacity unavailable or insufficient')
    docker_volume,_=volume(vhd)
    if peak.get(docker_volume,0)<guest_needed:failures.append('Docker guest growth is missing from its actual host-volume reservation')
    binding=[]
    for item in plan.get('nativeRoots',[]):
        path=Path(item['path']).resolve()
        if not path.is_dir() or not any(path.iterdir()):raise ValueError('Existing native/raw ownership root cannot be repointed to an empty directory')
        for proof in item.get('bindings',[]):
            file=(path/proof['relativePath']).resolve()
            if not file.is_relative_to(path) or digest(file)!=proof['sha256']:raise ValueError('Native/raw ownership binding changed')
        evidence={}
        if item.get('prefixEvidencePath'):
            if digest(item['prefixEvidencePath'])!=item['prefixEvidenceSha256']:raise ValueError('Native prefix ownership evidence changed')
            prefix=json.loads(Path(item['prefixEvidencePath']).read_text(encoding='utf-8-sig'))
            if not Path(prefix['SourceRoot']).samefile(path):raise ValueError('Native prefix ownership root changed')
            evidence={'prefixEvidencePath':item['prefixEvidencePath'],'prefixEvidenceSha256':item['prefixEvidenceSha256'],'scope':prefix['Scope'],'mapperCheckpoint':False}
        binding.append({'path':str(path),'role':item.get('role'),'bindings':item.get('bindings',[]),**evidence})
    if not binding:failures.append('Actual existing native/raw ownership roots were not inspected')
    planned=[]
    for item in plan.get('plannedInitialNativeRoots',[]):
        path=Path(item['path'])
        if not path.is_absolute() or item.get('previouslyEnabled') is not False or not item.get('requiresFreshPostBootstrapNamespaceProof'):raise ValueError('Uninitialized source owner requires explicit first-enable disposition')
        initialized=path.exists()
        if initialized:failures.append('First native source journal now exists; bind its authentic namespace and fresh prefix before arming')
        planned.append({**item,'path':str(path.resolve()),'initialized':initialized})
    return {'version':1,'kind':'SessionCapacityAdmission','capacityReady':not failures,'qualified':False,'captureRoot':str(root),
        'dockerVhdPath':str(vhd.resolve()),'dockerVhdAllocatedFileBytes':vhd.stat().st_size,'dockerGuestAvailableBytes':guest,
        'dockerGuestRequiredBytes':guest_needed,'checkedUtc':dt.datetime.now(dt.timezone.utc).isoformat(),'volumes':volumes,
        'phases':details,'measurements':measurement,'nativeRoots':binding,'plannedInitialNativeRoots':planned,'failures':failures,'reservationSemantics':'Peak additional bytes; each phase includes every retained artifact from earlier phases. One backend restore at a time.',
        'limitations':['Read-only admission does not preallocate space or prevent unrelated writers. Recheck before every phase and continuously monitor pressure.','Capacity evidence does not prove owner consistency, original source coverage or session qualification.']}
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--plan',required=True);parser.add_argument('--output',required=True);parser.add_argument('--refresh-measurements',action='store_true');parser.add_argument('--refreshed-plan-output');parser.add_argument('--bind-native-prefix');args=parser.parse_args()
    plan=json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    if args.bind_native_prefix:plan=bind_native_prefix(plan,args.bind_native_prefix)
    if args.refresh_measurements:plan=refresh_measurements(plan)
    if args.refreshed_plan_output:
        with Path(args.refreshed_plan_output).open('x',encoding='utf-8') as stream:json.dump(plan,stream,indent=2);stream.flush();os.fsync(stream.fileno())
    result=evaluate(plan);target=Path(args.output)
    with target.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2);stream.write('\n');stream.flush();os.fsync(stream.fileno())
    print(json.dumps({'capacityReady':result['capacityReady'],'qualified':False,'failures':result['failures']}));return 0 if result['capacityReady'] else 2
if __name__=='__main__':raise SystemExit(main())
