import datetime as dt,json,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import capacity_tool as tool

class CapacityTests(unittest.TestCase):
 def setUp(self):
  self.inspection=patch.object(tool,'inspect_docker',return_value=1000*tool.GiB);self.inspection.start();self.addCleanup(self.inspection.stop)
 def plan(self,root):
  vhd=root/'docker.vhdx';vhd.write_bytes(b'actual-inspected-placeholder');native=root/'native';native.mkdir();(native/'namespace.sha256').write_bytes(bytes(32))
  owner_plan=root/'owners.json';owner_plan.write_text(json.dumps({'snapshot':{'fileRoots':[{'owner':'fixture','path':str(native)}]}}))
  return {'version':1,'captureRoot':str(root/'capture'),'dockerVhdPath':str(vhd),'dockerGuestAvailableBytes':1000*tool.GiB,'dockerGuestRequiredBytes':100*tool.GiB,'measuredAtUtc':dt.datetime.now(dt.timezone.utc).isoformat(),'minimumFreeBytes':tool.GiB,'nativeRoots':[{'path':str(native),'bindings':[{'relativePath':'namespace.sha256','sha256':tool.digest(native/'namespace.sha256')}]}],
   'measurementSources':{'fileOwnerPlan':str(owner_plan)},'measurements':{'sourceSnapshotMinimumBytes':0,'sourceRestoreMinimumBytes':0,'nativeWorkingMinimumBytes':0,'fileOwnerCopies':[{'owner':'fixture','path':str(native),'bytes':32}]},
   'phases':[{'name':'capture','reservations':[{'name':'originals','path':str(root/'capture'),'bytes':20*tool.GiB}]},{'name':'serial-memory-file','reservations':[{'name':'retained+one-restore+all-exact-indexes','path':str(root/'replays'),'bytes':120*tool.GiB}]}]}
 def test_peak_not_sum_and_pressure_failure(self):
  with tempfile.TemporaryDirectory() as temp:
   plan=self.plan(Path(temp))
   with patch.object(tool.shutil,'disk_usage',return_value=SimpleNamespace(free=122*tool.GiB,total=1000*tool.GiB)):
    report=tool.evaluate(plan);self.assertTrue(report['capacityReady']);self.assertEqual(120*tool.GiB,next(iter(report['volumes'].values()))['requiredBytes']);self.assertFalse(report['qualified'])
   with patch.object(tool.shutil,'disk_usage',return_value=SimpleNamespace(free=120*tool.GiB,total=1000*tool.GiB)):self.assertFalse(tool.evaluate(plan)['capacityReady'])
 def test_stale_guest_and_original_root_bindings_fail_closed(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);plan=self.plan(root);plan['measuredAtUtc']='2020-01-01T00:00:00+00:00';self.assertFalse(tool.evaluate(plan)['capacityReady']);plan['nativeRoots'][0]['bindings'][0]['sha256']='1'*64
   with self.assertRaisesRegex(ValueError,'binding changed'):tool.evaluate(plan)
   plan['nativeRoots'][0]['path']=str(root/'empty');(root/'empty').mkdir()
   with self.assertRaisesRegex(ValueError,'empty'):tool.evaluate(plan)
 def test_docker_guest_pressure_and_missing_root_inspection(self):
  with tempfile.TemporaryDirectory() as temp:
   plan=self.plan(Path(temp));plan['nativeRoots']=[]
   with patch.object(tool,'inspect_docker',return_value=100*tool.GiB):report=tool.evaluate(plan)
   self.assertFalse(report['capacityReady']);self.assertEqual(2,len(report['failures']))
 def test_refresh_reads_actual_stores_and_fixed_capacity_cannot_be_raised_by_timestamp(self):
  with tempfile.TemporaryDirectory() as temp:
   plan=self.plan(Path(temp));plan['measurementSources'].update(postgres={'container':'actual_pg','database':'actual_db','user':'postgres'},scylla={'container':'actual_scylla'})
   plan['phases'][1]['reservations']=[{'name':'original-held-owner-snapshot','path':str(Path(temp)/'snapshot'),'bytes':100*tool.GiB},{'name':'one-disposable-restored-pg-scylla-plus-wal','path':str(Path(temp)/'restore'),'bytes':100*tool.GiB}]
   def measured(args):
    if args[:2]==['docker','inspect']:return 'actual-container-id'
    return str(90*tool.GiB) if 'psql' in args else str(10*tool.GiB)+' /var/lib/scylla/data'
   with patch.object(tool,'measured_command',side_effect=measured):refreshed=tool.refresh_measurements(plan)
   report=tool.evaluate(refreshed);self.assertFalse(report['capacityReady']);self.assertIn('Actual source stores exceed the bounded owner snapshot reservation',report['failures']);self.assertEqual(90*tool.GiB,report['measurements']['postgresBytes'])
 def test_first_source_owner_is_promoted_only_at_original_planned_root_and_namespace(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);plan=self.plan(root);source=root/'original-source';source.mkdir();(source/'namespace.sha256').write_bytes(bytes(32));plan['plannedInitialNativeRoots']=[{'path':str(source),'previouslyEnabled':False,'requiresFreshPostBootstrapNamespaceProof':True}];prefix=root/'prefix.json';prefix.write_text(json.dumps({'Version':1,'ForwardingLease':7,'SourceRoot':str(source),'Scope':'development','MapperCheckpoint':False,'RequiredIntentCheckpoint':False,'Files':[{'RelativePath':'namespace.sha256','Length':32}]}))
   bound=tool.bind_native_prefix(plan,prefix);self.assertFalse(bound['plannedInitialNativeRoots']);self.assertTrue(tool.evaluate(bound)['capacityReady']);(source/'namespace.sha256').write_bytes(bytes([1])*32)
   with self.assertRaisesRegex(ValueError,'binding changed'):tool.evaluate(bound)
if __name__=='__main__':unittest.main()
