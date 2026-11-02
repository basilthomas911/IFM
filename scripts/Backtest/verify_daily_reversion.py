"""Independent first-touch and causal-fill checks against the source tick files."""
import json
from pathlib import Path
from analyze_daily_reversion import event_ns, price_ticks, DaySimulator

root = Path('outputs/iti-daily-october-2026/verified')
report = json.loads((root/'analysis.json').read_text())
signals = json.loads(Path('.artifacts/iti-backtest/e1ce5fbc17b7/input.json.results.json').read_text())['signals']
dc_events = {(e['ValueDate'], e['IntrinsicTimeGroupId']): event_ns(e['IntrinsicTime'])//100
             for e in signals if e['TimePeriod']==1 and e['IntrinsicTimeMode']==2}
checked = rows = 0
for day in report['daily']:
    trades = [t for t in report['trades'] if t['trade_date'] == day['trade_date']]
    by_entry = {t['entry_source_ordinal']: t for t in trades}
    active = None
    previous = None
    cutoff = DaySimulator(day['trade_date']).end
    for ordinal,line in enumerate((Path('.artifacts/iti-october-2026')/(day['trade_date']+'.csv')).open('rb'),1):
        c = line.split(b',')
        ns, price = int(c[2]), price_ticks(c[4])/4
        rows += 1
        if active is not None:
            t = active
            sign = 1 if t['side']=='Long' else -1
            at_exit = ordinal == t['exit_source_ordinal']
            if ns >= cutoff:
                assert at_exit and t['exit_reason']=='Session flatten'
                mark = (ns,price) if ns==cutoff else previous
                assert mark[1] == t['exit_price']
                assert mark[0] <= cutoff and int(t['exit_event_ns'])==cutoff
                active = None
                checked += 1
            else:
                stop = sign*(price-t['stop_price']) <= 0
                fallback = t['_dc_seen'] and sign*(previous[1]-t['dc_price'])>=0 and sign*(price-t['dc_price'])<=0 and (previous[1]!=t['dc_price'] or price!=t['dc_price'])
                if stop or fallback:
                    assert at_exit, (t['trade_id'],ordinal,'earlier exit')
                if at_exit:
                    assert price == t['exit_price'] and ns==int(t['exit_event_ns'])
                    assert stop if t['exit_reason']=='Protective stop' else fallback
                    active = None
                    checked += 1
                if t['dc_group'] is not None and ns//100==dc_events[(t['trade_date'],t['dc_group'])] and price==t['dc_price']:
                    t['_dc_seen'] = True
        if ordinal in by_entry:
            assert active is None
            active = by_entry[ordinal]
            active['_dc_seen'] = False
            assert price==active['entry_price'] and ns==int(active['entry_event_ns'])
            assert previous[0]==int(active['entry_signal_event_ns'])
        previous = (ns,price)
    assert active is None
assert checked==report['totals']['trades'] and rows==report['checks']['verified_source_ticks']
(root/'independent-verification.json').write_text(json.dumps(dict(trades_checked=checked,source_ticks_checked=rows,first_touch_and_fill_prices_verified=True),indent=2))
print('Independent verification passed:', checked, 'trades;', rows, 'ticks')
