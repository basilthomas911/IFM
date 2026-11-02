"""Fixed three-feature entry-opportunity diagnostic; no fitted trading rule or P&L replay."""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import math
import statistics
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
import numpy as np
from analyze_daily_reversion import NY,NS,event_ns,ny_time
from analyze_two_reversal_two_band import stop_distance_ticks

BASELINE=Path('outputs/iti-nov2025-oct2026-one-reversal-one-band-one-loss-watermark')
OUTPUT=Path('outputs/iti-entry-signal-discovery-nov2025-oct2026')
MINUTE=60*NS
FEATURES=('reversal_speed_bands_per_minute','preceding_trend_efficiency','aligned_rsi_slope')
RSI_ZERO_TOLERANCE=1e-10


def indicator_arrays(closes,period=14):
    """Wilder RSI and signed price-path efficiency from closed bars only."""
    rsi=np.full(len(closes),np.nan)
    slope=np.full(len(closes),np.nan)
    efficiency=np.full(len(closes),np.nan)
    average_gain=average_loss=None
    seed_gain=seed_loss=0.
    changes=np.diff(closes)
    for i in range(1,len(closes)):
        change=float(changes[i-1])
        gain,loss=max(change,0),max(-change,0)
        if i<=period:
            seed_gain+=gain
            seed_loss+=loss
            if i==period:
                average_gain,average_loss=seed_gain/period,seed_loss/period
        else:
            average_gain=(average_gain*(period-1)+gain)/period
            average_loss=(average_loss*(period-1)+loss)/period
        if average_gain is not None:
            rsi[i]=(50. if average_gain==0 else 100.) if average_loss==0 else 100.-100./(1.+average_gain/average_loss)
            if np.isfinite(rsi[i-1]):
                slope[i]=rsi[i]-rsi[i-1]
        if i>=period:
            distance=float(np.abs(changes[i-period:i]).sum())
            efficiency[i]=(float(closes[i]-closes[i-period])/distance) if distance else 0.
    return rsi,slope,efficiency


def closed_bars(tape):
    minute=tape['ns']//MINUTE
    available=np.flatnonzero(minute[1:]!=minute[:-1])+1
    ends=(minute[available-1]+1)*MINUTE
    closes=tape['price'][available-1]
    rsi,slope,efficiency=indicator_arrays(closes)
    return available,ends,rsi,slope,efficiency


def asof_features(index,tape,bars):
    available,ends,rsi,slope,efficiency=bars
    j=int(np.searchsorted(available,index,side='right'))-1
    if j<0:
        return dict(bar_end_ns=None,bar_available_ns=None,rsi=None,slope=None,efficiency=None)
    assert available[j]<=index and ends[j]<=tape['ns'][index]
    fresh=int(tape['ns'][index])-int(ends[j])<=120*NS
    number=lambda values:float(values[j]) if fresh and np.isfinite(values[j]) else None
    return dict(bar_end_ns=str(int(ends[j])),bar_available_ns=str(int(tape['ns'][available[j]])),
                rsi=number(rsi),slope=number(slope),efficiency=number(efficiency))


def label_path(tape,fill,sign,band,cutoff):
    price=float(tape['price'][fill])
    risk=stop_distance_ticks(band,1)/4
    boundary=int(np.searchsorted(tape['ns'],cutoff,side='left'))
    assert fill<boundary
    gains=sign*(tape['price'][fill+1:boundary]-price)
    stops=np.flatnonzero(gains<=-risk)
    targets=np.flatnonzero(gains>=2*band)
    stop=int(stops[0]) if len(stops) else len(gains)
    target=int(targets[0]) if len(targets) else len(gains)
    if min(stop,target)<len(gains):
        offset=min(stop,target)
        exit_index=fill+1+offset
        reason='Target +2 bands' if target<stop else 'Stop -1 band'
        exit_ns=int(tape['ns'][exit_index])
    else:
        exit_index=boundary if boundary<len(tape) and tape['ns'][boundary]==cutoff else boundary-1
        exit_ns=cutoff
        reason='Session cutoff'
    return dict(outcome=reason,target_hit=reason=='Target +2 bands',
        planned_risk_points=risk,stop_price=price-sign*risk,target_price=price+sign*2*band,
        outcome_ordinal=exit_index+1,outcome_time_ny=ny_time(exit_ns),outcome_event_ns=str(exit_ns),
        outcome_price=float(tape['price'][exit_index]))


def baseline_status(signal_index,group,trades):
    ordinal=signal_index+1
    if any(t['entry_signal_ordinal']==ordinal for t in trades):
        return 'Executed'
    if any(t['gross_pnl_usd']<0 and t['exit_ordinal']<=ordinal for t in trades):
        return 'Daily loss halt'
    if any(t['entry_ordinal']<=ordinal<t['exit_ordinal'] for t in trades):
        return 'Position open'
    if any(t['setup_group']==group and t['entry_signal_ordinal']<ordinal for t in trades):
        return 'Excursion already used'
    return 'Other/unfilled'


def build_day(job):
    source,f,events,trades=job
    path=source/f['file']
    with path.open('rb') as stream:
        assert hashlib.file_digest(stream,'sha256').hexdigest()==f['sha256']
    tape=np.loadtxt(path,delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows'] and np.all(tape['ns'][1:]>=tape['ns'][:-1])
    bars=closed_bars(tape)
    hundred_ns=tape['ns']//100
    day=dt.date.fromisoformat(f['value_date'])
    start=int(dt.datetime.combine(day,dt.time(9,45),NY).timestamp())*NS
    cutoff=int(dt.datetime.combine(day,dt.time(15,45),NY).timestamp())*NS
    group=trend=qualification=extreme_index=extreme_price=None
    reversal_count=0
    last=-1
    rows=[]
    cancelled=[]
    for e in events:
        left=max(last+1,int(np.searchsorted(hundred_ns,e['_ns100'],side='left')))
        right=int(np.searchsorted(hundred_ns,e['_ns100'],side='right'))
        choices=np.flatnonzero(tape['price'][left:right]==float(e['IntrinsicPrice']))
        assert len(choices),'Every Daily event must match its next source row'
        index=left+int(choices[0])
        last=index
        ns=int(tape['ns'][index])
        mode,g,tr=e['IntrinsicTimeMode'],e['IntrinsicTimeGroupId'],e['IntrinsicTimeTrend']
        if mode==2 or group!=g:
            group,trend=g,tr
            qualification=None
            extreme_index=index
            extreme_price=float(e['TrendExtreme'])
            reversal_count=0
        elif mode in (1,7):
            extreme_index=index
            extreme_price=float(e['TrendExtreme'])
            reversal_count=0
            if mode==7:
                qualification=index
        elif mode in (3,8) and qualification is not None and start<=ns<cutoff:
            reversal_count+=1
            if reversal_count!=1:
                continue
            sign=-1 if trend==0 else 1
            band=float(e['BandSize'])
            assert math.isfinite(band) and band>0
            fill=index+1
            if fill>=len(tape) or tape['ns'][fill]>=cutoff:
                cancelled.append(dict(trade_date=f['value_date'],signal_ordinal=index+1,reason='No next tick before cutoff'))
                continue
            current=asof_features(index,tape,bars)
            prior=asof_features(extreme_index,tape,bars)
            elapsed=(ns-int(tape['ns'][extreme_index]))/NS/60
            distance=sign*(float(tape['price'][index])-extreme_price)/band
            r=dict(opportunity_id=f'{f["value_date"]}-{index+1}',trade_date=f['value_date'],contract_id=f['contract_id'],
                setup_group=group,side='Long' if sign==1 else 'Short',signal_mode=mode,
                signal_ordinal=index+1,signal_time_ny=ny_time(ns),signal_event_ns=str(ns),
                entry_ordinal=fill+1,entry_time_ny=ny_time(int(tape['ns'][fill])),entry_event_ns=str(int(tape['ns'][fill])),
                entry_price=float(tape['price'][fill]),entry_band_size=band,
                qualification_ordinal=qualification+1,extreme_ordinal=extreme_index+1,
                extreme_event_ns=str(int(tape['ns'][extreme_index])),extreme_price=extreme_price,
                reversal_elapsed_minutes=elapsed,reversal_distance_bands=distance,
                reversal_speed_bands_per_minute=distance/elapsed if elapsed>0 else None,
                preceding_trend_efficiency=-sign*prior['efficiency'] if prior['efficiency'] is not None else None,
                preceding_bar_end_ns=prior['bar_end_ns'],preceding_bar_available_ns=prior['bar_available_ns'],
                rsi14=current['rsi'],rsi14_slope=current['slope'],
                aligned_rsi_slope=sign*current['slope'] if current['slope'] is not None else None,
                rsi_bar_end_ns=current['bar_end_ns'],rsi_bar_available_ns=current['bar_available_ns'],
                baseline_status=baseline_status(index,group,trades))
            assert index>extreme_index and qualification<=index and distance>0
            r.update(label_path(tape,fill,sign,band,cutoff))
            rows.append(r)
    executed={r['signal_ordinal'] for r in rows if r['baseline_status']=='Executed'}
    assert executed=={t['entry_signal_ordinal'] for t in trades}
    for r in rows:
        if r['baseline_status']=='Executed':
            t=next(t for t in trades if t['entry_signal_ordinal']==r['signal_ordinal'])
            assert r['entry_ordinal']==t['entry_ordinal'] and r['entry_price']==t['entry_price']
            assert r['stop_price']==t['stop_price'] and r['entry_band_size']==t['entry_band_size']
    return rows,cancelled,len(tape),len(events),len(bars[0])


def summarize(rows):
    n=len(rows)
    return dict(opportunities=n,trading_dates=len({r['trade_date'] for r in rows}),
                target_hits=sum(r['target_hit'] for r in rows),
                target_rate=sum(r['target_hit'] for r in rows)/n if n else None,
                outcomes=dict(collections.Counter(r['outcome'] for r in rows)))


def group_name(row,feature,thresholds):
    value=row[feature]
    if value is None:
        return 'Missing'
    if feature=='aligned_rsi_slope':
        if abs(value)<=RSI_ZERO_TOLERANCE:
            return 'Flat'
        return 'Aligned' if value>0 else 'Opposed' if value<0 else 'Flat'
    return 'High' if value>thresholds[feature] else 'Low'


def week_bootstrap(rows,feature,thresholds):
    """Resample whole calendar weeks, retaining all within-week overlapping setups."""
    positive,negative=('Aligned','Opposed') if feature=='aligned_rsi_slope' else ('High','Low')
    weeks=collections.defaultdict(lambda:np.zeros(4))
    for r in rows:
        group=group_name(r,feature,thresholds)
        if group not in (positive,negative):
            continue
        date=dt.date.fromisoformat(r['trade_date'])
        monday=date-dt.timedelta(days=date.weekday())
        offset=0 if group==positive else 2
        weeks[monday][offset]+=r['target_hit']
        weeks[monday][offset+1]+=1
    values=np.array(list(weeks.values()))
    if not len(values):
        return None
    totals=values.sum(axis=0)
    if not totals[1] or not totals[3]:
        return None
    rng=np.random.default_rng(271828)
    samples=values[rng.integers(0,len(values),size=(2000,len(values)))].sum(axis=1)
    valid=(samples[:,1]>0)&(samples[:,3]>0)
    differences=samples[valid,0]/samples[valid,1]-samples[valid,2]/samples[valid,3]
    return dict(contrast=f'{positive} minus {negative}',difference=totals[0]/totals[1]-totals[2]/totals[3],
                weeks=len(weeks),bootstrap_samples=2000,ci95=list(np.quantile(differences,[.025,.975])),
                ci98_33_three_comparisons=list(np.quantile(differences,[.05/6,1-.05/6])))


def analyze(rows):
    development=[r for r in rows if r['trade_date']<'2026-02-01']
    thresholds={f:statistics.median(r[f] for r in development if r[f] is not None) for f in FEATURES[:2]}
    windows=[('Discovery: Nov-Jan','2025-11-01','2026-02-01'),
             ('Later internal replication: Feb-Oct','2026-02-01','2026-11-01'),
             ('Feb-Apr','2026-02-01','2026-05-01'),('May-Jul','2026-05-01','2026-08-01'),
             ('Aug-Oct','2026-08-01','2026-11-01')]
    reports=[]
    for name,start,end in windows:
        selected=[r for r in rows if start<=r['trade_date']<end]
        comparisons=[]
        for feature in FEATURES:
            groups=collections.defaultdict(list)
            for r in selected:
                groups[group_name(r,feature,thresholds)].append(r)
            comparisons.append(dict(feature=feature,groups={k:summarize(v) for k,v in sorted(groups.items())},
                                    weekly_bootstrap=week_bootstrap(selected,feature,thresholds)))
        reports.append(dict(window=name,start_date=start,end_date_exclusive=end,totals=summarize(selected),features=comparisons))
    return dict(thresholds_from_discovery_only=thresholds,windows=reports)


def main(output=OUTPUT):
    output.mkdir(parents=True,exist_ok=True)
    assert not (output/'protocol.json').exists(),'Existing evidence retained'
    baseline_path=BASELINE/'analysis.json'
    baseline=json.loads(baseline_path.read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    replay=json.loads((Path(baseline['source_run_directory'])/'input.json.results.json').read_text())
    assert manifest['input_sha256']==replay['input_sha256']==baseline['input_sha256']
    protocol=dict(baseline_directory=str(BASELINE),baseline_sha256=hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
        period='2025-11-01 inclusive through 2026-11-01 exclusive, NY trading dates',
        population='First ordinary/overshoot reversal after an observed overshoot and each extreme reset; includes structurally qualified setups blocked by positions, prior excursion use, or the daily loss halt',
        label='Next source tick fill; first +2 entry-time bands vs original inward-rounded -1-band stop, or 15:45 cutoff; observed prices; no other profit exit',
        features=list(FEATURES),
        reversal_speed='Trade-direction distance from latest observed ITI extreme / signal BandSize / elapsed minutes',
        preceding_strength='Signed efficiency of 14 completed one-minute close changes at the latest ITI extreme; net change / sum absolute changes, aligned with preceding ITI trend',
        rsi='Wilder RSI14 slope from latest completed one-minute trade bar at signal, signed to trade direction; no developing bar',
        rsi_numerical_zero_tolerance=RSI_ZERO_TOLERANCE,
        bar_rules='UTC minute boundaries; no synthetic empty bars; reset each NY source date and contract, warm on available overnight ticks; observations >120 seconds past bar end are missing',
        discovery='Nov-Jan feature medians for speed/efficiency; RSI sign groups; no outcome-based threshold search',
        internal_replication='Feb-Oct plus fixed Feb-Apr/May-Jul/Aug-Oct blocks; all dates already used for strategy development, not an untouched holdout',
        uncertainty='2000 whole-calendar-week bootstrap samples, seed 271828; 95% and 98.33% intervals for three feature contrasts; exploratory, not proof of an edge',
        strategy_backtest_run=False,model_fitted=False,parameters_searched=False,
        limitations=['Overlapping opportunities are correlated; no portfolio P&L inferred',
                    'Price-path labels differ from current stepped-watermark trade wins',
                    'Three exploratory features; no combinations or selected profitable regimes',
                    'Indicators reconstructed for this diagnostic; not a replay of every production indicator actor'])
    (output/'protocol.json').write_text(json.dumps(protocol,indent=2))
    events=collections.defaultdict(list)
    for e in replay['signals']:
        if e['TimePeriod']==1:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    trades=collections.defaultdict(list)
    for t in baseline['trades']:
        trades[t['trade_date']].append(t)
    dates={d['trade_date'] for d in baseline['daily']}
    files=[f for f in manifest['files'] if f['value_date'] in dates]
    results=[]
    cancelled=[]
    ticks=signals=bars=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        jobs=[(source,f,events[f['value_date']],trades[f['value_date']]) for f in files]
        for f,(rs,cs,n,e,b) in zip(files,pool.map(build_day,jobs)):
            results.extend(rs)
            cancelled.extend(cs)
            ticks+=n
            signals+=e
            bars+=b
            print(f['value_date'],len(rs),flush=True)
    assert ticks==baseline['checks']['raw_source_ticks'] and signals==baseline['checks']['daily_signals']
    assert sum(r['baseline_status']=='Executed' for r in results)==baseline['totals']['trades']
    report=dict(protocol=protocol,checks=dict(raw_source_ticks=ticks,daily_signals=signals,closed_bars=bars,
                source_hashes_verified=True,all_baseline_entries_reconciled=True),
                totals=summarize(results),baseline_statuses=dict(collections.Counter(r['baseline_status'] for r in results)),
                cancelled_signals=cancelled,analysis=analyze(results))
    (output/'analysis.json').write_text(json.dumps(report,indent=2))
    (output/'opportunities.json').write_text(json.dumps(results,indent=2))
    with (output/'opportunities.csv').open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(results[0]))
        writer.writeheader()
        writer.writerows(results)
    flat=[]
    for window in report['analysis']['windows']:
        for feature in window['features']:
            for group,summary in feature['groups'].items():
                flat.append(dict(window=window['window'],feature=feature['feature'],group=group,
                    opportunities=summary['opportunities'],target_hits=summary['target_hits'],target_rate=summary['target_rate'],
                    stop_outcomes=summary['outcomes'].get('Stop -1 band',0),cutoff_outcomes=summary['outcomes'].get('Session cutoff',0)))
    with (output/'feature-comparison.csv').open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(flat[0]))
        writer.writeheader()
        writer.writerows(flat)
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=OUTPUT)
    args=parser.parse_args()
    main(args.output_dir)
