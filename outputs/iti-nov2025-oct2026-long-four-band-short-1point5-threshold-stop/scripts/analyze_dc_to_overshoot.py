"""Causal Daily ES DC entries with configurable overshoot exits and session."""
import collections
import csv
import hashlib
import json
import math
import argparse
import datetime as dt
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from analyze_daily_reversion import DaySimulator, event_ns, ny_time, price_ticks, NY, NS


class DcSimulator:
    def __init__(self, date, policy, stop_bands=None, exit_rule='overshoot', start_time='03:00', daily_loss_limit=None):
        bounds = DaySimulator(date)
        self.date, self.policy = date, policy
        self.start, self.end = bounds.start, bounds.end
        self.start = int(dt.datetime.combine(dt.date.fromisoformat(date), dt.time.fromisoformat(start_time), NY).timestamp())*NS
        if self.start >= self.end:
            raise ValueError('Session start must precede 15:45 New York')
        self.previous = self.position = self.pending_entry = self.pending_exit = None
        self.finished = False
        self.ordinal = 0
        self.trades = []
        self.stop_bands = stop_bands
        self.canceled_entries = 0
        if daily_loss_limit is not None and (not isinstance(daily_loss_limit,int) or daily_loss_limit<1):
            raise ValueError('Daily loss limit must be a positive integer')
        self.daily_loss_limit=daily_loss_limit
        self.losses=0
        self.entry_blocked=False
        assert exit_rule in ('overshoot','first-reversal')
        self.exit_rule = exit_rule

    def close(self, ns, price, reason, reference_ns=None):
        p = self.position
        points = p['direction']*(price-p['price'])/4
        self.trades.append(dict(trade_date=self.date, side='Long' if p['direction']==1 else 'Short',
            entry_time_ny=ny_time(p['ns']), entry_price=p['price']/4,
            entry_signal_time_ny=ny_time(p['signal_ns']), setup_group=p['group'],
            dc_price=p['dc_ticks']/4, dc_band_size=p['band_size'],
            stop_price=p['stop_ticks']/4 if p['stop_ticks'] is not None else None,
            overshoot_time_ny=ny_time(p['overshoot_ns']) if p.get('overshoot_ns') is not None else None,
            overshoot_group=p.get('overshoot_group'),
            exit_time_ny=ny_time(ns), exit_price=price/4, exit_reason=reason,
            exit_signal_time_ny=ny_time(self.pending_exit['ns']) if self.pending_exit else None,
            exit_price_reference_time_ny=ny_time(reference_ns if reference_ns is not None else ns),
            entry_event_ns=str(p['ns']), exit_event_ns=str(ns),
            entry_ordinal=p['ordinal'], entry_signal_ordinal=p['signal_ordinal'],
            exit_ordinal=self.ordinal,
            exit_signal_ordinal=self.pending_exit['ordinal'] if self.pending_exit else None,
            points=points, gross_pnl_usd=points*50,
            max_favorable_points=p['mfe']/4, max_adverse_points=p['mae']/4))
        self.position = self.pending_exit = None
        if points<0:
            self.losses+=1
        if self.daily_loss_limit is not None and self.losses>=self.daily_loss_limit:
            self.entry_blocked=True
            self.pending_entry=None

    def tick(self, ns, price, event=None):
        self.ordinal += 1
        assert self.previous is None or self.previous[0] <= ns
        if not self.finished and ns >= self.end:
            if self.position:
                mark=(ns,price) if ns==self.end else self.previous
                self.close(self.end,mark[1],'Session flatten',mark[0])
            self.pending_entry = self.pending_exit = None
            self.finished = True
        if self.start <= ns < self.end and not self.finished:
            if self.position:
                p=self.position
                move=p['direction']*(price-p['price'])
                p['mfe'],p['mae']=max(p['mfe'],move),min(p['mae'],move)
                if p['stop_ticks'] is not None and p['direction']*(price-p['stop_ticks'])<=0:
                    self.pending_exit=None
                    self.close(ns,price,'Two-band stop')
            if self.pending_exit:
                self.close(ns,price,self.pending_exit['reason'])
            if self.pending_entry:
                assert self.position is None
                pending=self.pending_entry
                if pending['stop_ticks'] is not None and pending['direction']*(price-pending['stop_ticks'])<=0:
                    self.canceled_entries+=1
                else:
                    self.position=dict(pending,ns=ns,price=price,ordinal=self.ordinal,mfe=0,mae=0)
                self.pending_entry=None
            if event and not self.entry_blocked:
                direction=1 if event['IntrinsicTimeTrend']==0 else -1
                mode=event['IntrinsicTimeMode']
                if mode==2:
                    if self.position:
                        self.position.pop('overshoot_group',None)
                        self.position.pop('overshoot_ns',None)
                    if self.position and self.policy in ('reverse','exit-dc') and self.position['direction']!=direction:
                        self.pending_exit=dict(reason='Opposite DC',ns=ns,ordinal=self.ordinal)
                    if self.position is None or (self.pending_exit and self.policy=='reverse'):
                        band_size=float(event['BandSize']) if self.stop_bands is not None else None
                        if band_size is not None:
                            assert math.isfinite(band_size) and band_size>0
                        raw_stop=price-direction*self.stop_bands*band_size*4 if self.stop_bands is not None else None
                        stop=(math.floor(raw_stop) if direction==1 else math.ceil(raw_stop)) if raw_stop is not None else None
                        self.pending_entry=dict(direction=direction,group=event['IntrinsicTimeGroupId'],
                            signal_ns=ns,signal_ordinal=self.ordinal,dc_ticks=price,band_size=band_size,stop_ticks=stop)
                elif mode==7 and self.position and self.position['direction']==direction and (self.policy!='exit-dc' or self.position['group']==event['IntrinsicTimeGroupId']):
                    if self.exit_rule=='overshoot':
                        self.pending_exit=dict(reason='First overshoot',ns=ns,ordinal=self.ordinal)
                    elif self.position.get('overshoot_group')!=event['IntrinsicTimeGroupId']:
                        self.position.update(overshoot_group=event['IntrinsicTimeGroupId'],overshoot_ns=ns)
                elif mode in (3,8) and self.position and self.exit_rule=='first-reversal':
                    if self.position['direction']==direction and self.position.get('overshoot_group')==event['IntrinsicTimeGroupId']:
                        self.pending_exit=dict(reason='First reversal after overshoot',ns=ns,ordinal=self.ordinal)
        self.previous=(ns,price)

    def finish(self):
        # Short holiday sessions have no print at/after the scheduled cutoff.
        # Keep the same last-observed-price marking convention as ordinary days.
        if not self.finished and self.previous is not None:
            if self.position:
                self.close(self.end,self.previous[1],'Session flatten',self.previous[0])
            self.pending_entry=self.pending_exit=None
            self.finished=True
        assert self.finished and self.position is None and self.pending_entry is None
        for t in self.trades:
            assert t['entry_ordinal']==t['entry_signal_ordinal']+1
            assert int(t['entry_event_ns'])<int(t['exit_event_ns']) or t['entry_ordinal']<t['exit_ordinal']
            if t['exit_reason'] not in ('Session flatten','Two-band stop'):
                assert t['exit_ordinal']==t['exit_signal_ordinal']+1
            assert self.start<=int(t['entry_event_ns'])<self.end
            assert int(t['exit_event_ns'])<=self.end
            sign=1 if t['side']=='Long' else -1
            assert t['points']==sign*(t['exit_price']-t['entry_price'])
            assert t['gross_pnl_usd']==t['points']*50
            if t['stop_price'] is not None:
                assert sign*(t['entry_price']-t['stop_price'])>0
                if t['exit_reason']=='Two-band stop':
                    assert sign*(t['exit_price']-t['stop_price'])<=0
        for a,b in zip(self.trades,self.trades[1:]):
            assert a['exit_ordinal']<=b['entry_ordinal']
        if self.daily_loss_limit is not None:
            assert self.losses<=self.daily_loss_limit
            if self.losses==self.daily_loss_limit:
                assert self.trades[-1]['points']<0
        return self.trades


def summarize(trades):
    positive=sum(t['gross_pnl_usd'] for t in trades if t['gross_pnl_usd']>0)
    negative=sum(t['gross_pnl_usd'] for t in trades if t['gross_pnl_usd']<0)
    cumulative=peak=dd=0
    for t in trades:
        cumulative+=t['gross_pnl_usd']
        peak=max(peak,cumulative)
        dd=max(dd,peak-cumulative)
    return dict(trades=len(trades),wins=sum(t['points']>0 for t in trades),
        losses=sum(t['points']<0 for t in trades),flats=sum(t['points']==0 for t in trades),
        points=sum(t['points'] for t in trades),gross_pnl_usd=cumulative,
        profit_factor=positive/-negative if negative else None,max_closed_trade_drawdown_usd=dd,
        exit_reasons=dict(collections.Counter(t['exit_reason'] for t in trades)))


def analyze_day(job):
    f,src,daily_events,policies,stop_bands,exit_rule,start_time,daily_loss_limit=job
    date=f['value_date']
    sims={p:DcSimulator(date,p,stop_bands,exit_rule,start_time,daily_loss_limit) for p in policies}
    i=rows=0
    digest=hashlib.sha256()
    with (Path(src)/f['file']).open('rb') as stream:
        for line in stream:
            digest.update(line)
            c=line.split(b',')
            ns,price=int(c[2]),price_ticks(c[4])
            e=daily_events[i] if i<len(daily_events) else None
            assert e is None or e['_ns100']>=ns//100
            if e and e['_ns100']==ns//100 and price_ticks(e['IntrinsicPrice'])==price:
                i+=1
            else:
                e=None
            for sim in sims.values():
                sim.tick(ns,price,e)
            rows+=1
    assert rows==f['rows'] and digest.hexdigest()==f['sha256'] and i==len(daily_events)
    return f,rows,i,{p:(s.finish(),s.canceled_entries) for p,s in sims.items()}


def main(stop_bands=None, exit_rule='overshoot', start_time='03:00', policy=None, output_dir=None, run_dir='.artifacts/iti-backtest/e1ce5fbc17b7', input_dir='.artifacts/iti-october-2026', workers=1, month=None, daily_loss_limit=None):
    run=Path(run_dir)
    src=Path(input_dir)
    output=Path('outputs/iti-dc-to-overshoot-october-2026' if stop_bands is None else f'outputs/iti-dc-to-overshoot-{stop_bands:g}-band-stop-october-2026')
    if exit_rule=='first-reversal':
        output=output.with_name(output.name+'-first-reversal-exit')
    if output_dir is not None:
        output=Path(output_dir)
    elif start_time!='03:00':
        output=output.with_name(output.name+'-'+start_time.replace(':',''))
    output.mkdir(exist_ok=True,parents=True)
    assert not (output/'analysis.json').exists(), 'Existing analysis retained'
    result=json.loads((run/'input.json.results.json').read_text())
    manifest=json.loads((src/'input.json').read_text())
    assert result['input_sha256']==manifest['input_sha256']
    selected_files=[f for f in manifest['files'] if month is None or f['value_date'].startswith(month+'-')]
    assert selected_files,'No source dates in requested month'
    selected_dates={f['value_date'] for f in selected_files}
    events=collections.defaultdict(list)
    for e in result['signals']:
        if e['TimePeriod']==1 and e['ValueDate'] in selected_dates:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    names=('hold','exit-dc') if policy=='compare-dc-exit' else ((policy,) if policy else (('hold','reverse') if stop_bands is None else ('hold',)))
    policies={p:dict(trades=[],daily=[],canceled_entries=0) for p in names}
    total_rows=matched=0
    jobs=[(f,str(src),events[f['value_date']],tuple(policies),stop_bands,exit_rule,start_time,daily_loss_limit) for f in selected_files]
    executor=ProcessPoolExecutor(max_workers=workers) if workers>1 else None
    results=executor.map(analyze_day,jobs) if executor else map(analyze_day,jobs)
    for f,rows,i,daily in results:
        date=f['value_date']
        total_rows+=rows
        matched+=i
        for policy,(ts,canceled) in daily.items():
            policies[policy]['canceled_entries']+=canceled
            for t in ts:
                t['contract_id']=f.get('contract_id',manifest['contract_id'])
                t['trade_id']=f'{date}-{policy}-{len(policies[policy]["trades"])+1:03}'
                policies[policy]['trades'].append(t)
            day=dict(trade_date=date,**summarize(ts))
            policies[policy]['daily'].append(day)
        print(date,[(p,summarize(ts)['gross_pnl_usd']) for p,(ts,_) in daily.items()],flush=True)
    if executor:
        executor.shutdown()
    assert total_rows==sum(f['rows'] for f in selected_files) and matched==sum(len(es) for es in events.values())
    for p,v in policies.items():
        v['totals']=summarize(v['trades'])
        assert sum(d['gross_pnl_usd'] for d in v['daily'])==v['totals']['gross_pnl_usd']
        months=sorted({d['trade_date'][:7] for d in v['daily']})
        v['monthly']=[dict(month=m,**summarize([t for t in v['trades'] if t['trade_date'].startswith(m)])) for m in months]
        assert sum(m['gross_pnl_usd'] for m in v['monthly'])==v['totals']['gross_pnl_usd']
        for kind in ('trades','daily','monthly'):
            records=[{k:v for k,v in r.items() if k!='exit_reasons'} for r in v[kind]]
            with (output/f'{p}-{kind}.csv').open('w',newline='',encoding='utf-8') as stream:
                writer=csv.DictWriter(stream,fieldnames=list(records[0]))
                writer.writeheader()
                writer.writerows(records)
    report=dict(source_run=run.name,source_run_directory=str(run),input_sha256=manifest['input_sha256'],
        assumptions=dict(timeframe='Daily only',instrument=manifest['contract_id'],quantity=1,usd_per_point=50,
            input_directory=str(src), start=manifest['start'],end=manifest['end'],
            selected_month=month,selected_first_date=min(selected_dates),selected_last_date=max(selected_dates),
            short_sessions='At 15:45 mark at last available trade; an early holiday close is a stale-price estimate, not an executable 15:45 fill',
            entry='Direction change in indicated direction, next observed trade tick',
            exit='First subsequent TrendOvershootExtended in position direction, next observed trade tick' if exit_rule=='overshoot' else 'Arm on position-direction TrendOvershootExtended; exit first ordinary or overshoot reversal in that excursion, next observed tick; DC resets arm',
            hold='Ignore further DC while holding; an overshoot in a later same-direction excursion also exits',
            reverse='Opposite DC closes and reverses at next observed tick',
            exit_dc='First opposite DC closes at next tick; consume that DC for exit only; wait for a subsequent new DC to enter; overshoot exit must belong to entry excursion',
            session=f'{start_time} <= entry <15:45 New York; flatten at15:45 using last price observable at/before cutoff',
            start_time_ny=start_time, end_time_ny='15:45', exit_rule=exit_rule,
            stop_bands=stop_bands,
            daily_loss_limit=daily_loss_limit,
            daily_loss_rule='Count all closed trades with negative gross P&L, regardless of exit reason; gains and flats do not reset the count; stop new entries after limit; reset each NY trade date',
            stop_rule='Frozen DC price minus N times DC BandSize for long, plus for short; round outward to ES quarter tick; first reaching/crossing trade price' if stop_bands is not None else 'None',
            risk='No DC-return exit; one position; no fees, quote spread or latency'),
        checks=dict(source_rows=total_rows,matched_daily_events=matched,source_hashes_verified=True,
            next_tick_causality_verified=True,daily_pnl_reconciled=True),policies=policies)
    (output/'analysis.json').write_text(json.dumps(report,indent=2))
    print(json.dumps({p:v['totals'] for p,v in policies.items()}),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stop-bands',type=float)
    parser.add_argument('--exit-rule',choices=('overshoot','first-reversal'),default='overshoot')
    parser.add_argument('--start-time',default='03:00')
    parser.add_argument('--policy',choices=('hold','reverse','exit-dc','compare-dc-exit'))
    parser.add_argument('--output-dir')
    parser.add_argument('--run-dir',default='.artifacts/iti-backtest/e1ce5fbc17b7')
    parser.add_argument('--input-dir',default='.artifacts/iti-october-2026')
    parser.add_argument('--workers',type=int,default=1)
    parser.add_argument('--month',help='Restrict analysis to YYYY-MM, retaining the original replay and input hashes')
    parser.add_argument('--daily-loss-limit',type=int)
    args=parser.parse_args()
    if args.stop_bands is not None and (not math.isfinite(args.stop_bands) or args.stop_bands<=0):
        parser.error('--stop-bands must be positive and finite')
    if args.daily_loss_limit is not None and args.daily_loss_limit<1:
        parser.error('--daily-loss-limit must be positive')
    main(args.stop_bands,args.exit_rule,args.start_time,args.policy,args.output_dir,args.run_dir,args.input_dir,args.workers,args.month,args.daily_loss_limit)
