"""Container-local paged SELECT-only worker; stdout contains bounded JSON records only."""
import datetime, glob, json, os, sys
for directory in ('/opt/cassandra/lib','/usr/share/cassandra/lib'):
 for path in glob.glob(directory+'/cassandra-driver-internal-only-*.zip'):
  version=path.rsplit('/',1)[1][len('cassandra-driver-internal-only-'):-4];sys.path.insert(0,path+'/cassandra-driver-'+version)
 for prefix in ('pure_sasl-','wcwidth-','pyasyncore-'):
  sys.path[:0]=glob.glob(directory+'/'+prefix+'*.zip')
from cassandra.cluster import Cluster
from cassandra.query import SimpleStatement
from cassandra import ConsistencyLevel

request=json.load(sys.stdin);cluster=Cluster([request['container']],port=9042,protocol_version=4,connect_timeout=10,control_connection_timeout=10)
try:
 session=cluster.connect(request['keyspace']);session.default_timeout=30;seen=set()
 for scope in request['partitions']:
  table=cluster.metadata.keyspaces[request['keyspace']].tables[scope['table']]
  columns=table.columns;keys=[column.name for column in table.partition_key+table.clustering_key]
  relation=request['keyspace']+'.'+scope['table']
  if relation not in seen:
   print(json.dumps({'kind':'schema','relation':relation,'columns':{name:column.cql_type for name,column in columns.items()},'primaryKey':keys}),flush=True);seen.add(relation)
  contract='contract_id' if 'contract_id' in columns else 'contractid'
  date='value_date' if 'value_date' in columns else 'valuedate'
  clauses=[contract+'=%s',date+'=%s'];args=[scope['contractId'],datetime.date.fromisoformat(scope['valueDate'])]
  if 'asset_type_id' in columns:clauses.insert(0,'asset_type_id=%s');args.insert(0,int(scope['assetType']))
  query='SELECT JSON * FROM '+relation+' WHERE '+' AND '.join(clauses)
  rows=session.execute(SimpleStatement(query,fetch_size=16,consistency_level=ConsistencyLevel.LOCAL_QUORUM),args)
  count=0
  for row in rows:
   text=row[0];value=json.loads(text);key=[value[name] for name in keys];count+=1
   if count>request['maximumRecords']:raise ValueError('Raw partition record bound exceeded')
   # Preserve original JSON numeric text; host parses decimals exactly.
   print('{"kind":"row","relation":'+json.dumps(relation)+',"key":'+json.dumps(key,separators=(',',':'))+',"value":'+text+'}',flush=True)
  print(json.dumps({'kind':'partition-complete','relation':relation,'partition':scope,'rows':count}),flush=True)
finally:cluster.shutdown()
