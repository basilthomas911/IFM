"""June reversal entries, frozen band risk and an optional daily loss limit."""
import collections
import csv
import datetime as dt
import hashlib
import json
import math
import argparse
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from analyze_daily_reversion import NY,NS,event_ns,ny_time,price_ticks
from analyze_dc_to_overshoot import summarize

SOURCE=Path('.artifacts/iti-year-nov2025-oct2026')
RUN=Path('.artifacts/iti-year-replay/c4dcb85af9ee')
OUTPUT=Path('outputs/iti-june-2026-two-reversal-two-band-one-loss')


def stop_reason(stop_bands):
    if stop_bands is None:
        return None
    return {1:'One-band stop',2:'Two-band stop'}.get(stop_bands,f'{stop_bands:g}-band stop')


def stop_distance_ticks(band,stop_bands=2):
    if not math.isfinite(band) or band<=0:
        raise ValueError('Entry signal requires a finite positive BandSize')
    if not math.isfinite(stop_bands) or stop_bands<=0:
        raise ValueError('Protective stop bands must be positive and finite')
    distance=math.floor(stop_bands*band*4+1e-10)
    if distance<1:
        raise ValueError('Stop budget is smaller than one ES tick')
    assert distance/4<=stop_bands*band+1e-10
    return distance


class ReversalSimulator:
    def __init__(self,date,watermark=False,stop_bands=2,daily_loss_limit=1,entry_reversals=2,watermark_start_bands=2,watermark_lag_bands=0,watermark_buffer_bands=0,entry_direction='retracement',reversal_profit_exit=True,adverse_recovery=False,stop_sides='both',short_stop_bands=None):
        if stop_bands is not None and (not math.isfinite(stop_bands) or stop_bands<=0):
            raise ValueError('Protective stop bands must be positive and finite')
        if entry_direction not in ('retracement','original'):
            raise ValueError('Entry direction must be retracement or original')
        self.entry_direction=entry_direction
        self.reversal_profit_exit=reversal_profit_exit
        if stop_sides not in ('both','long','short'):
            raise ValueError('Stop sides must be both, long or short')
        if adverse_recovery and stop_sides!='both':
            raise ValueError('Adverse recovery requires stops on both sides')
        self.stop_sides=stop_sides
        if short_stop_bands is not None and (not math.isfinite(short_stop_bands) or short_stop_bands<=0):
            raise ValueError('Short stop bands must be finite and positive')
        if short_stop_bands is not None and (adverse_recovery or stop_sides!='both'):
            raise ValueError('Short stop override requires ordinary stops on both sides')
        self.short_stop_bands=short_stop_bands
        if adverse_recovery and stop_bands is None:
            raise ValueError('Adverse recovery requires a threshold distance')
        self.adverse_recovery=adverse_recovery
        day=dt.date.fromisoformat(date)
        self.start=int(dt.datetime.combine(day,dt.time(9,45),NY).timestamp())*NS
        self.end=int(dt.datetime.combine(day,dt.time(15,45),NY).timestamp())*NS
        self.date=date
        self.ordinal=0
        self.previous=self.position=self.pending_entry=self.pending_exit=None
        self.group=self.trend=self.extreme=self.qualification=None
        self.reversals=[]
        self.used=set()
        self.losses=0
        self.finished=False
        self.trades=[]
        self.watermark=watermark
        self.stop_bands=stop_bands
        self.stop_reason=stop_reason(stop_bands)
        if daily_loss_limit is not None and (not isinstance(daily_loss_limit,int) or daily_loss_limit<1):
            raise ValueError('Daily loss limit must be a positive integer or None')
        self.daily_loss_limit=daily_loss_limit
        if not isinstance(entry_reversals,int) or entry_reversals<1:
            raise ValueError('Entry reversal count must be a positive integer')
        if not isinstance(watermark_start_bands,int) or watermark_start_bands<1:
            raise ValueError('Watermark activation bands must be a positive integer')
        self.entry_reversals=entry_reversals
        self.watermark_start_bands=watermark_start_bands
        if not isinstance(watermark_lag_bands,int) or watermark_lag_bands<0:
            raise ValueError('Watermark lag must be a nonnegative integer')
        self.watermark_lag_bands=watermark_lag_bands
        if not math.isfinite(watermark_buffer_bands) or not 0<=watermark_buffer_bands<watermark_start_bands:
            raise ValueError('Watermark buffer must be finite, nonnegative and below the activation band count')
        self.watermark_buffer_bands=watermark_buffer_bands

    @property
    def entry_blocked(self):
        return self.daily_loss_limit is not None and self.losses>=self.daily_loss_limit

    def close(self,ns,price,reason,reference_ns=None):
        p=self.position
        position_stop_bands=p['stop_bands']
        position_stop_reason=stop_reason(position_stop_bands)
        points=p['direction']*(price-p['entry_ticks'])/4
        ideal_price=p['stop_ticks'] if reason==position_stop_reason else price
        ideal_points=p['direction']*(ideal_price-p['entry_ticks'])/4
        self.trades.append(dict(trade_date=self.date,side='Long' if p['direction']==1 else 'Short',
            entry_time_ny=ny_time(p['entry_ns']),entry_price=p['entry_ticks']/4,
            entry_signal_time_ny=ny_time(p['signal_ns']),entry_signal_ordinal=p['signal_ordinal'],entry_ordinal=p['entry_ordinal'],
            entry_event_ns=str(p['entry_ns']),setup_group=p['setup_group'],setup_trend=p['setup_trend'],
            qualification_time_ny=ny_time(p['qualification_ns']),first_reversal_time_ny=ny_time(p['first_reversal_ns']),
            overshoot_extreme=p['setup_extreme']/4,entry_band_size=p['band_size'],
            two_band_budget_points=2*p['band_size'],two_band_budget_usd=2*p['band_size']*50,
            protective_stop_bands=position_stop_bands,stop_budget_points=position_stop_bands*p['band_size'] if position_stop_bands is not None else None,stop_budget_usd=position_stop_bands*p['band_size']*50 if position_stop_bands is not None else None,
            planned_risk_points=p['distance']/4 if p['distance'] is not None else None,planned_risk_usd=p['distance']*12.5 if p['distance'] is not None else None,stop_price=p['stop_ticks']/4 if p['stop_ticks'] is not None else None,
            favorable_overshoot_time_ny=ny_time(p['profit_ns']) if p.get('profit_ns') else None,
            favorable_overshoot_group=p.get('profit_group'),
            watermark_bands=p.get('watermark_bands'),watermark_price=p.get('watermark_ticks')/4 if p.get('watermark_ticks') is not None else None,
            watermark_armed_time_ny=ny_time(p['watermark_ns']) if p.get('watermark_ns') is not None else None,
            exit_time_ny=ny_time(ns),exit_price=price/4,exit_event_ns=str(ns),exit_ordinal=self.ordinal,exit_reason=reason,
            exit_signal_ordinal=self.pending_exit['ordinal'] if self.pending_exit and reason not in (position_stop_reason,'Trailing watermark') else None,
            exit_signal_time_ny=ny_time(self.pending_exit['ns']) if self.pending_exit and reason not in (position_stop_reason,'Trailing watermark') else None,
            exit_price_reference_time_ny=ny_time(reference_ns if reference_ns is not None else ns),
            points=points,gross_pnl_usd=points*50,ideal_stop_fill_pnl_usd=ideal_points*50,
            stop_gap_loss_usd=(ideal_points-points)*50 if reason==position_stop_reason else 0,
            max_favorable_points=p['mfe']/4,max_adverse_points=p['mae']/4))
        if position_stop_bands!=2 or self.stop_sides!='both' or self.short_stop_bands is not None:
            self.trades[-1].pop('two_band_budget_points')
            self.trades[-1].pop('two_band_budget_usd')
        if self.adverse_recovery:
            self.trades[-1].update(
                adverse_breached_time_ny=ny_time(p['adverse_breached_ns']) if 'adverse_breached_ns' in p else None,
                adverse_extreme_price=p['adverse_extreme_ticks']/4 if 'adverse_extreme_ticks' in p else None,
                recovery_watermark_armed_time_ny=ny_time(p['recovery_armed_ns']) if 'recovery_armed_ns' in p else None,
                recovery_watermark_price=p['recovery_mark_ticks']/4 if 'recovery_mark_ticks' in p else None)
        self.losses+=points<0
        self.position=self.pending_exit=None
        if self.entry_blocked:
            self.pending_entry=None

    def tick(self,ns,price,e=None):
        self.ordinal+=1
        assert self.previous is None or ns>=self.previous[0]
        if not self.finished and ns>=self.end:
            if self.position:
                mark=(ns,price) if ns==self.end else self.previous
                self.close(self.end,mark[1],'Session flatten',mark[0])
            self.pending_entry=self.pending_exit=None
            self.finished=True
        active=self.start<=ns<self.end and not self.finished
        if active and self.position:
            p=self.position
            move=p['direction']*(price-p['entry_ticks'])
            p['mfe'],p['mae']=max(p['mfe'],move),min(p['mae'],move)
            if self.watermark and move>4*self.watermark_start_bands*p['band_size']:
                level=max(self.watermark_start_bands,math.floor(move/(4*p['band_size'])+1e-10)-self.watermark_lag_bands)-self.watermark_buffer_bands
                if level>p.get('watermark_bands',0):
                    p['watermark_bands']=level
                    p['watermark_ticks']=p['entry_ticks']+p['direction']*level*p['band_size']*4
                    p.setdefault('watermark_ns',ns)
            recovery_exit=self.adverse_exit(ns,price) if self.adverse_recovery else None
            if recovery_exit is not None:
                self.close(ns,price,recovery_exit)
            elif not self.adverse_recovery and p['stop_ticks'] is not None and p['direction']*(price-p['stop_ticks'])<=0:
                self.close(ns,price,stop_reason(p['stop_bands']))
            elif p.get('watermark_ticks') is not None and p['direction']*(price-p['watermark_ticks'])<0:
                self.close(ns,price,'Trailing watermark')
            elif self.pending_exit:
                self.close(ns,price,'First reversal after favorable overshoot')
        if active and self.pending_entry and not self.entry_blocked:
            p=self.pending_entry
            self.pending_entry=None
            side='long' if p['direction']==1 else 'short'
            position_stop_bands=self.stop_bands if self.stop_sides in ('both',side) else None
            if side=='short' and self.short_stop_bands is not None:
                position_stop_bands=self.short_stop_bands
            distance=stop_distance_ticks(p['band_size'],position_stop_bands) if position_stop_bands is not None else None
            self.position=dict(p,entry_ticks=price,entry_ns=ns,entry_ordinal=self.ordinal,
                               distance=distance,stop_bands=position_stop_bands,stop_ticks=price-p['direction']*distance if distance is not None else None,mfe=0,mae=0)
        if e is not None:
            mode,group,trend=e['IntrinsicTimeMode'],e['IntrinsicTimeGroupId'],e['IntrinsicTimeTrend']
            direction=1 if trend==0 else -1
            if active and self.position and self.reversal_profit_exit:
                p=self.position
                if mode==2:
                    p.pop('profit_group',None)
                    p.pop('profit_ns',None)
                elif mode==7 and direction==p['direction']:
                    if p.get('profit_group')!=group:
                        p.update(profit_group=group,profit_ns=ns)
                elif mode in (3,8) and direction==p['direction'] and p.get('profit_group')==group:
                    self.pending_exit=dict(ns=ns,ordinal=self.ordinal)
            if mode==2 or self.group!=group:
                self.group,self.trend=group,trend
                self.extreme=price_ticks(e['TrendExtreme'])
                self.qualification=None
                self.reversals=[]
            elif mode in (1,7):
                self.extreme=price_ticks(e['TrendExtreme'])
                self.reversals=[]
                if mode==7:
                    self.qualification=ns
            elif mode in (3,8) and self.qualification is not None and active:
                self.reversals.append((ns,self.ordinal))
                if len(self.reversals)==self.entry_reversals and group not in self.used and self.position is None and self.pending_entry is None and not self.entry_blocked:
                    self.used.add(group)
                    self.pending_entry=dict(direction=direction if self.entry_direction=='original' else -direction,setup_group=group,setup_trend=trend,setup_extreme=self.extreme,
                        qualification_ns=self.qualification,first_reversal_ns=self.reversals[0][0],
                        signal_ns=ns,signal_ordinal=self.ordinal,band_size=float(e['BandSize']))
        self.previous=(ns,price)

    def adverse_exit(self,ns,price):
        """After a breach, wait for recovery inside the threshold before arming.

        The armed mark advances on full-band favorable moves. Close on the first
        one-band pullback or adverse threshold touch/cross, retaining gap prices.
        """
        p=self.position
        direction=p['direction']
        band_ticks=4*p['band_size']
        if 'adverse_breached_ns' not in p:
            if direction*(price-p['stop_ticks'])<0:
                p.update(adverse_extreme_ticks=price,adverse_breached_ns=ns)
            return None
        if direction*(price-p['adverse_extreme_ticks'])<0:
            p['adverse_extreme_ticks']=price
        if 'recovery_mark_ticks' not in p:
            if direction*(price-p['stop_ticks'])>0:
                p.update(recovery_mark_ticks=price,recovery_armed_ns=ns)
            return None
        if direction*(price-p['recovery_mark_ticks'])>=band_ticks:
            p['recovery_mark_ticks']=price
        if direction*(price-p['stop_ticks'])<=0:
            return 'Recovered threshold recross'
        if direction*(price-p['recovery_mark_ticks'])<=-band_ticks:
            return 'Recovery watermark pullback'
        return None

    def finish(self):
        if not self.finished:
            if self.position:
                self.close(self.end,self.previous[1],'Session flatten',self.previous[0])
            self.pending_entry=self.pending_exit=None
            self.finished=True
        assert self.position is None
        if self.daily_loss_limit is not None:
            assert self.losses<=self.daily_loss_limit
        seen=set()
        for t in self.trades:
            assert t['setup_group'] not in seen
            seen.add(t['setup_group'])
            assert t['entry_ordinal']==t['entry_signal_ordinal']+1
            assert self.start<=int(t['entry_event_ns'])<self.end and int(t['exit_event_ns'])<=self.end
            if t['protective_stop_bands'] is not None:
                assert t['planned_risk_points']<=t['stop_budget_points']+1e-10
            else:
                assert t['stop_price'] is None and t['planned_risk_points'] is None
            if t['exit_reason']=='First reversal after favorable overshoot':
                assert t['exit_ordinal']==t['exit_signal_ordinal']+1
            if t['exit_reason']==stop_reason(t['protective_stop_bands']):
                assert t['gross_pnl_usd']<=-t['planned_risk_usd']
            if t['exit_reason']=='Trailing watermark':
                assert self.watermark and t['max_favorable_points']>self.watermark_start_bands*t['entry_band_size']
                sign=1 if t['side']=='Long' else -1
                assert sign*(t['exit_price']-t['watermark_price'])<0
        if self.entry_blocked:
            assert self.trades[-1]['gross_pnl_usd']<0
        return self.trades


def analyze_day(job):
    f,events,watermark,stop_bands,daily_loss_limit,entry_reversals,watermark_start_bands,watermark_lag_bands,watermark_buffer_bands=job
    sim=ReversalSimulator(f['value_date'],watermark,stop_bands,daily_loss_limit,entry_reversals,watermark_start_bands,watermark_lag_bands,watermark_buffer_bands)
    digest=hashlib.sha256()
    i=0
    with (SOURCE/f['file']).open('rb') as stream:
        for ordinal,line in enumerate(stream,1):
            digest.update(line)
            c=line.split(b',')
            ns,price=int(c[2]),price_ticks(c[4])
            e=events[i] if i<len(events) else None
            assert e is None or e['_ns100']>=ns//100
            if e is not None and e['_ns100']==ns//100 and price_ticks(e['IntrinsicPrice'])==price:
                i+=1
            else:
                e=None
            sim.tick(ns,price,e)
    assert ordinal==f['rows'] and digest.hexdigest()==f['sha256'] and i==len(events)
    return f,sim.finish(),ordinal,i


def main(output=OUTPUT,watermark=False,stop_bands=2,daily_loss_limit=1,entry_reversals=2,watermark_start_bands=2,start_date='2026-06-01',end_date='2026-07-01',watermark_lag_bands=0,watermark_buffer_bands=0):
    assert dt.date.fromisoformat(start_date)<dt.date.fromisoformat(end_date)
    output.mkdir(parents=True,exist_ok=True)
    assert not (output/'analysis.json').exists(),'Existing evidence retained'
    manifest=json.loads((SOURCE/'input.json').read_text())
    replay=json.loads((RUN/'input.json.results.json').read_text())
    assert replay['input_sha256']==manifest['input_sha256']
    files=[f for f in manifest['files'] if start_date<=f['value_date']<end_date]
    assert files,'No source trading dates in requested range'
    dates={f['value_date'] for f in files}
    events=collections.defaultdict(list)
    for e in replay['signals']:
        if e['TimePeriod']==1 and e['ValueDate'] in dates:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    trades=[]
    daily=[]
    rows=matched=0
    with ProcessPoolExecutor(max_workers=4) as executor:
        for f,ts,count,n in executor.map(analyze_day,[(f,events[f['value_date']],watermark,stop_bands,daily_loss_limit,entry_reversals,watermark_start_bands,watermark_lag_bands,watermark_buffer_bands) for f in files]):
            for t in ts:
                t.update(trade_id=f'{f["value_date"]}-{len(trades)+1:03}',contract_id=f['contract_id'])
                trades.append(t)
            daily.append(dict(trade_date=f['value_date'],**summarize(ts)))
            rows+=count
            matched+=n
            print(f['value_date'],len(ts),sum(t['gross_pnl_usd'] for t in ts),flush=True)
    assert rows==sum(f['rows'] for f in files) and matched==sum(map(len,events.values()))
    monthly=[dict(month=month,**summarize([t for t in trades if t['trade_date'].startswith(month)]))
             for month in sorted({date[:7] for date in dates})]
    report=dict(start_date=start_date,end_date_exclusive=end_date,monthly=monthly,input_sha256=manifest['input_sha256'],source_run_directory=str(RUN),input_directory=str(SOURCE),
        assumptions=dict(timeframe='Daily',quantity=1,usd_per_point=50,session='09:45 <= entry < 15:45 New York; last-observed-price flatten at cutoff',
            entry_reversals=entry_reversals,
            entry=f'After observed overshoot, {entry_reversals} progressive ordinary/overshoot reversal event(s) in same excursion; new extreme/DC resets count; reverse primary direction; next tick fill; one attempt per excursion; only in-window reversals counted',
            protective_stop_bands=stop_bands,
            stop=f'Freeze BandSize from entry reversal signal; adverse {stop_bands:g}-band distance from actual fill, rounded inward to quarter point; first touching/crossing raw trade price',
            profit_exit='First ordinary/overshoot reversal after favorable overshoot in position direction; next tick fill; DC resets profit arm',
            daily_loss_limit=daily_loss_limit,loss_definition='Any realized negative gross trade; halt new entries only if the configured daily count is reached; None disables the limit',
            watermark_enabled=watermark,
            watermark_start_bands=watermark_start_bands,
            watermark_lag_bands=watermark_lag_bands,
            watermark_buffer_bands=watermark_buffer_bands,
            watermark_rule=f'Arm only after gain >{watermark_start_bands} frozen entry band(s); signed profit floor is max({watermark_start_bands}, floor(favorable gain / entry band) - {watermark_lag_bands}) - {watermark_buffer_bands} bands; never retreats; exit first raw trade strictly below the signed watermark; protective stop takes priority, then watermark, then pending original profit exit',
            costs='No commissions, quote spread or latency; actual stop gaps are retained; ideal stop-price P&L separately reported'),
        totals=summarize(trades),ideal_stop_fill_pnl_usd=sum(t['ideal_stop_fill_pnl_usd'] for t in trades),
        stop_gap_loss_usd=sum(t['stop_gap_loss_usd'] for t in trades),
        checks=dict(raw_source_ticks=rows,daily_signals=matched,source_hashes_verified=True),daily=daily,trades=trades)
    for name,records in [('trades',trades),('daily',daily),('monthly',monthly)]:
        cleaned=[{k:v for k,v in r.items() if k!='exit_reasons'} for r in records]
        with (output/(name+'.csv')).open('w',newline='') as stream:
            writer=csv.DictWriter(stream,fieldnames=list(cleaned[0]) if cleaned else ['trade_id'])
            writer.writeheader()
            writer.writerows(cleaned)
    assert sum(d['gross_pnl_usd'] for d in daily)==report['totals']['gross_pnl_usd']
    assert sum(m['gross_pnl_usd'] for m in monthly)==report['totals']['gross_pnl_usd']
    (output/'analysis.json').write_text(json.dumps(report,indent=2))
    print(json.dumps({k:v for k,v in report.items() if k not in ('daily','trades','assumptions')}),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--watermark',action='store_true')
    parser.add_argument('--output-dir',type=Path,default=OUTPUT)
    parser.add_argument('--stop-bands',type=float,default=2)
    parser.add_argument('--daily-loss-limit',type=int,default=1)
    parser.add_argument('--no-daily-loss-limit',action='store_const',const=None,dest='daily_loss_limit')
    parser.add_argument('--entry-reversals',type=int,default=2)
    parser.add_argument('--watermark-start-bands',type=int,default=2)
    parser.add_argument('--watermark-lag-bands',type=int,default=0)
    parser.add_argument('--watermark-buffer-bands',type=float,default=0)
    parser.add_argument('--start-date',default='2026-06-01')
    parser.add_argument('--end-date',default='2026-07-01',help='Exclusive end date')
    args=parser.parse_args()
    if not math.isfinite(args.stop_bands) or args.stop_bands<=0:
        parser.error('--stop-bands must be positive and finite')
    if args.daily_loss_limit is not None and args.daily_loss_limit<1:
        parser.error('--daily-loss-limit must be positive')
    if args.entry_reversals<1 or args.watermark_start_bands<1:
        parser.error('Entry reversals and watermark activation bands must be positive integers')
    if args.watermark_lag_bands<0:
        parser.error('Watermark lag bands must be nonnegative')
    if not math.isfinite(args.watermark_buffer_bands) or not 0<=args.watermark_buffer_bands<args.watermark_start_bands:
        parser.error('Watermark buffer must be finite, nonnegative and below activation bands')
    main(args.output_dir,args.watermark,args.stop_bands,args.daily_loss_limit,args.entry_reversals,args.watermark_start_bands,args.start_date,args.end_date,args.watermark_lag_bands,args.watermark_buffer_bands)
