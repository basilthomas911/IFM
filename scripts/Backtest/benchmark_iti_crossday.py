"""Separate-earlier-date robustness check for the ITI matched timing benchmark."""
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
from discover_iti_entry_signals import label_path
from benchmark_iti_entry_timing import INPUT,MINUTE,SEED,MATCHES,volatility_bars,volatility_asof,match_daily_events,write_csv

OUTPUT=Path('outputs/iti-entry-crossday-benchmark-nov2025-oct2026')


def extract_day(job):
    source,f,day_index,events,opportunities=job
    path=source/f['file']
    with path.open('rb') as stream:
        assert hashlib.file_digest(stream,'sha256').hexdigest()==f['sha256']
    tape=np.loadtxt(path,delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows']
    bars=volatility_bars(tape)
    published,bands=match_daily_events(tape,events)
    triggers=np.array(sorted(int(r['signal_event_ns']) for r in opportunities),dtype=np.int64)
    date=dt.date.fromisoformat(f['value_date'])
    start=int(dt.datetime.combine(date,dt.time(9,45),NY).timestamp())*NS
    cutoff=int(dt.datetime.combine(date,dt.time(15,45),NY).timestamp())*NS
    pool=[]
    for scheduled in range(start+30*NS,cutoff,MINUTE):
        index=int(np.searchsorted(tape['ns'],scheduled))
        if index+1>=len(tape) or tape['ns'][index+1]>=cutoff or tape['ns'][index]-scheduled>5*NS:
            continue
        ns=int(tape['ns'][index])
        prior=int(np.searchsorted(triggers,ns,side='right'))-1
        if prior>=0 and ns-int(triggers[prior])<=MINUTE:
            continue
        vol=volatility_asof(index,tape,bars)
        latest=int(np.searchsorted(published,index,side='right'))-1
        if vol is None or latest<0 or not np.isfinite(bands[latest]) or bands[latest]<.25:
            continue
        pool.append(dict(pool_id=f'{f["value_date"]}-{index+1}',trade_date=f['value_date'],contract_id=f['contract_id'],
            day_index=day_index,clock_minutes=(ns-start)/MINUTE,scheduled_event_ns=str(scheduled),
            signal_ordinal=index+1,signal_event_ns=str(ns),entry_ordinal=index+2,entry_event_ns=str(int(tape['ns'][index+1])),
            entry_price=float(tape['price'][index+1]),band=float(bands[latest]),band_source_ordinal=int(published[latest])+1,**vol))
    features=[]
    for r in opportunities:
        vol=volatility_asof(r['signal_ordinal']-1,tape,bars)
        features.append(dict(id=r['opportunity_id'],day_index=day_index,contract_id=f['contract_id'],
            clock_minutes=(int(r['signal_event_ns'])-start)/MINUTE,band=r['entry_band_size'],
            volatility=vol['volatility'] if vol else None))
    return pool,features,len(tape)


def select_controls(event,pools):
    if event['volatility'] is None:
        return [],0
    candidates=[c for day in range(max(0,event['day_index']-20),event['day_index']) for c in pools.get(day,[])
        if c['contract_id']==event['contract_id'] and abs(c['clock_minutes']-event['clock_minutes'])<=15
        and .75<=c['volatility']/event['volatility']<=1.25 and .9<=c['band']/event['band']<=1.1]
    if len(candidates)<MATCHES:
        return [],len(candidates)
    seed=int.from_bytes(hashlib.sha256(f'{SEED}:{event["id"]}'.encode()).digest()[:8],'little')
    indices=np.random.default_rng(seed).choice(len(candidates),size=MATCHES,replace=False)
    return [candidates[int(i)] for i in sorted(indices)],len(candidates)


def label_controls(job):
    source,f,controls=job
    tape=np.loadtxt(source/f['file'],delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows']
    date=dt.date.fromisoformat(f['value_date'])
    cutoff=int(dt.datetime.combine(date,dt.time(15,45),NY).timestamp())*NS
    boundary=int(np.searchsorted(tape['ns'],cutoff))
    results=[]
    for c in controls:
        fill=c['entry_ordinal']-1
        assert float(tape['price'][fill])==c['entry_price'] and int(tape['ns'][fill])==int(c['entry_event_ns'])
        sign=1 if c['side']=='Long' else -1
        outcome=label_path(tape,fill,sign,c['band'],cutoff)
        # A separate prefix check ensures no earlier source trade touched either barrier.
        end=boundary if outcome['outcome']=='Session cutoff' else outcome['outcome_ordinal']-1
        prefix=sign*(tape['price'][fill+1:end]-c['entry_price'])
        assert np.all(prefix<2*c['band']) and np.all(prefix>-outcome['planned_risk_points'])
        results.append(dict(c,**outcome))
    return results


def summarize(pairs):
    if not pairs:
        return dict(events=0)
    weeks=collections.defaultdict(list)
    for r in pairs:
        date=dt.date.fromisoformat(r['trade_date'])
        weeks[date-dt.timedelta(days=date.weekday())].append(r['paired_difference'])
    values=np.array([(sum(weeks[w]),len(weeks[w])) for w in sorted(weeks)])
    length=min(5,len(values))
    blocks=math.ceil(len(values)/length)
    rng=np.random.default_rng(SEED)
    starts=rng.integers(0,len(values)-length+1,size=(5000,blocks))
    indices=(starts[:,:,None]+np.arange(length)).reshape(5000,-1)[:,:len(values)]
    samples=values[indices].sum(axis=1)
    return dict(events=len(pairs),calendar_weeks=len(weeks),
        iti_target_hits=sum(r['iti_target_hit'] for r in pairs),iti_target_rate=float(np.mean([r['iti_target_hit'] for r in pairs])),
        matched_control_target_rate=float(np.mean([r['control_target_rate'] for r in pairs])),
        paired_difference=float(np.mean([r['paired_difference'] for r in pairs])),
        moving_five_week_bootstrap_ci95=list(np.quantile(samples[:,0]/samples[:,1],[.025,.975])),
        iti_cutoff_rate=sum(r['iti_outcome']=='Session cutoff' for r in pairs)/len(pairs),
        control_cutoff_rate=float(np.mean([r['control_cutoff_rate'] for r in pairs])))


def main(output=OUTPUT):
    output.mkdir(parents=True,exist_ok=True)
    assert not (output/'protocol.json').exists(),'Existing evidence retained'
    opportunities=json.loads((INPUT/'opportunities.json').read_text())
    discovery=json.loads((INPUT/'analysis.json').read_text())
    baseline=json.loads((Path(discovery['protocol']['baseline_directory'])/'analysis.json').read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    replay=json.loads((Path(baseline['source_run_directory'])/'input.json.results.json').read_text())
    protocol=dict(opportunity_directory=str(INPUT),baseline_directory=discovery['protocol']['baseline_directory'],
        opportunity_sha256=hashlib.sha256((INPUT/'opportunities.json').read_bytes()).hexdigest(),
        input_sha256=manifest['input_sha256'],period='2025-11-01 inclusive through 2026-11-01 exclusive',
        motivation='Robustness to same-day prior controls sharing the pre-reversal path; specified after inspecting that favorable result, before separate-day outcomes',
        primary='Full-year paired +2-band hit-rate difference across all opportunities with five controls',
        controls='Five sampled without replacement from the preceding 20 source trading dates, strictly different earlier dates, same ES contract, assigned event trade direction',
        clock_time='NY decision clock within 15 minutes of ITI entry signal clock, both 09:45-15:45; control decision at first tick within five seconds of HH:MM:30; next tick fill',
        matching='Trailing 14 completed-minute RMS price change within 25%; own latest published Daily BandSize within 10%; no structural entry trigger in prior 60 seconds',
        outcomes='Same +2 own entry-time bands / inward-rounded -1 band / 15:45 cutoff; no outcome-based selection',
        seed=SEED,matching_control_count=MATCHES,calipers_widened=False,
        uncertainty='5000 moving-block bootstrap samples of five consecutive case calendar weeks to accommodate overlapping outcomes and prior-20-date control reuse',
        limitations=['Observational matched benchmark, not causal proof or portfolio P&L',
                    'Unmatched initial/contract-roll/volatility cases excluded; coverage reported',
                    'Whole year already used for development; not untouched validation',
                    'This robustness check follows the same-day result; both results retained and reported'],
        model_fitted=False,strategy_backtest_run=False,parameters_searched=False)
    (output/'protocol.json').write_text(json.dumps(protocol,indent=2))
    by_date=collections.defaultdict(list)
    for r in opportunities:
        by_date[r['trade_date']].append(r)
    events=collections.defaultdict(list)
    for e in replay['signals']:
        if e['TimePeriod']==1:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    files=manifest['files']
    pools={}
    features=[]
    ticks=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        jobs=[(source,f,i,events[f['value_date']],by_date[f['value_date']]) for i,f in enumerate(files)]
        for i,(controls,fs,n) in enumerate(pool.map(extract_day,jobs)):
            pools[i]=controls
            features.extend(fs)
            ticks+=n
            print('Extract',files[i]['value_date'],len(controls),flush=True)
    assert ticks==baseline['checks']['raw_source_ticks'] and len(features)==len(opportunities)
    (output/'control-pool.json').write_text(json.dumps([r for values in pools.values() for r in values]))
    (output/'event-matching-features.json').write_text(json.dumps(features,indent=2))
    originals={r['opportunity_id']:r for r in opportunities}
    selections=[]
    unmatched=[]
    needed={}
    for event in features:
        r=originals[event['id']]
        controls,count=select_controls(event,pools)
        if not controls:
            unmatched.append(dict(opportunity_id=event['id'],trade_date=r['trade_date'],baseline_status=r['baseline_status'],eligible_controls=count))
            continue
        ids=[]
        for c in controls:
            key=f'{c["pool_id"]}-{r["side"]}'
            needed[key]=dict(c,control_id=key,side=r['side'])
            ids.append(key)
        selections.append(dict(opportunity_id=event['id'],eligible_controls=count,control_ids=ids))
    # Freeze matching identities before any selected control is labeled.
    (output/'selection-before-outcomes.json').write_text(json.dumps(selections,indent=2))
    label_jobs=collections.defaultdict(list)
    for r in needed.values():
        label_jobs[r['trade_date']].append(r)
    labeled={}
    with ProcessPoolExecutor(max_workers=4) as pool:
        jobs=[(source,f,label_jobs[f['value_date']]) for f in files if f['value_date'] in label_jobs]
        for records in pool.map(label_controls,jobs):
            for r in records:
                labeled[r['control_id']]=r
    pairs=[]
    control_rows=[]
    fs_by_id={r['id']:r for r in features}
    for selection in selections:
        r=originals[selection['opportunity_id']]
        feature=fs_by_id[r['opportunity_id']]
        controls=[labeled[key] for key in selection['control_ids']]
        rate=sum(c['target_hit'] for c in controls)/MATCHES
        pairs.append(dict(opportunity_id=r['opportunity_id'],trade_date=r['trade_date'],baseline_status=r['baseline_status'],
            side=r['side'],iti_target_hit=r['target_hit'],iti_outcome=r['outcome'],control_target_rate=rate,
            paired_difference=float(r['target_hit'])-rate,
            control_cutoff_rate=sum(c['outcome']=='Session cutoff' for c in controls)/MATCHES))
        for c in controls:
            control_rows.append(dict(opportunity_id=r['opportunity_id'],trading_dates_before=feature['day_index']-c['day_index'],
                clock_difference_minutes=c['clock_minutes']-feature['clock_minutes'],
                volatility_ratio=c['volatility']/feature['volatility'],band_ratio=c['band']/feature['band'],**c))
    assert len(pairs)+len(unmatched)==len(opportunities) and len(control_rows)==MATCHES*len(pairs)
    windows=[]
    for name,start,end in [('Nov-Jan','2025-11-01','2026-02-01'),('Feb-Oct','2026-02-01','2026-11-01'),
                           ('Feb-Apr','2026-02-01','2026-05-01'),('May-Jul','2026-05-01','2026-08-01'),('Aug-Oct','2026-08-01','2026-11-01')]:
        windows.append(dict(window=name,**summarize([r for r in pairs if start<=r['trade_date']<end])))
    report=dict(protocol=protocol,checks=dict(raw_source_ticks=ticks,source_hashes_verified=True,control_first_touch_prefix_checked=True),
        coverage=dict(total_opportunities=len(opportunities),matched=len(pairs),unmatched=len(unmatched),
            fraction_matched=len(pairs)/len(opportunities),executed_total=sum(r['baseline_status']=='Executed' for r in opportunities),
            executed_matched=sum(r['baseline_status']=='Executed' for r in pairs)),
        primary=summarize(pairs),executed_subset=summarize([r for r in pairs if r['baseline_status']=='Executed']),windows=windows,
        control_reuse=dict(matched_rows=len(control_rows),unique_controls=len(labeled)),
        balance={key:dict(mean=float(np.mean([r[key] for r in control_rows])),minimum=min(r[key] for r in control_rows),maximum=max(r[key] for r in control_rows))
                 for key in ('trading_dates_before','clock_difference_minutes','volatility_ratio','band_ratio')})
    (output/'analysis.json').write_text(json.dumps(report,indent=2))
    for name,records in [('pairs',pairs),('controls',control_rows),('unmatched',unmatched)]:
        (output/(name+'.json')).write_text(json.dumps(records,indent=2))
        write_csv(output/(name+'.csv'),records)
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=OUTPUT)
    args=parser.parse_args()
    main(args.output_dir)
