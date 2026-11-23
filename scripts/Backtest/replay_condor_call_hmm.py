"""Offline walk-forward comparison for the 29 saved 35-DTE ES condors."""

import argparse
from bisect import bisect_left, bisect_right
from datetime import datetime, time, timedelta
import json
from pathlib import Path
import sys
from zoneinfo import ZoneInfo

import numpy as np

REPORTS = Path(__file__).resolve().parents[2] / '.artifacts/condor-economic-calendar'
NY = ZoneInfo('America/New_York')
NS = 10**9
STATE_COUNT = 3
MIN_TRAIN = 5
MIN_DELTA = 18.0
THRESHOLD_QUANTILES = (0.50, 0.65, 0.80, 0.90, 0.95)


def local(ns):
    return datetime.fromtimestamp(ns / NS, NY)


def timestamp(iso):
    return int(datetime.fromisoformat(iso).timestamp()) * NS


def next_weekday_close(mark):
    day = mark.date() + timedelta(days=1)
    while day.weekday() >= 5:
        day += timedelta(days=1)
    return datetime.combine(day, time(16), NY)


def checkpoints(trade, minutes=30, minute_offset=0):
    """Scheduled marks with the completed ES bar and the replay's option quotes."""
    sample_times = [row[0] for row in trade['samples']]
    quote_times = [row[0] for row in trade['quotes']]
    exit_ns = timestamp(trade['baseline_exit'])
    entry_ns = timestamp(trade['entry'])
    rows = []
    for t, es, delta in trade['samples']:
        stamp = local(t)
        if t >= exit_ns or stamp.minute % minutes != minute_offset or stamp.second != 0:
            continue
        previous_index = bisect_right(sample_times, t - 3600 * NS) - 1
        if previous_index < 0:
            previous_index = bisect_left(sample_times, t) - 1
        if previous_index >= 0:
            _, previous_es, previous_delta = trade['samples'][previous_index]
        else:
            previous_es, previous_delta = trade['es_entry'], trade['entry_delta']
        assert entry_ns < t
        q = bisect_right(quote_times, t)
        fill = None
        if q < len(quote_times) and quote_times[q] < exit_ns:
            fill_ns, debit = trade['quotes'][q]
            fill = {'exit': local(fill_ns).isoformat(), 'debit': debit,
                    'pnl': round((trade['credit'] - debit) * 50, 2)}
        near_stop = (trade['group'] == 'call stop'
                     and local(exit_ns) <= next_weekday_close(stamp))
        rows.append({'at': stamp.isoformat(), 'x': [delta, delta-previous_delta,
                     (es/previous_es-1)*100], 'label': int(near_stop), 'fill': fill})
    assert rows, trade['day']
    return rows


class ThreeStateHMM:
    """Three-state diagonal Gaussian HMM, with per-trade sequence boundaries."""

    def __init__(self, sequences):
        xs = np.concatenate(sequences)
        weights = np.concatenate([np.full(len(x), 1 / len(x)) for x in sequences])
        self.center = np.average(xs, weights=weights, axis=0)
        self.scale = np.sqrt(np.average((xs - self.center)**2, weights=weights, axis=0))
        self.scale = np.maximum(self.scale, 0.05)
        standardized = [(x - self.center) / self.scale for x in sequences]
        data = np.concatenate(standardized)
        groups = np.array_split(data[np.argsort(data[:, 0])], STATE_COUNT)
        self.means = np.stack([g.mean(axis=0) for g in groups])
        self.vars = np.stack([np.maximum(g.var(axis=0), .15) for g in groups])
        self.transition = np.eye(STATE_COUNT)*.85 + np.full((STATE_COUNT, STATE_COUNT), .05)
        self.initial = np.full(STATE_COUNT, 1 / STATE_COUNT)
        typical_length = np.mean([len(x) for x in standardized])
        for _ in range(18):
            self.update(standardized, typical_length)
        order = np.argsort(self.means[:, 0])
        self.means, self.vars, self.initial = self.means[order], self.vars[order], self.initial[order]
        self.transition = self.transition[np.ix_(order, order)]

    def likelihood(self, x):
        logp = -.5 * (np.log(self.vars) + (x[:, None, :] - self.means)**2 / self.vars).sum(axis=2)
        return np.exp(logp - logp.max(axis=1, keepdims=True)).clip(1e-120)

    def forward(self, likelihood):
        alpha = np.empty_like(likelihood)
        normalizers = np.empty(len(likelihood))
        prediction = self.initial
        for i, obs in enumerate(likelihood):
            joint = prediction * obs
            normalizers[i] = joint.sum()
            alpha[i] = joint / normalizers[i]
            prediction = alpha[i] @ self.transition
        return alpha, normalizers

    def update(self, sequences, typical_length):
        initial = np.ones(STATE_COUNT)*.1
        transitions = np.eye(STATE_COUNT)*2 + .25
        mass = np.zeros(STATE_COUNT)
        total = np.zeros_like(self.means)
        squares = np.zeros_like(self.means)
        for x in sequences:
            weight = typical_length / len(x)
            likelihood = self.likelihood(x)
            alpha, norm = self.forward(likelihood)
            beta = np.ones_like(likelihood)
            for i in range(len(x)-2, -1, -1):
                beta[i] = self.transition @ (likelihood[i+1]*beta[i+1]) / norm[i+1]
            posterior = alpha * beta
            posterior /= posterior.sum(axis=1, keepdims=True)
            initial += posterior[0]
            right = likelihood[1:] * beta[1:] / norm[1:, None]
            transitions += weight * (alpha[:-1].T @ right) * self.transition
            mass += weight * posterior.sum(axis=0)
            total += weight * posterior.T @ x
            squares += weight * posterior.T @ (x*x)
        self.initial = initial / initial.sum()
        self.transition = transitions / transitions.sum(axis=1, keepdims=True)
        self.means = total / mass[:, None]
        self.vars = np.maximum(squares / mass[:, None] - self.means**2, .15)

    def filtered(self, sequence):
        likelihood = self.likelihood((sequence - self.center) / self.scale)
        return self.forward(likelihood)[0]


def as_array(trade):
    return np.array([r['x'] for r in trade['checks']])


def state_stop_rates(model, training):
    """Estimate next-session call exit frequency with fractional state membership."""
    exposure = np.zeros(STATE_COUNT)
    events = np.zeros(STATE_COUNT)
    for trade in training:
        mask = as_array(trade)[:, 0] >= MIN_DELTA
        if not mask.any():
            continue
        weights = model.filtered(as_array(trade))[mask] / mask.sum()
        labels = np.array([r['label'] for r in trade['checks']])[mask]
        exposure += weights.sum(axis=0)
        events += labels @ weights
    pooled = events.sum() / exposure.sum() if exposure.sum() else 0
    # Half a trade of shrinkage per state toward the pooled training rate.
    return (events + .5*pooled) / (exposure + .5)


def trend_scores(trade):
    data = as_array(trade)
    return data[:, 0] + np.maximum(0, data[:, 1])


def first_pullback_retention(trade):
    """Delta points above entry that survived the first qualifying ES pullback."""
    entry_es = trade['es_entry']
    entry_delta = trade['entry_delta']
    first_push = entry_es * 1.005
    pullback_size = entry_es * .0025
    peak_es = peak_delta = trough_delta = None
    pulled_back = complete = False
    retained = 0.
    marks = {}
    for t, es, delta in trade['samples']:
        if not complete:
            if pulled_back:
                if es > peak_es:
                    complete = True
                else:
                    trough_delta = min(trough_delta, delta)
            elif es >= first_push and (peak_es is None or es >= peak_es):
                peak_es, peak_delta, trough_delta = es, delta, delta
            elif peak_es is not None:
                trough_delta = min(trough_delta, delta)
                pulled_back = peak_es - es >= pullback_size
            if pulled_back:
                retained = max(0., min(peak_delta, trough_delta) - entry_delta)
        marks[t] = retained
    return np.array([marks[timestamp(row['at'])] for row in trade['checks']])


def retention_scores(trade):
    return trend_scores(trade) + trade['retained_delta']


def action(trade, scores, threshold, exit_delta):
    if threshold is None:
        return None
    for row, score in zip(trade['checks'], scores):
        if row['x'][0] >= exit_delta and score >= threshold and row['fill']:
            return {**row['fill'], 'signal': row['at'], 'delta': row['x'][0],
                    'delta_change': row['x'][1], 'es_change_pct': row['x'][2],
                    'score': float(score)}
    return None


def next_regular_check(stamp, minutes, session_open):
    if stamp.time() < time(16):
        return stamp + timedelta(minutes=minutes)
    next_session = next_weekday_close(stamp)
    return next_session.replace(hour=session_open.hour, minute=session_open.minute)


def confirmation_action(trade, scores, threshold, exit_delta, minutes, grace_minutes,
                        session_open=time(9, 30)):
    """Keep the model's half-hour signal cadence and check delta after the delay."""
    audit = []
    if threshold is None:
        return None, audit
    pending = None
    events = {row['at']: (row, None) for row in trade.get('confirmation_marks', [])}
    events.update({row['at']: (row, score) for row, score in zip(trade['checks'], scores)})
    for row, score in (events[key] for key in sorted(events)):
        stamp = datetime.fromisoformat(row['at'])
        delta = row['x'][0]
        if pending and stamp >= pending[2]:
            signal, signal_delta, due = pending
            within_grace = stamp <= due + timedelta(minutes=grace_minutes)
            confirmed = within_grace and delta >= exit_delta and delta > signal_delta
            audit.append({'signal': signal, 'signal_delta': signal_delta,
                          'expected_check': due.isoformat(), 'next_observed_check': row['at'],
                          'confirmation_delta': delta if within_grace else None,
                          'confirmed': confirmed, 'early_fill': bool(confirmed and row['fill'])})
            if confirmed:
                if row['fill']:
                    return {**row['fill'], 'signal': signal, 'signal_delta': signal_delta,
                            'confirmation': row['at'], 'delta': delta}, audit
                return None, audit
            pending = None
        if pending is None and score is not None and delta >= exit_delta and score >= threshold:
            pending = (row['at'], delta, next_regular_check(stamp, minutes, session_open))
    if pending:
        signal, signal_delta, due = pending
        audit.append({'signal': signal, 'signal_delta': signal_delta,
                      'expected_check': due.isoformat(), 'next_observed_check': None,
                      'confirmation_delta': None, 'confirmed': False, 'early_fill': False})
    return None, audit


def threshold_for(training, score_vectors, exit_delta):
    eligible = np.concatenate([s[as_array(t)[:, 0] >= exit_delta] for t, s in zip(training, score_vectors)])
    if not len(eligible):
        return None
    best = (0., float('inf'), None)
    for threshold in sorted(set(np.quantile(eligible, THRESHOLD_QUANTILES))):
        gain = sum(
            exit_['pnl'] - trade['baseline_pnl']
            for trade, scores in zip(training, score_vectors)
            if (exit_ := action(trade, scores, threshold, exit_delta)) is not None
        )
        if (gain, threshold) > best[:2]:
            best = (gain, float(threshold), float(threshold))
    return best[2]


def summarize(positions, name):
    return {
        'pnl': round(sum((p['exits'][name] or {'pnl': p['baseline_pnl']})['pnl'] for p in positions), 2),
        'call_stops_caught': [p['day'] for p in positions if p['group']=='call stop' and p['exits'][name]],
        'winners_cut': [p['day'] for p in positions if p['group']=='winner' and p['exits'][name]],
        'other_losses_cut': [p['day'] for p in positions if p['group']=='other loss' and p['exits'][name]],
    }


def collect_quotes():
    # The economic-calendar adapter regenerates the exact same entry and baseline quote paths.
    sys.path.insert(0, str(REPORTS))
    from reconstruct_condor_call_paths import collect
    return collect()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--exit-delta', type=float, default=MIN_DELTA,
                        help='Minimum short-call delta at the early exit signal.')
    parser.add_argument('--keep-score-thresholds', action='store_true',
                        help='Use the prior 18-delta run’s score thresholds and change only the exit floor.')
    parser.add_argument('--confirm-next-check', action='store_true',
                        help='Require delta at or above the floor and rising at confirmation.')
    parser.add_argument('--confirmation-minutes', choices=(15, 30), type=int, default=30,
                        help='Minutes after the half-hour signal to confirm delta in regular hours.')
    parser.add_argument('--confirmation-grace-minutes', choices=(0, 1), type=int, default=0,
                        help='Wait one extra minute only when the scheduled 15-minute mark is missing.')
    parser.add_argument('--delta-retention', action='store_true',
                        help='Also test the simple score plus delta retained through the first ES pullback.')
    args = parser.parse_args()
    if args.confirmation_minutes != 30 and not args.confirm_next_check:
        parser.error('--confirmation-minutes requires --confirm-next-check')
    if args.confirmation_grace_minutes and not (args.confirm_next_check and args.confirmation_minutes == 15):
        parser.error('--confirmation-grace-minutes requires 15-minute confirmation')
    if args.delta_retention and args.keep_score_thresholds:
        parser.error('The retention score requires its own walk-forward thresholds')
    saved_thresholds = {}
    if args.keep_score_thresholds:
        prior = json.loads((REPORTS / 'call-hmm-walk-forward-29.json').read_text())
        saved_thresholds = {p['day']:p for p in prior['positions'] if p['scored']}
    input_file = REPORTS / 'call-hmm-input-29.json'
    if not input_file.exists():
        input_file.write_text(json.dumps(collect_quotes(), separators=(',', ':')))
    trades = sorted(json.loads(input_file.read_text()), key=lambda x: timestamp(x['entry']))
    reference = json.loads((REPORTS / 'short-call-delta-retest-22-floor-29-condors.json').read_text())
    rules = {p['day']:p for p in reference['positions']}
    assert len(trades) == len(rules) == 29
    for trade in trades:
        trade['checks'] = checkpoints(trade)
        if args.delta_retention:
            trade['retained_delta'] = first_pullback_retention(trade)
        if args.confirm_next_check and args.confirmation_minutes == 15:
            trade['confirmation_marks'] = checkpoints(trade, minutes=15)
            if args.confirmation_grace_minutes:
                trade['confirmation_marks'] += checkpoints(trade, minutes=15, minute_offset=1)
        assert trade['baseline_pnl'] == rules[trade['day']]['baseline_pnl']
        assert trade['baseline_exit'] == rules[trade['day']]['baseline_exit']

    output = []
    for trade in trades:
        entry_ns = timestamp(trade['entry'])
        training = [t for t in trades if timestamp(t['baseline_exit']) < entry_ns]
        usable = len(training) >= MIN_TRAIN and all(any(t['group']==g for t in training) for g in ('winner','call stop'))
        hit = rules[trade['day']]['rules']['22 delta']
        result = {k:trade[k] for k in ('day','year','group','entry','baseline_exit','baseline_pnl')}
        result.update({'training_days':[t['day'] for t in training], 'scored':usable,
                       'exits': {'baseline':None, '22 delta':hit if hit and 'pnl' in hit else None,
                                 'HMM':None, 'delta trend':None}})
        if args.delta_retention:
            result['exits']['delta trend + retention'] = None
        if usable:
            model = ThreeStateHMM([as_array(t) for t in training])
            stop_rates = state_stop_rates(model, training)
            hmm_scores = lambda t: model.filtered(as_array(t)) @ stop_rates
            result['state_next_session_stop_rates'] = stop_rates.tolist()
            result['state_delta_means'] = (model.means[:,0]*model.scale[0]+model.center[0]).tolist()
            result['thresholds'] = {}
            if args.confirm_next_check:
                result['confirmation_checks'] = {}
            result['forecasts'] = [{'at':r['at'], 'delta':r['x'][0], 'label':r['label']}
                                   for r in trade['checks']]
            scorers = [('HMM',hmm_scores), ('delta trend',trend_scores)]
            if args.delta_retention:
                scorers.append(('delta trend + retention', retention_scores))
                for row, retained in zip(result['forecasts'], trade['retained_delta']):
                    row['retained_delta'] = float(retained)
            for label, score_fn in scorers:
                if args.keep_score_thresholds:
                    prior_fold = saved_thresholds[trade['day']]
                    assert prior_fold['training_days'] == result['training_days']
                    threshold = prior_fold['thresholds'][label]
                else:
                    threshold = threshold_for(training, [score_fn(t) for t in training], args.exit_delta)
                scores = score_fn(trade)
                result['thresholds'][label] = threshold
                if args.confirm_next_check:
                    exit_, audit = confirmation_action(
                        trade, scores, threshold, args.exit_delta, args.confirmation_minutes,
                        args.confirmation_grace_minutes)
                    result['exits'][label] = exit_
                    result['confirmation_checks'][label] = audit
                else:
                    result['exits'][label] = action(trade, scores, threshold, args.exit_delta)
                for row, score in zip(result['forecasts'], scores):
                    row[label] = float(score)
        output.append(result)
        print(result['day'], len(training), result.get('thresholds'), [
              (key, val['pnl']) for key, val in result['exits'].items() if val], flush=True)

    methods = ('baseline','22 delta','delta trend','HMM')
    if args.delta_retention:
        methods += ('delta trend + retention',)
    comparable = [p for p in output if p['scored']]
    summary = {'full_replay':{name:summarize(output,name) for name in methods},
               'common_walk_forward':{name:summarize(comparable,name) for name in methods},
               'warmup_trades': len(output)-len(comparable), 'scored_trades':len(comparable)}
    summary['exit_delta_floor'] = args.exit_delta
    summary['kept_prior_score_thresholds'] = args.keep_score_thresholds
    summary['confirm_next_check'] = args.confirm_next_check
    if args.confirm_next_check:
        summary['confirmation_minutes'] = args.confirmation_minutes
        summary['confirmation_grace_minutes'] = args.confirmation_grace_minutes
    suffix = '' if args.exit_delta == MIN_DELTA else f'-delta-{args.exit_delta:g}'
    if args.keep_score_thresholds:
        suffix += '-fixed-thresholds'
    if args.confirm_next_check:
        suffix += '-confirm-next-check'
        if args.confirmation_minutes != 30:
            suffix += f'-{args.confirmation_minutes}min'
        if args.confirmation_grace_minutes:
            suffix += f'-{args.confirmation_grace_minutes}min-grace'
    if args.delta_retention:
        suffix += '-delta-retention'
    report = REPORTS / f'call-hmm-walk-forward-29{suffix}.json'
    report.write_text(json.dumps({'summary':summary,'positions':output},indent=2))
    print(json.dumps(summary,indent=2))


if __name__ == '__main__':
    main()
