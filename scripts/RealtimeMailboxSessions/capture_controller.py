"""Bounded, resumable state journal for two original sessions and serial Memory/File replays.

Execution changes the explicitly authorized development profile. Validation/arming alone does not.
No phase can convert a future capture, quiet socket or zero counter into owner qualification.
"""
from __future__ import annotations
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import shutil
import time
import session_tool as tools
import operational_boundary as boundary
import capacity_tool


def archive_copy_directory(output_root, session_id):
    if not isinstance(session_id,str) or not 1<=len(session_id)<=256:raise ValueError('Original archive session identity is required')
    epoch='archive_'+__import__('hashlib').sha256(session_id.encode()).hexdigest()[:24]
    return Path(output_root)/'final'/'archive-copy'/epoch


def verified_resume(config_path, config):
    root=Path(config['outputDirectory'])
    if not root.exists():return None
    state=tools.load(root/'controller.json')
    if state.get('configSha256')!=tools.digest(config_path) or state.get('state') not in ('ArmedWaitingInitialBoundary','CapturingOriginalFlow'):
        raise ValueError('Existing controller phase requires explicit inspection; never overwrite a partial boundary')
    required=('initialSnapshot','runtimeSettings','frozenRuntime') if state['state']=='CapturingOriginalFlow' else ()
    for key in required:
        if not isinstance(state.get(key),dict):raise ValueError('Completed original phase omitted '+key)
    for item in [*state.get('artifacts',[]),*(state[key] for key in required)]:
        if tools.digest(item['path'])!=item['sha256']:raise ValueError('Original completed phase evidence changed before resume')
    if required:
        from replay_after_capture import runtime_entries
        runtime_entries(state) # Verify the complete frozen dependency tree, not merely its manifest.
    return state


def validate(config, now=None, allow_started=False):
    if config.get('version')!=1: raise ValueError('Unsupported operational controller configuration')
    now=now or dt.datetime.now(tools.UTC)
    plan=tools.validate_plan(tools.load(config['plan']))
    initial=tools.timestamp(config['initialBoundaryUtc']); deadline=min(tools.timestamp(s['captureStartUtc']) for s in plan['sessions'])
    end=max(tools.timestamp(s['captureEndUtc']) for s in plan['sessions'])
    if not initial<deadline<end or not allow_started and not now<initial or end-now>dt.timedelta(days=8) or now>=end:
        raise ValueError('Initial snapshot must finish before two finite normal-session windows; missed windows cannot be retroactively certified')
    for key in ('plan','capacityPlan','managerControl','runtimeOverlayPath'):
        if not Path(config[key]).is_absolute() or not Path(config[key]).is_file(): raise ValueError('Required operational input is absent: '+key)
    powershell=config.get('powershell')
    if not isinstance(powershell,str) or not Path(powershell).is_absolute() or not Path(powershell).is_file():
        raise ValueError('Actual configured PowerShell executable is absent')
    overlay=tools.load(config['runtimeOverlayPath'])
    if overlay.get('Actors',{}).get('RealtimeMailboxes',{}).get('Enabled') is not False: raise ValueError('Original capture must retain Core mode; durable activation is forbidden')
    if overlay.get('Actors',{}).get('SessionCapture',{}).get('Enabled') is not True: raise ValueError('Original archive must be enabled independently')
    for key in ('apiUrl','nativeUrl'): tools.local_url(config[key])
    if {x['owner'] for x in config.get('ownerLeaseRoots',[])}!={'source-outbox','domain-journals','indicator-state','workflow-state','emulator-ledger','registrations-config'}:
        raise ValueError('Actual file-owner providers must be fully populated before arming')
    if not plan['snapshot']['postgres'] or not plan['snapshot']['scylla']: raise ValueError('Actual PostgreSQL and Scylla providers are required')
    capture_root=Path(tools.load(config['capacityPlan'])['captureRoot']).resolve()
    output=Path(config['outputDirectory']).resolve()
    if output==capture_root or not output.is_relative_to(capture_root):raise ValueError('Controller evidence must stay in its explicitly reserved capture root')
    for mode in ('Memory','File'):
        action=config['replayActions'][mode]
        if not isinstance(action.get('argv'),list) or not action['argv'] or not action.get('resultPath'): raise ValueError('An actual replay/restore action is required for '+mode)
    for action in [*config['replayActions'].values(),config['reconcileAction'],config['originalOutcomeAction'],config['freezeRuntimeAction']]:
        argv=action['argv']
        if not shutil.which(argv[0]) and not Path(argv[0]).is_file():raise ValueError('An operational executable is absent')
        if Path(argv[0]).stem.lower() in ('python','python3') and not Path(argv[1]).is_file():raise ValueError('Concrete operation script is absent')
        if '--config' in argv and not Path(argv[argv.index('--config')+1]).is_file():raise ValueError('Concrete replay/outcome provider configuration is absent')
        if not 1<=action.get('maximumSeconds',0)<=604800:raise ValueError('Every operation requires a finite timeout')
    return plan,initial,deadline,end


def capacity(config, output):
    plan=capacity_tool.refresh_measurements(tools.load(config['capacityPlan']))
    tools.write(str(output)+'.measurements.json',plan)
    result=capacity_tool.evaluate(plan); tools.write(output,result)
    if result.get('capacityReady') is not True: raise ValueError('Fresh finite-volume capacity check failed')
    return result


def runtime(config, token):
    startup=boundary.request(config['apiUrl'],'/health/launch-ready',token)
    if boundary.field(startup,'Status')!='Healthy':
        raise ValueError('Original application startup is incomplete; no capture boundary may be acquired')
    archive=boundary.request(config['apiUrl'],'/internal/session-capture/status',token)
    if boundary.field(archive,'Enabled') is not True or archive.get('durableMailboxEnabled') is not False or boundary.field(archive,'Failure'):
        raise ValueError('Actual original runtime is not healthy source-capture-only/Core mode')
    expected='archive_'+__import__('hashlib').sha256(config['archiveSessionId'].encode()).hexdigest()[:24]
    if Path(boundary.field(archive,'SessionDirectory','')).resolve()!=Path(config['archiveDirectory']).resolve()/expected:
        raise ValueError('Actual archive owner is not this exact configured original session')
    native=boundary.request(config['nativeUrl'],'/internal/session-capture/native/status',token)
    if boundary.field(boundary.field(native,'Status',{}),'Supported') is not True: raise ValueError('Actual native source capture/forwarding capability is unavailable')
    local=boundary.request(config['apiUrl'],'/internal/session-capture/boundary',token)
    iti=boundary.field(local,'Iti')
    if not isinstance(iti,dict) or boundary.field(iti,'Failed') is not False:
        raise ValueError('Original ordered ITI owner is missing or failed; capture cannot continue')
    # Freshness and a zero pending count are not required while original live sources run,
    # including a closed Sunday market. A failed resident owner is never ignored.
    return {'observedUtc':tools.utc(),'startup':startup,'archive':archive,'native':native,'orderedIti':iti,'qualified':False}


def action(config, name, state_path, mode=None, fence=None):
    spec=config[name] if mode is None else config[name][mode]
    args=[x.replace('{state}',str(state_path)).replace('{mode}',mode or '').replace('{fence}',str(fence or '')) for x in spec['argv']]
    if any('{state}' in x or '{mode}' in x for x in args): raise ValueError('Unresolved operation argument')
    result=subprocess.run(args,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,
                          shell=False,timeout=spec.get('maximumSeconds',200000),check=False)
    if result.returncode: raise RuntimeError('Required isolated operation failed; protected output withheld')
    path=Path(spec['resultPath'].replace('{mode}',mode or ''))
    value=tools.load(path)
    return path,value


def run(config_path):
    config=tools.load(config_path);state=verified_resume(config_path,config)
    plan,initial,deadline,end=validate(config,allow_started=state is not None and state['state']=='CapturingOriginalFlow')
    token=os.environ.get('IFM_SESSION_CAPTURE_ACCESS_TOKEN','')
    if not 32<=len(token)<=256: raise ValueError('A bounded protected environment token is required')
    root=Path(config['outputDirectory']).resolve();state_path=root/'controller.json'
    if state is not None:
        if state['state']=='ArmedWaitingInitialBoundary' and dt.datetime.now(tools.UTC)>=initial:
            raise ValueError('Initial boundary start was missed; never reconstruct an already elapsed baseline from current state')
    else:
        root.mkdir(parents=True,exist_ok=False)
        state={'version':1,'state':'ArmedWaitingInitialBoundary','qualified':False,'configPath':str(Path(config_path).resolve()),'configSha256':tools.digest(config_path),'sessions':plan['sessions'],'artifacts':[],'archiveDirectory':config['archiveDirectory'],'archiveSessionId':config['archiveSessionId']}
    def save(name,**changes): state.update(state=name,updatedUtc=tools.utc(),**changes);tools.write(state_path,state)
    try:
        capacity(config,root/'initial-capacity.json');tools.write(root/'armed-runtime.json',runtime(config,token))
        if state['state']=='ArmedWaitingInitialBoundary':
            save('ArmedWaitingInitialBoundary')
            while dt.datetime.now(tools.UTC)<initial:time.sleep(min(30,(initial-dt.datetime.now(tools.UTC)).total_seconds()))
            capacity(config,root/'before-snapshot-capacity.json');save('AcquiringActualInitialBoundary')
            with boundary.HeldBoundary(config,plan,token,root/'initial') as held:
                held.snapshot()
                state['initialSnapshot']={'path':str(root/'initial'/'owners'/'snapshot.json'),'sha256':tools.digest(root/'initial'/'owners'/'snapshot.json')}
                state['runtimeSettings']={'path':str(root/'initial'/'effective-configuration.json'),'sha256':tools.digest(root/'initial'/'effective-configuration.json')}
                state['initialBoundary']={'api':held.boundary,'native':held.native_boundary,'nativeCertificate':str(held.native_certificate)}
                save('InitialOwnersCapturedAwaitingRestart')
                path,result=action(config,'freezeRuntimeAction',state_path)
                state['frozenRuntime']={'path':str(path),'sha256':tools.digest(path)}
                save('InitialOwnersAndRuntimeCapturedAwaitingRestart')
            if dt.datetime.now(tools.UTC)>=deadline: raise TimeoutError('Baseline did not finish before the first capture window')
            save('CapturingOriginalFlow')
        with (root/'runtime-samples.jsonl').open('a',encoding='utf-8') as samples:
            next_capacity=0.0
            while dt.datetime.now(tools.UTC)<end:
                if time.monotonic()>=next_capacity:
                    capacity(config,root/f'capacity-capture-{time.time_ns()}.json');next_capacity=time.monotonic()+600
                row=runtime(config,token); samples.write(json.dumps(row,separators=(',',':'))+'\n');samples.flush();os.fsync(samples.fileno())
                if samples.tell()>64*1024*1024: raise ValueError('Runtime evidence reached its finite budget')
                time.sleep(min(30,(end-dt.datetime.now(tools.UTC)).total_seconds()))
        capacity(config,root/'before-final-capacity.json');save('AcquiringActualFinalBoundary')
        with boundary.HeldBoundary(config,plan,token,root/'final',copy_native_prefixes=False) as held:
            state['finalBoundary']={'path':str(root/'final'/'held-evidence.json'),'sha256':tools.digest(root/'final'/'held-evidence.json')}
            held.check()
            from replay_after_capture import validate_final_runtime
            state['finalRuntimeProof']=validate_final_runtime(state,root/'final'/'effective-configuration.json')
            archive_copy=archive_copy_directory(root,config['archiveSessionId'])
            tools.copy_archive_prefix(config['job'],archive_copy,root/'final'/'initial-archive-prefix.json')
            state['sourceArchive']={'path':str(archive_copy/'archive-prefix-copy.json'),'sha256':tools.digest(archive_copy/'archive-prefix-copy.json')}
            save('ActualWindowsCapturedOwnerReconciliationRequired')
            path,result=action(config,'originalOutcomeAction',state_path,fence=root/'final'/'held-evidence.json')
            if not result.get('path') or not result.get('sha256') or tools.digest(result['path'])!=result['sha256']:
                raise ValueError('Actual original outcome export did not return a verified business manifest')
            state['originalOutcomeEvidence']=result
            state['originalOutcome']=state['originalOutcomeEvidence']
            if not result.get('rawPartitions'):raise ValueError('Original export omitted its actual source-derived partition manifest')
            state['rawPartitions']=result['rawPartitions']
            held.check();held.complete=True
            save('OriginalStoppedOwnerOutcomesExported')
        for mode in ('Memory','File'):
            capacity(config,root/f'before-{mode.lower()}-capacity.json');save('RestoringAndReplaying'+mode)
            path,result=action(config,'replayActions',state_path,mode)
            if boundary.field(result,'State')!='ExecutedAwaitingOwnerReconciliation': raise ValueError('Actual replay did not complete cleanly for '+mode)
            state['artifacts'].append({'mode':mode,'path':str(path),'sha256':tools.digest(path)});save(mode+'ReplayAwaitingReconciliation')
        path,result=action(config,'reconcileAction',state_path)
        if result.get('passed') is not True: raise ValueError('Actual exact owner outcomes did not reconcile')
        state['artifacts'].append({'path':str(path),'sha256':tools.digest(path)});save('TwoSessionsComparedAwaitingCoverage',qualified=False)
    except BaseException as error:
        save('BlockedRetainedEvidence',error=type(error).__name__,reason=str(error).replace(token,'[protected]')[:1024])
        raise
    return state


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('command',choices=('validate','validate-now','boundary-smoke','prepare-shutdown','run'));parser.add_argument('--config',required=True);parser.add_argument('--output')
    args=parser.parse_args();config=tools.load(args.config)
    if args.command=='prepare-shutdown':
        if not args.output:raise ValueError('Shutdown preparation requires a new explicit evidence directory')
        token=os.environ.get('IFM_SESSION_CAPTURE_ACCESS_TOKEN','')
        if not 32<=len(token)<=256:raise ValueError('Protected capture token is required')
        runtime(config,token)
        result=boundary.prepare_profile_shutdown(config,token,args.output)
    elif args.command=='boundary-smoke':
        if not args.output:raise ValueError('Smoke requires a new explicit evidence directory')
        plan,_,_,_=validate(config);token=os.environ.get('IFM_SESSION_CAPTURE_ACCESS_TOKEN','')
        if not 32<=len(token)<=256:raise ValueError('Protected capture token is required')
        runtime(config,token)
        with boundary.HeldBoundary(config,plan,token,args.output,copy_native_prefixes=False) as held:
            held.check(force=True);result={'version':1,'state':'OwnerLeaseSmokePassedNoDataBaseline','qualified':False,
                'snapshotCreated':False,'boundary':{'path':str(Path(args.output)/'held-evidence.json'),'sha256':tools.digest(Path(args.output)/'held-evidence.json')}}
            tools.write(Path(args.output)/'smoke.json',result);held.complete=True
    elif args.command=='run': result=run(args.config)
    else:
        resume=verified_resume(args.config,config)
        validate(config,allow_started=resume is not None and resume['state']=='CapturingOriginalFlow');result={'version':1,'state':'ConfigurationValidatedNotStarted','qualified':False}
        if args.command=='validate-now':
            capacity(config,args.output or str(Path(args.config).with_suffix('.capacity.json')))
            result.update(runtime=runtime(config,os.environ.get('IFM_SESSION_CAPTURE_ACCESS_TOKEN','')),state='ActualRuntimeValidatedNotStarted')
    if args.output and args.command not in ('boundary-smoke','prepare-shutdown'): tools.write(args.output,result)
    print(json.dumps({'state':result['state'],'qualified':result.get('qualified',False)}))

if __name__=='__main__': main()
