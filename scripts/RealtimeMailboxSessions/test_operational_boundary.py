import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import operational_boundary as boundary
import capture_controller
import session_tool


class OperationalBoundaryTests(unittest.TestCase):
    def test_feed_startup_finishes_before_pause_without_waiving_unhealthy_owner(self):
        unhealthy={'entries':{'market_data_runtime':{'status':'Degraded'}}}
        healthy={'status':'Unhealthy','entries':{'market_data_runtime':{'status':'Healthy'},'market_data_indicators':{'status':'Unhealthy'}}}
        with patch.object(boundary,'request',side_effect=[unhealthy,healthy]),patch.object(boundary.time,'sleep') as delay:
            boundary.wait_for_settled_market_data({'apiUrl':'http://127.0.0.1:1'},'protected')
            delay.assert_called_once_with(.5)
        with patch.object(boundary,'request',return_value=unhealthy),patch.object(boundary.time,'monotonic',side_effect=[0,2]):
            with self.assertRaisesRegex(TimeoutError,'no snapshot pause was acquired'):
                boundary.wait_for_settled_market_data({'apiUrl':'http://127.0.0.1:1','drainSeconds':1},'protected')

    def test_existing_owner_directory_identity_accepts_windows_verbatim_spelling_only_for_same_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);other=root/'other';other.mkdir()
            self.assertTrue(session_tool.same_directory(root,root))
            self.assertFalse(session_tool.same_directory(root,other))
            self.assertFalse(session_tool.same_directory(root,root/'missing'))
            if boundary.os.name=='nt':
                self.assertTrue(session_tool.same_directory(root,'\\\\?\\'+str(root.resolve())))

    def test_native_pause_drains_before_forwarding_is_held_for_the_final_prefix(self):
        calls=[];prefixes=iter([{'PendingCount':1,'NextOrdinal':3},{'PendingCount':0,'NextOrdinal':3}])
        def request(url,route,token,post=False,timeout=15):
            calls.append(route)
            if 'pause-production' in route:return {'Status':{'Supported':True,'Lease':7,'ActiveCallbacks':0},'RawRetentionLease':7}
            if route.endswith('/status'):return {'Status':{'Supported':True,'Lease':7,'ActiveSends':0}}
            if '/prefix/' in route:return next(prefixes)
            return {}
        with patch.object(boundary,'request',side_effect=request),patch.object(boundary.time,'sleep'):
            result=boundary.pause_and_drain_native({'nativeUrl':'http://127.0.0.1:1','drainSeconds':1},'protected',7)
        self.assertEqual(0,result['PendingCount']);self.assertIn('pause-production',calls[0])
        self.assertEqual(1,len([x for x in calls if '/release/' in x]));self.assertEqual(2,len([x for x in calls if '/hold/' in x]))

    def test_post_image_native_append_cannot_be_certified_as_the_old_owner_state(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);(root/'source.pubwal').write_bytes(b'original')
            prefix={'SourceRoot':str(root),'Files':[{'RelativePath':'source.pubwal','Length':8}]}
            boundary.stopped_native_prefix_unchanged(prefix)
            with (root/'source.pubwal').open('ab')as stream:stream.write(b'unrepresented-tail')
            with self.assertRaisesRegex(ValueError,'changed after'):boundary.stopped_native_prefix_unchanged(prefix)

    def test_elapsed_initial_time_is_allowed_only_for_verified_capture_phase_not_a_missed_new_baseline(self):
        import datetime as dt,sys
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);capacity=root/'capacity.json';session_tool.write(capacity,{'captureRoot':str(root)})
            overlay=root/'overlay.json';session_tool.write(overlay,{'Actors':{'RealtimeMailboxes':{'Enabled':False},'SessionCapture':{'Enabled':True}}})
            action={'argv':[sys.executable,str(Path(__file__).resolve())],'resultPath':str(root/'result'),'maximumSeconds':30}
            config={'version':1,'plan':str(overlay),'capacityPlan':str(capacity),'managerControl':str(overlay),'runtimeOverlayPath':str(overlay),
                    'powershell':sys.executable,
                    'initialBoundaryUtc':'2026-11-29T17:00:00Z','apiUrl':'http://127.0.0.1:1','nativeUrl':'http://127.0.0.1:2',
                    'ownerLeaseRoots':[{'owner':name}for name in ('source-outbox','domain-journals','indicator-state','workflow-state','emulator-ledger','registrations-config')],
                    'outputDirectory':str(root/'run'),'replayActions':{'Memory':action,'File':action},'reconcileAction':action,'originalOutcomeAction':action,'freezeRuntimeAction':action}
            plan={'sessions':[{'captureStartUtc':'2026-11-29T22:50:00Z','captureEndUtc':'2026-11-30T23:10:00Z'},
                              {'captureStartUtc':'2026-11-30T22:50:00Z','captureEndUtc':'2026-12-01T23:10:00Z'}],
                  'snapshot':{'postgres':[{}],'scylla':[{}]}}
            now=dt.datetime(2026,11,30,12,tzinfo=session_tool.UTC)
            with patch.object(session_tool,'validate_plan',return_value=plan):
                with self.assertRaisesRegex(ValueError,'missed windows'):capture_controller.validate(config,now=now)
                self.assertEqual(plan,capture_controller.validate(config,now=now,allow_started=True)[0])
                with self.assertRaisesRegex(ValueError,'PowerShell executable is absent'):
                    capture_controller.validate({**config,'powershell':str(root/'missing-pwsh.exe')},now=now,allow_started=True)
                with self.assertRaisesRegex(ValueError,'missed windows'):capture_controller.validate(config,now=dt.datetime(2026,12,2,tzinfo=session_tool.UTC),allow_started=True)

    def test_resume_requires_original_config_and_complete_hash_verified_phase(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);config_path=root/'config.json';output=root/'run';output.mkdir()
            config={'outputDirectory':str(output)};session_tool.write(config_path,config)
            state={'state':'CapturingOriginalFlow','configSha256':session_tool.digest(config_path)}
            for key in ('initialSnapshot','runtimeSettings','frozenRuntime'):
                artifact=root/(key+'.json');artifact.write_bytes(key.encode());state[key]={'path':str(artifact),'sha256':session_tool.digest(artifact)}
            session_tool.write(output/'controller.json',state)
            with patch('replay_after_capture.runtime_entries') as verify_tree:
                self.assertEqual('CapturingOriginalFlow',capture_controller.verified_resume(config_path,config)['state']);verify_tree.assert_called_once()
            (root/'initialSnapshot.json').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'evidence changed'):capture_controller.verified_resume(config_path,config)
            state['state']='AcquiringActualInitialBoundary';session_tool.write(output/'controller.json',state)
            with self.assertRaisesRegex(ValueError,'explicit inspection'):capture_controller.verified_resume(config_path,config)

    def test_runtime_rejects_failed_iti_but_not_market_closed_or_transient_pending_inputs(self):
        import hashlib
        config={'apiUrl':'http://127.0.0.1:1','nativeUrl':'http://127.0.0.1:2','archiveSessionId':'original','archiveDirectory':'D:/capture'}
        archive={'enabled':True,'durableMailboxEnabled':False,'failure':None,'sessionDirectory':'D:/capture/archive_'+hashlib.sha256(b'original').hexdigest()[:24]}
        native={'status':{'supported':True}}
        iti={'failed':False,'pending':3,'reason':'Market closed; stale source timestamp'}
        with patch.object(boundary,'request',side_effect=[{'status':'Healthy'},archive,native,{'iti':iti}]):
            self.assertEqual(3,capture_controller.runtime(config,'protected')['orderedIti']['pending'])
        with patch.object(boundary,'request',side_effect=[{'status':'Healthy'},archive,native,{'iti':{**iti,'failed':True}}]):
            with self.assertRaisesRegex(ValueError,'owner is missing or failed'):capture_controller.runtime(config,'protected')
        with patch.object(boundary,'request',return_value={'status':'Unhealthy'}) as request:
            with self.assertRaisesRegex(ValueError,'startup is incomplete'):capture_controller.runtime(config,'protected')
            self.assertEqual(1,request.call_count)

    def test_actual_archive_copy_lives_inside_its_protocol_epoch_without_a_second_body_copy(self):
        import hashlib
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);session='original-two-normal-days';epoch='archive_'+hashlib.sha256(session.encode()).hexdigest()[:24]
            source=root/'original'/epoch;(source/'sources').mkdir(parents=True);(source/'sources'/'001.rto').write_bytes(b'original-wire-prefix')
            overlay=root/'overlay.json';session_tool.write(overlay,{'Actors':{'SessionCapture':{'Directory':str(root/'original'),'MaximumArchiveBytes':1024,'MaximumReceiptBytes':1024}}})
            job=root/'job.json';session_tool.write(job,{'overlay':str(overlay)})
            prefix=root/'prefix.json';session_tool.write(prefix,{'ArchiveOwnershipOnly':True,'OwnerSnapshot':False,'MapperCheckpoint':False,'SessionDirectory':str(source),'Files':[{'RelativePath':'sources/001.rto','Length':20}]})
            target=capture_controller.archive_copy_directory(root/'captured',session)
            session_tool.copy_archive_prefix(job,target,prefix)
            manifest=target/'archive-prefix-copy.json'
            self.assertEqual(epoch,manifest.parent.name)
            self.assertEqual(b'original-wire-prefix',(manifest.parent/'sources'/'001.rto').read_bytes())
            self.assertEqual(1,len(list((root/'captured').rglob('*.rto'))))

    def test_only_exact_held_zero_work_receipts_admit_local_drain(self):
        value={'state':'LocallyDrained','locallyDrained':True,'leaseId':'a','boundaryId':'b','rootProducersPaused':True,
               'activeRootPublications':0,'actorQueued':0,'actorProcessing':0,'rawPending':0,'rawUnacknowledged':0,'archivePending':0,'archiveFailure':None}
        self.assertTrue(boundary.drained(value,'a','b'))
        for key,bad in [('actorQueued',1),('rawPending',False),('rootProducersPaused',False),('archiveFailure','disk_full'),('leaseId','other')]:
            changed={**value,key:bad};self.assertFalse(boundary.drained(changed,'a','b'))
        iti={'drained':True,'failed':False,'pending':0,'inFlightCaptures':0,'preparedSources':0,'unflushedSources':0}
        self.assertTrue(boundary.drained({**value,'iti':iti},'a','b'))
        for key,bad in [('failed',True),('pending',1),('inFlightCaptures',1),('preparedSources',1),('unflushedSources',1),('pending',False)]:
            self.assertFalse(boundary.drained({**value,'iti':{**iti,key:bad}},'a','b'))

    def test_actual_existing_file_lease_never_creates_a_lookalike(self):
        with tempfile.TemporaryDirectory() as temporary:
            path=Path(temporary)/'actual-ledger.json.owner';path.write_bytes(b'original')
            with boundary.existing_file_lease(path) as reader:
                self.assertEqual(b'original',reader.read())
                if boundary.os.name=='nt':
                    with self.assertRaises((PermissionError,OSError)):path.open('wb')
                else:
                    with self.assertRaises((BlockingIOError,OSError)):
                        with boundary.existing_file_lease(path):pass
            self.assertEqual(b'original',path.read_bytes())
            with self.assertRaises(FileNotFoundError):
                with boundary.existing_file_lease(Path(temporary)/'owner.lock'):pass
            self.assertFalse((Path(temporary)/'owner.lock').exists())

    def test_capacity_refresh_invokes_actual_measurement_before_evaluation(self):
        with tempfile.TemporaryDirectory() as temporary:
            source=Path(temporary)/'capacity.json';session_tool.write(source,{'measuredAtUtc':'old'})
            calls=[]
            def refresh(value):calls.append(('measure',value['measuredAtUtc']));return {'measuredAtUtc':'fresh','actualBytes':123}
            def evaluate(value):calls.append(('evaluate',value['actualBytes']));return {'capacityReady':True,'qualified':False}
            with patch.object(capture_controller.capacity_tool,'refresh_measurements',side_effect=refresh),patch.object(capture_controller.capacity_tool,'evaluate',side_effect=evaluate):
                result=capture_controller.capacity({'capacityPlan':str(source)},Path(temporary)/'result.json')
            self.assertEqual([('measure','old'),('evaluate',123)],calls);self.assertFalse(result['qualified'])
            self.assertEqual(123,session_tool.load(Path(temporary)/'result.json.measurements.json')['actualBytes'])

    def test_native_certificate_requires_exact_original_artifacts_not_response_flags(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);folder=root/'012345abcdef';folder.mkdir();value={'Version':1,'Kind':'FrozenNativeAndBackendSnapshot'}
            for prefix,name in [('Checkpoint','native.checkpoint'),('BackendSnapshot','backend.bin'),('SourcePrefix','prefix.json')]:
                path=folder/name;path.write_bytes(prefix.encode());value.update({prefix+'File':name,prefix+'Bytes':path.stat().st_size,prefix+'Sha256':session_tool.digest(path)})
            session_tool.write(folder/'checkpoint.json',value)
            self.assertEqual(folder/'checkpoint.json',boundary.certificate(root,value,'012345abcdef'))
            wire={key[0].lower()+key[1:]:item for key,item in value.items()}
            self.assertEqual(folder/'checkpoint.json',boundary.certificate(root,wire,'012345abcdef'))
            with self.assertRaisesRegex(ValueError,'readback differs'):
                boundary.certificate(root,{**wire,'version':2},'012345abcdef')
            with self.assertRaisesRegex(ValueError,'ambiguous'):
                boundary.certificate(root,{**wire,'Version':1},'012345abcdef')
            (folder/'native.checkpoint').write_bytes(b'changed')
            with self.assertRaises(ValueError):boundary.certificate(root,value,'012345abcdef')


if __name__=='__main__':unittest.main()
