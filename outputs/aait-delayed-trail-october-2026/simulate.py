"""October AAIT delayed trailing stop using the C# Daily replay and archived ES trades."""
import csv
import datetime as dt
import hashlib
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts' / 'Backtest'))
from analyze_dc_to_overshoot import summarize
from analyze_daily_reversion import NS, NY, event_ns, ny_time, price_ticks


def simulate(file, source, signals, flipped):
    date = dt.date.fromisoformat(file['value_date'])
    start = int(dt.datetime.combine(date, dt.time(9, 30), NY).timestamp()) * NS
    cutoff = int(dt.datetime.combine(date, dt.time(15, 55), NY).timestamp()) * NS
    digest = hashlib.sha256()
    trades = []
    position = pending = previous = None
    blocked_side = None
    matched = eligible = 0
    groups = set()
    flattened = False

    def close(ns, price, reason, raw_row):
        nonlocal position, blocked_side
        trade = position
        sign = trade['sign']
        assert ns >= trade['entry_ns']
        stop = trade['stop_anchor_ticks'] - sign * trade['distance_ticks']
        if reason == 'Trailing stop':
            assert sign * (price - stop) <= 0
            blocked_side = sign
        points = sign * (price - trade['entry_ticks']) / 4
        trades.append(dict(date=str(date), side='Long' if sign == 1 else 'Short',
            dc_group=trade['group'], dc_row=trade['signal_row'], entry_row=trade['signal_row']+1, exit_row=raw_row, dc_time=ny_time(trade['signal_ns']),
            entry_time=ny_time(trade['entry_ns']), entry_price=trade['entry_ticks']/4,
            dc_threshold=trade['threshold'], best_price=trade['best_ticks']/4,
            final_stop=stop/4, exit_time=ny_time(ns), exit_price=price/4,
            exit_reason=reason, points=points, gross_pnl_usd=points*50))
        position = None

    with (source / file['file']).open('rb') as stream:
        for count, line in enumerate(stream, 1):
            digest.update(line)
            fields = line.split(b',')
            ns, price = int(fields[2]), price_ticks(fields[4])
            assert previous is None or ns >= previous[0]
            event = signals[matched] if matched < len(signals) else None
            if event:
                assert event['_ns100'] >= ns//100
                if event['_ns100'] == ns//100 and event['_ticks'] == price:
                    matched += 1
                else:
                    event = None
            if not flattened and ns >= cutoff:
                if position:
                    mark = (ns, price) if ns == cutoff else previous
                    close(*mark, 'Session flatten', count if ns == cutoff else count-1)
                pending = None
                flattened = True
            if start <= ns < cutoff:
                flat_on_arrival = position is None and pending is None
                if position:
                    sign = position['sign']
                    old_best = position['best_ticks']
                    if sign * (price - old_best) > 0:
                        position['best_ticks'] = price
                    distance = position['distance_ticks']
                    if sign * (position['best_ticks'] - position['entry_ticks']) >= distance:
                        position['stop_anchor_ticks'] = position['best_ticks']
                    stop = position['stop_anchor_ticks'] - sign * distance
                    if sign * (price - stop) <= 0:
                        close(ns, price, 'Trailing stop', count)
                if pending:
                    assert count == pending['signal_row'] + 1 and position is None
                    position = dict(pending, entry_ns=ns, entry_ticks=price, best_ticks=price, stop_anchor_ticks=price)
                    pending = None
                if event and event['IntrinsicTimeMode'] == 2 and event['IntrinsicTimeGroupId'] > 0:
                    eligible += 1
                    dc_sign = 1 if event['IntrinsicTimeTrend'] == 0 else -1
                    sign = -dc_sign if flipped else dc_sign
                    if flat_on_arrival and sign != blocked_side:
                        group = event['IntrinsicTimeGroupId']
                        assert group not in groups
                        groups.add(group)
                        threshold = event['Threshold']
                        assert threshold > 0 and (dc_sign == -1 or threshold == 5)
                        # Integer tick trigger equals the first traded tick crossing the exact threshold.
                        pending = dict(sign=sign, group=group, signal_ns=ns, signal_row=count,
                            threshold=threshold, distance_ticks=math.ceil(threshold*4))
                        blocked_side = None
            previous = (ns, price)
    assert flattened, file['file']
    assert count == file['rows'] and digest.hexdigest() == file['sha256'] and matched == len(signals)
    assert len(trades) == len({t['dc_group'] for t in trades})
    return dict(date=str(date), ticks=count, matched_signals=matched, session_dcs=eligible), trades


def main():
    replay, output = map(Path, sys.argv[1:3])
    assert sys.argv[3] in ('original', 'flipped')
    flipped = sys.argv[3] == 'flipped'
    source = ROOT / '.artifacts' / 'iti-october-2026'
    manifest = json.loads((source / 'input.json').read_text(encoding='utf-8-sig'))
    receipt = json.loads((replay / 'run.json').read_text(encoding='utf-8-sig'))
    result = json.loads((replay / 'input.json.results.json').read_text(encoding='utf-8-sig'))
    proof = json.loads((replay / 'persistence-proof.json').read_text(encoding='utf-8-sig'))
    assert receipt['test_exit_code'] == 0 and manifest['input_sha256'] == result['input_sha256']
    by_date = defaultdict(list)
    for event in result['signals']:
        assert event['TimePeriod'] == 1
        event['_ns100'] = event_ns(event['IntrinsicTime'])//100
        event['_ticks'] = price_ticks(event['IntrinsicPrice'])
        by_date[event['ValueDate']].append(event)
    daily, trades = [], []
    for file in manifest['files']:
        counts, todays_trades = simulate(file, source, by_date[file['value_date']], flipped)
        daily.append(counts)
        trades.extend(todays_trades)
    assert sum(d['ticks'] for d in daily) == manifest['es_rows']
    assert sum(d['matched_signals'] for d in daily) == len(result['signals']) == proof['history_rows']
    by_side = {side: summarize([t for t in trades if t['side'] == side]) for side in ('Long', 'Short')}
    with (output / 'trades.csv').open('w', newline='', encoding='utf-8') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(trades[0]))
        writer.writeheader()
        writer.writerows(trades)
    summary = dict(start='2026-10-01', end_exclusive='2026-11-01', replay=receipt['run_id'], entry_direction=sys.argv[3],
        fills='One ES contract, next archived trade tick after entry DC, first trade through stop. Flatten at last observed price at or before 15:55 ET. Gross, without costs.',
        policy='Confirmed Daily DC when flat between 09:30 and 15:55 ET. Exclude group-zero initialization. After a stop-out, require an opposite DC before a new entry. Opposite DCs while holding do not exit the position.',
        stop='Fixed one threshold from entry until the favorable move reaches one threshold, then trail by the same distance from the best price since fill.',
        by_side=by_side, daily=daily)
    (output / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(by_side, indent=2))


if __name__ == '__main__':
    main()
