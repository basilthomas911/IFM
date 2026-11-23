"""Add 9:00 ET score checks to the saved combined condor exit replay."""

import argparse
from datetime import datetime, time
import json

from replay_condor_call_hmm import REPORTS, checkpoints, confirmation_action, summarize

EXACT_TAPE = 'call-hmm-input-29.json'
EARLY_TAPE = 'call-score-input-early-hours-29.json'
PRIOR_SCORE = 'call-hmm-walk-forward-29-delta-22-confirm-next-check-15min-1min-grace.json'
PRIOR_COMBINED = 'combined-pullback-or-15min-delta-confirmation-1min-grace-29-condors.json'
MODEL = 'delta trend'
EARLY_SCORE = '09:00 delta score'
HARD_EXIT = '09:00 hard call exit'
COMBINED = 'combined 09:00'


def load(name):
    return json.loads((REPORTS / name).read_text())


def by_day(rows):
    result = {row['day']: row for row in rows}
    assert len(result) == len(rows) == 29
    return result


def clock(row):
    return datetime.fromisoformat(row['at']).time()


def hard_exit(marks, delta_floor):
    for row in marks:
        delta = row['x'][0]
        if delta >= delta_floor and row['fill']:
            return {**row['fill'], 'signal': row['at'], 'delta': delta}
    return None


def candidates(trade, early_tape, prior, early_signals):
    checks = checkpoints(trade)
    forecasts = prior['forecasts']
    assert [r['at'] for r in checks] == [r['at'] for r in forecasts]
    scores = {r['at']: r[MODEL] for r in forecasts}
    for row in early_signals:
        delta, change, _ = row['x']
        scores[row['at']] = delta + max(0., change)
    checks = sorted(checks + early_signals, key=lambda r: r['at'])
    regular_marks = checkpoints(trade, 15) + checkpoints(trade, 15, minute_offset=1)
    early_marks = checkpoints(early_tape, 15) + checkpoints(early_tape, 15, minute_offset=1)
    early_marks = [row for row in early_marks if time(9) <= clock(row) < time(9, 30)]
    inputs = {'checks': checks, 'confirmation_marks': regular_marks + early_marks}
    threshold = prior['thresholds'][MODEL]
    hit, audit = confirmation_action(inputs, [scores[r['at']] for r in checks],
                                     threshold, 22, 15, 1, session_open=time(9))
    observed = [{'at':r['at'], 'delta':r['x'][0], 'score':scores[r['at']]}
                for r in early_signals]
    return hit, audit, observed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--hard-call-delta', type=float,
                        help='Add an independent hard exit at the exact 9:00 ET check.')
    args = parser.parse_args()
    regular = by_day(load(EXACT_TAPE))
    extended = by_day(load(EARLY_TAPE))
    prior = by_day(load(PRIOR_SCORE)['positions'])
    baseline = load(PRIOR_COMBINED)
    previous = by_day(baseline['positions'])
    results = []
    for day, saved in previous.items():
        tape = regular[day]
        early = extended[day]
        assert tape['baseline_exit'] == early['baseline_exit'] == saved['baseline_exit']
        assert tape['baseline_pnl'] == early['baseline_pnl'] == saved['baseline_pnl']
        prior_position = prior[day]
        exit_, confirmations, marks = None, [], []
        early_signals = [row for row in checkpoints(early) if clock(row) == time(9)]
        if prior_position['scored']:
            exit_, confirmations, marks = candidates(tape, early, prior_position, early_signals)
        pullback = saved['exits']['22-delta pullback']
        alternatives = {'22-delta pullback': pullback, EARLY_SCORE: exit_}
        if args.hard_call_delta is not None:
            alternatives[HARD_EXIT] = hard_exit(early_signals, args.hard_call_delta)
        combined_rule, combined = min(((rule, hit) for rule, hit in alternatives.items() if hit),
                                      key=lambda pair: datetime.fromisoformat(pair[1]['exit']),
                                      default=(None, None))
        results.append({**saved, 'exits': {**saved['exits'], **alternatives, COMBINED: combined},
                        'first_rule_09:00': combined_rule,
                        '09:00_marks': marks, 'score_confirmations': confirmations})
    summary = summarize(results, COMBINED)
    old_summary = baseline['full_replay_with_grace']
    summary['previous_pnl'] = old_summary['one_lot_gross_pnl']
    summary['pnl_change'] = summary['pnl'] - summary['previous_pnl']
    summary['changed_trades'] = [
        p['day'] for p in results if p['exits'][COMBINED] != p['exits']['first executable exit with grace']
    ]
    suffix = ''
    if args.hard_call_delta is not None:
        summary['hard_call_delta_09:00'] = args.hard_call_delta
        suffix = f'-hard-{args.hard_call_delta:g}'
    output = REPORTS / f'combined-pullback-or-09h00-delta-score-15min-1min-grace{suffix}-29-condors.json'
    output.write_text(json.dumps({'summary':summary, 'positions':results}, indent=2) + '\n')
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    main()
