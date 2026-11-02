"""Fixed descriptive post-exit paths, without selecting or backtesting a new strategy."""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import statistics
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from analyze_daily_reversion import NY,NS,ny_time


class Continuation:
    def __init__(self,trade,cutoff):
        self.trade=trade
        self.cutoff=cutoff
        self.sign=1 if trade['side']=='Long' else -1
        self.band=trade['entry_band_size']
        self.minimum=self.maximum=self.peak=trade['points']
        self.drawdown=0
        self.hits={}
        self.finished=False
        self.breakeven_ns=None

    def observe(self,ns,price,ordinal):
        assert not self.finished and ordinal>self.trade['exit_ordinal'] and ns<self.cutoff
        gain=self.sign*(price-self.trade['entry_price'])
        self.minimum=min(self.minimum,gain)
        self.maximum=max(self.maximum,gain)
        self.peak=max(self.peak,gain)
        self.drawdown=max(self.drawdown,self.peak-gain)
        if gain<=0 and self.breakeven_ns is None:
            self.breakeven_ns=ns
        if self.sign*(price-self.trade['stop_price'])<=0:
            self.finish(ns,price,ordinal,'Original stop')
            return
        for target in (2,3):
            if target not in self.hits and gain>=target*self.band:
                self.hits[target]=dict(time_ny=ny_time(ns),event_ns=str(ns),ordinal=ordinal,price=price,
                    observed_points=gain,minimum_points=self.minimum,
                    giveback_from_original_exit_points=max(0,self.trade['points']-self.minimum),
                    maximum_drawdown_points=self.drawdown,
                    minutes_after_original_exit=(ns-int(self.trade['exit_event_ns']))/NS/60)

    def finish(self,ns,price,ordinal,reason):
        assert not self.finished
        self.finished=True
        self.terminal=dict(time_ny=ny_time(ns),price=price,ordinal=ordinal,reason=reason,
                           points=self.sign*(price-self.trade['entry_price']))

    def result(self):
        assert self.finished
        t=self.trade
        r=dict(trade_id=t['trade_id'],trade_date=t['trade_date'],side=t['side'],
               entry_time_ny=t['entry_time_ny'],entry_price=t['entry_price'],entry_band_size=self.band,
               original_exit_time_ny=t['exit_time_ny'],original_exit_price=t['exit_price'],
               original_exit_points=t['points'],original_watermark_bands=t['watermark_bands'],
               original_stop_price=t['stop_price'],terminal_reason=self.terminal['reason'],
               terminal_time_ny=self.terminal['time_ny'],terminal_price=self.terminal['price'],
               terminal_points=self.terminal['points'],
               post_exit_minimum_points=self.minimum,post_exit_maximum_points=self.maximum,
               post_exit_maximum_drawdown_points=self.drawdown,
               revisited_entry_before_stop_or_cutoff=self.breakeven_ns is not None)
        for target in (2,3):
            hit=self.hits.get(target)
            r[f'target_{target}_hit']=hit is not None
            for k in ('time_ny','event_ns','ordinal','price','observed_points','minimum_points',
                      'giveback_from_original_exit_points','maximum_drawdown_points','minutes_after_original_exit'):
                r[f'target_{target}_{k}']=hit[k] if hit else None
        return r


def examine_day(job):
    source,f,trades=job
    cutoff=int(dt.datetime.combine(dt.date.fromisoformat(f['value_date']),dt.time(15,45),NY).timestamp())*NS
    opening={t['exit_ordinal']:t for t in trades}
    assert len(opening)==len(trades)
    active=[]
    results=[]
    previous=None
    digest=hashlib.sha256()
    seen=0
    with (source/f['file']).open('rb') as stream:
        for ordinal,line in enumerate(stream,1):
            digest.update(line)
            c=line.split(b',')
            ns,price=int(c[2]),float(c[4])
            if active:
                for state in active:
                    if ns>=cutoff:
                        mark=(ns,price,ordinal) if ns==cutoff else previous
                        state.finish(cutoff,mark[1],mark[2],'Session cutoff')
                    else:
                        state.observe(ns,price,ordinal)
                    if state.finished:
                        results.append(state.result())
                active=[s for s in active if not s.finished]
            if ordinal in opening:
                t=opening[ordinal]
                assert ns==int(t['exit_event_ns']) and price==t['exit_price'] and ns<cutoff
                assert t['contract_id']==f['contract_id']
                active.append(Continuation(t,cutoff))
                seen+=1
            previous=(ns,price,ordinal)
    for state in active:
        assert previous[0]<cutoff
        state.finish(cutoff,previous[1],previous[2],'Session cutoff')
        results.append(state.result())
    assert ordinal==f['rows'] and digest.hexdigest()==f['sha256']
    assert seen==len(trades)==len(results)
    return sorted(results,key=lambda r:r['original_exit_time_ny']),ordinal


def summarize(records):
    n=len(records)
    r=dict(exits=n,terminal_reasons=dict(collections.Counter(t['terminal_reason'] for t in records)),
           revisited_entry=sum(t['revisited_entry_before_stop_or_cutoff'] for t in records))
    for target in (2,3):
        hits=[t for t in records if t[f'target_{target}_hit']]
        def median(field):
            return statistics.median(t[f'target_{target}_{field}'] for t in hits) if hits else None
        r[f'target_{target}']=dict(hits=len(hits),percent=100*len(hits)/n if n else None,
            failed_stop=sum(not t[f'target_{target}_hit'] and t['terminal_reason']=='Original stop' for t in records),
            failed_cutoff=sum(not t[f'target_{target}_hit'] and t['terminal_reason']=='Session cutoff' for t in records),
            hit_paths_revisited_entry=sum(t[f'target_{target}_minimum_points']<=0 for t in hits),
            median_minutes_to_hit=median('minutes_after_original_exit'),
            median_giveback_from_original_exit_points=median('giveback_from_original_exit_points'),
            median_maximum_drawdown_points=median('maximum_drawdown_points'),
            max_giveback_from_original_exit_points=max((t[f'target_{target}_giveback_from_original_exit_points'] for t in hits),default=None))
        assert r[f'target_{target}']['hits']+r[f'target_{target}']['failed_stop']+r[f'target_{target}']['failed_cutoff']==n
    return r


def main(baseline_dir,output):
    output.mkdir(parents=True,exist_ok=True)
    assert not (output/'protocol.json').exists(),'Existing evidence retained'
    baseline_path=baseline_dir/'analysis.json'
    baseline=json.loads(baseline_path.read_text())
    proof=json.loads((baseline_dir/'independent-verification.json').read_text())
    assert proof['complete'] and proof['verified_trades']==baseline['totals']['trades']
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    assert manifest['input_sha256']==baseline['input_sha256']
    protocol=dict(baseline_directory=str(baseline_dir),baseline_analysis_sha256=hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
        targets_entry_time_bands=[2,3],primary_population='Original trailing-watermark exits with watermark level exactly one band',
        secondary_population='All original trailing-watermark exits',
        start='First source row after actual original exit ordinal, including later rows with the same timestamp',
        end='First original protective-stop touch/cross or 15:45 NY, whichever occurs first; targets are measured independently',
        thresholds='Original frozen entry BandSize; signed gain measured from original entry, not from exit; first observed trade at/beyond target',
        drawdown='Track minimum signed entry-relative profit and peak-to-trough drawdown from original exit through first target touch',
        no_parameter_search=True,no_strategy_changes=True,
        limitations=['Conditioned on original profitable watermark exits; not all entries',
                    'Hypothetical continuations can overlap later original trades; not portfolio P&L or a full strategy replay',
                    'No other profit exit after original watermark exit; descriptive room available until original stop or cutoff',
                    'Entire year has already informed strategy development; no untouched holdout claimed',
                    'Trade-price path only; no executable quotes; holiday cutoff uses last available trade'])
    (output/'protocol.json').write_text(json.dumps(protocol,indent=2))
    dates={d['trade_date'] for d in baseline['daily']}
    files=[f for f in manifest['files'] if f['value_date'] in dates]
    trades=[t for t in baseline['trades'] if t['exit_reason']=='Trailing watermark']
    by_date=collections.defaultdict(list)
    for t in trades:
        by_date[t['trade_date']].append(t)
    results=[]
    rows=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        for f,(records,count) in zip(files,pool.map(examine_day,[(source,f,by_date[f['value_date']]) for f in files])):
            results.extend(records)
            rows+=count
            print(f['value_date'],len(records),flush=True)
    assert rows==baseline['checks']['raw_source_ticks'] and len(results)==len(trades)
    initial=[r for r in results if r['original_watermark_bands']==1]
    monthly=[dict(month=month,**summarize([r for r in initial if r['trade_date'].startswith(month)]))
             for month in sorted({d[:7] for d in dates})]
    report=dict(protocol=protocol,checks=dict(source_rows=rows,source_hashes_verified=True,original_exits_matched=True),
                initial_one_band=summarize(initial),all_watermark=summarize(results),monthly_initial_one_band=monthly,paths=results)
    (output/'analysis.json').write_text(json.dumps(report,indent=2))
    with (output/'continuation-paths.csv').open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(results[0]))
        writer.writeheader()
        writer.writerows(results)
    print(json.dumps({k:v for k,v in report.items() if k not in ('paths','protocol')},indent=2),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-dir',type=Path,default=Path('outputs/iti-nov2025-oct2026-one-reversal-one-band-one-loss-watermark'))
    parser.add_argument('--output-dir',type=Path,default=Path('outputs/iti-nov2025-oct2026-watermark-continuation'))
    args=parser.parse_args()
    main(args.baseline_dir,args.output_dir)
