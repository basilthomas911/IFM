"""Raw-tape reference verification of matched-control selection and first-passage labels."""
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


def verify_day(job):
    source,f,events,opportunities,pairs,control_rows,unmatched=job
    tape=np.loadtxt(source/f['file'],delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows']
    minute=tape['ns']//MINUTE
    available=np.flatnonzero(np.diff(minute)!=0)+1
    end_ns=(minute[available-1]+1)*MINUTE
    closes=tape['price'][available-1]

    def vol_at(index):
        k=int(np.searchsorted(available,index,side='right'))-1
        if k<14 or int(tape['ns'][index])-int(end_ns[k])>120*NS:
            return None
        differences=[float(closes[i]-closes[i-1]) for i in range(k-13,k+1)]
        rms=math.sqrt(sum(x*x for x in differences)/14)
        if not rms:
            return None
        return rms,k

    published=[]
    last=-1
    ns100=tape['ns']//100
    for e in events:
        first=max(last+1,int(np.searchsorted(ns100,e['_ns100'],side='left')))
        end=int(np.searchsorted(ns100,e['_ns100'],side='right'))
        indices=np.flatnonzero(tape['price'][first:end]==float(e['IntrinsicPrice']))
        assert len(indices)
        last=first+int(indices[0])
        published.append((last,float(e['BandSize'])))
    published_indices=np.array([p[0] for p in published])
    date=dt.date.fromisoformat(f['value_date'])
    start=int(dt.datetime.combine(date,dt.time(9,45),NY).timestamp())*NS
    cutoff=int(dt.datetime.combine(date,dt.time(15,45),NY).timestamp())*NS
    triggers=np.array(sorted(int(r['signal_event_ns']) for r in opportunities),dtype=np.int64)
    pool=[]
    for scheduled in range(start+30*NS,cutoff,MINUTE):
        index=int(np.searchsorted(tape['ns'],scheduled))
        if index+1>=len(tape) or tape['ns'][index+1]>=cutoff or tape['ns'][index]>scheduled+5*NS:
            continue
        now=int(tape['ns'][index])
        prior_triggers=triggers[(triggers<=now)&(triggers>=now-MINUTE)]
        if len(prior_triggers):
            continue
        v=vol_at(index)
        prior=int(np.searchsorted(published_indices,index,side='right'))-1
        if v is None or prior<0 or not math.isfinite(published[prior][1]) or published[prior][1]<.25:
            continue
        pool.append((index,now,published[prior][1],v[0],scheduled,published[prior][0]))
    controls=collections.defaultdict(list)
    for c in control_rows:
        controls[c['opportunity_id']].append(c)
    paired={r['opportunity_id']:r for r in pairs}
    excluded={r['opportunity_id']:r for r in unmatched}
    originals={r['opportunity_id']:r for r in opportunities}
    for r in opportunities:
        index=r['signal_ordinal']-1
        assert int(tape['ns'][index])==int(r['signal_event_ns'])
        v=vol_at(index)
        candidates=[]
        if v is not None:
            for c in pool:
                if 0<int(r['signal_event_ns'])-c[1]<=15*MINUTE and .75<=c[3]/v[0]<=1.25 and .9<=c[2]/r['entry_band_size']<=1.1:
                    candidates.append(c)
        key=r['opportunity_id']
        if len(candidates)<5:
            assert key in excluded and key not in paired
            assert excluded[key]['eligible_controls']==len(candidates)
            continue
        assert key in paired and key not in excluded
        p=paired[key]
        assert p['eligible_controls']==len(candidates) and p['iti_target_hit']==r['target_hit']
        assert len(controls[key])==5 and len({c['control_id'] for c in controls[key]})==5
        seed=int.from_bytes(hashlib.sha256(f'20261102:{key}'.encode()).digest()[:8],'little')
        choices=np.random.default_rng(seed).choice(len(candidates),size=5,replace=False)
        expected={candidates[int(i)][0]+1 for i in choices}
        assert {c['signal_ordinal'] for c in controls[key]}==expected
        assert math.isclose(p['iti_volatility'],v[0],abs_tol=1e-12)
        assert p['iti_volatility_bar_end_ns']==str(int(end_ns[v[1]]))
        assert p['iti_volatility_bar_available_ns']==str(int(tape['ns'][available[v[1]]]))
        rate=sum(c['target_hit'] for c in controls[key])/5
        assert p['control_target_rate']==rate and p['paired_difference']==float(r['target_hit'])-rate
    unique={c['control_id']:c for c in control_rows}
    boundary=int(np.searchsorted(tape['ns'],cutoff))
    for c in unique.values():
        index=c['signal_ordinal']-1
        fill=c['entry_ordinal']-1
        ns=int(tape['ns'][index])
        assert fill==index+1 and int(tape['ns'][fill])==int(c['entry_event_ns'])
        assert float(tape['price'][fill])==c['entry_price']
        assert ns==int(c['signal_event_ns']) and start<=ns<cutoff
        assert int(c['scheduled_event_ns'])%MINUTE==30*NS
        assert 0<=ns-int(c['scheduled_event_ns'])<=5*NS
        assert not np.any((triggers<=ns)&(triggers>=ns-MINUTE))
        v=vol_at(index)
        assert math.isclose(c['volatility'],v[0],abs_tol=1e-12)
        assert c['volatility_bar_end_ns']==str(int(end_ns[v[1]]))
        assert c['volatility_bar_available_ns']==str(int(tape['ns'][available[v[1]]]))
        prior=int(np.searchsorted(published_indices,index,side='right'))-1
        assert c['band_source_ordinal']==published[prior][0]+1
        assert c['entry_band_size']==published[prior][1]
        sign=1 if c['side']=='Long' else -1
        risk=math.floor(c['entry_band_size']*4+1e-10)/4
        assert c['stop_price']==c['entry_price']-sign*risk
        assert c['target_price']==c['entry_price']+sign*2*c['entry_band_size']
        if c['outcome']=='Session cutoff':
            segment=tape['price'][fill+1:boundary]
            mark=boundary if boundary<len(tape) and tape['ns'][boundary]==cutoff else boundary-1
            assert c['outcome_ordinal']==mark+1 and c['outcome_event_ns']==str(cutoff)
        else:
            mark=c['outcome_ordinal']-1
            assert fill<mark<boundary and c['outcome_event_ns']==str(int(tape['ns'][mark]))
            segment=tape['price'][fill+1:mark]
            move=sign*(tape['price'][mark]-c['entry_price'])
            assert move>=2*c['entry_band_size'] if c['target_hit'] else move<=-risk
        gains=sign*(segment-c['entry_price'])
        assert np.all(gains<2*c['entry_band_size']) and np.all(gains>-risk)
        assert c['outcome_price']==float(tape['price'][mark])
        assert c['outcome_time_ny']==ny_time(int(c['outcome_event_ns']))
    for c in control_rows:
        r=originals[c['opportunity_id']]
        assert c['side']==r['side'] and c['contract_id']==r['contract_id'] and c['trade_date']==r['trade_date']
        assert math.isclose(c['minutes_before_iti'],(int(r['signal_event_ns'])-int(c['signal_event_ns']))/MINUTE,abs_tol=1e-12)
        assert 0<c['minutes_before_iti']<=15
        assert .75<=c['volatility_ratio']<=1.25 and .9<=c['band_ratio']<=1.1
    return len(pairs),len(unique),len(tape)


def main(output):
    report=json.loads((output/'analysis.json').read_text())
    original=Path(report['protocol']['opportunity_directory'])
    assert hashlib.sha256((original/'opportunities.json').read_bytes()).hexdigest()==report['protocol']['opportunity_sha256']
    opportunities=json.loads((original/'opportunities.json').read_text())
    pairs=json.loads((output/'pairs.json').read_text())
    controls=json.loads((output/'controls.json').read_text())
    unmatched=json.loads((output/'unmatched.json').read_text())
    baseline=json.loads((Path(report['protocol']['baseline_directory'])/'analysis.json').read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    replay=json.loads((Path(baseline['source_run_directory'])/'input.json.results.json').read_text())
    groups=[]
    for values in (opportunities,pairs,controls,unmatched):
        by_date=collections.defaultdict(list)
        for r in values:
            by_date[r['trade_date']].append(r)
        groups.append(by_date)
    events=collections.defaultdict(list)
    for e in replay['signals']:
        if e['TimePeriod']==1 and e['ValueDate'] in groups[0]:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    files=[f for f in manifest['files'] if f['value_date'] in groups[0]]
    checked=unique=ticks=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        jobs=[(source,f,events[f['value_date']],*[g[f['value_date']] for g in groups]) for f in files]
        for f,(n,k,rows) in zip(files,pool.map(verify_day,jobs)):
            checked+=n
            unique+=k
            ticks+=rows
            print(f['value_date'],checked,flush=True)
    assert checked==len(pairs)==report['coverage']['matched']
    assert unique==report['control_reuse']['unique_controls'] and ticks==report['checks']['raw_source_ticks']
    assert len(pairs)+len(unmatched)==len(opportunities)
    proof=dict(complete=True,matched_events_verified=checked,unique_control_paths_verified=unique,raw_rows_loaded=ticks,
        prior_only_matching_and_fixed_calipers_verified=True,seeded_selection_and_unmatched_coverage_verified=True,
        trailing_volatility_and_asof_band_sources_verified=True,no_recent_iti_trigger_verified=True,
        next_tick_control_fills_and_first_outcome_touches_verified=True,paired_rates_reconciled=True)
    (output/'independent-verification.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=Path('outputs/iti-entry-matched-benchmark-nov2025-oct2026'))
    args=parser.parse_args()
    main(args.output_dir)
