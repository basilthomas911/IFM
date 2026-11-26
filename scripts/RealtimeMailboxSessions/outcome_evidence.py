"""Read-only, bounded actual-owner evidence export for isolated qualification stores.

No export certifies a stopped owner, source coverage, financial correctness or a complete session.
Opaque bytea and original IDs/timestamps are retained; differences are never normalized away.
"""
from __future__ import annotations
import argparse, contextlib, datetime as dt, decimal, hashlib, heapq, itertools, json, os
from pathlib import Path
import re, shutil, sqlite3, struct, subprocess, tempfile, threading, time, uuid
import session_tool as session_common

CATEGORIES = ('sources','destinations','rawRows','indicators','workflows','orders','fills','cash','holds','positions','plans','eod','sourceCursors')
DEFAULT_RELATIONS = {
 'indicators': ['market_data.futures_trade_session_bar','market_data.futures_ema_signal','market_data.futures_rsi_signal','market_data.futures_adx_signal','market_data.futures_atr_signal','market_data.futures_macd_signal','market_data.futures_tdi_signal','market_data.futures_vwap_signal','market_data.futures_vx_term_structure_signal','market_data.futures_iti_signal'],
 'workflows': ['trade.intrinsic_time_strategy_workflow','trade.intrinsic_time_strategy_signal_queue','trade.intrinsic_time_strategy_workflow_timeline','trade.intrinsic_time_strategy_workflow_start_attempt'],
 'orders': ['trade.trade_order','trade.order_execution','portfolio.fund_order','portfolio.fund_composition','portfolio.accepted_trade_order','portfolio.accepted_trade_order_leg'],
 'fills': ['trade.order_execution_fill','trade.trade_fill','trade.trade_fill_data'],
 'cash': ['trade.broker_account_read_model','portfolio_financial.ledger_transaction','portfolio_financial.ledger_journal','portfolio_financial.ledger_entry','portfolio_financial.ledger_account_balance','portfolio_financial.financial_source_receipt'],
 'holds': ['portfolio_financial.financial_encumbrance','portfolio_financial.capacity_reservation','portfolio_financial.capacity_usage','portfolio_financial.accepted_execution_capacity'],
 'positions': ['trade.strategy_position_current','trade.strategy_position_history','trade.open_position_route','trade.trade_position','trade.trade_position_state'],
 'plans': ['trade_plan.futures_trade_plan','trade_plan.vertical_spread_trade_plan','trade_plan.iron_condor_trade_plan','trade_plan.position_exit_workflow','trade_plan.iron_condor_exit_intent'],
 'eod': ['trade.position_eod_recovery','market_data.futures_eod_observation','market_data.position_eod_quote_evidence','market_data.position_eod_quote_original'],
 'sourceCursors': ['public.event_log','public.event_stream_id','public.event_aggregate_checkpoint','public.event_projector_state','public.event_projector_stream_checkpoint','portfolio_financial.financial_projection_checkpoint']}


def utc(): return dt.datetime.now(dt.timezone.utc).isoformat()
def load(path):
    with Path(path).open(encoding='utf-8-sig') as stream: return json.load(stream,parse_float=decimal.Decimal)
def canonical(value):
    """Exact decimals, stable object order, original array order; no float/identity/time erasure."""
    if value is None: return b'null'
    if value is True: return b'true'
    if value is False: return b'false'
    if isinstance(value,int): return str(value).encode()
    if isinstance(value,decimal.Decimal):
        if not value.is_finite() or abs(value.as_tuple().exponent)>1000000: raise ValueError('Non-finite or oversized decimal evidence')
        text=format(value,'f')
        if '.' in text:text=text.rstrip('0').rstrip('.')
        return ('0' if decimal.Decimal(text)==0 else text).encode()
    if isinstance(value,float): raise ValueError('Binary float must be decoded as exact decimal')
    if isinstance(value,str): return json.dumps(value,ensure_ascii=False,separators=(',',':')).encode('utf-8')
    if isinstance(value,list) or isinstance(value,tuple): return b'['+b','.join(map(canonical,value))+b']'
    if isinstance(value,dict):
        if any(not isinstance(key,str) for key in value): raise ValueError('Evidence object keys must be strings')
        return b'{'+b','.join(canonical(k)+b':'+canonical(value[k]) for k in sorted(value))+b'}'
    raise ValueError('Unsupported evidence value type')
def sha_file(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(262144),b''):h.update(block)
    return h.hexdigest()
def write(path,value):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    with path.open('xb') as stream:stream.write(canonical(value)+b'\n');stream.flush();os.fsync(stream.fileno())
def identifier(value):
    if not isinstance(value,str) or not re.fullmatch(r'[a-z_][a-z0-9_]*',value):raise ValueError('Unsafe database identifier')
    return value

def original_fence(config):
    if config.get('role')!='OriginalStoppedOwners' or config.get('backend')!='Original':raise ValueError('Original export requires an explicitly stopped original owner boundary')
    path=Path(config['originalFencePath'])
    if sha_file(path)!=config['originalFenceSha256']:raise ValueError('Original stopped-owner proof changed')
    expected=load(path);current=session_common.fence({'boundary':{'statusUrl':expected['statusUrl']}})
    for key in ('boundaryId','leaseId','owners','participants','nativeCheckpoint','apiLocalDrainPath','apiLocalDrainSha256'):
        if not expected.get(key) or current.get(key)!=expected[key]:raise ValueError('Original stopped-owner boundary changed: '+key)
    native=expected['nativeCheckpoint']
    if sha_file(native['path'])!=native['sha256']:raise ValueError('Original native checkpoint changed')
    if sha_file(expected['apiLocalDrainPath'])!=expected['apiLocalDrainSha256']:raise ValueError('Original API drain evidence changed')
    return expected

def guard_original(config):
    if config.get('role')=='OriginalStoppedOwners':original_fence(config)

def verify_target(config,kind):
    run=config.get('runId','')
    if config.get('role')=='OriginalStoppedOwners':
        if not re.fullmatch('[a-f0-9]{12}',run):raise ValueError('Original export requires an isolated evidence run ID')
        proof=original_fence(config);target=config[kind];name=target['container']
        if not re.fullmatch('[A-Za-z0-9][A-Za-z0-9_.-]{0,127}',name):raise ValueError('Unsafe actual owner container')
        participant=next((item for item in proof['participants'] if item.get('kind')==kind and item.get('container')==name),None)
        actual=subprocess.run(['docker','inspect','--format','{{.Id}}',name],capture_output=True,check=True,timeout=10).stdout.decode().strip()
        if not participant or participant.get('containerId')!=actual:raise ValueError('Actual original store container differs from held owner proof')
        provider_key='database' if kind=='postgres' else 'keyspace'
        if participant.get(provider_key)!=target.get(provider_key):raise ValueError('Actual original database/keyspace differs from held owner proof')
        return target
    if config.get('role')!='DisposableQualification' or not re.fullmatch('[a-f0-9]{12}',run):raise ValueError('A disposable qualification run identity is required')
    target=config[kind];name=target['container']
    if not re.fullmatch('ifm_session_'+run+r'_[a-z0-9_]+',name):raise ValueError('Container is outside the isolated qualification run')
    result=subprocess.run(['docker','inspect','--format','{{json .Config.Labels}}',name],capture_output=True,check=True,timeout=10)
    if json.loads(result.stdout).get('ifm.session-qualification')!=run:raise ValueError('Container qualification label does not match')
    return target

def process_lines(command,body,limits,guard=None):
    """Bounded streamed subprocess output; never composes shell commands or prints connection secrets."""
    if guard:guard()
    error=tempfile.TemporaryFile();proc=subprocess.Popen(command,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=error)
    timed_out=threading.Event()
    def expire():timed_out.set();proc.kill()
    timer=threading.Timer(limits.get('maximumSeconds',600),expire);timer.start()
    feeder=threading.Thread(target=lambda:(proc.stdin.write(body),proc.stdin.close()),daemon=True);feeder.start()
    total=0;next_guard=0
    try:
        while True:
            line=proc.stdout.readline(limits.get('maximumRecordBytes',16*1024*1024)+1)
            if not line:break
            if guard and time.monotonic()>=next_guard:guard();next_guard=time.monotonic()+5
            total+=len(line)
            if len(line)>limits.get('maximumRecordBytes',16*1024*1024) or total>limits.get('maximumInputBytes',limits.get('maximumBytes',64*1024**3)):raise ValueError('Owner evidence process exceeded its record/input byte limit')
            if line.strip():yield line
        code=proc.wait(timeout=10);feeder.join(timeout=2)
        if timed_out.is_set() or code!=0:
            error.seek(0);detail=error.read(2048).decode('utf-8',errors='replace')
            detail=re.sub(r'(?i)(password|token|api[_-]?key)\s*[=:]\s*[^\s,;]+',r'\1=<redacted>',detail)
            raise ValueError('Owner evidence query failed or timed out; no complete manifest may be issued: '+detail)
        if guard:guard()
    finally:
        timer.cancel()
        if proc.poll() is None:proc.kill();proc.wait(timeout=10)
        proc.stdout.close();error.close()

def pg_command(target):return ['docker','exec','-i',target['container'],'psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1','-U',identifier(target.get('user','postgres')),'-d',identifier(target['database'])]
def pg_catalog(config):
    target=verify_target(config,'postgres')
    sql="""BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;SET LOCAL statement_timeout='30s';
SELECT jsonb_build_object('schema',n.nspname,'table',c.relname,'columns',
 (SELECT jsonb_agg(jsonb_build_object('name',a.attname,'type',format_type(a.atttypid,a.atttypmod),'nullable',NOT a.attnotnull) ORDER BY a.attnum) FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped),
 'identity',(SELECT jsonb_build_object('kind',CASE WHEN i.indisprimary THEN 'PrimaryKey' ELSE 'UniqueNotDistinctOrNonNull' END,'columns',(SELECT jsonb_agg(a.attname ORDER BY k.ordinality) FROM unnest(i.indkey) WITH ORDINALITY k(num,ordinality) JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.num)) FROM pg_index i JOIN pg_class ix ON ix.oid=i.indexrelid WHERE i.indrelid=c.oid AND i.indisunique AND i.indpred IS NULL AND i.indexprs IS NULL AND (i.indnullsnotdistinct OR NOT EXISTS(SELECT 1 FROM unnest(i.indkey) k(num) JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.num WHERE NOT a.attnotnull)) ORDER BY i.indisprimary DESC,ix.relname LIMIT 1))::text
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' ORDER BY n.nspname,c.relname;ROLLBACK;"""
    return {row['schema']+'.'+row['table']:row for row in (json.loads(line) for line in process_lines(pg_command(target),sql.encode(),config.get('limits',{})))}

class Ledger:
    """Disk-bounded external ordering and chain output; memory is limited to a chunk plus one record per merge input."""
    def __init__(self,directory,limits,*,streamed_bundle=False):
        self.root=Path(directory);self.root.mkdir(parents=True,exist_ok=False);self.limits=limits;self.buffer=[];self.buffer_bytes=0;self.spills=[];self.rows=0;self.bytes=0;self.disk_bytes=0;self.allow_redundant_facts=False
        self.value_mode=limits.get('valueMode','FullCanonical');self.input_bytes=0;self.next_disk_probe=0;self.peak_disk_bytes=0;self.sorted_input=None
        self.limits=dict(limits)
        self.maximum_ledger_bytes=None
        if self.value_mode=='ExactCanonicalDigest':
            self.limits.setdefault('chunkBytes',64*1024**2)
            self.limits['maximumBytes']=min(self.limits.get('maximumBytes',64*1024**3),(32 if streamed_bundle else 16)*1024**3)
            self.maximum_ledger_bytes=(32 if streamed_bundle else 8)*1024**3
        if self.value_mode not in ('FullCanonical','ExactCanonicalDigest'):raise ValueError('Unsupported exact evidence value mode')
    def add(self,category,owner,relation,key,value):
        if self.value_mode=='ExactCanonicalDigest' and key!=['$schema']:
            raw=canonical(value);self.input_bytes+=len(raw)
            if len(raw)>self.limits.get('maximumRecordBytes',16*1024**2):raise ValueError('Owner outcome record is too large')
            if self.input_bytes>self.limits.get('maximumInputBytes',512*1024**3):raise ValueError('Exact canonical input budget exhausted')
            value={'$exactCanonicalSha256':hashlib.sha256(raw).hexdigest(),'$exactCanonicalBytes':len(raw)}
        self.add_preserved(category,owner,relation,key,value)
    def add_preserved(self,category,owner,relation,key,value):
        if category not in CATEGORIES:raise ValueError('Unknown outcome category')
        record={'category':category,'owner':owner,'relation':relation,'key':key,'value':value};data=canonical(record)
        if len(data)>self.limits.get('maximumRecordBytes',16*1024**2):raise ValueError('Owner outcome record is too large')
        sort=canonical([category,owner,relation,key]);self.buffer.append((sort,data));self.buffer_bytes+=len(sort)+len(data);self.rows+=1;self.bytes+=len(sort)+len(data)
        if self.rows>self.limits.get('maximumRecords',100000000) or self.bytes>self.limits.get('maximumBytes',64*1024**3):raise ValueError('Outcome ledger capacity exhausted')
        if self.buffer_bytes>=self.limits.get('chunkBytes',4*1024**2):self.spill()
    def emit(self,stream,data):
        if self.disk_bytes+len(data)>self.limits.get('maximumBytes',64*1024**3):raise ValueError('Evidence disk reservation exhausted')
        if self.limits.get('minimumFreeBytes',0) and self.disk_bytes>=self.next_disk_probe:
            if shutil.disk_usage(self.root).free<max(len(data),16*1024**2)+self.limits['minimumFreeBytes']:raise ValueError('Evidence volume pressure exceeded reserved headroom')
            self.next_disk_probe=self.disk_bytes+16*1024**2
        stream.write(data);self.disk_bytes+=len(data);self.peak_disk_bytes=max(self.peak_disk_bytes,self.disk_bytes)
    def spill(self):
        if not self.buffer:return
        path=self.root/('sort-'+str(len(self.spills))+'.tmp')
        with path.open('xb') as stream:
            for key,value in sorted(self.buffer):self.emit(stream,key.hex().encode()+b'\t'+value+b'\n')
        self.spills.append(path);self.buffer=[];self.buffer_bytes=0
        if len(self.spills)>512:raise ValueError('External merge fan-in exceeds512; choose a larger bounded chunk size')
    def finish(self):
        if self.sorted_input is None:self.spill()
        previous=bytes(32);count=0;category_counts={};last_key=None;last_data=None
        with contextlib.ExitStack() as stack:
            streams=[stack.enter_context(path.open('rb')) for path in self.spills]
            def rows(stream):
                for line in stream:
                    key,data=line.rstrip(b'\n').split(b'\t',1);yield bytes.fromhex(key.decode()),data
            with (self.root/'outcomes.jsonl').open('xb') as output:
                for key,data in (self.sorted_input if self.sorted_input is not None else heapq.merge(*(rows(stream) for stream in streams))):
                    if key==last_key:
                        if self.allow_redundant_facts and data==last_data:continue
                        raise ValueError('Duplicate owner relation/key in evidence')
                    if last_key is not None and key<last_key:raise ValueError('Streamed owner bundle is not ordered')
                    last_key=key;last_data=data;record=json.loads(data,parse_float=decimal.Decimal);count+=1;previous=hashlib.sha256(previous+data).digest()
                    if count>self.limits.get('maximumRecords',100000000):raise ValueError('Outcome record count exhausted')
                    line=canonical({'ordinal':count,'sha256':previous.hex(),'record':record})+b'\n'
                    if self.maximum_ledger_bytes is not None and output.tell()+len(line)>self.maximum_ledger_bytes:raise ValueError('Compact owner ledger bound exceeded; no partial export may qualify')
                    self.emit(output,line);category_counts[record['category']]=category_counts.get(record['category'],0)+1
                output.flush();os.fsync(output.fileno())
        for path in self.spills:self.disk_bytes-=path.stat().st_size;path.unlink()
        return {'path':'outcomes.jsonl','records':count,'sha256':sha_file(self.root/'outcomes.jsonl'),'chainSha256':previous.hex(),'categories':category_counts,'valueMode':self.value_mode,'canonicalInputBytes':self.input_bytes,'peakEvidenceBytes':self.peak_disk_bytes}

def base_manifest(config,kind):
    original=config.get('role')=='OriginalStoppedOwners'
    if original:guard_original(config)
    if (config.get('role') not in ('DisposableQualification','OriginalStoppedOwners') or original and config.get('backend')!='Original') or not re.fullmatch('[a-f0-9]{12}',config.get('runId','')) or config.get('backend') not in ('Original','Memory','File'):raise ValueError('Actual owner export requires a supported stopped or disposable qualification scope')
    return {'version':1,'kind':'ActualOwnerOutcomeLedgers','adapter':kind,'role':config.get('role'),'backend':config.get('backend'),
            'runId':config.get('runId'),'boundary':config.get('boundary',{}),'state':'Exporting','qualified':False,'startedUtc':utc(),
            'coverageFailures':[],'relations':[],'limitations':['Read-only actual-data export does not prove a stopped owner or complete session.','Original IDs, clocks and opaque bytea content are retained; divergences are not normalized away.']}
def finalize(config,ledger,manifest,finished=None):
    guard_original(config)
    manifest['ledger']=ledger.finish() if finished is None else finished;manifest['coveredCategories']=sorted(manifest['ledger']['categories']);manifest['missingOutcomeCategories']=sorted(set(manifest.get('requiredOutcomeCategories',CATEGORIES))-set(manifest['coveredCategories']));manifest['state']='Complete';manifest['endedUtc']=utc()
    if config.get('role')=='OriginalStoppedOwners':manifest['originalFenceSha256']=config['originalFenceSha256'];manifest['originalFencePath']=str(Path(config['originalFencePath']).resolve())
    write(ledger.root/'manifest.json',manifest);return manifest

def export_postgres(config,output):
    target=verify_target(config,'postgres');catalog=pg_catalog(config);manifest=base_manifest(config,'PostgreSQL');ledger=Ledger(output,config.get('limits',{}));queries=[]
    relations=config.get('relations',DEFAULT_RELATIONS)
    query_seconds=config.get('limits',{}).get('maximumQuerySeconds',120)
    if not isinstance(query_seconds,int) or not 1<=query_seconds<=86400:raise ValueError('Finite query deadline required')
    queries.append("BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;SET LOCAL statement_timeout='"+str(query_seconds)+"s';SET LOCAL TIME ZONE 'UTC';SET LOCAL bytea_output='hex';")
    queries.append("SELECT jsonb_build_object('kind','snapshot','snapshot',txid_current_snapshot()::text,'database',current_database())::text;")
    for category, names in relations.items():
        for name in sorted(names):
            schema,table=name.split('.');identifier(schema);identifier(table)
            meta=catalog.get(name)
            if not meta or not meta['identity']:raise ValueError('Required relation or authoritative unique identity unavailable: '+name)
            manifest['relations'].append({'category':category,'relation':name,'schemaSha256':hashlib.sha256(canonical(meta)).hexdigest()})
            ledger.add(category,'PostgreSQL',name,['$schema'],meta)
            pk=','.join('t."'+identifier(key)+'"' for key in meta['identity']['columns'])
            queries.append("SELECT jsonb_build_object('kind','row','category','"+category+"','relation','"+name+"','key',jsonb_build_array("+pk+"),'value',to_jsonb(t))::text FROM \""+schema+'\".\"'+table+'\" t ORDER BY '+pk+';')
    queries.append('ROLLBACK;')
    for line in process_lines(pg_command(target),'\n'.join(queries).encode(),config.get('limits',{}),guard=lambda:guard_original(config)):
        row=json.loads(line,parse_float=decimal.Decimal)
        if row['kind']=='snapshot':manifest['postgresSnapshot']=row
        else:ledger.add(row['category'],'PostgreSQL',row['relation'],['$row',row['key']],row['value'])
    guard_original(config);return finalize(config,ledger,manifest)

def export_scylla(config,output):
    target=verify_target(config,'scylla');run=config['runId'];identifier(target['keyspace'])
    original=config.get('role')=='OriginalStoppedOwners';network=target.get('network') if original else 'ifm_session_'+run
    if not isinstance(network,str) or not re.fullmatch('[A-Za-z0-9][A-Za-z0-9_.-]{0,127}',network):raise ValueError('Actual Scylla network is required')
    labels=subprocess.run(['docker','network','inspect','--format','{{json .Labels}}',network],capture_output=True,check=True,timeout=10)
    if original:
        info=json.loads(subprocess.run(['docker','inspect',target['container']],capture_output=True,check=True,timeout=10).stdout)[0]
        if network not in info['NetworkSettings']['Networks']:raise ValueError('Original Scylla is not attached to the pinned helper network')
    elif json.loads(labels.stdout).get('ifm.session-qualification')!=run:raise ValueError('Scylla helper network is not isolated qualification')
    allowed={'tick_trade_data','tick_quote_data','futures_tick_data','futures_tick_data_by_time','futures_option_tick_data'}
    scopes=target.get('partitions',[]);unique=set()
    if not scopes or len(scopes)>100000:raise ValueError('Explicit bounded raw partitions are required; no full-ring scan is allowed')
    for scope in scopes:
        if scope['table'] not in allowed or not isinstance(scope['contractId'],str) or not 0<len(scope['contractId'])<=256:raise ValueError('Unsupported raw owner table or contract')
        dt.date.fromisoformat(scope['valueDate']);key=(scope['table'],scope.get('assetType'),scope['contractId'],scope['valueDate'])
        if key in unique:raise ValueError('Repeated raw partition would duplicate evidence')
        unique.add(key)
    image=config.get('cqlToolboxImage','cassandra:5.0')
    if not re.fullmatch(r'[a-z0-9./:_-]+',image):raise ValueError('Unsafe toolbox image')
    image_id=subprocess.run(['docker','image','inspect','--format','{{.Id}}',image],capture_output=True,check=True,timeout=10).stdout.decode().strip()
    worker=Path(__file__).with_name('cql_outcome_worker.py').read_text()
    command=['docker','run','--rm','-i','--pull=never','--network',network,'--label','ifm.session-qualification='+run,
        '--name',network+'_cql_evidence_'+uuid.uuid4().hex[:8],'--memory','512m','--cpus','1','--read-only',
        '--tmpfs','/tmp:rw,noexec,nosuid,size=16m','-e','PYTHONDONTWRITEBYTECODE=1','--entrypoint','python3',image,'-u','-c',worker]
    request={**target,'maximumRecords':config.get('limits',{}).get('maximumRecords',100000000)}
    ledger=Ledger(output,config.get('limits',{}));manifest=base_manifest(config,'ScyllaCqlRows');manifest['toolboxImageId']=image_id;completed=[]
    try:
        for line in process_lines(command,json.dumps(request).encode(),config.get('limits',{}),guard=lambda:guard_original(config)):
            row=json.loads(line,parse_float=decimal.Decimal)
            if row['kind']=='schema':
                manifest['relations'].append({'category':'rawRows','relation':row['relation'],'schemaSha256':hashlib.sha256(canonical(row)).hexdigest()})
                ledger.add('rawRows','Scylla',row['relation'],['$schema'],row)
            elif row['kind']=='partition-complete':completed.append(row['partition'])
            elif row['kind']=='row':ledger.add('rawRows','Scylla',row['relation'],['$row',row['key']],row['value'])
            else:raise ValueError('Unknown raw evidence worker frame')
    finally:
        # Killing an attached Docker CLI does not prove its worker exited. Remove only this exact generated helper.
        subprocess.run(['docker','rm','-f','-v',command[command.index('--name')+1]],capture_output=True,timeout=15)
    if len(completed)!=len(scopes) or {canonical(scope) for scope in completed}!={canonical(scope) for scope in scopes}:raise ValueError('Raw partition export did not complete exactly')
    manifest['partitions']=completed;manifest['limitations'].append('Explicit raw partitions are exported; the authentic source partition manifest must independently prove these are all required partitions.')
    return finalize(config,ledger,manifest)

class JsonItems:
    def __init__(self,stream,limit):self.stream=stream;self.limit=limit;self.buffer='';self.at=0;self.eof=False;self.decoder=json.JSONDecoder(parse_float=decimal.Decimal)
    def more(self):
        chunk=self.stream.read(65536);self.buffer=self.buffer[self.at:]+chunk;self.at=0
        if not chunk:self.eof=True
        if len(self.buffer)>self.limit:raise ValueError('Checkpoint value exceeds bounded record memory')
        if not self.buffer:self.eof=True
    def char(self):
        while True:
            if self.at==len(self.buffer):self.more()
            if self.at==len(self.buffer):raise ValueError('Checkpoint JSON truncated')
            if self.buffer[self.at].isspace():self.at+=1;continue
            return self.buffer[self.at]
    def expect(self,char):
        if self.char()!=char:raise ValueError('Unexpected checkpoint JSON structure')
        self.at+=1
    def value(self):
        self.char()
        while True:
            try:
                value,end=self.decoder.raw_decode(self.buffer,self.at)
                if end==len(self.buffer) and not self.eof:self.more();continue
                if end<len(self.buffer) and self.buffer[end] not in ':,]} \r\n\t':
                    if not self.eof:self.more();continue
                    raise ValueError('Malformed checkpoint JSON value')
                self.at=end;return value
            except json.JSONDecodeError:
                old=len(self.buffer)-self.at;self.more()
                if len(self.buffer)==old:raise ValueError('Checkpoint JSON is truncated')
    def fields(self):
        self.expect('{')
        while self.char()!='}':
            key=self.value();self.expect(':')
            if self.char()=='[':
                self.at+=1;index=0
                if self.char()==']':yield key,-1,[]
                while self.char()!=']':
                    yield key,index,self.value();index+=1
                    if self.char()==',':self.at+=1
                    elif self.char()!=']':raise ValueError('Checkpoint array separator missing')
                self.at+=1
            else:yield key,None,self.value()
            if self.char()==',':self.at+=1
            elif self.char()!='}':raise ValueError('Checkpoint object separator missing')
        self.at+=1
        if self.buffer[self.at:].strip():raise ValueError('Checkpoint trailing content')
        for tail in iter(lambda:self.stream.read(65536),''):
            if tail.strip():raise ValueError('Checkpoint trailing content')

def export_emulator(config,output):
    path=Path(config['emulatorCheckpointPath']);maximum=config.get('limits',{}).get('maximumBytes',256*1024**2)
    if not path.is_file() or path.stat().st_size>min(maximum,256*1024**2):raise ValueError('Emulator head unavailable or exceeds its actual256MiB bound')
    before=sha_file(path);ledger=Ledger(output,config.get('limits',{}));manifest=base_manifest(config,'FileEmulatorLedgerStore');manifest['checkpointSha256']=before
    counts={};scalars={};mapping={'Orders':'orders','Operations':'sourceCursors','Positions':'positions','Journal':'cash','Observations':'fills','LatestQuotes':'sourceCursors','QuoteReceipts':'sourceCursors','MatchReceipts':'sourceCursors','OwnerCheckpoints':'sourceCursors'}
    with path.open(encoding='utf-8-sig') as stream:
        for field,index,value in JsonItems(stream,config.get('limits',{}).get('maximumRecordBytes',16*1024**2)).fields():
            counts[field]=counts.get(field,0)+1
            if index is None:scalars[field]=value;key=[field];category='cash' if field=='Cash' else 'sourceCursors'
            else:
                category=mapping.get(field)
                if category is None:raise ValueError('Unsupported emulator checkpoint field: '+field)
                if index==-1:identity='$empty'
                elif field=='Orders':identity=value['Request']['BrokerOrderId']
                elif field=='Operations':identity=value['OperationId']
                elif field=='Positions':identity=value['ContractId']
                elif field=='Journal':identity=value['Sequence']
                elif field=='Observations':identity=value['ObservationId']
                elif field=='LatestQuotes':identity=value['ContractId']
                elif field=='QuoteReceipts':identity=[value['Quote']['ContractId'],value['Quote']['SourceEpoch'],value['Quote']['SourceSequence']]
                elif field=='OwnerCheckpoints':identity=value['BoundaryId']
                else:identity=value['Key']
                key=[field,identity]
            ledger.add(category,'FileEmulatorLedgerStore','checkpoint',key,value)
    if sha_file(path)!=before:raise ValueError('Emulator checkpoint changed during export')
    for name in ('Cash','Sequence','Generation'):
        if name not in scalars:raise ValueError('Missing actual emulator scalar: '+name)
    for name in ('Orders','Operations','Positions','Journal','Observations'):
        if name not in counts:raise ValueError('Missing actual emulator array: '+name)
    manifest['checkpointFields']=counts;manifest['commitRevision']=scalars.get('CommitRevision',0)
    if manifest['commitRevision']<=0:manifest['coverageFailures'].append('Legacy zero CommitRevision cannot prove emulator/PG source floor')
    return finalize(config,ledger,manifest)

def export_proofs(config,output):
    root=Path(config['realtimeJournalDirectory']);ledger=Ledger(output,config.get('limits',{}));manifest=base_manifest(config,'RealtimeSourceOutbox');ledger.allow_redundant_facts=True
    database=sqlite3.connect(ledger.root/'full-proof-index.tmp')
    try:
        database.execute('PRAGMA cache_size=-2048');database.execute('PRAGMA max_page_count='+str(max(1,min(4*1024**3,config.get('limits',{}).get('maximumBytes',4*1024**3))//4096)));database.execute('CREATE TABLE proofs(sourcekey BLOB PRIMARY KEY,sourcehash BLOB) WITHOUT ROWID')
        if (root.parent/'ownership.protocol').read_text()!='2:source-until-handler-proof':raise ValueError('Unknown handler proof protocol')
        files={}
        for name,size in [('receipts.bin',148),('delivery-receipts.bin',128)]:
            path=root/name;before=sha_file(path);files[name]=before
            if path.stat().st_size%size:raise ValueError('Torn authoritative receipt file; offline export never truncates it')
            with path.open('rb') as stream:
                while row:=stream.read(size):
                    payload=row[:-32]
                    if hashlib.sha256(payload).digest()!=row[-32:]:raise ValueError('Authoritative receipt checksum mismatch')
                    if size==148:
                        count=struct.unpack_from('<i',row,112)[0]
                        if not 0<=count<=256:raise ValueError('Frozen recipient count invalid')
                        value={'sourceKey':row[:32].hex(),'sourceSha256':row[32:64].hex(),'sourceEventId':str(uuid.UUID(bytes_le=row[64:80])),'manifestSha256':row[80:112].hex(),'destinationCount':count}
                        database.execute('INSERT OR IGNORE INTO proofs VALUES (?,?)',(row[:32],row[32:64]))
                        ledger.add('sources','RealtimeSourceOutbox','full-source-receipt',[value['sourceKey']],value)
                        ledger.add('destinations','RealtimeSourceOutbox','frozen-manifest-completion',[value['sourceKey']],{'sourceSha256':value['sourceSha256'],'manifestSha256':value['manifestSha256'],'destinationCount':count,'proof':'EveryFrozenRequiredDestinationCompleted'})
                    else:
                        completed=database.execute('SELECT sourcehash FROM proofs WHERE sourcekey=?',(row[:32],)).fetchone()
                        if completed:
                            if completed[0]!=row[32:64]:raise ValueError('Partial/full source proof conflict')
                            continue
                        ledger.add('destinations','RealtimeSourceOutbox','partial-destination-receipt',[row[:32].hex(),row[64:96].hex()],{'sourceSha256':row[32:64].hex(),'mailboxKey':row[64:96].hex()})
            if sha_file(path)!=before:raise ValueError('Authoritative receipt file changed during export')
    finally:
        database.close()
        if (ledger.root/'full-proof-index.tmp').exists():(ledger.root/'full-proof-index.tmp').unlink()
    manifest['originalReceiptFiles']=files;manifest['limitations'].append('Full-source receipt references a frozen manifest hash; original source archive must supply its exact recipient identities before complete coverage can be claimed.')
    return finalize(config,ledger,manifest)

def records(manifest_path):
    manifest=load(manifest_path);root=Path(manifest_path).resolve().parent;relative=Path(manifest['ledger']['path'])
    if relative.is_absolute() or '..' in relative.parts:raise ValueError('Ledger path escapes export root')
    path=root/relative
    if not path.resolve().is_relative_to(root) or path.is_symlink():raise ValueError('Ledger path is not an owned evidence file')
    mode=manifest['ledger'].get('valueMode','FullCanonical')
    if mode not in ('FullCanonical','ExactCanonicalDigest'):raise ValueError('Unsupported exact evidence value mode')
    previous=bytes(32);count=0;last=None
    with path.open('rb') as stream:
        while line:=stream.readline(32*1024**2+1):
            if len(line)>32*1024**2:raise ValueError('Ledger line bound exceeded')
            item=json.loads(line,parse_float=decimal.Decimal);count+=1;data=canonical(item['record']);previous=hashlib.sha256(previous+data).digest()
            if item['ordinal']!=count or item['sha256']!=previous.hex():raise ValueError('Ledger record chain/ordinal mismatch')
            r=item['record'];key=canonical([r['category'],r['owner'],r['relation'],r['key']])
            if mode=='ExactCanonicalDigest' and r['key']!=['$schema']:
                value=r['value']
                if not isinstance(value,dict) or set(value)!={'$exactCanonicalSha256','$exactCanonicalBytes'} or not re.fullmatch('[a-f0-9]{64}',value.get('$exactCanonicalSha256','')) or not isinstance(value.get('$exactCanonicalBytes'),int) or not 0<=value['$exactCanonicalBytes']<=16*1024**2:raise ValueError('Invalid complete canonical value digest')
            if last is not None and key<=last:raise ValueError('Ledger keys unordered or duplicated')
            last=key;yield r
    if count!=manifest['ledger']['records'] or previous.hex()!=manifest['ledger']['chainSha256'] or sha_file(path)!=manifest['ledger']['sha256']:raise ValueError('Ledger manifest/file mismatch')

def compare(expected_path,actual_path,output):
    left=load(expected_path);right=load(actual_path);failures=[];differences=[]
    if any(value.get('state')!='Complete' or value.get('kind')!='ActualOwnerOutcomeLedgers' for value in (left,right)):
        raise ValueError('Only completed actual-owner evidence exports may be compared')
    for key in ('sessionId','initialSnapshotSha256','sourceCaptureSha256','calendarEvidenceSha256'):
        if not left.get('boundary',{}).get(key) or left['boundary'][key]!=right.get('boundary',{}).get(key):failures.append('boundary:'+key)
    if left.get('ledger',{}).get('valueMode','FullCanonical')!=right.get('ledger',{}).get('valueMode','FullCanonical'):failures.append('exact-evidence-value-mode-differs')
    for key in ('relations','adapter','comparisonProfile'):
        if left.get(key)!=right.get(key):failures.append('schema-or-adapter:'+key)
    for l,r in itertools.zip_longest(records(expected_path),records(actual_path)):
        if l!=r and len(differences)<100:differences.append({'expected':l,'actual':r})
    if differences:failures.append('actual-owner-record-identities-or-content-differ')
    if left.get('coverageFailures') or right.get('coverageFailures'):failures.append('owner-export-coverage-incomplete')
    result={'version':1,'kind':'ActualOwnerRecordComparison','matched':not failures,'qualified':False,'failures':failures,'differences':differences,
            'expectedManifestSha256':sha_file(expected_path),'actualManifestSha256':sha_file(actual_path),'checkedUtc':utc(),
            'limitations':['Matching actual owner rows alone does not establish all-family session coverage, authentic baseline, or stopped-owner boundary.']}
    write(output,result);return result

def bundle(component_paths,output,profile='Full'):
    """Combine real exports, deriving coverage from schema/records rather than supplied family-success flags."""
    if not 1<=len(component_paths)<=32:raise ValueError('Owner component count must be bounded')
    components=[load(path) for path in component_paths];first=components[0]
    if any(item.get('state')!='Complete' or item.get('kind')!='ActualOwnerOutcomeLedgers' for item in components):raise ValueError('Incomplete owner export component')
    for item in components:
        if item.get('boundary')!=first.get('boundary') or item.get('backend')!=first.get('backend') or item.get('runId')!=first.get('runId'):raise ValueError('Owner components have different replay boundaries')
    if profile not in ('Full','BusinessOnly'):raise ValueError('Unsupported actual-owner comparison profile')
    cfg={'role':first['role'],'runId':first['runId'],'backend':first['backend'],'boundary':first['boundary']}
    if first['role']=='OriginalStoppedOwners':
        cfg.update(originalFencePath=first['originalFencePath'],originalFenceSha256=first['originalFenceSha256'])
        if any(item.get('originalFenceSha256')!=cfg['originalFenceSha256'] for item in components):raise ValueError('Original outcome components have different stopped-owner fences')
    modes={item['ledger'].get('valueMode','FullCanonical') for item in components}
    if len(modes)!=1:raise ValueError('Owner components use different exact evidence value modes')
    mode=modes.pop()
    if mode=='ExactCanonicalDigest' and (len(components)>4 or any(item['ledger']['records']>100000000 or (Path(path).resolve().parent/item['ledger']['path']).stat().st_size>8*1024**3 for path,item in zip(component_paths,components))):raise ValueError('Compact all-owner budget permits four bounded8GiB component ledgers')
    ledger=Ledger(output,{'valueMode':mode,'chunkBytes':64*1024**2,'maximumBytes':32*1024**3 if mode=='ExactCanonicalDigest' else 64*1024**3},streamed_bundle=True);result=base_manifest(cfg,'AllOwnerBundle');result['comparisonProfile']=profile;relations=[];adapters=set();seen_categories=set()
    for path,item in zip(component_paths,components):
        adapters.add(item['adapter']);relations.extend(item.get('relations',[]));result['coverageFailures'].extend(item.get('coverageFailures',[]))
    def original_records(path):
        for record in records(path):
            seen_categories.add(record['category']);yield canonical([record['category'],record['owner'],record['relation'],record['key']]),canonical(record)
    ledger.sorted_input=heapq.merge(*(original_records(path) for path in component_paths))
    finished=ledger.finish()
    result['relations']=sorted(relations,key=lambda row:(row['category'],row['relation']))
    required_categories=set(CATEGORIES)-({'sources','destinations'} if profile=='BusinessOnly' else set());required_adapters={'PostgreSQL','ScyllaCqlRows','FileEmulatorLedgerStore'}|({'RealtimeSourceOutbox'} if profile=='Full' else set())
    result['requiredOutcomeCategories']=sorted(required_categories)
    for missing in sorted(required_categories-seen_categories):result['coverageFailures'].append('Missing actual owner category: '+missing)
    for missing in sorted(required_adapters-adapters):result['coverageFailures'].append('Missing actual owner adapter: '+missing)
    if profile=='BusinessOnly' and (seen_categories&{'sources','destinations'} or 'RealtimeSourceOutbox' in adapters):result['coverageFailures'].append('Business-only profile cannot relabel transport proofs as business-owner data')
    actual_pg={row['relation'] for item in components if item['adapter']=='PostgreSQL' for row in item.get('relations',[])}
    for missing in sorted({name for group in DEFAULT_RELATIONS.values() for name in group}-actual_pg):result['coverageFailures'].append('Missing required production owner relation: '+missing)
    result['componentManifestSha256']=[sha_file(path) for path in component_paths]
    result['limitations'].append('Even complete owner/category schemas do not establish all-family input coverage or a stopped matching multi-store boundary.')
    return finalize(cfg,ledger,result,finished)

def export_original_bundle(config,output):
    """Execute only inside the controller's currently held original owner fence; never restore or mutate stores."""
    original_fence(config);root=Path(output);root.mkdir(parents=True,exist_ok=False);paths=[]
    for name,exporter in [('postgres',export_postgres),('scylla',export_scylla),('emulator',export_emulator)]:
        original_fence(config);exporter(config,root/name);paths.append(root/name/'manifest.json')
    manifest=bundle(paths,root/'business','BusinessOnly');original_fence(config);path=root/'business/manifest.json'
    if manifest['coverageFailures']:raise ValueError('Original business-owner coverage is incomplete; no successful outcome reference may be issued')
    result={'path':str(path.resolve()),'sha256':sha_file(path),'qualified':False,'comparisonProfile':'BusinessOnly'}
    if config.get('rawPartitionsEvidence'):result['rawPartitions']=config['rawPartitionsEvidence']
    write(root/'result.json',result);return result

def original_configuration(config,state_path,fence_path):
    """Resolve only independently hash-bound original controller artifacts; no inferred floor or substituted owner."""
    state=load(state_path);initial=state['initialSnapshot'];source=state['sourceArchive']
    for item in (initial,source):
        if sha_file(item['path'])!=item['sha256']:raise ValueError('Original controller owner/source artifact changed')
    archive=load(source['path'])
    if archive.get('kind')!='CommittedSourceArchivePrefixes' or archive.get('archiveOwnershipOnly') is not True:raise ValueError('Original committed archive copy is required')
    calendar=config['calendarEvidencePath']
    if sha_file(calendar)!=config['calendarEvidenceSha256']:raise ValueError('Original calendar evidence changed')
    resolved={**config,'originalFencePath':str(Path(fence_path).resolve()),'originalFenceSha256':sha_file(fence_path),
        'boundary':{'sessionId':state['archiveSessionId'],'initialSnapshotSha256':initial['sha256'],'sourceCaptureSha256':source['sha256'],'calendarEvidenceSha256':config['calendarEvidenceSha256']}}
    if config.get('scylla',{}).get('partitionManifestFromState'):
        proof=state.get('rawPartitions')
        if not proof:
            import replay_after_capture
            proof=replay_after_capture.generate_partitions(state,load(config['replayConfigurationPath']))
        if sha_file(proof['path'])!=proof['sha256']:raise ValueError('Original typed raw partition inventory changed')
        inventory=load(proof['path'])
        if inventory.get('kind')!='OriginalRawPartitionManifest' or inventory.get('sourceArchiveSha256')!=source['sha256'] or inventory.get('archiveSessionId')!=state['archiveSessionId']:raise ValueError('Raw partition inventory does not belong to this original source archive')
        resolved['scylla']={**config['scylla'],'partitions':inventory['partitions']};resolved['rawPartitionsEvidence']=proof
    original_fence(resolved);return resolved

def main():
    parser=argparse.ArgumentParser(description=__doc__);sub=parser.add_subparsers(dest='command',required=True)
    for name in ('export-postgres','export-scylla','export-emulator','export-proofs','export-original'):
        p=sub.add_parser(name);p.add_argument('--config',required=True);p.add_argument('--output',required=True)
        if name=='export-original':p.add_argument('--state');p.add_argument('--fence')
    p=sub.add_parser('bundle');p.add_argument('--component',required=True,nargs='+');p.add_argument('--output',required=True);p.add_argument('--profile',choices=('Full','BusinessOnly'),default='Full')
    p=sub.add_parser('compare');p.add_argument('--expected',required=True);p.add_argument('--actual',required=True);p.add_argument('--output',required=True)
    args=parser.parse_args()
    if args.command=='bundle':result=bundle(args.component,args.output,args.profile)
    elif args.command=='compare':result=compare(args.expected,args.actual,args.output)
    else:
        config=load(args.config)
        if args.command=='export-original' and (args.state or args.fence):
            if not args.state or not args.fence:raise ValueError('Original state and held fence must be supplied together')
            config=original_configuration(config,args.state,args.fence)
        result={'export-postgres':export_postgres,'export-scylla':export_scylla,'export-emulator':export_emulator,'export-proofs':export_proofs,'export-original':export_original_bundle}[args.command](config,args.output)
    print(json.dumps({'state':result.get('state'),'matched':result.get('matched'),'qualified':False}));return 0 if result.get('matched',True) else 2
if __name__=='__main__':raise SystemExit(main())
