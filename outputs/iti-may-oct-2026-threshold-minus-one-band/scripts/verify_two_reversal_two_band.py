"""Independent raw-tape verification of the June two-reversal entry experiment."""
import collections
import hashlib
import json
import math
import argparse
from pathlib import Path
from analyze_daily_reversion import event_ns,DaySimulator,NS,ny_time

ROOT=Path('outputs/iti-june-2026-two-reversal-two-band-one-loss')


def main(root=ROOT):
    r=json.loads((root/'analysis.json').read_text())
    watermark=r['assumptions'].get('watermark_enabled',False)
    loss_limit=r['assumptions'].get('daily_loss_limit',1)
    entry_reversals=r['assumptions'].get('entry_reversals',2)
    watermark_start=r['assumptions'].get('watermark_start_bands',2)
    watermark_lag=r['assumptions'].get('watermark_lag_bands',0)
    watermark_buffer=r['assumptions'].get('watermark_buffer_bands',0)
    stop_bands=r['assumptions'].get('protective_stop_bands',2)
    entry_direction=r['assumptions'].get('entry_direction','retracement')
    reversal_profit_exit=r['assumptions'].get('reversal_profit_exit',True)
    adverse_recovery=r['assumptions'].get('adverse_recovery',False)
    recovery_v2=r['assumptions'].get('adverse_recovery_rule')=='inside-threshold-then-pullback-v2'
    stop_label=None if stop_bands is None else {1:'One-band stop',2:'Two-band stop'}.get(stop_bands,f'{stop_bands:g}-band stop')
    source=Path(r['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    signals=json.loads((Path(r['source_run_directory'])/'input.json.results.json').read_text())['signals']
    dates={d['trade_date'] for d in r['daily']}
    assert dates and len(dates)==len(r['daily'])
    assert dates<={f['value_date'] for f in manifest['files']}
    events=collections.defaultdict(list)
    for e in signals:
        if e['TimePeriod']==1 and e['ValueDate'] in dates:
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            events[e['ValueDate']].append(e)
    checked=rows=matched=0
    for f in manifest['files']:
        date=f['value_date']
        if date not in dates:
            continue
        ts=[t for t in r['trades'] if t['trade_date']==date]
        entries={t['entry_ordinal']:t for t in ts}
        cutoff=DaySimulator(date).end
        start=cutoff-6*3600*NS
        active=previous=None
        next_entry=next_exit=None
        arm=None
        losses=i=0
        group=trend=extreme=qualification=None
        reversal_events=[]
        used=set()
        digest=hashlib.sha256()
        with (source/f['file']).open('rb') as stream:
            for ordinal,line in enumerate(stream,1):
                digest.update(line)
                c=line.split(b',')
                ns,price=int(c[2]),float(c[4])
                e=events[date][i] if i<len(events[date]) else None
                assert e is None or e['_ns100']>=ns//100
                if e is not None and e['_ns100']==ns//100 and float(e['IntrinsicPrice'])==price:
                    i+=1
                else:
                    e=None
                if active:
                    t=active
                    sign=1 if t['side']=='Long' else -1
                    at_exit=ordinal==t['exit_ordinal']
                    if ns>=cutoff:
                        assert at_exit and t['exit_reason']=='Session flatten'
                        mark=(ns,price) if ns==cutoff else previous
                        assert t['exit_price']==mark[1] and int(t['exit_event_ns'])==cutoff
                        losses+=t['gross_pnl_usd']<0
                        checked+=1
                        active=next_exit=None
                    else:
                        move=sign*(price-t['entry_price'])
                        t['_mfe'],t['_mae']=max(t['_mfe'],move),min(t['_mae'],move)
                        if watermark:
                            if not t['_wm_level'] and move>watermark_start*t['entry_band_size']:
                                t['_wm_level']=watermark_start-watermark_buffer
                                t['_wm_ns']=ns
                            if t['_wm_level']:
                                while move>=(t['_wm_level']+watermark_buffer+1+watermark_lag)*t['entry_band_size']:
                                    t['_wm_level']+=1
                        trail=t['_wm_level']>0 and move<t['_wm_level']*t['entry_band_size']
                        recovery_exit=None
                        if adverse_recovery and recovery_v2:
                            boundary=-t['planned_risk_points']
                            if t['_breached_ns'] is None:
                                if move<boundary:
                                    t['_breached_ns']=ns
                                    t['_adverse_extreme']=move
                            else:
                                t['_adverse_extreme']=min(t['_adverse_extreme'],move)
                                if t['_recovery_mark'] is None:
                                    if move>boundary:
                                        t['_recovery_mark']=move
                                        t['_recovery_ns']=ns
                                else:
                                    if move>=t['_recovery_mark']+t['entry_band_size']:
                                        t['_recovery_mark']=move
                                    if move<=boundary:
                                        recovery_exit='Recovered threshold recross'
                                    elif move<=t['_recovery_mark']-t['entry_band_size']:
                                        recovery_exit='Recovery watermark pullback'
                        elif adverse_recovery:
                            if t['_adverse_mark'] is None:
                                if move < -t['planned_risk_points']:
                                    t['_adverse_mark']=move
                                    t['_adverse_ns']=ns
                            else:
                                if move<=t['_adverse_mark']-t['entry_band_size']:
                                    t['_adverse_mark']=move
                                if move>=-t['planned_risk_points']:
                                    recovery_exit='Threshold recovery'
                                elif move>=t['_adverse_mark']+t['entry_band_size']:
                                    recovery_exit='Adverse watermark rebound'
                        stop=not adverse_recovery and stop_bands is not None and sign*(price-t['stop_price'])<=0
                        profit=next_exit==ordinal
                        if stop or recovery_exit or trail or profit:
                            assert at_exit and price==t['exit_price'] and ns==int(t['exit_event_ns'])
                            assert t['exit_reason']==(recovery_exit or (stop_label if stop else 'Trailing watermark' if trail else 'First reversal after favorable overshoot'))
                            if not stop and not recovery_exit and not trail:
                                assert ordinal==t['exit_signal_ordinal']+1
                            losses+=t['gross_pnl_usd']<0
                            checked+=1
                            active=next_exit=None
                        else:
                            assert not at_exit or t['exit_reason']=='Session flatten'
                if next_entry is not None and ordinal==next_entry['ordinal'] and ns<cutoff:
                    assert ordinal in entries,'Missing valid two-reversal entry'
                if ordinal in entries:
                    t=entries[ordinal]
                    assert active is None and (loss_limit is None or losses<loss_limit) and next_entry is not None
                    assert ordinal==next_entry['ordinal'] and start<=ns<cutoff
                    assert t['setup_group']==next_entry['group'] and t['entry_band_size']==next_entry['band']
                    assert t['entry_price']==price and int(t['entry_event_ns'])==ns
                    assert t['entry_signal_ordinal']==ordinal-1
                    sign=1 if t['side']=='Long' else -1
                    original_sign=1 if next_entry['trend']==0 else -1
                    assert sign==(original_sign if entry_direction=='original' else -original_sign)
                    if stop_bands is None:
                        assert t['stop_price'] is None and t['planned_risk_points'] is None and t['planned_risk_usd'] is None
                    else:
                        distance=math.floor(4*stop_bands*next_entry['band']+1e-10)/4
                        assert t['stop_price']==price-sign*distance
                        assert t['planned_risk_points']==distance<=stop_bands*next_entry['band']+1e-10
                        if stop_bands==5:
                            assert math.isclose(5*next_entry['band'],next_entry['threshold'],rel_tol=1e-12)
                            assert t['planned_risk_points']<=next_entry['threshold']+1e-10
                        if stop_bands in (3,3.5,4):
                            threshold_minus_bands=next_entry['threshold']-(5-stop_bands)*next_entry['band']
                            assert math.isclose(stop_bands*next_entry['band'],threshold_minus_bands,rel_tol=1e-12)
                            assert t['planned_risk_points']==math.floor(4*threshold_minus_bands+1e-10)/4
                    active=t
                    t['_mfe']=t['_mae']=0
                    t['_wm_level']=0
                    t['_wm_ns']=None
                    t['_adverse_mark']=t['_adverse_ns']=None
                    t['_breached_ns']=t['_adverse_extreme']=t['_recovery_mark']=t['_recovery_ns']=None
                    arm=next_exit=None
                    next_entry=None
                if e is not None:
                    mode=e['IntrinsicTimeMode']
                    g=e['IntrinsicTimeGroupId']
                    tr=e['IntrinsicTimeTrend']
                    if active and ns<cutoff and reversal_profit_exit:
                        td=0 if active['side']=='Long' else 1
                        if mode==2:
                            arm=None
                        elif mode==7 and tr==td:
                            arm=g
                        elif mode in (3,8) and tr==td and arm==g:
                            next_exit=ordinal+1
                    if mode==2 or group!=g:
                        group,trend=g,tr
                        extreme=float(e['TrendExtreme'])
                        qualification=None
                        reversal_events=[]
                    elif mode in (1,7):
                        extreme=float(e['TrendExtreme'])
                        reversal_events=[]
                        if mode==7:
                            qualification=ns
                    elif mode in (3,8) and qualification is not None and start<=ns<cutoff:
                        reversal_events.append((ns,price,mode))
                        if len(reversal_events)==entry_reversals and active is None and (loss_limit is None or losses<loss_limit) and next_entry is None and g not in used:
                            # Progressive reversals in the primary excursion reverse the entry side.
                            if entry_reversals>1:
                                assert all((b[1]-a[1])*(1 if tr==0 else -1)<0 for a,b in zip(reversal_events,reversal_events[1:]))
                            used.add(g)
                            next_entry=dict(ordinal=ordinal+1,group=g,trend=tr,band=float(e['BandSize']),threshold=float(e['Threshold']))
                previous=(ns,price)
                rows+=1
        if active:
            assert previous[0]<cutoff and active['exit_reason']=='Session flatten'
            assert active['exit_price']==previous[1] and active['exit_ordinal']==ordinal
            losses+=active['gross_pnl_usd']<0
            checked+=1
        assert (loss_limit is None or losses<=loss_limit) and i==len(events[date])
        assert ordinal==f['rows'] and digest.hexdigest()==f['sha256']
        matched+=i
        for t in ts:
            assert t.pop('_mfe')==t['max_favorable_points'] and t.pop('_mae')==t['max_adverse_points']
            sign=1 if t['side']=='Long' else -1
            assert t['gross_pnl_usd']==sign*(t['exit_price']-t['entry_price'])*50
            level=t.pop('_wm_level')
            armed_ns=t.pop('_wm_ns')
            if level:
                assert level==t['watermark_bands']
                assert math.isclose(t['watermark_price'],t['entry_price']+sign*level*t['entry_band_size'],abs_tol=1e-9)
                assert t['watermark_armed_time_ny']==ny_time(armed_ns)
            else:
                assert t.get('watermark_bands') is None
            adverse_mark=t.pop('_adverse_mark')
            adverse_ns=t.pop('_adverse_ns')
            breached_ns=t.pop('_breached_ns')
            adverse_extreme=t.pop('_adverse_extreme')
            recovery_mark=t.pop('_recovery_mark')
            recovery_ns=t.pop('_recovery_ns')
            if adverse_recovery and recovery_v2:
                assert t['adverse_breached_time_ny']==(ny_time(breached_ns) if breached_ns is not None else None)
                assert t['recovery_watermark_armed_time_ny']==(ny_time(recovery_ns) if recovery_ns is not None else None)
                for field,mark in [('adverse_extreme_price',adverse_extreme),('recovery_watermark_price',recovery_mark)]:
                    if mark is None:
                        assert t[field] is None
                    else:
                        assert math.isclose(t[field],t['entry_price']+sign*mark,abs_tol=1e-9)
            elif adverse_recovery:
                if adverse_mark is None:
                    assert t['adverse_watermark_armed_time_ny'] is None and t['adverse_watermark_price'] is None
                else:
                    assert t['adverse_watermark_armed_time_ny']==ny_time(adverse_ns)
                    assert math.isclose(t['adverse_watermark_price'],t['entry_price']+sign*adverse_mark,abs_tol=1e-9)
        if loss_limit is not None and losses==loss_limit:
            assert ts[-1]['gross_pnl_usd']<0
        print(date,checked,rows,flush=True)
    assert checked==r['totals']['trades']
    assert rows==r['checks']['raw_source_ticks']==sum(f['rows'] for f in manifest['files'] if f['value_date'] in dates)
    assert matched==r['checks']['daily_signals']==sum(map(len,events.values()))
    proof=dict(complete=True,verified_trades=checked,raw_ticks=rows,daily_signals=matched,
        entry_reversals=entry_reversals,qualifying_reversal_entries_verified=True,next_tick_fills_verified=True,
        entry_direction=entry_direction,reversal_profit_exit=reversal_profit_exit,
        entry_threshold_stop_verified=stop_bands==5 and not adverse_recovery,
        entry_threshold_minus_one_band_stop_verified=stop_bands==4 and not adverse_recovery,
        entry_threshold_minus_one_and_half_bands_stop_verified=stop_bands==3.5 and not adverse_recovery,
        entry_threshold_minus_two_bands_stop_verified=stop_bands==3 and not adverse_recovery,
        entry_threshold_distance_verified=stop_bands==5,
        adverse_recovery=adverse_recovery,adverse_recovery_rule=r['assumptions'].get('adverse_recovery_rule','v1'),
        strict_breach_band_steps_and_first_recovery_exit_verified=adverse_recovery,
        deep_rebounds_ignored_until_inside_threshold_and_first_armed_exit_verified=recovery_v2,
        protective_stop_bands=stop_bands,frozen_band_stops_and_first_touches_verified=stop_bands is not None and not adverse_recovery,no_stop_verified=stop_bands is None,first_reversal_after_favorable_overshoot_verified=reversal_profit_exit,
        daily_loss_limit=loss_limit,daily_loss_policy_verified=True,mfe_mae_recomputed=True,raw_hashes_verified=True,
        watermark_enabled=watermark,watermark_start_bands=watermark_start,watermark_lag_bands=watermark_lag,watermark_buffer_bands=watermark_buffer,strict_watermark_activation_ratchet_and_first_cross_verified=watermark)
    (root/'independent-verification.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=ROOT)
    args=parser.parse_args()
    main(args.output_dir)
