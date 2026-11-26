"""Execute isolated original-input replays, restoring one certified baseline per backend.
No live endpoint, capture flag, broker order or development container is changed.
"""
from __future__ import annotations
import argparse, datetime as dt, hashlib, json, os, re, secrets, shutil, socket, subprocess, time, uuid
from pathlib import Path
import session_tool as tools
import session_restore as restore
import outcome_evidence as outcomes

ROOTS=['Realtime.FuturesTradeSessionBarSignal.Barrier','Realtime.FuturesTradePositionRealtime.EvaluateFuturesExitClock','Realtime.IronCondorTradePositionRealtime.IronCondorMonitoringInputsChanged']

def field(obj,name,default=None):return obj.get(name,obj.get(name[0].lower()+name[1:],default))
def pinned(value):
    path=Path(value['path']).resolve(strict=True)
    if tools.digest(path).lower()!=value['sha256'].lower():raise ValueError('Pinned replay input changed')
    return path

def run(argv,*,env=None,timeout=600):
    result=subprocess.run(list(map(str,argv)),stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,env=env,shell=False,timeout=timeout)
    if result.returncode:raise RuntimeError('Isolated replay action failed; protected provider output withheld')
    if len(result.stdout)>8*1024**2:raise ValueError('Provider metadata exceeds bound')
    return result.stdout.decode('utf-8-sig').strip()
def wait(fn,seconds=180):
    end=time.monotonic()+seconds
    while True:
        try:return fn()
        except Exception:
            if time.monotonic()>=end:raise
            time.sleep(.5)
def free_port():
    with socket.socket() as value:value.bind(('127.0.0.1',0));return value.getsockname()[1]
def cp(source,target,sha=None):
    source=Path(source);target=Path(target);target.parent.mkdir(parents=True,exist_ok=True)
    with source.open('rb') as incoming,target.open('xb') as outgoing:
        shutil.copyfileobj(incoming,outgoing,256*1024);outgoing.flush();os.fsync(outgoing.fileno())
    if sha and tools.digest(target).lower()!=sha.lower():raise ValueError('Copied original bytes changed')

def validate_config(path):
    config=tools.load(path)
    if config.get('version')!=1:raise ValueError('Unknown replay orchestration config')
    for key in ('benchmarkAssembly','apiAssembly','nativeLibrary','calendarEvidence'):pinned(config[key])
    if not re.fullmatch('sha256:[a-f0-9]{64}',config.get('natsImageId','')):raise ValueError('Actual pinned NATS image is required')
    run(['docker','image','inspect',config['natsImageId']],timeout=10)
    api=pinned(config['apiAssembly'])
    if not api.with_suffix('.runtimeconfig.json').is_file() or not (api.parent/'ifm-deployment-manifest.json').is_file():raise ValueError('Actual API runtime/deployment files are absent')
    return {'version':1,'state':'PreparedActualReplayActionsNotExecuted','configurationSha256':tools.digest(path),'qualified':False}
def validate(state_path,config_path):
    state=tools.load(state_path);config=tools.load(config_path)
    if config.get('version')!=1 or len(state.get('sessions',[]))!=2:raise ValueError('Exactly two captured sessions are required')
    initial=pinned(state['initialSnapshot']);archive=pinned(state['sourceArchive']);settings=pinned(state['runtimeSettings'])
    if tools.load(initial).get('state')!='CapturedUnderHeldFence':raise ValueError('Initial owner baseline is incomplete')
    if tools.load(archive).get('kind')!='CommittedSourceArchivePrefixes':raise ValueError('Original complete source copy is unavailable')
    evidence=tools.load(settings)
    if field(evidence,'Kind')!='SanitizedEffectiveReplayConfiguration' or field(evidence,'AllOwnerPathsExplicit') is not True:raise ValueError('Actual explicit owner configuration evidence is required')
    if state.get('frozenRuntime'):runtime_entries(state)
    else:
        for name in ('benchmarkAssembly','apiAssembly','nativeLibrary','calendarEvidence'):pinned(config[name])
    partitions=tools.load(pinned(state['rawPartitions']))
    if partitions.get('kind')!='OriginalRawPartitionManifest' or partitions.get('sourceArchiveSha256','').lower()!=state['sourceArchive']['sha256'].lower() or not partitions.get('partitions'):raise ValueError('Actual source-bound raw partitions are required')
    initial_manifest=tools.load(initial);native_refs=[item for item in initial_manifest['artifacts'] if item.get('owner')=='native-checkpoint' and Path(item['path']).name=='checkpoint.json']
    if len(native_refs)!=1 or partitions.get('nativeCheckpointSha256','').lower()!=native_refs[0]['sha256'].lower():raise ValueError('Raw partition identities belong to another native baseline')
    native=tools.load(native_refs[0]['path'])
    native_copies=[item for item in initial_manifest['artifacts'] if item.get('owner')=='native-checkpoint' and Path(item['path']).name=='prefix-copy.json']
    if len(native_copies)!=1 or sum(row['bytes'] for row in tools.load(native_copies[0]['path'])['files'])*2>32*1024**3:raise ValueError('All native/raw working copies exceed the qualified32GiB bound')
    if partitions.get('initialBackendSnapshotSha256','').lower()!=native['BackendSnapshotSha256'].lower():raise ValueError('Raw partition inventory uses another initial backend snapshot')
    if not re.fullmatch('sha256:[a-f0-9]{64}',config.get('natsImageId','')):raise ValueError('An actual locally pinned NATS image is required')
    return state,config,evidence

def relocate(settings,owner_roots,restored):
    result=dict(settings)
    for key,value in list(result.items()):
        if not isinstance(value,str) or not (key.endswith('Recovery:Directory') or key in ('Actors:RealtimeMailboxes:Directory','RawFuturesStorage:Directory','TradeBroker:Emulator:LedgerPath','ResidentActorPilots:FuturesItiJournalDirectory')):continue
        original=Path(value).resolve();matches=[]
        for owner in owner_roots:
            root=Path(owner['path']).resolve()
            if original==root or root in original.parents:matches.append((len(root.parts),owner,original.relative_to(root)))
        if not matches:raise ValueError('A real owner path is not covered by the initial snapshot: '+key)
        _,owner,relative=max(matches,key=lambda row:row[0]);target=Path(restored)/'owners'/owner.get('restoreOwner',owner['owner'])/owner.get('restoreRelativePath','')/relative
        if key!='TradeBroker:Emulator:LedgerPath':target.mkdir(parents=True,exist_ok=True)
        result[key]=str(target)
    return result

def fixtures(manifest,root,run_id,nats_image,password):
    prefix='ifm_session_'+run_id;network=prefix;label='ifm.session-qualification='+run_id
    run(['docker','network','create','--driver','bridge','--label',label,network])
    pg=next(item for item in manifest['artifacts'] if item['kind']=='postgres');cql=next(item for item in manifest['artifacts'] if item['kind']=='scylla')
    specs=[('postgres',pg['providerImageId'],5432,['-e','POSTGRES_USER=postgres','-e','POSTGRES_PASSWORD','-e','POSTGRES_DB='+prefix+'_replay'], '/var/lib/postgresql/data',[]),
           ('scylla',cql['providerImageId'],9042,[],'/var/lib/scylla',['--smp','2','--memory','4G','--overprovisioned','1','--developer-mode','1']),
           ('nats',nats_image,4222,[],'/data',['-js','-sd','/data'])]
    targets={'version':1,'runId':run_id}
    for kind,image,port,environment,mount,args in specs:
        run(['docker','image','inspect',image]);volume=prefix+'_'+kind+'_data';name=prefix+'_'+kind
        run(['docker','volume','create','--label',label,volume])
        run(['docker','run','--pull=never','-d','--name',name,'--network',network,'--label',label,'-p','127.0.0.1::'+str(port),'-v',volume+':'+mount,*environment,image,*args],env={**os.environ,'POSTGRES_PASSWORD':password})
        targets[kind]={'kind':kind,'container':name}
    targets['postgres']['database']=prefix+'_replay';targets['postgres']['user']='postgres'
    wait(lambda:run(['docker','exec','-e','PGPASSWORD',targets['postgres']['container'],'psql','-h','127.0.0.1','-U','postgres','-d',targets['postgres']['database'],'-Atc','SELECT 1'],env={**os.environ,'PGPASSWORD':password}))
    wait(lambda:run(['docker','exec',targets['scylla']['container'],'cqlsh','-e','DESCRIBE KEYSPACES;']))

    return targets

def freeze_runtime(configuration,config_path,output):
    evidence=tools.load(configuration);config=tools.load(config_path);root=Path(output).resolve();root.mkdir(parents=True,exist_ok=False)
    loaded=field(evidence,'RuntimeAssemblies')
    if not loaded or len(loaded)>1024:raise ValueError('Actual loaded/runtime disk identity evidence is required')
    files=[];total=0
    if any(not re.fullmatch('[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}',str(field(row,'ModuleVersionId',''))) or field(row,'ModuleVersionId')=='00000000-0000-0000-0000-000000000000' for row in loaded):raise ValueError('Loaded module identity is incomplete')
    expected={Path(field(row,'RelativePath')).as_posix().lower():row for row in loaded};seen=set()
    entries={}
    for kind,key in [('api','apiAssembly'),('benchmark','benchmarkAssembly'),('native','nativeLibrary')]:
        entry=pinned(config[key]);source_root=entry.parent
        for source in ([entry] if kind=='native' else sorted(source_root.rglob('*'))):
            if not source.is_file():continue
            relative=source.relative_to(source_root)
            if any(part.lower() in ('data','logs','.artifacts','node_modules') for part in relative.parts):continue
            name=source.name.lower()
            if not (name.endswith(('.dll','.exe','.so','.dylib','.deps.json','.runtimeconfig.json','.staticwebassets.endpoints.json')) or name=='ifm-deployment-manifest.json'):continue
            if source.is_symlink():raise ValueError('Runtime binaries cannot contain links')
            size=source.stat().st_size;total+=size
            if len(files)>=8192 or total>8*1024**3:raise ValueError('Full frozen runtime exceeds finite binary budget')
            sha=tools.digest(source);row=expected.get(relative.as_posix().lower()) if kind=='api' else None
            if row:
                if sha.lower()!=field(row,'Sha256').lower() or size!=field(row,'Bytes'):raise ValueError('Loaded API code no longer matches its actual disk identity')
                seen.add(relative.as_posix().lower())
            target=root/kind/relative;cp(source,target,sha)
            if tools.digest(source)!=sha:raise ValueError('Runtime binary changed during frozen copy')
            files.append({'relativePath':str(target.relative_to(root)),'bytes':size,'sha256':sha})
        target=root/kind/entry.name
        if not target.is_file() or tools.digest(target).lower()!=config[key]['sha256'].lower():raise ValueError('Frozen runtime entry was not copied exactly')
        entries[kind]={'relativePath':str(target.relative_to(root)),'sha256':tools.digest(target)}
    if seen!=set(expected):raise ValueError('Frozen runtime omitted an actually loaded API assembly')
    calendar=pinned(config['calendarEvidence']);cp(calendar,root/'calendar-evidence.json',config['calendarEvidence']['sha256']);files.append({'relativePath':'calendar-evidence.json','bytes':calendar.stat().st_size,'sha256':tools.digest(calendar)});entries['calendar']={'relativePath':'calendar-evidence.json','sha256':tools.digest(calendar)}
    result={'version':1,'kind':'FrozenActualRuntimeBinaries','configurationEvidenceSha256':tools.digest(configuration),'entries':entries,'files':files,'qualified':False}
    tools.write(root/'runtime.json',result);return {'path':str(root/'runtime.json'),'sha256':tools.digest(root/'runtime.json')}

def runtime_entries(state):
    manifest=pinned(state['frozenRuntime']);value=tools.load(manifest)
    if value.get('kind')!='FrozenActualRuntimeBinaries' or value.get('configurationEvidenceSha256')!=state['runtimeSettings']['sha256']:raise ValueError('Frozen runtime is not bound to actual original configuration/code')
    root=manifest.parent;seen=set()
    for item in value['files']:
        relative=Path(item['relativePath']);path=(root/relative).resolve()
        if relative.is_absolute() or '..' in relative.parts or not path.is_relative_to(root) or path in seen:raise ValueError('Frozen runtime file escapes/repeats')
        seen.add(path)
        if path.stat().st_size!=item['bytes'] or tools.digest(path)!=item['sha256']:raise ValueError('Frozen runtime binary changed')
    return {key:{'path':str(root/item['relativePath']),'sha256':item['sha256']} for key,item in value['entries'].items()}

def validate_final_runtime(state,final_configuration_path):
    manifest=pinned(state['frozenRuntime']);frozen=tools.load(manifest);runtime_entries(state)
    initial=tools.load(pinned(state['runtimeSettings']));final=tools.load(final_configuration_path)
    if field(final,'Kind')!='SanitizedEffectiveReplayConfiguration' or not field(final,'AllOwnerPathsExplicit'):raise ValueError('Final actual runtime configuration is not certified')
    files={Path(row['relativePath']).as_posix().lower():row for row in frozen['files']}
    original={Path(field(row,'RelativePath')).as_posix().lower():row for row in field(initial,'RuntimeAssemblies')}
    if not field(final,'RuntimeAssemblies'):raise ValueError('Final loaded runtime identity evidence is absent')
    for row in field(final,'RuntimeAssemblies'):
        relative=Path(field(row,'RelativePath')).as_posix().lower();expected=files.get('api/'+relative)
        if expected is None or expected['sha256'].lower()!=field(row,'Sha256').lower() or expected['bytes']!=field(row,'Bytes'):raise ValueError('Original API loaded different code during capture')
        if relative in original and field(original[relative],'ModuleVersionId')!=field(row,'ModuleVersionId'):raise ValueError('Original loaded module identity changed during capture')
    return {'version':1,'state':'ActualFinalLoadedRuntimeMatchesFrozenBaseline','initialRuntimeSha256':state['frozenRuntime']['sha256'],
            'finalConfigurationSha256':tools.digest(final_configuration_path),'qualified':False}
def generate_partitions(state,config):
    manifest=tools.load(pinned(state['initialSnapshot']));candidates=[Path(x['path']) for x in manifest['artifacts'] if x.get('owner')=='native-checkpoint' and Path(x['path']).name=='checkpoint.json']
    if len(candidates)!=1:raise ValueError('Actual initial native image is missing')
    cert=candidates[0];native=tools.load(cert);snapshot=cert.parent/native['BackendSnapshotFile'];entries=runtime_entries(state)
    directory=Path(state['sourceArchive']['path']).resolve().parents[2];output=directory/'raw-partitions.json';job=directory/'raw-partition-export.json'
    spec={'Version':1,'ArchiveCopyManifest':str(pinned(state['sourceArchive'])),'ArchiveCopyManifestSha256':state['sourceArchive']['sha256'],'ArchiveSessionId':state['archiveSessionId'],
          'NativeCheckpointCertificate':str(cert),'NativeCheckpointCertificateSha256':tools.digest(cert),'InitialBackendSnapshot':str(snapshot),'InitialBackendSnapshotSha256':tools.digest(snapshot),
          'OutputPath':str(output),'MaximumRecords':100000000,'MaximumPartitions':100000}
    tools.write(job,spec);run(['dotnet',pinned(entries['benchmark']),'--export-original-raw-partitions',str(job)],timeout=21600)
    return {'path':str(output),'sha256':tools.digest(output)}
def native_paths(root,files):
    candidates=[Path(x['path']) for x in files if x['owner']=='native-checkpoint' and Path(x['path']).name=='checkpoint.json']
    if len(candidates)!=1:raise ValueError('Exactly one restored native certificate is required')
    cert=candidates[0];prefix=cert.parent/'prefixes'/'prefix-copy.json'
    if not prefix.is_file():raise ValueError('Original copied native/raw prefixes are absent')
    floor=Path(root)/'owners'/'session-archive-boundary'/'initial-archive-boundary.json'
    if not floor.is_file():raise ValueError('Original restored archive floor is absent')
    return cert,prefix,floor

def export_replay(config,state,mode,run_id,targets,settings,output):
    boundary={'sessionId':state['archiveSessionId'],'initialSnapshotSha256':state['initialSnapshot']['sha256'],
              'sourceCaptureSha256':state['sourceArchive']['sha256'],'calendarEvidenceSha256':config['calendarEvidence']['sha256']}
    keyspaces=[item['keyspace'] for item in tools.load(state['initialSnapshot']['path'])['artifacts'] if item['kind']=='scylla']
    if len(keyspaces)!=1:raise ValueError('Replay export currently requires one actual raw keyspace')
    cfg={'role':'DisposableQualification','runId':run_id,'backend':mode,'boundary':boundary,
         'postgres':{**targets['postgres']},'scylla':{**targets['scylla'],'keyspace':keyspaces[0],'partitions':tools.load(pinned(state['rawPartitions']))['partitions']},
         'emulatorCheckpointPath':settings['TradeBroker:Emulator:LedgerPath'],
         'realtimeJournalDirectory':str(Path(settings['Actors:RealtimeMailboxes:Directory'])/settings['Actors:RealtimeMailboxes:Epoch']),
         'limits':{'valueMode':'ExactCanonicalDigest','maximumInputBytes':512*1024**3,'maximumRecords':100000000,'chunkBytes':64*1024**2,
                   'maximumSeconds':21600,'maximumQuerySeconds':21600,'minimumFreeBytes':32*1024**3}}
    journal=Path(settings['Actors:RealtimeMailboxes:Directory'])/settings['Actors:RealtimeMailboxes:Epoch']/'sources'
    if not (journal/'receipts.bin').is_file() or (journal.parent/'ownership.protocol').read_text()!='2:source-until-handler-proof':raise ValueError('Actual replay source receipt owner is absent')
    cfg['realtimeJournalDirectory']=str(journal);components=[]
    for name,fn in [('postgres',outcomes.export_postgres),('scylla',outcomes.export_scylla),('emulator',outcomes.export_emulator)]:
        path=Path(output)/name;fn(cfg,path);components.append(str(path/'manifest.json'))
    business=Path(output)/'business';outcomes.bundle(components,business,profile='BusinessOnly')
    proof=Path(output)/'proofs';outcomes.export_proofs(cfg,proof)
    return {'business':{'path':str(business/'manifest.json'),'sha256':tools.digest(business/'manifest.json')},
            'proofs':{'path':str(proof/'manifest.json'),'sha256':tools.digest(proof/'manifest.json')}}

def replay(state_path,mode,config_path):
    state,config,evidence=validate(state_path,config_path);entries=runtime_entries(state);config={**config,'apiAssembly':entries['api'],'benchmarkAssembly':entries['benchmark'],'nativeLibrary':entries['native'],'calendarEvidence':entries['calendar']};base=Path(state_path).resolve().parent/'replays'/mode
    base.mkdir(parents=True,exist_ok=False);run_id=uuid.uuid4().hex[:12];snapshot=Path(state['initialSnapshot']['path']).parent
    manifest=tools.load(state['initialSnapshot']['path']);targets=None;success=False
    try:
        password=secrets.token_hex(24)
        targets=fixtures(manifest,base,run_id,config['natsImageId'],password);target_file=base/'targets.json';tools.write(target_file,{**targets,'postgres':[targets['postgres']],'scylla':[targets['scylla']]})
        restored=base/'restored';certificate=restore.restore(snapshot,target_file,restored,True)
        settings=relocate(field(evidence,'Settings'),manifest['ownerRoots'],restored)
        pg=certificate['endpoints']['postgres'];cql=certificate['endpoints']['scylla'];broker=certificate['endpoints']['nats']['url']
        for key in list(settings):
            if key.startswith('ConnectionStrings:'):settings[key]=f"Contact Points=127.0.0.1;Port={cql['port']};Default Keyspace="+next(x['keyspace'] for x in manifest['artifacts'] if x['kind']=='scylla') if key=='ConnectionStrings:TradeTickQuoteDbConnection' else f"Host=127.0.0.1;Port={pg['port']};Database={pg['database']}"
        settings.update({'IFM_TEST_POSTGRES_CONNECTION':f"Host=127.0.0.1;Port={pg['port']};Database={pg['database']}",
            'Storage:PostgresConnection':f"Host=127.0.0.1;Port={pg['port']};Database={pg['database']}",
            'Actors:RealtimeMailboxes:Enabled':'true','Actors:RealtimeMailboxes:CapacityQualified':'true','Actors:RealtimeMailboxes:Storage':mode,
            'Actors:RealtimeMailboxes:Epoch':'replay_'+run_id,'Actors:SessionCapture:Enabled':'false','SessionCapture:OwnerCheckpoints:Enabled':'true',
            'Nats:Consumer:Url':broker,'Nats:JetStreamConsumer:Url':broker,'IFM_TEST_NATS_URL':broker,'MarketDataHost:NatsUrl':broker})
        for key,value in config.get('qualifiedMailboxLimits',{}).items():
            if not key.startswith('Actors:RealtimeMailboxes:') or key.endswith(':Enabled'):raise ValueError('Unexpected qualified replay budget key')
            settings[key]=str(value)
        cert,native_prefix,floor=native_paths(restored,certificate['ownerFiles']);native=tools.load(cert);archive_manifest=pinned(state['sourceArchive']);archive_epoch='archive_'+hashlib.sha256(state['archiveSessionId'].encode()).hexdigest()[:24]
        if archive_manifest.parent.name!=archive_epoch:raise ValueError('Final archive copy must retain its original epoch layout; another full-body copy is forbidden')
        authority_source=archive_manifest.parent
        authority=restored/'monitoring-authority.jsonl'
        run(['dotnet',pinned(config['benchmarkAssembly']),'--export-recorded-monitoring-authority',str(authority_source.parent),state['archiveSessionId'],state['sourceArchive']['sha256'],str(floor),tools.digest(floor),str(authority)],timeout=21600)
        host={'version':1,'runId':run_id,'restoreCertificatePath':str(restored/'restore.json'),'restoreCertificateSha256':tools.digest(restored/'restore.json'),
              'nativeCheckpointCertificatePath':str(cert),'nativeCheckpointCertificateSha256':tools.digest(cert),'initialUtc':field(evidence,'ObservedUtc'),
              'natsUrl':broker,'httpUrl':'http://127.0.0.1:'+str(free_port()),'scope':native['Scope'],'configuration':settings,
              'recordedMonitoringAuthority':{'path':str(authority),'sha256':tools.digest(authority),'sourceArchiveCopyManifestSha256':state['sourceArchive']['sha256'],'initialArchiveBoundarySha256':tools.digest(floor)}}
        host_path=base/'host.json';tools.write(host_path,host)
        job={'Version':1,'HostConfiguration':str(host_path),'HostConfigurationSha256':tools.digest(host_path),'ApiAssembly':str(pinned(config['apiAssembly'])),'ApiAssemblySha256':config['apiAssembly']['sha256'],
             'NativeLibrary':str(pinned(config['nativeLibrary'])),'NativeLibrarySha256':config['nativeLibrary']['sha256'],'NativeCheckpointCertificate':str(cert),'NativeCheckpointCertificateSha256':tools.digest(cert),
             'WorkingArchiveDirectory':str(archive_manifest.parent.parent),'UseCertifiedSharedArchive':True,'ArchiveSessionId':state['archiveSessionId'],'OutputDirectory':str(base/'actual-runner'),
             'RootRealtimeVerbs':ROOTS,'AccountObservationMode':'RegenerateFromFileEmulator','MaximumRecords':100000000,'MaximumReplaySeconds':200000,
             'ArchiveCopyManifest':str(archive_manifest),'ArchiveCopyManifestSha256':state['sourceArchive']['sha256'],'NativePrefixCopyManifest':str(native_prefix),'NativePrefixCopyManifestSha256':tools.digest(native_prefix),
             'InitialArchiveBoundary':str(floor),'InitialArchiveBoundarySha256':tools.digest(floor)}
        job_path=base/'runner.json';tools.write(job_path,job)
        env=os.environ.copy();env['IFM_REPLAY_CONTROL_TOKEN']=secrets.token_hex(32);env['POSTGRES_TEST_KEY']=json.dumps({'userid':'postgres','password':password});env['SCYLLADB_TEST_KEY']=json.dumps({'userid':'cassandra','password':'cassandra'})
        run(['dotnet',pinned(config['benchmarkAssembly']),'--replay-original-session',str(job_path)],env=env,timeout=200100)
        actual=tools.load(base/'actual-runner'/'replay.json')
        if field(actual,'State')!='ExecutedAwaitingOwnerReconciliation' or field(actual,'ApiExited') is not True:raise ValueError('Actual production-handler replay did not cleanly stop')
        exports=export_replay(config,state,mode,run_id,targets,settings,base/'outcomes')
        result={'Version':1,'State':'ExecutedAwaitingOwnerReconciliation','Qualified':False,'backend':mode,'restoreOnceThenCarryState':True,
                'sessions':[s['id'] for s in state['sessions']],'initialSnapshotSha256':state['initialSnapshot']['sha256'],'sourceCaptureSha256':state['sourceArchive']['sha256'],
                'actualRunner':{'path':str(base/'actual-runner'/'replay.json'),'sha256':tools.digest(base/'actual-runner'/'replay.json')},**exports}
        tools.write(base/'replay.json',result);success=True;return result
    finally:
        # Keep failed owned fixtures for evidence; no development resource name is ever accepted here.
        if success and targets and config.get('removeSuccessfulFixtures',True):
            for kind,target in targets.items():
                if kind in ('postgres','scylla','nats'):restore.verify_target(target,run_id,kind);run(['docker','rm','-f',target['container']]);run(['docker','volume','rm',target['container']+'_data'])
            run(['docker','network','rm','ifm_session_'+run_id])
            cleanup_regenerable(base,state)

def cleanup_regenerable(base,state):
    base=Path(base).resolve(strict=True);restored=base/'restored';removed=[]
    targets=[restored/'working-archive',restored/'native-sources',restored/'native-raw',restored/'sstables',
             restored/'owners'/'native-checkpoint'/'prefixes'/'source',restored/'owners'/'native-checkpoint'/'prefixes'/'raw',
             restored/'owners'/'session-archive-boundary'/'initial-archive']
    for target in targets:
        if not target.exists():continue
        actual=target.resolve(strict=True)
        if actual==base or not actual.is_relative_to(base) or target.is_symlink() or getattr(target,'is_junction',lambda:False)():raise ValueError('Regenerable cleanup escaped its exact private replay mode')
        # Every target above is a fixed generated input copy; source originals, ledgers and receipt owners are excluded.
        shutil.rmtree(target);removed.append(str(target.relative_to(base)))
    tools.write(base/'regenerable-cleanup.json',{'version':1,'removedOnlyPrivateGeneratedCopies':removed,
        'initialSnapshotSha256':state['initialSnapshot']['sha256'],'sourceArchiveSha256':state['sourceArchive']['sha256'],
        'immutableOriginalsRetained':True,'financialAndReceiptOwnersRetained':True,'qualified':False})
def reconcile(state_path,config_path):
    state,config,_=validate(state_path,config_path);root=Path(state_path).resolve().parent/'replays';original=pinned(state['originalOutcomeEvidence']);reports=[]
    modes={mode:tools.load(root/mode/'replay.json') for mode in ('Memory','File')}
    for mode,value in modes.items():
        if field(value,'State')!='ExecutedAwaitingOwnerReconciliation':raise ValueError('Replay was incomplete')
        result=outcomes.compare(original,pinned(value['business']),root/(mode.lower()+'-original-comparison.json'));reports.append(result)
    reports.append(outcomes.compare(pinned(modes['Memory']['proofs']),pinned(modes['File']['proofs']),root/'memory-file-proof-comparison.json'))
    result={'version':1,'kind':'TwoSessionActualOwnerComparison','passed':all(item['matched'] for item in reports),'qualified':False,
            'restoredOncePerBackend':True,'sessions':[s['id'] for s in state['sessions']],
            'limitations':['Business owner matching and Memory/File receipt equality do not replace independent complete-session family coverage.']}
    tools.write(root/'comparison.json',result);return result

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('command',choices=('validate','validate-config','replay','reconcile','freeze-runtime'));p.add_argument('--state');p.add_argument('--configuration');p.add_argument('--output');p.add_argument('--config',required=True);p.add_argument('--mode',choices=('Memory','File'));a=p.parse_args()
    if a.command=='validate-config':print(json.dumps(validate_config(a.config)));return
    if a.command=='freeze-runtime':print(json.dumps(freeze_runtime(a.configuration,a.config,a.output)));return
    if a.command=='validate':validate(a.state,a.config);print('{"valid":true,"qualified":false}');return
    value=replay(a.state,a.mode,a.config) if a.command=='replay' else reconcile(a.state,a.config)
    print(json.dumps({'state':field(value,'State'),'passed':value.get('passed'),'qualified':False}))
    if value.get('passed') is False:raise SystemExit(2)
if __name__=='__main__':main()
