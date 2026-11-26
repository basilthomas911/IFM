import ast,copy,decimal,hashlib,io,json,os,re,struct,subprocess,tempfile,time,unittest,uuid
from pathlib import Path
from unittest.mock import patch
from types import SimpleNamespace
import outcome_evidence as evidence

HERE=Path(__file__).resolve().parent;REPO=HERE.parent.parent
BOUNDARY={'sessionId':'fixture','initialSnapshotSha256':'1'*64,'sourceCaptureSha256':'2'*64,'calendarEvidenceSha256':'3'*64}
def config():return {'role':'DisposableQualification','runId':'012345abcdef','backend':'Memory','boundary':BOUNDARY,'limits':{'chunkBytes':256,'maximumBytes':32*1024**2,'maximumRecordBytes':2*1024**2,'maximumSeconds':120}}
def checkpoint():return {'Cash':decimal.Decimal('10000.25'),'Sequence':7,'Generation':2,'CommitRevision':11,'Orders':[], 'Operations':[], 'Positions':[{'ContractId':'MESZ6','SignedQuantity':2,'AveragePrice':decimal.Decimal('5000.125')}], 'Journal':[], 'Observations':[]}

class OutcomeEvidenceTests(unittest.TestCase):
 def test_canonical_exact_decimal_and_original_timestamp_identity(self):
  a=json.loads('{"n":123456789012345678901234567890.1200,"date":"2026-11-25T23:00:00.1234567Z"}',parse_float=decimal.Decimal)
  self.assertIn(b'123456789012345678901234567890.12',evidence.canonical(a));self.assertEqual(b'0',evidence.canonical(decimal.Decimal('-0.000')))
  self.assertNotEqual(evidence.canonical(a),evidence.canonical({**a,'date':'2026-11-25T23:00:00Z'}))
  with self.assertRaises(ValueError):evidence.canonical(1.25)
 def ledger(self,root,rows):
  cfg=config();writer=evidence.Ledger(root,cfg['limits'])
  for key,value in rows:writer.add('cash','ActualFixture','ledger.account',[key],value)
  return evidence.finalize(cfg,writer,evidence.base_manifest(cfg,'ActualFixture'))
 def test_external_order_exact_rows_and_decimal_comparison(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);self.ledger(root/'memory',[(2,{'balance':decimal.Decimal('2.50')}),(1,{'balance':decimal.Decimal('1.25')})]);self.ledger(root/'file',[(1,{'balance':decimal.Decimal('1.2500')}),(2,{'balance':decimal.Decimal('2.5')})])
   result=evidence.compare(root/'memory/manifest.json',root/'file/manifest.json',root/'match.json');self.assertTrue(result['matched']);self.assertFalse(result['qualified'])
   self.ledger(root/'changed',[(1,{'balance':decimal.Decimal('1.25')}),(2,{'balance':decimal.Decimal('2.75')})]);result=evidence.compare(root/'memory/manifest.json',root/'changed/manifest.json',root/'different.json')
   self.assertFalse(result['matched']);self.assertEqual('2.75',str(result['differences'][0]['actual']['value']['balance']))
 def test_corruption_missing_and_duplicate_identity_fail_closed(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);self.ledger(root/'a',[(1,{'amount':1})]);path=root/'a/outcomes.jsonl';wire=path.read_bytes();path.write_bytes(wire.replace(b'amount',b'amounX'))
   with self.assertRaises(ValueError):list(evidence.records(root/'a/manifest.json'))
   writer=evidence.Ledger(root/'duplicate',config()['limits']);writer.add('cash','o','r',[1],1);writer.add('cash','o','r',[1],1)
   with self.assertRaises(ValueError):writer.finish()
 def test_incomplete_manifest_cannot_match_even_if_its_rows_and_hashes_are_valid(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);self.ledger(root/'a',[(1,{'amount':1})]);self.ledger(root/'b',[(1,{'amount':1})]);path=root/'b/manifest.json';manifest=evidence.load(path);manifest['state']='Exporting';path.write_bytes(evidence.canonical(manifest))
   with self.assertRaisesRegex(ValueError,'completed actual-owner'):evidence.compare(root/'a/manifest.json',path,root/'result.json')
 def test_bundle_derives_missing_actual_owner_coverage_and_never_promotes_digest_match(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);self.ledger(root/'a',[(1,{'amount':1})]);self.ledger(root/'b',[(2,{'amount':2})])
   result=evidence.bundle([root/'a/manifest.json',root/'b/manifest.json'],root/'bundle')
   self.assertFalse(result['qualified']);self.assertTrue(any('rawRows' in text for text in result['coverageFailures']))
   self.assertTrue(any('production owner relation' in text for text in result['coverageFailures']))
   compared=evidence.compare(root/'bundle/manifest.json',root/'bundle/manifest.json',root/'comparison.json');self.assertFalse(compared['matched'])
 def test_failed_scylla_query_removes_only_its_exact_generated_helper(self):
  with tempfile.TemporaryDirectory() as temp:
   cfg={**config(),'scylla':{'container':'ifm_session_012345abcdef_scylla','keyspace':'ifm_qual','partitions':[{'table':'tick_trade_data','assetType':1,'contractId':'ESZ6','valueDate':'2026-11-25'}]}}
   def invoke(args,**kwargs):
    value=b'{"ifm.session-qualification":"012345abcdef"}' if args[:3]==['docker','network','inspect'] else b'sha256:cached'
    return SimpleNamespace(stdout=value,returncode=0)
   with patch.object(evidence,'verify_target',return_value=cfg['scylla']),patch.object(evidence.subprocess,'run',side_effect=invoke) as commands,patch.object(evidence,'process_lines',side_effect=ValueError('Injected query failure')):
    with self.assertRaisesRegex(ValueError,'Injected query'):evidence.export_scylla(cfg,Path(temp)/'failed')
   cleanups=[call.args[0] for call in commands.call_args_list if call.args[0][:3]==['docker','rm','-f']]
   self.assertEqual(1,len(cleanups));self.assertEqual(['docker','rm','-f','-v'],cleanups[0][:4]);self.assertTrue(cleanups[0][4].startswith('ifm_session_012345abcdef_cql_evidence_'))
 def test_emulator_streams_nested_arrays_empty_arrays_and_refuses_missing_fields_and_mutation(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);path=root/'checkpoint.json';evidence.write(path,checkpoint());cfg={**config(),'emulatorCheckpointPath':str(path)};result=evidence.export_emulator(cfg,root/'export')
   self.assertEqual(11,result['commitRevision']);self.assertFalse(result['coverageFailures']);rows=list(evidence.records(root/'export/manifest.json'))
   self.assertTrue(any(row['category']=='positions' and row['value']['SignedQuantity']==2 for row in rows));self.assertTrue(any(row['key']==['Orders','$empty'] for row in rows))
   path.write_bytes(evidence.canonical({k:v for k,v in checkpoint().items() if k!='Orders'}))
   with self.assertRaisesRegex(ValueError,'Missing actual emulator array'):evidence.export_emulator(cfg,root/'missing')
   path.write_bytes(evidence.canonical(checkpoint())+b' '*70000+b'not-json')
   with self.assertRaisesRegex(ValueError,'trailing'):evidence.export_emulator(cfg,root/'trailing')
 def test_emulator_owner_checkpoint_receipt_keeps_boundary_revision_and_original_snapshot(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);path=root/'checkpoint.json';value=checkpoint();receipt={'BoundaryId':'original-boundary','CommitRevision':11,'Snapshot':{'Cash':decimal.Decimal('9999.12345678'),'SourceSequence':11,'Positions':[{'ContractId':'MESZ6','Quantity':2}]}};value['OwnerCheckpoints']=[receipt];evidence.write(path,value)
   evidence.export_emulator({**config(),'emulatorCheckpointPath':str(path)},root/'export');record=next(row for row in evidence.records(root/'export/manifest.json') if row['key']==['OwnerCheckpoints','original-boundary']);self.assertEqual(receipt,record['value'])
 def test_incremental_parser_does_not_cut_number_at_chunk_boundary(self):
  value={'pad':'x'*65509,'n':123456789012345678901234567890,'Orders':[]}
  items=list(evidence.JsonItems(io.StringIO(json.dumps(value,separators=(',',':'))),1000000).fields())
  self.assertEqual(123456789012345678901234567890,next(v for k,i,v in items if k=='n'))
 def test_receipts_actual_binary_formats_and_compaction_independence(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);epoch=root/'epoch';journal=epoch/'sources';journal.mkdir(parents=True);(epoch/'ownership.protocol').write_text('2:source-until-handler-proof')
   key=bytes.fromhex('11'*32);source=bytes.fromhex('22'*32);event=uuid.uuid4();manifest=bytes.fromhex('33'*32);dest=bytes.fromhex('44'*32)
   full=key+source+event.bytes_le+manifest+struct.pack('<i',2);partial=key+source+dest
   (journal/'receipts.bin').write_bytes(full+hashlib.sha256(full).digest());(journal/'delivery-receipts.bin').write_bytes(partial+hashlib.sha256(partial).digest())
   cfg={**config(),'realtimeJournalDirectory':str(journal)};one=evidence.export_proofs(cfg,root/'before');(journal/'delivery-receipts.bin').write_bytes(b'');two=evidence.export_proofs(cfg,root/'compacted')
   self.assertEqual(one['ledger']['chainSha256'],two['ledger']['chainSha256']);records=list(evidence.records(root/'compacted/manifest.json'));self.assertEqual(str(event),next(r for r in records if r['category']=='sources')['value']['sourceEventId'])
   (journal/'receipts.bin').write_bytes(b'torn')
   with self.assertRaisesRegex(ValueError,'Torn'):evidence.export_proofs(cfg,root/'torn')
 def test_default_relations_are_from_actual_production_schema(self):
  names=set()
  for file in (REPO/'TomasAI.IFM.Application.Storage').glob('*/Schema/*Sql.cs'):
   for name in re.findall(r'CREATE TABLE IF NOT EXISTS\s+([\w.]+)',file.read_text(),re.I):names.add(name.lower() if '.' in name else 'public.'+name.lower())
  self.assertFalse(set(itertools_name for rows in evidence.DEFAULT_RELATIONS.values() for itertools_name in rows)-names)
 def test_live_targets_and_wrong_roles_are_rejected_before_docker(self):
  for cfg in [{'role':'Production','runId':'012345abcdef','postgres':{'container':'ifm_db'}},{**config(),'postgres':{'container':'ifm_db'}}]:
   with self.assertRaises(ValueError):evidence.verify_target(cfg,'postgres')

class ExactDigestTests(unittest.TestCase):
 def test_business_profile_excludes_only_transport_and_retains_production_relation_requirements(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);cfg=config();paths=[]
   for name,adapter,category in [('pg','PostgreSQL','cash'),('raw','ScyllaCqlRows','rawRows'),('emulator','FileEmulatorLedgerStore','positions')]:
    writer=evidence.Ledger(root/name,{'valueMode':'ExactCanonicalDigest'});writer.add(category,adapter,'fixture',[1],{'original':1});evidence.finalize(cfg,writer,evidence.base_manifest(cfg,adapter));paths.append(root/name/'manifest.json')
   business=evidence.bundle(paths,root/'business','BusinessOnly');full=evidence.bundle(paths,root/'full','Full')
   self.assertNotIn('sources',business['requiredOutcomeCategories']);self.assertEqual(11,len(business['requiredOutcomeCategories']));self.assertTrue(any('production owner relation' in x for x in business['coverageFailures']));self.assertTrue(any('sources' in x for x in full['coverageFailures']))
   self.assertFalse(evidence.compare(root/'business/manifest.json',root/'full/manifest.json',root/'profiles.json')['matched'])
 def test_original_configuration_binds_controller_source_owner_and_calendar_hashes(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);initial=root/'snapshot.json';source=root/'archive-copy.json';calendar=root/'calendar.json';fence=root/'held.json';state=root/'controller.json'
   for path,value in [(initial,{'state':'CapturedUnderHeldFence'}),(source,{'kind':'CommittedSourceArchivePrefixes','archiveOwnershipOnly':True}),(calendar,{'actual':'calendar'}),(fence,{'actual':'held'})]:evidence.write(path,value)
   evidence.write(state,{'archiveSessionId':'original-session','initialSnapshot':{'path':str(initial),'sha256':evidence.sha_file(initial)},'sourceArchive':{'path':str(source),'sha256':evidence.sha_file(source)}})
   cfg={**config(),'calendarEvidencePath':str(calendar),'calendarEvidenceSha256':evidence.sha_file(calendar)}
   with patch.object(evidence,'original_fence'):
    resolved=evidence.original_configuration(cfg,state,fence);self.assertEqual(evidence.sha_file(source),resolved['boundary']['sourceCaptureSha256']);self.assertEqual(evidence.sha_file(initial),resolved['boundary']['initialSnapshotSha256'])
    partition=root/'raw-partitions.json';scopes=[{'table':'tick_trade_data','assetType':1,'contractId':'ESZ6','valueDate':'2026-11-30'}];evidence.write(partition,{'kind':'OriginalRawPartitionManifest','sourceArchiveSha256':evidence.sha_file(source),'archiveSessionId':'original-session','partitions':scopes});state_value=evidence.load(state);state_value['rawPartitions']={'path':str(partition),'sha256':evidence.sha_file(partition)};state.write_bytes(evidence.canonical(state_value));cfg['scylla']={'partitionManifestFromState':True}
    resolved=evidence.original_configuration(cfg,state,fence);self.assertEqual(scopes,resolved['scylla']['partitions']);self.assertEqual(state_value['rawPartitions'],resolved['rawPartitionsEvidence'])
    source.write_bytes(b'changed')
    with self.assertRaisesRegex(ValueError,'artifact changed'):evidence.original_configuration(cfg,state,fence)
 def test_original_store_access_requires_dynamic_held_boundary_and_exact_container_identity(self):
  import http.server,threading,datetime
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);native=root/'native.cert';native.write_bytes(b'original-native-certificate');drain=root/'api-drain.json';drain.write_bytes(b'original-api-drain')
   proof={'version':1,'state':'Held','leaseId':'original-lease','boundaryId':'original-boundary','expiresUtc':(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=1)).isoformat(),'sourceContinuity':'RetainedBeforeForwardPause','forcedStop':False,'owners':[{'owner':name,'state':'Frozen','checkpointSha256':'1'*64,'evidence':{'fixture':'owner-held'}} for name in evidence.session_common.OWNERS],'participants':[{'kind':'postgres','container':'actual_original_pg','containerId':'actual-id'}],'nativeCheckpoint':{'path':str(native),'sha256':evidence.sha_file(native)},'apiLocalDrainPath':str(drain),'apiLocalDrainSha256':evidence.sha_file(drain)}
   proof['participants'][0]['database']='original_db'
   class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
     wire=json.dumps(proof).encode();self.send_response(200);self.send_header('Content-Length',str(len(wire)));self.end_headers();self.wfile.write(wire)
    def log_message(self,*args):pass
   server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
   try:
    proof['statusUrl']='http://127.0.0.1:'+str(server.server_port)+'/boundary';path=root/'held.json';evidence.write(path,proof);cfg={**config(),'role':'OriginalStoppedOwners','backend':'Original','originalFencePath':str(path),'originalFenceSha256':evidence.sha_file(path),'postgres':{'container':'actual_original_pg','database':'original_db'}}
    with patch.object(evidence.subprocess,'run',return_value=SimpleNamespace(stdout=b'actual-id')):self.assertEqual('original_db',evidence.verify_target(cfg,'postgres')['database'])
    with patch.object(evidence.subprocess,'run',return_value=SimpleNamespace(stdout=b'foreign-id')):
     with self.assertRaisesRegex(ValueError,'container differs'):evidence.verify_target(cfg,'postgres')
    proof['owners'][0]['checkpointSha256']='2'*64
    with self.assertRaisesRegex(ValueError,'boundary changed'):evidence.original_fence(cfg)
    proof['state']='Released'
    with self.assertRaisesRegex(ValueError,'not held'):evidence.original_fence(cfg)
   finally:server.shutdown();server.server_close();thread.join()
 def test_every_column_key_and_decimal_is_hashed_without_retaining_payload(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);cfg=config();cfg['limits']={**cfg['limits'],'valueMode':'ExactCanonicalDigest'}
   value={'opaque':'abcd'*20000,'cash':decimal.Decimal('12345678901234567890.12345678'),'timestamp':'original-time','id':'original-id'}
   for label,item in [('a',value),('b',value),('changed',{**value,'cash':decimal.Decimal('12345678901234567890.12345679')})]:
    writer=evidence.Ledger(root/label,cfg['limits']);writer.add('cash','owner','full.row',['original-key'],item);evidence.finalize(cfg,writer,evidence.base_manifest(cfg,'ActualFixture'))
   self.assertTrue(evidence.compare(root/'a/manifest.json',root/'b/manifest.json',root/'same.json')['matched'])
   self.assertFalse(evidence.compare(root/'a/manifest.json',root/'changed/manifest.json',root/'changed.json')['matched'])
   record=next(evidence.records(root/'a/manifest.json'));self.assertEqual(evidence.hashlib.sha256(evidence.canonical(value)).hexdigest(),record['value']['$exactCanonicalSha256']);self.assertLess((root/'a/outcomes.jsonl').stat().st_size,1000)
   path=root/'a/outcomes.jsonl';path.write_bytes(path.read_bytes().replace(b'original-key',b'changed--key'))
   with self.assertRaises(ValueError):list(evidence.records(root/'a/manifest.json'))
 def test_compact_bundle_does_not_rehash_hash_and_full_mode_cannot_silently_match(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);cfg=config();writer=evidence.Ledger(root/'a',{'valueMode':'ExactCanonicalDigest'});writer.add('cash','owner','row',[1],{'payload':'x'*10000});evidence.finalize(cfg,writer,evidence.base_manifest(cfg,'ActualFixture'));evidence.bundle([root/'a/manifest.json'],root/'bundle')
   self.assertEqual(list(evidence.records(root/'a/manifest.json')),list(evidence.records(root/'bundle/manifest.json')))
   writer=evidence.Ledger(root/'full',{});writer.add('cash','owner','row',[1],{'payload':'x'*10000});evidence.finalize(cfg,writer,evidence.base_manifest(cfg,'ActualFixture'));self.assertFalse(evidence.compare(root/'a/manifest.json',root/'full/manifest.json',root/'different-modes.json')['matched'])

@unittest.skipUnless(os.environ.get('IFM_OUTCOME_DISPOSABLE_TESTS')=='1','Explicit disposable PostgreSQL/Scylla evidence fixture required')
class ActualOwnerStoreTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.run_id=uuid.uuid4().hex[:12];cls.network='ifm_session_'+cls.run_id;cls.pg=cls.network+'_pg';cls.scylla=cls.network+'_scylla';cls.temp=tempfile.TemporaryDirectory();cls.root=Path(cls.temp.name)
  cls.boundary={**config(),'runId':cls.run_id};cls.started=[]
  try:
   cls.cmd(['docker','network','create','--internal','--label','ifm.session-qualification='+cls.run_id,cls.network])
   cls.cmd(['docker','run','-d','--pull=never','--name',cls.pg,'--network',cls.network,'--label','ifm.session-qualification='+cls.run_id,'-e','POSTGRES_HOST_AUTH_METHOD=trust','postgres:17.2']);cls.started.append(cls.pg)
   for i in range(60):
    if subprocess.run(['docker','exec',cls.pg,'pg_isready','-h','127.0.0.1','-U','postgres'],capture_output=True).returncode==0:break
    time.sleep(.5)
   else:raise RuntimeError('Disposable PostgreSQL not ready')
   for db in ['ifm_qual_memory','ifm_qual_file']:cls.psql('CREATE DATABASE '+db+';',database='postgres')
  except Exception:cls.tearDownClass();raise
 @classmethod
 def cmd(cls,args,body=None):return subprocess.run(args,input=body,capture_output=True,check=True,timeout=120).stdout
 @classmethod
 def psql(cls,sql,database='ifm_qual_memory'):return cls.cmd(['docker','exec','-i',cls.pg,'psql','-X','-q','-v','ON_ERROR_STOP=1','-U','postgres','-d',database],sql.encode())
 @classmethod
 def tearDownClass(cls):
  for name in getattr(cls,'started',[]):subprocess.run(['docker','rm','-f','-v',name],capture_output=True)
  if hasattr(cls,'network'):subprocess.run(['docker','network','rm',cls.network],capture_output=True)
  if hasattr(cls,'temp'):cls.temp.cleanup()
 def test_actual_postgres_full_columns_decimal_blob_identity_and_schema_or_data_mismatch(self):
  source=(REPO/'TomasAI.IFM.Application.Storage/TradeDb/Schema/TradeSchemaSql.cs').read_text();ddl=re.search(r'CREATE TABLE IF NOT EXISTS trade.trade_order \(.*?\);',source,re.S).group()
  schema="CREATE SCHEMA trade;"+ddl+"INSERT INTO trade.trade_order VALUES(101,701,9001,1,'Filled','2026-11-25','2026-11-25T20:59:59.123456Z','original',decode('010203ff','hex'));"
  for db in ['ifm_qual_memory','ifm_qual_file']:
   self.psql(schema,database=db)
   self.psql("CREATE SCHEMA portfolio_financial;CREATE TABLE portfolio_financial.owner_cash (book integer, fund integer, currency text, balance numeric(40,8),payload bytea,UNIQUE NULLS NOT DISTINCT(book,fund,currency));INSERT INTO portfolio_financial.owner_cash VALUES(1,NULL,'USD',12345678901234567890123456.12345678,decode('deadbeef','hex'));",database=db)
  paths=[]
  for mode in ['memory','file']:
   cfg={**self.boundary,'backend':mode.title(),'postgres':{'container':self.pg,'database':'ifm_qual_'+mode,'user':'postgres'},'relations':{'orders':['trade.trade_order'],'cash':['portfolio_financial.owner_cash']}}
   output=self.root/mode;evidence.export_postgres(cfg,output);paths.append(output/'manifest.json')
  self.assertTrue(evidence.compare(*paths,self.root/'pg-match.json')['matched'])
  digest_paths=[]
  for mode in ['memory','file']:
   digest_cfg={**self.boundary,'backend':mode.title(),'postgres':{'container':self.pg,'database':'ifm_qual_'+mode,'user':'postgres'},'relations':{'orders':['trade.trade_order'],'cash':['portfolio_financial.owner_cash']},'limits':{'valueMode':'ExactCanonicalDigest','chunkBytes':1024,'maximumInputBytes':32*1024**2}}
   evidence.export_postgres(digest_cfg,self.root/('digest-'+mode));digest_paths.append(self.root/('digest-'+mode)/'manifest.json')
  self.assertTrue(evidence.compare(*digest_paths,self.root/'pg-digest-match.json')['matched'])
  rows=list(evidence.records(paths[0]));self.assertTrue(any(r['value'].get('payload')=='\\xdeadbeef' for r in rows if isinstance(r['value'],dict)))
  self.psql("UPDATE portfolio_financial.owner_cash SET balance=balance+0.00000001;",database='ifm_qual_file')
  cfg['postgres']['database']='ifm_qual_file';evidence.export_postgres(cfg,self.root/'changed')
  result=evidence.compare(paths[0],self.root/'changed/manifest.json',self.root/'pg-diff.json');self.assertFalse(result['matched']);self.assertTrue(result['differences'])
  evidence.export_postgres({**cfg,'limits':{'valueMode':'ExactCanonicalDigest'}},self.root/'digest-changed');self.assertFalse(evidence.compare(digest_paths[0],self.root/'digest-changed/manifest.json',self.root/'pg-digest-diff.json')['matched'])
  cfg['relations']={'cash':['portfolio_financial.missing']}
  with self.assertRaises(ValueError):evidence.export_postgres(cfg,self.root/'missing-relation')
 def test_actual_scylla_partition_paging_and_quote_or_trade_values(self):
  self.cmd(['docker','run','-d','--pull=never','--name',self.scylla,'--network',self.network,'--label','ifm.session-qualification='+self.run_id,'--memory','2g','scylladb/scylla:6.2.2','--smp','1','--memory','768M','--reserve-memory','0M','--overprovisioned','1','--developer-mode','1']);self.started.append(self.scylla)
  for i in range(90):
   if subprocess.run(['docker','exec',self.scylla,'cqlsh','-e','SELECT release_version FROM system.local;'],capture_output=True).returncode==0:break
   time.sleep(1)
  else:raise RuntimeError('Disposable Scylla not ready')
  text=(REPO/'TomasAI.IFM.Application.Storage/TradeTickQuoteDb/Schema/TradeTickQuoteSchemaSql.cs').read_text();ddl=re.search(r'CREATE TABLE IF NOT EXISTS futures_tick_data \(.*?CLUSTERING ORDER BY.*?;',text,re.S).group()
  quote_type=re.search(r'CREATE TYPE IF NOT EXISTS tick_quote_item \(.*?\);',text,re.S).group()
  current_trade=re.search(r'CREATE TABLE IF NOT EXISTS tick_trade_data \(.*?CLUSTERING ORDER BY.*?;',text,re.S).group()
  current_quote=re.search(r'CREATE TABLE IF NOT EXISTS tick_quote_data \(.*?CLUSTERING ORDER BY.*?;',text,re.S).group()
  sql="CREATE KEYSPACE ifm_qual WITH replication={'class':'SimpleStrategy','replication_factor':1};USE ifm_qual;"+ddl+quote_type+current_trade+current_quote
  for i in range(40):sql+=f"INSERT INTO futures_tick_data(contractid,valuedate,tickid,ticktime,price,size)VALUES('ESZ6','2026-11-25',{i},'10:00:00.000000001',5000.1234567890123456789,{i+1});"
  sql+="INSERT INTO tick_trade_data(asset_type_id,contract_id,value_date,aggregation_time,sequence_id,source_sequence,price,size)VALUES(1,'ESZ6','2026-11-25','10:00:00.000000001',7,1007,5000.1234567890123456789,2);"
  sql+="INSERT INTO tick_quote_data(asset_type_id,contract_id,value_date,aggregation_time,sequence_id,quote_count,quote_data)VALUES(2,'ESZ6C5000','2026-11-25','10:00:00.000000002',8,1,[{source_sequence:1008,bid_price:5000.125,ask_price:5000.375,bid_size:10,ask_size:11}]);"
  self.cmd(['docker','exec','-i',self.scylla,'cqlsh'],sql.encode())
  cfg={**self.boundary,'scylla':{'container':self.scylla,'keyspace':'ifm_qual','partitions':[{'table':'futures_tick_data','contractId':'ESZ6','valueDate':'2026-11-25'},{'table':'tick_trade_data','assetType':1,'contractId':'ESZ6','valueDate':'2026-11-25'},{'table':'tick_quote_data','assetType':2,'contractId':'ESZ6C5000','valueDate':'2026-11-25'}]}}
  one=evidence.export_scylla(cfg,self.root/'raw-memory');two=evidence.export_scylla(cfg,self.root/'raw-file')
  self.assertEqual(45,one['ledger']['records']);self.assertEqual(one['ledger']['chainSha256'],two['ledger']['chainSha256'])
  digest_cfg={**cfg,'limits':{'valueMode':'ExactCanonicalDigest'}};evidence.export_scylla(digest_cfg,self.root/'raw-digest-before')
  self.cmd(['docker','exec',self.scylla,'cqlsh','-e',"UPDATE ifm_qual.tick_quote_data SET quote_data=[{source_sequence:1008,bid_price:5001.125,ask_price:5001.375,bid_size:10,ask_size:11}] WHERE asset_type_id=2 AND contract_id='ESZ6C5000' AND value_date='2026-11-25' AND aggregation_time='10:00:00.000000002' AND sequence_id=8;"])
  evidence.export_scylla(cfg,self.root/'raw-change');self.assertFalse(evidence.compare(self.root/'raw-memory/manifest.json',self.root/'raw-change/manifest.json',self.root/'raw-diff.json')['matched'])
  evidence.export_scylla(digest_cfg,self.root/'raw-digest-after');self.assertFalse(evidence.compare(self.root/'raw-digest-before/manifest.json',self.root/'raw-digest-after/manifest.json',self.root/'raw-digest-diff.json')['matched'])
 def test_actual_postgres_compact_storage_measurement_hashes_every_original_column(self):
  self.psql("CREATE SCHEMA IF NOT EXISTS evidence_scale;CREATE TABLE evidence_scale.owner_payload(id bigint PRIMARY KEY, original_clock text, amount numeric(40,8), payload bytea);INSERT INTO evidence_scale.owner_payload SELECT i,'2026-11-26T12:00:00.1234567Z',12345678901234567890.12345678,decode(repeat('abcdef01',8192),'hex') FROM generate_series(1,256) i;")
  results={};paths={}
  for mode in ('FullCanonical','ExactCanonicalDigest'):
   cfg={**self.boundary,'postgres':{'container':self.pg,'database':'ifm_qual_memory'},'relations':{'workflows':['evidence_scale.owner_payload']},'limits':{'valueMode':mode,'chunkBytes':65536,'maximumBytes':256*1024**2,'maximumInputBytes':256*1024**2,'maximumSeconds':120}}
   start=time.perf_counter();paths[mode]=self.root/('scale-'+mode);result=evidence.export_postgres(cfg,paths[mode]);results[mode]={'seconds':round(time.perf_counter()-start,4),'ledgerBytes':(paths[mode]/'outcomes.jsonl').stat().st_size,'peakEvidenceBytes':result['ledger']['peakEvidenceBytes'],'records':result['ledger']['records']}
  for full,compact in zip(evidence.records(paths['FullCanonical']/'manifest.json'),evidence.records(paths['ExactCanonicalDigest']/'manifest.json')):
   self.assertEqual(full['key'],compact['key'])
   if full['key']!=['$schema']:self.assertEqual(hashlib.sha256(evidence.canonical(full['value'])).hexdigest(),compact['value']['$exactCanonicalSha256'])
  self.assertEqual(257,results['FullCanonical']['records']);self.assertEqual(257,results['ExactCanonicalDigest']['records']);self.assertLess(results['ExactCanonicalDigest']['peakEvidenceBytes']*20,results['FullCanonical']['peakEvidenceBytes'])
  print('ACTUAL_POSTGRES_STORAGE_MEASUREMENT '+json.dumps(results),flush=True)

if __name__=='__main__':unittest.main()
