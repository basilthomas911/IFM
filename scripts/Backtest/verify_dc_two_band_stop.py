"""Independent raw-tick entry, optional stop, signal exit and cutoff verification."""
import json
import math
import argparse
from pathlib import Path
from analyze_daily_reversion import event_ns
from analyze_dc_to_overshoot import DcSimulator

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--first-reversal',action='store_true')
parser.add_argument('--output-dir')
parser.add_argument('--policy',choices=('hold','exit-dc'),default='hold')
args=parser.parse_args()
output=Path('outputs/iti-dc-to-overshoot-2-band-stop-october-2026'+('-first-reversal-exit' if args.first_reversal else ''))
if args.output_dir:
    output=Path(args.output_dir)
report=json.loads((output/'analysis.json').read_text())
first_reversal=report['assumptions'].get('exit_rule', 'first-reversal' if args.first_reversal else 'overshoot')=='first-reversal'
stop_bands=report['assumptions']['stop_bands']
loss_limit=report['assumptions'].get('daily_loss_limit')
source=Path(report['assumptions'].get('input_directory','.artifacts/iti-october-2026'))
run=Path(report.get('source_run_directory',str(Path('.artifacts/iti-backtest')/report['source_run'])))
signals=json.loads((run/'input.json.results.json').read_text())['signals']
dc={(e['ValueDate'],e['IntrinsicTimeGroupId']):e for e in signals if e['TimePeriod']==1 and e['IntrinsicTimeMode']==2}
daily_events={}
for e in signals:
    if e['TimePeriod']==1:
        e['_ns100']=event_ns(e['IntrinsicTime'])//100
        daily_events.setdefault(e['ValueDate'],[]).append(e)
checked=rows=0
for day in report['policies'][args.policy]['daily']:
    date=day['trade_date']
    ts=[t for t in report['policies'][args.policy]['trades'] if t['trade_date']==date]
    entries={t['entry_ordinal']:t for t in ts}
    active=None
    previous=None
    bounds=DcSimulator(date,'hold',start_time=report['assumptions'].get('start_time_ny','03:00'))
    cutoff=bounds.end
    signal_index=0
    armed_group=expected_exit=None
    expected_reason=None
    expected_entry=None
    losses=0
    with (source/(date+'.csv')).open('rb') as stream:
        for ordinal,line in enumerate(stream,1):
            c=line.split(b',')
            ns,price=int(c[2]),float(c[4])
            rows+=1
            e=daily_events[date][signal_index] if signal_index<len(daily_events[date]) else None
            assert e is None or e['_ns100']>=ns//100
            if e is not None and e['_ns100']==ns//100 and float(e['IntrinsicPrice'])==price:
                signal_index+=1
            else:
                e=None
            if active:
                t=active
                sign=1 if t['side']=='Long' else -1
                at_exit=ordinal==t['exit_ordinal']
                stop=t['stop_price'] is not None and sign*(price-t['stop_price'])<=0
                if ns>=cutoff:
                    assert at_exit and t['exit_reason']=='Session flatten'
                    mark=(ns,price) if ns==cutoff else previous
                    assert mark[1]==t['exit_price'] and int(t['exit_event_ns'])==cutoff
                    active=None
                    losses+=t['gross_pnl_usd']<0
                    checked+=1
                elif at_exit and t['exit_reason']!='Session flatten':
                    assert price==t['exit_price'] and ns==int(t['exit_event_ns'])
                    if t['exit_reason']=='Two-band stop':
                        assert stop
                    else:
                        assert not stop and ordinal==t['exit_signal_ordinal']+1
                        assert t['exit_reason']==expected_reason
                        assert expected_exit==ordinal
                    active=None
                    losses+=t['gross_pnl_usd']<0
                    checked+=1
                else:
                    assert not stop,(t['trade_id'],'missed earlier stop')
                    assert expected_exit!=ordinal,(t['trade_id'],'missed earlier signal exit')
            if stop_bands is None and expected_entry==ordinal and ns<cutoff:
                assert ordinal in entries,('missed eligible DC entry',date,ordinal)
            if ordinal in entries:
                assert active is None
                assert loss_limit is None or losses<loss_limit,('entry after daily loss limit',date,ordinal)
                if stop_bands is None:
                    assert expected_entry==ordinal
                active=entries[ordinal]
                armed_group=expected_exit=None
                expected_reason=None
                t=active
                entry_event=dc[(date,t['setup_group'])]
                sign=1 if t['side']=='Long' else -1
                stop=None
                if stop_bands is not None:
                    raw=float(entry_event['IntrinsicPrice'])-sign*stop_bands*float(entry_event['BandSize'])
                    stop=(math.floor(raw*4) if sign==1 else math.ceil(raw*4))/4
                assert stop==t['stop_price'] and price==t['entry_price'] and ns==int(t['entry_event_ns'])
                assert bounds.start<=previous[0]<=ns<cutoff
                assert previous[0]//100==event_ns(entry_event['IntrinsicTime'])//100
                assert previous[1]==float(entry_event['IntrinsicPrice'])
                assert sign==(1 if entry_event['IntrinsicTimeTrend']==0 else -1)
            if active is not None and e is not None and ns<cutoff:
                trade_direction=0 if active['side']=='Long' else 1
                if e['IntrinsicTimeMode']==2:
                    armed_group=None
                    if args.policy=='exit-dc' and e['IntrinsicTimeTrend']!=trade_direction:
                        expected_exit=ordinal+1
                        expected_reason='Opposite DC'
                elif e['IntrinsicTimeMode']==7 and e['IntrinsicTimeTrend']==trade_direction and (args.policy!='exit-dc' or e['IntrinsicTimeGroupId']==active['setup_group']):
                    if first_reversal:
                        armed_group=e['IntrinsicTimeGroupId']
                    else:
                        expected_exit=ordinal+1
                        expected_reason='First overshoot'
                elif first_reversal and e['IntrinsicTimeMode'] in (3,8) and e['IntrinsicTimeTrend']==trade_direction and armed_group==e['IntrinsicTimeGroupId']:
                    expected_exit=ordinal+1
                    expected_reason='First reversal after overshoot'
            if active is None and e is not None and e['IntrinsicTimeMode']==2 and bounds.start<=ns<cutoff and (loss_limit is None or losses<loss_limit):
                expected_entry=ordinal+1
            previous=(ns,price)
    if active is not None:
        # No post-cutoff print on short holiday sessions: verify the last-price mark.
        assert previous[0]<cutoff and active['exit_reason']=='Session flatten'
        assert active['exit_ordinal']==ordinal and active['exit_price']==previous[1]
        assert int(active['exit_event_ns'])==cutoff
        active=None
        losses+=ts[-1]['gross_pnl_usd']<0
        checked+=1
    assert active is None
    assert signal_index==len(daily_events[date])
    if loss_limit is not None:
        assert losses<=loss_limit
        if losses==loss_limit:
            assert ts[-1]['gross_pnl_usd']<0
    print(json.dumps(dict(verified_date=date,verified_trades=checked,verified_source_ticks=rows)),flush=True)
assert checked==report['policies'][args.policy]['totals']['trades'] and rows==report['checks']['source_rows']
if args.policy=='exit-dc':
    ts=report['policies'][args.policy]['trades']
    for t in ts:
        if t['exit_reason']=='First reversal after overshoot':
            assert t['setup_group']==t['overshoot_group']
    for a,b in zip(ts,ts[1:]):
        if a['trade_date']==b['trade_date'] and a['exit_reason']=='Opposite DC':
            assert b['entry_signal_ordinal']>a['exit_signal_ordinal']
proof=dict(verified_trades=checked,verified_source_ticks=rows,dc_entry_anchors_verified=True,dc_stop_anchors_verified=stop_bands is not None,
           first_stop_crossings_verified=stop_bands is not None,no_stop_verified=stop_bands is None,
           session_bounds_verified=True,entry_and_exit_prices_verified=True,first_eligible_signal_exits_verified=True,
           policy=args.policy,opposite_dc_exits_and_subsequent_dc_entries_verified=args.policy=='exit-dc',
           daily_loss_limit=loss_limit,daily_loss_limit_verified=loss_limit is not None)
name='independent-verification.json' if args.policy=='hold' else f'{args.policy}-independent-verification.json'
(output/name).write_text(json.dumps(proof,indent=2))
print(json.dumps(proof))
