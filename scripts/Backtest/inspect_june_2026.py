"""Trace June's existing trades without changing entries, exits or strategy rules."""
import collections
import hashlib
import json
from pathlib import Path
from analyze_daily_reversion import event_ns,ny_time,MODES

OUTPUT=Path('outputs/iti-dc-first-reversal-no-stop-0945-1545-nov2025-oct2026')


def main():
    report=json.loads((OUTPUT/'analysis.json').read_text())
    source=Path(report['assumptions']['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    signals=json.loads((Path(report['source_run_directory'])/'input.json.results.json').read_text())['signals']
    daily=collections.defaultdict(list)
    for e in signals:
        if e['TimePeriod']==1 and e['ValueDate'].startswith('2026-06'):
            e['_ns100']=event_ns(e['IntrinsicTime'])//100
            daily[e['ValueDate']].append(e)
    trades=[dict(t,opposite_dc_count=0,first_opposite_dc=None,entry_excursion_reached_overshoot=False,
                 same_direction_overshoot_events=0,opposite_direction_overshoot_events=0,timeline=[])
            for t in report['policies']['hold']['trades'] if t['trade_date'].startswith('2026-06')]
    total_rows=matched=0
    for f in manifest['files']:
        date=f['value_date']
        if not date.startswith('2026-06'):
            continue
        ts=[t for t in trades if t['trade_date']==date]
        i=trade_index=0
        pending={}
        digest=hashlib.sha256()
        with (source/f['file']).open('rb') as stream:
            for ordinal,line in enumerate(stream,1):
                digest.update(line)
                c=line.split(b',')
                ns,price=int(c[2]),float(c[4])
                if ordinal in pending:
                    t=pending.pop(ordinal)
                    sign=1 if t['side']=='Long' else -1
                    t['first_opposite_dc'].update(next_tick_time_ny=ny_time(ns),next_tick_price=price,
                        same_entry_exit_pnl_usd=sign*(price-t['entry_price'])*50)
                while trade_index<len(ts) and ordinal>ts[trade_index]['exit_ordinal']:
                    trade_index+=1
                active=ts[trade_index] if trade_index<len(ts) and ts[trade_index]['entry_ordinal']<=ordinal<=ts[trade_index]['exit_ordinal'] else None
                e=daily[date][i] if i<len(daily[date]) else None
                assert e is None or e['_ns100']>=ns//100
                if e is not None and e['_ns100']==ns//100 and float(e['IntrinsicPrice'])==price:
                    i+=1
                else:
                    e=None
                if active is not None:
                    t=active
                    sign=1 if t['side']=='Long' else -1
                    mark_eligible=ordinal<t['exit_ordinal'] or t['exit_reason']!='Session flatten'
                    if mark_eligible:
                        move=sign*(price-t['entry_price'])
                        t['_mfe']=max(t.get('_mfe',0),move)
                        t['_mae']=min(t.get('_mae',0),move)
                    if e is not None and ordinal<t['exit_ordinal']:
                        same=e['IntrinsicTimeTrend']==(0 if sign==1 else 1)
                        mode=e['IntrinsicTimeMode']
                        item=dict(time_ny=ny_time(ns),price=price,mode=MODES[mode],group=e['IntrinsicTimeGroupId'],
                                  direction='Up' if e['IntrinsicTimeTrend']==0 else 'Down',band_level=e['BandLevel'])
                        if mode in (2,3,7,8):
                            t['timeline'].append(item)
                        if mode==2 and not same:
                            t['opposite_dc_count']+=1
                            if t['first_opposite_dc'] is None:
                                t['first_opposite_dc']=dict(item)
                                pending[ordinal+1]=t
                        if mode==7:
                            t['same_direction_overshoot_events' if same else 'opposite_direction_overshoot_events']+=1
                            if same and e['IntrinsicTimeGroupId']==t['setup_group']:
                                t['entry_excursion_reached_overshoot']=True
                total_rows+=1
        assert not pending
        assert ordinal==f['rows'] and digest.hexdigest()==f['sha256'] and i==len(daily[date])
        matched+=i
        for t in ts:
            assert t.pop('_mfe',0)==t['max_favorable_points']
            assert t.pop('_mae',0)==t['max_adverse_points']
        print(date,len(ts),flush=True)
    def totals(ts):
        return dict(trades=len(ts),pnl_usd=sum(t['gross_pnl_usd'] for t in ts),wins=sum(t['gross_pnl_usd']>0 for t in ts),
                    losses=sum(t['gross_pnl_usd']<0 for t in ts),held_through_opposite_dc=sum(t['opposite_dc_count']>0 for t in ts))
    groups={reason:totals([t for t in trades if t['exit_reason']==reason]) for reason in sorted({t['exit_reason'] for t in trades})}
    result=dict(month='2026-06',assumptions=report['assumptions'],totals=totals(trades),by_exit=groups,
        by_side={side:totals([t for t in trades if t['side']==side]) for side in ('Long','Short')},
        winners_total_usd=sum(t['gross_pnl_usd'] for t in trades if t['gross_pnl_usd']>0),
        losers_total_usd=sum(t['gross_pnl_usd'] for t in trades if t['gross_pnl_usd']<0),
        entry_excursion_overshoot_count=sum(t['entry_excursion_reached_overshoot'] for t in trades),
        first_opposite_dc_comparison='Same original entries only; next observed tick after first opposite DC. Diagnostic, not a backtest of re-entry/reversal rules.',
        checks=dict(raw_source_ticks=total_rows,daily_signals_matched=matched,source_hashes_verified=True,trade_mfe_mae_recomputed=True),trades=trades)
    assert result['totals']['pnl_usd']==-25675 and len(trades)==54
    (OUTPUT/'june-2026-diagnostics.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k not in ('trades','assumptions')}),flush=True)
    for t in sorted(trades,key=lambda t:t['gross_pnl_usd'])[:6]:
        print(json.dumps({k:v for k,v in t.items() if k!='timeline'}),flush=True)


if __name__=='__main__':
    main()
