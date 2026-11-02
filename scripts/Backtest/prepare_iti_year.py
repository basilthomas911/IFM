"""Build immutable annual replay from the verified raw-import CSV snapshots.

Uses production expiry-based front selection; October reuses its original proof.
No database mutations, downloads, synthetic volatility or back-adjusted prices.
"""
import datetime as dt
import hashlib
import json
import os
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from zoneinfo import ZoneInfo

NY=ZoneInfo('America/New_York')
NS=1_000_000_000
ROOT=Path('.artifacts/iti-year-nov2025-oct2026')
RAW=Path('.artifacts/backtest-nov2025-sep2026')


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def read_partition(symbol,p):
    path=RAW/symbol/p['file']
    if digest(path)!=p['sha256']:
        raise ValueError('Source checksum mismatch: '+str(path))
    date=dt.date.fromisoformat(p['value_date'])
    # Resolve each local hour through the zone: midnight + elapsed hours is
    # incorrect on the Sunday evening following a DST transition.
    hour_start={h:int(dt.datetime.combine(date-dt.timedelta(days=h>=18),dt.time(h),NY).timestamp())*NS for h in range(24)}
    result=[]
    with path.open('rb') as stream:
        for line in stream:
            c=line.rstrip().split(b',')
            h,m,s=c[3].split(b':')
            sec,frac=s.split(b'.')
            ns=hour_start[int(h)]+(int(m)*60+int(sec))*NS+int(frac.ljust(9,b'0'))
            result.append((ns,int(c[2]),c[4]))
    assert len(result)==p['rows']
    result.sort()
    assert result[0][0]==p['min_event_ns'] and result[-1][0]==p['max_event_ns'], 'Source epoch range differs from import proof'
    return result


def export_day(job):
    date,es,vx,source,current_partition,prior_partition=job
    current=read_partition('VX',current_partition) if current_partition else []
    prior=read_partition('VX',prior_partition) if prior_partition else []
    latest=prior[-1] if prior else None
    es_rows=read_partition('ES',source)
    index=count=missing=max_age=0
    path=ROOT/(date+'.csv')
    last_second=prefix=None
    with path.open('w',encoding='ascii',newline='') as stream:
        for ns,tickid,price in es_rows:
            while index<len(current) and current[index][0]<ns:
                latest=current[index]
                index+=1
            if latest is None:
                missing+=1
                continue
            assert latest[0]<ns and float(price)>0 and float(latest[2])>0
            seconds,fraction=divmod(ns,NS)
            if seconds!=last_second:
                prefix=dt.datetime.fromtimestamp(seconds,dt.timezone.utc).isoformat(timespec='seconds')[:19]
                last_second=seconds
            timestamp=prefix+f'.{fraction//100:07d}Z'
            stream.write(f'{date},{timestamp},{ns},{tickid},{price.decode()},{latest[0]},{latest[1]},{latest[2].decode()},{vx}\n')
            count+=1
            max_age=max(max_age,ns-latest[0])
    return dict(file=path.name,value_date=date,contract_id=es,vx_contract_id=vx,rows=count,
                source_rows=len(es_rows),skipped_no_prior_vx=missing,max_vx_age_seconds=max_age/NS,sha256=digest(path))


def main():
    ROOT.mkdir(parents=True,exist_ok=True)
    if (ROOT/'input.json').exists():
        raise ValueError('Completed input retained')
    manifests={s:json.loads((RAW/s/'import-manifest.json').read_text()) for s in ('ES','VX')}
    maps={s:{(p['value_date'],p['contract_id']):p for p in v['partitions']} for s,v in manifests.items()}
    contracts={s:sorted({p['contract_id'] for p in v['partitions']}) for s,v in manifests.items()}
    expiry=lambda c:dt.datetime.strptime(c[-8:],'%Y%m%d').date().isoformat()
    dates=sorted({d for d,c in maps['ES'] if d<'2026-10-01'})
    files=[]
    journal=ROOT/'export-days.json'
    if journal.exists():
        completed={f['value_date']:f for f in json.loads(journal.read_text())}
    else:
        # Recover complete, hashed days from the initial export's retained log.
        log=Path('.artifacts/iti-year-export.log')
        completed={}
        if log.exists():
            for line in log.read_text(encoding='utf-16' if log.read_bytes().startswith(b'\xff\xfe') else 'utf-8-sig').splitlines():
                if line.startswith('{'):
                    item=json.loads(line)
                    if 'value_date' in item:
                        completed[item['value_date']]=item
    jobs=[]
    for date in dates:
        es=next(c for c in contracts['ES'] if expiry(c)>date and int(c[-4:-2]) in (3,6,9,12))
        vx=next(c for c in contracts['VX'] if expiry(c)>date)
        source=maps['ES'].get((date,es))
        if source is None:
            raise ValueError('Missing expected front ES '+date+' '+es)
        if date in completed:
            item=completed[date]
            assert digest(ROOT/item['file'])==item['sha256']
            files.append(item)
            continue
        earlier=sorted((d,p) for (d,c),p in maps['VX'].items() if c==vx and d<date and p['rows'])
        jobs.append((date,es,vx,source,maps['VX'].get((date,vx)),earlier[-1][1] if earlier else None))
    with ProcessPoolExecutor(max_workers=4) as executor:
        for item in executor.map(export_day,jobs):
            files.append(item)
            journal.write_text(json.dumps(files))
            print(json.dumps(item),flush=True)
            (ROOT/'export-progress.json').write_text(json.dumps(dict(completed_days=len(files),last_date=item['value_date'],rows=sum(f['rows'] for f in files))))
    files.sort(key=lambda f:f['value_date'])
    october=Path('.artifacts/iti-october-2026')
    old=json.loads((october/'input.json').read_text())
    for f in old['files']:
        assert digest(october/f['file'])==f['sha256']
        path=ROOT/f['file']
        if not path.exists():
            os.link(october/f['file'],path)
        assert digest(path)==f['sha256']
        files.append(dict(f,contract_id=old['contract_id']))
    proof=dict(format='iti-csv-v1',keyspace='trade_tick_quote_backtest_db',contract_id='ES_FRONT',files=files,
               start='2025-11-01T00:00:00Z',end='2026-11-01T00:00:00Z',es_rows=sum(f['rows'] for f in files),
               skipped_no_prior_vx=sum(f['skipped_no_prior_vx'] for f in files),
               initialization='Cold start per contract and Daily/Weekly/Monthly bucket; no pre-November warmup',
               rollover='Earliest quarterly ES maturity and earliest VX maturity strictly after NY value date, matching production expiry-based selection',
               vx_policy='Strictly earlier front VX trade; carry prior front and next-contract observations across dates',
               october_policy='Original October replay inputs retained byte-for-byte for comparability',
               raw_import_completion_proof_sha256=digest(RAW/'completion-proof.json'))
    proof['input_sha256']=hashlib.sha256(json.dumps(files,sort_keys=True,separators=(',',':')).encode()).hexdigest()
    (ROOT/'input.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps({k:v for k,v in proof.items() if k!='files'}),flush=True)


if __name__=='__main__':
    main()
