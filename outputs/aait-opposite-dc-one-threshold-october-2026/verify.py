"""Independently verify the recorded trade ledger against the original ticks."""
import csv
import datetime as dt
import json
import sys
from collections import defaultdict
from pathlib import Path

root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / 'scripts' / 'Backtest'))
from analyze_daily_reversion import NY, NS, event_ns, price_ticks

replay, output = map(Path, sys.argv[1:])
source = root / '.artifacts' / 'iti-october-2026'
manifest = json.loads((source / 'input.json').read_text())
result = json.loads((replay / 'input.json.results.json').read_text(encoding='utf-8-sig'))
ledger = defaultdict(list)
with (output / 'trades.csv').open(newline='') as stream:
    for trade in csv.DictReader(stream):
        ledger[trade['date']].append(trade)
dcs = {(e['ValueDate'], e['IntrinsicTimeGroupId']): e for e in result['signals']
       if e['IntrinsicTimeMode'] == 2 and e['IntrinsicTimeGroupId'] > 0}
assert len(dcs) == sum(e['IntrinsicTimeMode'] == 2 and e['IntrinsicTimeGroupId'] > 0
                       for e in result['signals'])
checked = 0
for file in manifest['files']:
    date = file['value_date']
    date_value = dt.date.fromisoformat(date)
    start = int(dt.datetime.combine(date_value, dt.time(9, 30), NY).timestamp()) * NS
    end = int(dt.datetime.combine(date_value, dt.time(15, 55), NY).timestamp()) * NS
    trades = ledger[date]
    assert len({t['dc_group'] for t in trades}) == len(trades)
    index, best, next_ns = 0, None, None
    previous_exit = 0
    with (source / file['file']).open('rb') as stream:
        for row, line in enumerate(stream, 1):
            if next_ns is not None:
                assert int(line.split(b',')[2]) > end
                next_ns = None
            if index == len(trades):
                break
            trade = trades[index]
            dc_row, entry_row, exit_row = (int(trade[k]) for k in ('dc_row', 'entry_row', 'exit_row'))
            if row < dc_row:
                continue
            cells = line.split(b',')
            ns, price = int(cells[2]), price_ticks(cells[4])/4
            side = 1 if trade['side'] == 'Long' else -1
            threshold = float(trade['dc_threshold'])
            if row == dc_row:
                assert dc_row > previous_exit and entry_row == dc_row + 1 <= exit_row
                e = dcs[(date, int(trade['dc_group']))]
                assert ns//100 == event_ns(e['IntrinsicTime'])//100 and price == e['IntrinsicPrice']
                assert threshold == e['Threshold'] and (side == -1) == (e['IntrinsicTimeTrend'] == 0)
                assert start <= ns < end
                continue
            if row == entry_row:
                assert start <= ns < end and price == float(trade['entry_price'])
                best = side * price
            if ns < end:
                best = max(best, side * price)
            crossed = (best - side * price) >= threshold
            if row < exit_row:
                assert ns < end and not crossed
                continue
            assert row == exit_row and price == float(trade['exit_price'])
            assert best == side * float(trade['best_price'])
            if trade['exit_reason'] == 'Trailing stop':
                assert ns < end and crossed
            else:
                assert trade['exit_reason'] == 'Session flatten' and ns <= end
                if ns < end:
                    assert not crossed
                    next_ns = ns
            assert side * (price - float(trade['entry_price'])) == float(trade['points'])
            assert float(trade['points'])*50 == float(trade['gross_pnl_usd'])
            previous_exit, index, checked = row, index + 1, checked + 1
    assert index == len(trades) and next_ns is None
proof = {'verified_trades': checked, 'confirmed_daily_dcs': len(dcs),
         'checks': ['C# DC identity, opposite side and frozen threshold', 'next tick entry',
                    'first threshold crossing', 'cutoff mark', 'one position and one trade per DC', 'gross P&L']}
(output / 'verification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps(proof))
