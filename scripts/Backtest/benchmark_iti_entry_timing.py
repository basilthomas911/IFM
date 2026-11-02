"""Predefined matched timing benchmark, with past-only matching features and no strategy fitting."""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import math
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
import numpy as np
from analyze_daily_reversion import NY,NS,event_ns,ny_time
from discover_iti_entry_signals import label_path

INPUT=Path('outputs/iti-entry-signal-discovery-nov2025-oct2026')
OUTPUT=Path('outputs/iti-entry-matched-benchmark-nov2025-oct2026')
MINUTE=60*NS
MATCHES=5
SEED=20261102


def volatility_bars(tape):
    minute=tape['ns']//MINUTE
    available=np.flatnonzero(minute[1:]!=minute[:-1])+1
    closes=tape['price'][available-1]
    ends=(minute[available-1]+1)*MINUTE
    rms=np.full(len(closes),np.nan)
    for i in range(14,len(closes)):
        changes=np.diff(closes[i-14:i+1])
        rms[i]=math.sqrt(float(np.mean(changes*changes)))
    return available,ends,rms


def volatility_asof(index,tape,bars):
    available,ends,rms=bars
    j=int(np.searchsorted(available,index,side='right'))-1
    if j<0 or not np.isfinite(rms[j]) or rms[j]<=0 or tape['ns'][index]-ends[j]>120*NS:
        return None
    assert available[j]<=index and ends[j]<=tape['ns'][index]
    return dict(volatility=float(rms[j]),bar_end_ns=str(int(ends[j])),
                bar_available_ns=str(int(tape['ns'][available[j]])))


def match_indices(event,pool):
    """Only decision-time matching features enter this function, never an outcome."""
    candidates=[i for i,c in enumerate(pool)
        if 0<event['ns']-c['ns']<=15*MINUTE
        and .75<=c['volatility']/event['volatility']<=1.25
        and .9<=c['band']/event['band']<=1.1]
    if len(candidates)<MATCHES:
        return [],len(candidates)
    seed=int.from_bytes(hashlib.sha256(f'{SEED}:{event["id"]}'.encode()).digest()[:8],'little')
    selected=np.random.default_rng(seed).choice(candidates,size=MATCHES,replace=False)
    return sorted(int(i) for i in selected),len(candidates)


def match_daily_events(tape,events):
    keys=tape['ns']//100
    indices=[]
    last=-1
    for e in events:
        left=max(last+1,int(np.searchsorted(keys,e['_ns100'],side='left')))
        right=int(np.searchsorted(keys,e['_ns100'],side='right'))
        matches=np.flatnonzero(tape['price'][left:right]==float(e['IntrinsicPrice']))
        assert len(matches)
        last=left+int(matches[0])
        indices.append(last)
    return np.array(indices,dtype=np.int64),np.array([float(e['BandSize']) for e in events])


def build_day(job):
    source,f,events,opportunities=job
    path=source/f['file']
    with path.open('rb') as stream:
        assert hashlib.file_digest(stream,'sha256').hexdigest()==f['sha256']
    tape=np.loadtxt(path,delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows'] and np.all(tape['ns'][1:]>=tape['ns'][:-1])
    bars=volatility_bars(tape)
    event_indices,event_bands=match_daily_events(tape,events)
    triggers=np.array(sorted(int(r['signal_event_ns']) for r in opportunities),dtype=np.int64)
    date=dt.date.fromisoformat(f['value_date'])
    start=int(dt.datetime.combine(date,dt.time(9,45),NY).timestamp())*NS
    cutoff=int(dt.datetime.combine(date,dt.time(15,45),NY).timestamp())*NS
    pool=[]
    for scheduled in range(start+30*NS,cutoff,MINUTE):
        index=int(np.searchsorted(tape['ns'],scheduled,side='left'))
        if index+1>=len(tape):
            continue
        ns=int(tape['ns'][index])
        if ns-scheduled>5*NS or tape['ns'][index+1]>=cutoff:
            continue
        recent=int(np.searchsorted(triggers,ns,side='right'))-1
        if recent>=0 and ns-int(triggers[recent])<=MINUTE:
            continue
        vol=volatility_asof(index,tape,bars)
        latest=int(np.searchsorted(event_indices,index,side='right'))-1
        if vol is None or latest<0 or not np.isfinite(event_bands[latest]) or event_bands[latest]<.25:
            continue
        pool.append(dict(ns=ns,decision_index=index,scheduled_ns=scheduled,band=float(event_bands[latest]),
            band_source_ordinal=int(event_indices[latest])+1,**vol))
    selections=[]
    unmatched=[]
    for r in opportunities:
        index=r['signal_ordinal']-1
        assert int(tape['ns'][index])==int(r['signal_event_ns'])
        vol=volatility_asof(index,tape,bars)
        if vol is None:
            unmatched.append(dict(opportunity_id=r['opportunity_id'],trade_date=r['trade_date'],
                baseline_status=r['baseline_status'],reason='No fresh warm nonzero trailing volatility',eligible_controls=0))
            continue
        features=dict(id=r['opportunity_id'],ns=int(r['signal_event_ns']),band=r['entry_band_size'],volatility=vol['volatility'])
        selected,count=match_indices(features,pool)
        if not selected:
            unmatched.append(dict(opportunity_id=r['opportunity_id'],trade_date=r['trade_date'],
                baseline_status=r['baseline_status'],reason='Fewer than five prior matched controls',eligible_controls=count))
            continue
        selections.append((r,vol,selected,count))
    # Matching is completely fixed before evaluating any control's future price path.
    pairs=[]
    controls=[]
    cache={}
    for r,vol,selected,count in selections:
        sign=1 if r['side']=='Long' else -1
        checked_event=label_path(tape,r['entry_ordinal']-1,sign,r['entry_band_size'],cutoff)
        assert checked_event['outcome']==r['outcome'] and checked_event['outcome_ordinal']==r['outcome_ordinal']
        linked=[]
        for selection in selected:
            c=pool[selection]
            key=(c['decision_index'],sign)
            if key not in cache:
                decision=c['decision_index']
                fill=decision+1
                cache[key]=dict(control_id=f'{r["trade_date"]}-{decision+1}-{r["side"]}',trade_date=r['trade_date'],
                    contract_id=r['contract_id'],side=r['side'],scheduled_event_ns=str(c['scheduled_ns']),
                    signal_ordinal=decision+1,signal_event_ns=str(c['ns']),signal_time_ny=ny_time(c['ns']),
                    entry_ordinal=fill+1,entry_event_ns=str(int(tape['ns'][fill])),entry_time_ny=ny_time(int(tape['ns'][fill])),
                    entry_price=float(tape['price'][fill]),entry_band_size=c['band'],
                    band_source_ordinal=c['band_source_ordinal'],volatility=c['volatility'],
                    volatility_bar_end_ns=c['bar_end_ns'],volatility_bar_available_ns=c['bar_available_ns'],
                    **label_path(tape,fill,sign,c['band'],cutoff))
            linked.append(cache[key])
            controls.append(dict(opportunity_id=r['opportunity_id'],minutes_before_iti=(int(r['signal_event_ns'])-c['ns'])/MINUTE,
                volatility_ratio=c['volatility']/vol['volatility'],band_ratio=c['band']/r['entry_band_size'],**cache[key]))
        rate=sum(c['target_hit'] for c in linked)/MATCHES
        pairs.append(dict(opportunity_id=r['opportunity_id'],trade_date=r['trade_date'],side=r['side'],
            baseline_status=r['baseline_status'],iti_signal_ordinal=r['signal_ordinal'],iti_signal_time_ny=r['signal_time_ny'],
            iti_signal_event_ns=r['signal_event_ns'],iti_entry_band_size=r['entry_band_size'],
            iti_volatility=vol['volatility'],iti_volatility_bar_end_ns=vol['bar_end_ns'],iti_volatility_bar_available_ns=vol['bar_available_ns'],
            iti_target_hit=r['target_hit'],iti_outcome=r['outcome'],eligible_controls=count,
            matched_controls=MATCHES,control_target_rate=rate,paired_difference=float(r['target_hit'])-rate,
            control_stop_rate=sum(c['outcome']=='Stop -1 band' for c in linked)/MATCHES,
            control_cutoff_rate=sum(c['outcome']=='Session cutoff' for c in linked)/MATCHES))
    return pairs,controls,unmatched,len(tape),len(pool),len(events)


def summarize(pairs):
    if not pairs:
        return dict(events=0)
    weeks=collections.defaultdict(list)
    for r in pairs:
        date=dt.date.fromisoformat(r['trade_date'])
        weeks[date-dt.timedelta(days=date.weekday())].append(r['paired_difference'])
    totals=np.array([(sum(values),len(values)) for values in weeks.values()])
    rng=np.random.default_rng(SEED)
    samples=totals[rng.integers(0,len(totals),size=(5000,len(totals)))].sum(axis=1)
    intervals=np.quantile(samples[:,0]/samples[:,1],[.025,.975])
    return dict(events=len(pairs),trading_dates=len({r['trade_date'] for r in pairs}),calendar_weeks=len(weeks),
        iti_target_hits=sum(r['iti_target_hit'] for r in pairs),iti_target_rate=float(np.mean([r['iti_target_hit'] for r in pairs])),
        matched_control_target_rate=float(np.mean([r['control_target_rate'] for r in pairs])),
        paired_difference=float(np.mean([r['paired_difference'] for r in pairs])),weekly_bootstrap_ci95=list(intervals),
        iti_cutoff_rate=sum(r['iti_outcome']=='Session cutoff' for r in pairs)/len(pairs),
        control_cutoff_rate=float(np.mean([r['control_cutoff_rate'] for r in pairs])))


def write_csv(path,records):
    with path.open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(records[0]) if records else ['opportunity_id'])
        writer.writeheader()
        writer.writerows(records)


def main(output=OUTPUT):
    output.mkdir(parents=True,exist_ok=True)
    assert not (output/'protocol.json').exists(),'Existing evidence retained'
    opportunities=json.loads((INPUT/'opportunities.json').read_text())
    proof=json.loads((INPUT/'independent-verification.json').read_text())
    assert proof['complete'] and proof['verified_opportunities']==len(opportunities)
    discovery=json.loads((INPUT/'analysis.json').read_text())
    baseline=json.loads((Path(discovery['protocol']['baseline_directory'])/'analysis.json').read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    replay=json.loads((Path(baseline['source_run_directory'])/'input.json.results.json').read_text())
    assert manifest['input_sha256']==replay['input_sha256']==baseline['input_sha256']
    protocol=dict(opportunity_directory=str(INPUT),baseline_directory=discovery['protocol']['baseline_directory'],
        opportunity_sha256=hashlib.sha256((INPUT/'opportunities.json').read_bytes()).hexdigest(),
        input_sha256=manifest['input_sha256'],period='2025-11-01 inclusive through 2026-11-01 exclusive',
        primary='All structural ITI opportunities with five matched prior controls; full-year paired +2-band hit-rate difference',
        controls='Same NY date, contract and assigned direction; strictly preceding 15 minutes; five without replacement within each event, reuse across events allowed',
        grid='Decision at first trade at/after HH:MM:30, within five seconds; 09:45-15:45 NY; next source tick fill',
        no_recent_entry='No structural ITI entry trigger at or during the preceding 60 seconds; future triggers are not excluded',
        volatility='RMS of 14 completed one-minute close changes, warm and no more than 120 seconds stale; control/event ratio in [0.75,1.25]',
        bands='Latest published Daily ITI BandSize at the control decision row; control/event ratio in [0.9,1.1]; no borrowing future event band size',
        outcome='Each entry uses its own as-of band: +2 bands before inward-rounded -1-band stop, else 15:45 NY cutoff; same source trade-price fill conventions',
        selection_seed=SEED,matching_control_count=MATCHES,matching_uses_outcomes=False,calipers_widened=False,
        uncertainty='5000 whole-calendar-week bootstrap resamples of paired event-minus-control-mean differences; controls not treated as independent trades',
        secondary='Original executed-entry subset and fixed chronological blocks; exploratory, no threshold/model fitting',
        limitations=['Observational timing benchmark, not a causal experiment or portfolio P&L',
                    'Unmatched/early entries excluded from matched inference; coverage and balance reported',
                    'Earlier controls have slightly more time until 15:45; cutoff rates reported',
                    'Controls may experience later ITI events; excluding those would use future information',
                    'Band availability assumes ideal immediate publication, like the existing replay; no feed/execution latency',
                    'Entire year already informed development; not untouched validation'],
        strategy_backtest_run=False,model_fitted=False,parameters_searched=False)
    (output/'protocol.json').write_text(json.dumps(protocol,indent=2))
    dates=collections.defaultdict(list)
    for r in opportunities:
        dates[r['trade_date']].append(r)
    events=collections.defaultdict(list)
    for e in replay['signals']:
        if e['TimePeriod']==1 and e['ValueDate'] in dates:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    files=[f for f in manifest['files'] if f['value_date'] in dates]
    pairs=[]
    controls=[]
    unmatched=[]
    ticks=pools=signals=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        jobs=[(source,f,events[f['value_date']],dates[f['value_date']]) for f in files]
        for f,(ps,cs,us,n,p,e) in zip(files,pool.map(build_day,jobs)):
            pairs.extend(ps)
            controls.extend(cs)
            unmatched.extend(us)
            ticks+=n
            pools+=p
            signals+=e
            print(f['value_date'],len(ps),len(us),flush=True)
    assert len(pairs)+len(unmatched)==len(opportunities)
    assert len(controls)==MATCHES*len(pairs) and ticks==sum(f['rows'] for f in files)
    windows=[]
    for name,start,end in [('Nov-Jan','2025-11-01','2026-02-01'),('Feb-Oct','2026-02-01','2026-11-01'),
                           ('Feb-Apr','2026-02-01','2026-05-01'),('May-Jul','2026-05-01','2026-08-01'),('Aug-Oct','2026-08-01','2026-11-01')]:
        windows.append(dict(window=name,**summarize([r for r in pairs if start<=r['trade_date']<end])))
    report=dict(protocol=protocol,checks=dict(raw_source_ticks=ticks,daily_signals=signals,source_hashes_verified=True),
        coverage=dict(total_opportunities=len(opportunities),matched=len(pairs),unmatched=len(unmatched),
            fraction_matched=len(pairs)/len(opportunities),candidate_control_decisions=pools,
            executed_total=sum(r['baseline_status']=='Executed' for r in opportunities),
            executed_matched=sum(r['baseline_status']=='Executed' for r in pairs),
            unmatched_reasons=dict(collections.Counter(r['reason'] for r in unmatched))),
        primary=summarize(pairs),executed_subset=summarize([r for r in pairs if r['baseline_status']=='Executed']),
        windows=windows,balance={field:dict(mean=float(np.mean([r[field] for r in controls])),
            median=float(np.median([r[field] for r in controls])),minimum=min(r[field] for r in controls),maximum=max(r[field] for r in controls))
            for field in ('minutes_before_iti','volatility_ratio','band_ratio')},
        control_reuse=dict(matched_rows=len(controls),unique_controls=len({r['control_id'] for r in controls})))
    (output/'analysis.json').write_text(json.dumps(report,indent=2))
    (output/'pairs.json').write_text(json.dumps(pairs,indent=2))
    (output/'controls.json').write_text(json.dumps(controls,indent=2))
    (output/'unmatched.json').write_text(json.dumps(unmatched,indent=2))
    write_csv(output/'pairs.csv',pairs)
    write_csv(output/'controls.csv',controls)
    write_csv(output/'unmatched.csv',unmatched)
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=OUTPUT)
    args=parser.parse_args()
    main(args.output_dir)
