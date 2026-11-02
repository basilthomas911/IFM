"""Independent checks of separate-date matching, decision-time features and control barriers."""
import argparse
import collections
import datetime as dt
import hashlib
import json
import math
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
import numpy as np
from analyze_daily_reversion import NY,NS,event_ns,ny_time

MINUTE=60*NS


def verify_raw_day(job):
    source,f,events,opportunities,features,controls=job
    tape=np.loadtxt(source/f['file'],delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows']
    minute=tape['ns']//MINUTE
    available=np.flatnonzero(minute[1:]!=minute[:-1])+1
    closes=tape['price'][available-1]
    ends=(minute[available-1]+1)*MINUTE

    def reference_vol(index):
        k=int(np.searchsorted(available,index,side='right'))-1
        if k<14 or tape['ns'][index]-ends[k]>120*NS:
            return None
        changes=[float(closes[i]-closes[i-1]) for i in range(k-13,k+1)]
        value=math.sqrt(sum(x*x for x in changes)/14)
        return (value,k) if value else None

    published=[]
    last=-1
    times=tape['ns']//100
    for e in events:
        start=max(last+1,int(np.searchsorted(times,e['_ns100'],side='left')))
        end=int(np.searchsorted(times,e['_ns100'],side='right'))
        offsets=np.flatnonzero(tape['price'][start:end]==float(e['IntrinsicPrice']))
        assert len(offsets)
        last=start+int(offsets[0])
        published.append((last,float(e['BandSize'])))
    positions=np.array([p[0] for p in published])
    date=dt.date.fromisoformat(f['value_date'])
    start=int(dt.datetime.combine(date,dt.time(9,45),NY).timestamp())*NS
    cutoff=int(dt.datetime.combine(date,dt.time(15,45),NY).timestamp())*NS
    boundary=int(np.searchsorted(tape['ns'],cutoff))
    by_id={r['opportunity_id']:r for r in opportunities}
    triggers=np.array(sorted(int(r['signal_event_ns']) for r in opportunities),dtype=np.int64)
    for r in features:
        original=by_id[r['id']]
        index=original['signal_ordinal']-1
        v=reference_vol(index)
        assert r['band']==original['entry_band_size'] and r['contract_id']==original['contract_id']
        assert math.isclose(r['clock_minutes'],(int(tape['ns'][index])-start)/MINUTE,abs_tol=1e-12)
        if v is None:
            assert r['volatility'] is None
        else:
            assert math.isclose(r['volatility'],v[0],abs_tol=1e-12)
    for c in controls:
        decision=c['signal_ordinal']-1
        fill=c['entry_ordinal']-1
        assert fill==decision+1
        ns=int(tape['ns'][decision])
        assert c['signal_event_ns']==str(ns) and c['entry_event_ns']==str(int(tape['ns'][fill]))
        assert tape['price'][fill]==c['entry_price'] and start<=ns<int(tape['ns'][fill])+1<=cutoff
        assert int(c['scheduled_event_ns'])%MINUTE==30*NS and 0<=ns-int(c['scheduled_event_ns'])<=5*NS
        assert not np.any((triggers<=ns)&(triggers>=ns-MINUTE))
        vol=reference_vol(decision)
        assert math.isclose(c['volatility'],vol[0],abs_tol=1e-12)
        assert c['bar_end_ns']==str(int(ends[vol[1]]))
        assert c['bar_available_ns']==str(int(tape['ns'][available[vol[1]]]))
        prior=int(np.searchsorted(positions,decision,side='right'))-1
        assert c['band_source_ordinal']==published[prior][0]+1 and c['band']==published[prior][1]
        sign=1 if c['side']=='Long' else -1
        risk=math.floor(c['band']*4+1e-10)/4
        assert c['stop_price']==c['entry_price']-sign*risk and c['target_price']==c['entry_price']+sign*2*c['band']
        if c['outcome']=='Session cutoff':
            prefix=tape['price'][fill+1:boundary]
            mark=boundary if boundary<len(tape) and tape['ns'][boundary]==cutoff else boundary-1
            assert c['outcome_ordinal']==mark+1 and c['outcome_event_ns']==str(cutoff)
        else:
            mark=c['outcome_ordinal']-1
            assert fill<mark<boundary and c['outcome_event_ns']==str(int(tape['ns'][mark]))
            prefix=tape['price'][fill+1:mark]
            move=sign*(tape['price'][mark]-c['entry_price'])
            assert move>=2*c['band'] if c['target_hit'] else move<=-risk
        gains=sign*(prefix-c['entry_price'])
        assert np.all(gains<2*c['band']) and np.all(gains>-risk)
        assert c['outcome_price']==float(tape['price'][mark]) and c['outcome_time_ny']==ny_time(int(c['outcome_event_ns']))
    return len(features),len(controls),len(tape)


def main(output):
    report=json.loads((output/'analysis.json').read_text())
    original_dir=Path(report['protocol']['opportunity_directory'])
    opportunities=json.loads((original_dir/'opportunities.json').read_text())
    assert hashlib.sha256((original_dir/'opportunities.json').read_bytes()).hexdigest()==report['protocol']['opportunity_sha256']
    original={r['opportunity_id']:r for r in opportunities}
    pool=json.loads((output/'control-pool.json').read_text())
    features=json.loads((output/'event-matching-features.json').read_text())
    selection=json.loads((output/'selection-before-outcomes.json').read_text())
    selected={r['opportunity_id']:r for r in selection}
    pairs=json.loads((output/'pairs.json').read_text())
    paired={r['opportunity_id']:r for r in pairs}
    controls=json.loads((output/'controls.json').read_text())
    unique={r['control_id']:r for r in controls}
    unmatched={r['opportunity_id']:r for r in json.loads((output/'unmatched.json').read_text())}
    pools=collections.defaultdict(list)
    for c in pool:
        pools[c['day_index']].append(c)
    for e in features:
        r=original[e['id']]
        candidates=[]
        if e['volatility'] is not None:
            candidates=[c for day in range(max(0,e['day_index']-20),e['day_index']) for c in pools[day]
                if c['contract_id']==e['contract_id'] and abs(c['clock_minutes']-e['clock_minutes'])<=15
                and .75<=c['volatility']/e['volatility']<=1.25 and .9<=c['band']/e['band']<=1.1]
        if len(candidates)<5:
            assert e['id'] in unmatched and unmatched[e['id']]['eligible_controls']==len(candidates)
            continue
        s=selected[e['id']]
        assert s['eligible_controls']==len(candidates) and len(set(s['control_ids']))==5
        seed=int.from_bytes(hashlib.sha256(f'20261102:{e["id"]}'.encode()).digest()[:8],'little')
        choices=np.random.default_rng(seed).choice(len(candidates),size=5,replace=False)
        expected={f'{candidates[int(i)]["pool_id"]}-{r["side"]}' for i in choices}
        assert set(s['control_ids'])==expected
        cs=[unique[key] for key in s['control_ids']]
        assert all(c['trade_date']<r['trade_date'] and c['contract_id']==r['contract_id'] and c['side']==r['side'] for c in cs)
        p=paired[e['id']]
        assert p['iti_target_hit']==r['target_hit']
        rate=sum(c['target_hit'] for c in cs)/5
        assert p['control_target_rate']==rate and p['paired_difference']==float(r['target_hit'])-rate
    baseline=json.loads((Path(report['protocol']['baseline_directory'])/'analysis.json').read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    assert [f['value_date'] for f in manifest['files']]==sorted(f['value_date'] for f in manifest['files'])
    replay=json.loads((Path(baseline['source_run_directory'])/'input.json.results.json').read_text())
    groups=[]
    for values,key in [(opportunities,'trade_date'),(features,None),(list(unique.values()),'trade_date')]:
        by_date=collections.defaultdict(list)
        for r in values:
            date=r[key] if key else original[r['id']]['trade_date']
            by_date[date].append(r)
        groups.append(by_date)
    events=collections.defaultdict(list)
    for e in replay['signals']:
        if e['TimePeriod']==1:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    checked=paths=ticks=0
    with ProcessPoolExecutor(max_workers=4) as executor:
        files=[f for f in manifest['files'] if f['value_date'] in groups[1] or f['value_date'] in groups[2]]
        jobs=[(source,f,events[f['value_date']],*[g[f['value_date']] for g in groups]) for f in files]
        for f,(n,k,rows) in zip(files,executor.map(verify_raw_day,jobs)):
            checked+=n
            paths+=k
            ticks+=rows
            print(f['value_date'],checked,paths,flush=True)
    assert checked==len(opportunities) and paths==len(unique)
    assert len(pairs)==report['coverage']['matched'] and len(pairs)+len(unmatched)==len(opportunities)
    assert math.isclose(report['primary']['paired_difference'],sum(p['paired_difference'] for p in pairs)/len(pairs),abs_tol=1e-12)
    proof=dict(complete=True,entry_features_verified=checked,matched_events_verified=len(pairs),unique_control_paths_verified=paths,
        raw_rows_loaded=ticks,separate_earlier_date_contract_direction_matching_verified=True,
        fixed_clock_volatility_band_calipers_and_seeded_selection_verified=True,unmatched_coverage_verified=True,
        asof_volatility_and_band_sources_verified=True,no_recent_entry_trigger_verified=True,
        next_tick_fills_and_first_barrier_touches_verified=True,paired_rates_reconciled=True)
    (output/'independent-verification.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=Path('outputs/iti-entry-crossday-benchmark-nov2025-oct2026'))
    args=parser.parse_args()
    main(args.output_dir)
