"""Observe one-minute loss marks on the original condors without changing their exits."""
from argparse import ArgumentParser
from collections import Counter
import gzip
import json
from statistics import median

from replay_condor_monte_carlo import Engine, OUTPUT, checkpoints, et, reconstruct


def loss_bookmarks(rows):
    record = None
    recovery = None
    last_touch = None
    for row in rows:
        if 'midpoint_pnl' not in row:
            continue
        pnl = round(row['midpoint_pnl'], 2)
        if record is not None:
            worst = round(record['midpoint_pnl'], 2)
            if pnl > worst:
                if recovery is None or pnl > recovery['midpoint_pnl']:
                    recovery = row
                continue
            if recovery is not None:
                recovered_pnl = round(recovery['midpoint_pnl'], 2)
                fraction = (recovered_pnl - worst) / -worst
                yield dict(kind='retouch', row=row, prior_peak=record, recovery=recovery,
                           previous_touch=last_touch, recovery_fraction=fraction, recovered_half=fraction >= .50)
                recovery = None
                last_touch = row
            if pnl == worst:
                continue
        if pnl < 0:
            yield dict(kind='new_worst', row=row, prior_peak=record)
            record = row
            last_touch = row


def compare_retouches(bookmarks, reference='prior_peak'):
    changes = []
    unpaired = 0
    for marker in bookmarks:
        peak, touch = marker[reference], marker['row']
        if 'forecast' not in peak or 'forecast' not in touch:
            unpaired += 1
            continue
        changes.append(100 * (touch['forecast']['call_stop'] - peak['forecast']['call_stop']))
    return dict(retouches=len(bookmarks), paired=len(changes), unpaired=unpaired,
                higher=sum(d > 0 for d in changes), lower=sum(d < 0 for d in changes),
                equal=sum(d == 0 for d in changes),
                higher_by_1pp=sum(d >= 1 for d in changes), lower_by_1pp=sum(d <= -1 for d in changes),
                median_change_pp=round(median(changes), 3) if changes else None,
                min_change_pp=round(min(changes), 3) if changes else None)


def flush(batch, engine):
    if batch:
        for (row, _), forecast in zip(batch, engine.run([args for _, args in batch]), strict=True):
            row['forecast'] = forecast
        batch.clear()


def audit_position(position, engine):
    rows = []
    batch = []
    for row, status, args in checkpoints(position, minute_timer=True):
        row['status'] = status
        rows.append(row)
        if args is not None:
            assert row['midpoint_pnl'] < 0 and row['quote_at'] == row['iti_at']
            batch.append((row, args))
        if len(batch) >= 256:
            flush(batch, engine)
    flush(batch, engine)
    assert all(a['iti_at'] < b['iti_at'] for a, b in zip(rows, rows[1:]))
    assert all(row['iti_at'] < et(position['end']).isoformat() for row in rows)
    return rows


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument('--paths', type=int, default=16384)
    parser.add_argument('--seed', type=int, default=20261115)
    args = parser.parse_args()
    engine = Engine(OUTPUT / 'condor_mc.dll', args.paths, args.seed)
    engine.verify()
    print(f'Deterministic CUDA checks passed on {engine.name}', flush=True)
    positions = reconstruct()
    report = []
    counts = Counter()
    scratch = OUTPUT / 'minute-loss-checks.partial.jsonl.gz'
    with gzip.open(scratch, 'wt', encoding='utf-8') as stream:
        for position in positions:
            rows = audit_position(position, engine)
            counts.update(row['status'] for row in rows)
            markers = list(loss_bookmarks(rows))
            worst = [marker for marker in markers if marker['kind'] == 'new_worst']
            call_forecasts = [
                row for row in rows if row.get('forecast') and row['pressured_side'] == 'call'
            ]
            first_85 = next((row for row in call_forecasts if row['forecast']['call_stop'] >= .85), None)
            latest_eligible = call_forecasts[-1] if call_forecasts else None
            summary = dict(day=position['day'], exit=et(position['end']).isoformat(),
                           original_exit=position['original_exit'], original_pnl=position['original_pnl'],
                           call_stop=position['original_exit'] == '30 delta'
                           and (position['original_deltas'][1] or 0) >= .30,
                           forecasts=sum('forecast' in row for row in rows),
                           worst_mark=worst[-1]['row'] if worst else None,
                           first_call_forecast_at_85=first_85, last_call_forecast=latest_eligible,
                           bookmarks=markers)
            report.append(summary)
            stream.write(json.dumps(dict(day=position['day'], checks=rows), separators=(',', ':')) + '\n')
            print(f"{position['day']}  {summary['forecasts']} forecasts, {len(markers)} bookmarks", flush=True)

    assert engine.forecasts == counts['forecast']
    assert sum(r['call_stop'] for r in report) == 11
    assert sum(r['original_pnl'] for r in report) == -3725
    touches = [m for r in report for m in r['bookmarks'] if m['kind'] == 'retouch']
    totals = dict(
        fills=len(report), original_pnl=sum(r['original_pnl'] for r in report),
        checks=counts, positions_with_loss=sum(r['worst_mark'] is not None for r in report),
        new_worst=sum(m['kind'] == 'new_worst' for r in report for m in r['bookmarks']),
        exact_retouches=compare_retouches([
            m for m in touches if round(m['row']['midpoint_pnl'], 2) == round(m['prior_peak']['midpoint_pnl'], 2)
        ]),
        retouches=compare_retouches(touches),
        retouches_vs_previous_touch=compare_retouches(touches, 'previous_touch'),
        retouches_after_half_recovery=compare_retouches([m for m in touches if m['recovered_half']]),
        retouches_call_pressured_both_times=compare_retouches([
            m for m in touches
            if m['row'].get('pressured_side') == m['prior_peak'].get('pressured_side') == 'call'
        ]),
        call_stops_with_85_forecast=sum(r['call_stop'] and r['first_call_forecast_at_85'] is not None for r in report),
        winners_with_85_forecast=sum(r['original_pnl'] > 0 and r['first_call_forecast_at_85'] is not None for r in report),
    )
    output = OUTPUT / 'minute-loss-bookmarks.json'
    output.write_text(json.dumps(dict(device=engine.name, paths=engine.paths, seed=engine.seed,
                                     kernel_ms=engine.kernel_ms, totals=totals, positions=report), indent=2))
    scratch.replace(OUTPUT / 'minute-loss-checks.jsonl.gz')
    print(json.dumps(totals, indent=2), flush=True)


if __name__ == '__main__':
    main()
