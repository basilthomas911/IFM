"""Replay the saved 35-DTE ES fills and Monthly ITI steps through the CUDA DLL."""
from argparse import ArgumentParser
from bisect import bisect_right
from collections import Counter, defaultdict
import ctypes as c
from datetime import datetime, time, timedelta
from hashlib import sha256
import json
import math
from pathlib import Path
from statistics import NormalDist
import sys
from time import perf_counter
from zoneinfo import ZoneInfo

import databento as db


REPO = Path(__file__).resolve().parents[2]
ART = REPO / '.artifacts'
OUTPUT = ART / 'condor-monte-carlo'
NY = ZoneInfo('America/New_York')
NS = 10**9
MIN = 60 * NS
DAY = 86400 * NS
RATE = .041
IV_LOG_VOL = .50
RISK_EXIT_PROBABILITY = .50
CALL_SWEEP = range(70, 101, 5)
MAX_QUOTE_AGE = 5 * MIN
NORM = NormalDist()


class Input(c.Structure):
    _fields_ = [(key, c.c_float) for key in ('futures_price', 'opening_credit', 'half_spread',
                'annualized_es_vol', 'rate', 'annualized_iv_log_vol', 'years_to_expiry')] + [
        ('strikes', c.c_float * 4), ('iv', c.c_float * 4), ('step_offset', c.c_uint32), ('step_count', c.c_uint32)]


class Step(c.Structure):
    _fields_ = [('variance_years', c.c_float), ('years_to_expiry', c.c_float)]


class Result(c.Structure):
    _fields_ = [(key, c.c_uint32) for key in ('call_stops', 'put_stops', 'profit_targets', 'time_exits',
                                             'recovered')] + [('expected_pnl_points', c.c_float)]


class Engine:
    def __init__(self, dll, paths, seed):
        self.dll = c.CDLL(str(dll.resolve()))
        self.paths, self.seed = paths, seed
        self.batches = self.forecasts = 0
        self.kernel_ms = self.wall_seconds = 0.
        self.dll.condor_mc_abi_version.argtypes = []
        self.dll.condor_mc_abi_version.restype = c.c_uint32
        self.dll.condor_mc_device_v1.argtypes = [c.c_int32, c.POINTER(c.c_char), c.c_uint32]
        self.dll.condor_mc_device_v1.restype = c.c_int32
        self.dll.condor_mc_run_v1.argtypes = [c.c_int32, c.POINTER(Input), c.c_uint32, c.POINTER(Step), c.c_uint32,
            c.c_uint32, c.c_uint64, c.POINTER(Result), c.POINTER(c.c_float), c.POINTER(c.c_char), c.c_uint32]
        self.dll.condor_mc_run_v1.restype = c.c_int32
        if self.dll.condor_mc_abi_version() != 1 or [c.sizeof(x) for x in (Input, Step, Result)] != [68, 8, 24]:
            raise RuntimeError('CUDA DLL ABI mismatch')
        name = c.create_string_buffer(256)
        for i in range(16):
            if self.dll.condor_mc_device_v1(i, name, len(name)) != 0:
                break
            if 'RTX 4090' in name.value.decode():
                self.device, self.name = i, name.value.decode()
                return
        raise RuntimeError('The requested RTX 4090 was not found')

    def run(self, items, count_metrics=True):
        flat = []
        inputs = (Input * len(items))()
        for i, (fields, schedule) in enumerate(items):
            inputs[i] = Input(*fields, len(flat), len(schedule))
            flat.extend(schedule)
        steps = (Step * len(flat))(*flat)
        results = (Result * len(items))()
        ms = c.c_float()
        err = c.create_string_buffer(1024)
        start = perf_counter()
        code = self.dll.condor_mc_run_v1(self.device, inputs, len(inputs), steps, len(steps), self.paths,
                                        self.seed, results, c.byref(ms), err, len(err))
        duration = perf_counter() - start
        if code:
            raise RuntimeError(err.value.decode())
        if count_metrics:
            self.batches += 1
            self.forecasts += len(items)
            self.kernel_ms += ms.value
            self.wall_seconds += duration
        answer = []
        for x in results:
            counts = [x.call_stops, x.put_stops, x.profit_targets, x.time_exits]
            assert sum(counts) == self.paths and 0 <= x.recovered <= self.paths and math.isfinite(x.expected_pnl_points)
            answer.append(dict(zip(('call_stop', 'put_stop', 'profit_target', 'time_exit', 'recovery'),
                                   (n / self.paths for n in counts + [x.recovered]))) |
                          dict(expected_hold_pnl=x.expected_pnl_points * 50))
        return answer

    def verify(self):
        def scenario(price, spread):
            fields = (price, 10., spread, 0., 0., 0., .09,
                      (c.c_float * 4)(90, 85, 110, 115), (c.c_float * 4)(.05, .05, .05, .05))
            return fields, [Step(.001, .08)]

        fixtures = [scenario(112, 0), scenario(88, 0), scenario(100, 0), scenario(100, 8)]
        expected = ['call_stop', 'put_stop', 'profit_target', 'time_exit']
        results = self.run(fixtures, count_metrics=False)
        assert all(result[key] == 1 for key, result in zip(expected, results, strict=True))
        assert results[2]['recovery'] == 1 and results[3]['recovery'] == 1
        noisy = list(fixtures[-1][0])
        noisy[3], noisy[5] = .40, .50
        batch = [(noisy, [Step(.001, .08), Step(.001, .075)])]
        assert self.run(batch, False) == self.run(batch, False)


def read(path):
    return json.loads(path.read_text())


def stamp(s):
    return int(datetime.fromisoformat(s).timestamp()) * NS


def et(s):
    return datetime.fromtimestamp(s / NS, NY)


def replace_once(code, anchor, replacement):
    if code.count(anchor) != 1:
        raise ValueError(f'Unexpected replay source around {anchor!r}')
    return code.replace(anchor, replacement)


def load_harness(year, folder, *, selection_time=time(10), strict_setup_quotes=False):
    if year == 2023:
        src = ART / 'es-condor-two-year/test-third-year-friday-condors.py'
        loader = src.read_text(encoding='utf-8-sig').split("\nassert mode=='run'", 1)[0]
        env, args = {}, sys.argv
        try:
            sys.argv = [str(src), 'cache-only']
            exec(compile(loader, str(src), 'exec'), env)
        finally:
            sys.argv = args
        code = env['code']
    elif year == 2024:
        src = folder / 'annual.py'
        code = src.read_text(encoding='utf-8-sig').replace('def run(day):', 'def run(day, earliest=None):')
        code = code.replace('if 10<=bid<50:', 'if 10<=bid<50 and (earliest is None or t>=earliest):')
    else:
        src = folder.parent / 'replay-40pct.py'
        code = src.read_text(encoding='utf-8-sig').split('\nimport statistics\n', 1)[0]

    if year < 2025:
        anchor = (
            '        if local.weekday()>=5 or not dt.time(9,30)<=local.time()<=dt.time(16):continue\n'
            '        s=snapshot(t)\n'
            '        if s is None:continue\n'
        )
        code = replace_once(code, anchor, (
            '        s=snapshot(t)\n'
            '        if s is None:continue\n'
            '        trace.append((t, s[0], s[1], s[2], s[4]))\n'
            '        if local.weekday()>=5 or not dt.time(9,30)<=local.time()<=dt.time(16):continue\n'
        ))
    else:
        anchor = (
            '        if stamp.weekday()>=5 or not dt.time(9,30)<=stamp.time()<=dt.time(16):\n'
            '            continue\n'
            '        snap=evaluate(t)\n'
            '        if snap is None:\n'
            '            continue\n'
            '        q,bid,ask,ds=snap\n'
        )
        code = replace_once(code, anchor, (
            '        snap=evaluate(t)\n'
            '        if snap is None:\n'
            '            continue\n'
            '        q,bid,ask,ds=snap\n'
            '        if fill is not None and t>fill:\n'
            '            trace.append((t, fx[underlying].get(t//MIN-1), bid, ask, q))\n'
            '        if stamp.weekday()>=5 or not dt.time(9,30)<=stamp.time()<=dt.time(16):\n'
            '            continue\n'
        ))
    if selection_time != time(10):
        setup = f'dt.time({selection_time.hour}, {selection_time.minute})'
        code = replace_once(code, 'dt.datetime.combine(day,dt.time(10),NY)',
                            f'dt.datetime.combine(day,{setup},NY)')
        code = replace_once(code, '.replace(hour=15)', '.replace(hour=15, minute=0)')
        setup_label = f'{selection_time.hour:02}:{selection_time.minute:02}'
        code = code.replace('10 ET', f'{setup_label} ET').replace('10:00', setup_label)
        if year >= 2025:
            code = replace_once(code, 'utc(entry-dt.timedelta(minutes=30)),\n                        utc(entry+dt.timedelta(minutes=1))',
                                'utc(entry-dt.timedelta(minutes=5)),\n                        utc(entry+dt.timedelta(minutes=1))')
    if strict_setup_quotes:
        if year < 2025:
            code = replace_once(code, 'utc(start-td(minutes=5)),utc(start+td(minutes=1))',
                                'utc(start),utc(start+td(minutes=1))')
        else:
            lookback = 30 if selection_time == time(10) else 5
            code = replace_once(
                code, f'utc(entry-dt.timedelta(minutes={lookback})),\n                        utc(entry+dt.timedelta(minutes=1))',
                'utc(entry),\n                        utc(entry+dt.timedelta(minutes=1))',
            )

    env = {'__name__': 'cuda_condor_replay'}
    exec(compile(code, str(src), 'exec'), env)

    def cached(name, schema, symbols, stype, start, end):
        if isinstance(symbols, dict):
            ids = ','.join(str(i) for i in sorted(set(symbols.values())))
            name += '-' + sha256((ids + '|' + start + '|' + end).encode()).hexdigest()[:16]
        path = folder / (name + '.dbn.zst')
        if not path.exists():
            raise FileNotFoundError(path)
        return db.DBNStore.from_file(path)

    env['get'] = cached
    return env


def reconstruct():
    inputs = [
        (2023, ART / 'es-condor-2023-2024', ART / 'es-condor-two-year/monthly-42-friday-gate/2023'),
        (2024, ART / 'es-condor-2024-2025', ART / 'es-condor-any-day/bar-replay/2024'),
        (2025, ART / 'es-condor-full-year/35dte-biweekly', ART / 'es-condor-any-day/bar-replay/2025'),
    ]
    positions = []
    for year, folder, monthly_folder in inputs:
        original = read(folder / 'friday-35dte-40pct-rerun.json')['positions']
        front = {r['value_date']: r['contract_id'] for r in read(monthly_folder / 'input.json')['files']}
        events = sorted((json.loads(line) for line in (monthly_folder / 'signals.jsonl').read_text().splitlines() if line),
                        key=lambda r: (r['available_at_ns'], r['source_row']))
        env = load_harness(year, folder)
        last_exit = None
        for r in original:
            if r['status'] != 'Closed':
                continue
            day = datetime.fromisoformat(r['entry']).date()
            old = r.get('exits', {}).get('40', r)
            earliest = stamp(last_exit) if last_exit and et(stamp(last_exit)).date() == day else None
            env['trace'] = []
            rerun = env['run'](day, earliest)
            result = rerun.get('exits', {}).get(40, rerun)
            assert rerun['entry'] == r['entry'] and rerun['credit'] == r['credit']
            assert all(result[k] == old[k] for k in ('exit', 'reason', 'gross_usd', 'debit', 'exit_deltas'))
            last_exit = old['exit']
            start, end = stamp(r['entry']), stamp(last_exit)
            trace = [q for q in env['trace'] if start < q[0] <= end]
            if year == 2025:
                legs = r['locked_legs']
            else:
                legs = r['legs']
            assert len(trace) and len(legs) == 4
            assert all(a[0] < b[0] for a, b in zip(trace, trace[1:]))
            assert trace[-1][0] == end and trace[-1][3] == old['debit']
            chosen_events = [x for x in events if start < x['available_at_ns'] < end
                             and x['contract'] == front.get(x['file_value_date'])]
            assert all(x['source_timestamp_ns'] <= x['available_at_ns'] for x in chosen_events)
            positions.append(dict(year=year, day=str(day), start=start, end=end, original_pnl=old['gross_usd'],
                original_exit=old['reason'], original_deltas=old['exit_deltas'], credit=r['credit'],
                expiry=legs[0]['expiry'], strikes=[leg['strike'] for leg in legs], events=chosen_events, trace=trace))
        print(f'Reconstructed {year}: {sum(row["year"] == year for row in positions)} fills', flush=True)
    assert len(positions) == 37 and sum(x['original_pnl'] for x in positions) == -3725
    return positions


def option_iv(f, k, mid, years, sign):
    disc = math.exp(-RATE * years)
    if not disc * max(0, sign * (f-k)) < mid < disc * (f if sign > 0 else k):
        return None
    lo, hi = 0., 3. * math.sqrt(years)
    for _ in range(40):
        scale = (lo + hi) / 2
        d1 = math.log(f/k)/scale + scale/2
        value = disc * sign * (f*NORM.cdf(sign*d1) - k*NORM.cdf(sign*(d1-scale)))
        if value > mid:
            hi = scale
        else:
            lo = scale
    scale = (lo + hi)/2
    delta = disc * NORM.cdf(sign*(math.log(f/k)/scale + scale/2))
    return scale / math.sqrt(years), delta


def schedule(now, deadline, expiry):
    """Regular-hours checks every 15 minutes. Assign 25% of a session's variance to its overnight gap."""
    now, limit = et(now), et(deadline)
    day = now.date()
    previous = now
    while day <= limit.date():
        if day.weekday() < 5:
            opening = datetime.combine(day, time(9, 30), NY)
            closing = min(datetime.combine(day, time(16), NY), limit)
            for i in range(27):
                current = min(opening + timedelta(minutes=i*15), closing)
                if current <= previous or current < opening:
                    continue
                intraday = (current - max(previous, opening)).total_seconds() / (6.5 * 3600)
                variance = (.75*intraday + (.25 if previous < opening else 0.)) / 252
                yield Step(variance, (expiry - int(current.timestamp())*NS)/(365*DAY))
                previous = current
                if current == closing:
                    break
        day += timedelta(days=1)


def minute_events(position):
    first = (position['start'] // MIN + 1) * MIN
    for when in range(first, position['end'], MIN):
        local = et(when)
        if local.weekday() < 5 and time(9, 30) <= local.time() <= time(16):
            yield {'available_at_ns': when, 'mode': 'MinuteTimer', 'direction': None}


def checkpoints(position, *, minute_timer=False):
    trace = position['trace']
    times = [q[0] for q in trace]
    credit, expiry = position['credit'], position['expiry']
    strikes = position['strikes']
    # Calendar duration, as in the original replay, rather than the realized exit.
    deadline = int((et(position['start']) + timedelta(days=14)).timestamp()) * NS
    events = minute_events(position) if minute_timer else position['events']
    for event in events:
        when = event['available_at_ns']
        local = et(when)
        detail = dict(iti_at=local.isoformat(), iti_mode=event['mode'], iti_direction=event['direction'],
                      regular_hours=local.weekday() < 5 and time(9, 30) <= local.time() <= time(16))
        i = bisect_right(times, when) - 1
        if i < 0 or when - times[i] > (0 if minute_timer else MAX_QUOTE_AGE):
            yield detail, 'missing fresh as-of quote', None
            continue
        q_at, futures, bid, ask, legs = trace[i]
        mid = (ask + bid) / 2
        detail |= dict(quote_at=et(q_at).isoformat(), midpoint_pnl=(credit-mid)*50, close_now_pnl=(credit-ask)*50)
        if minute_timer:
            detail['futures'] = futures
        if mid <= credit:
            yield detail, 'nonnegative mark', None
            continue
        if not futures or ask < bid:
            yield detail, 'invalid model inputs', None
            continue
        years = (expiry - when) / (365*DAY)
        # Fit to the minute mark that generated the book; decay to ITI availability in the forecast.
        quote_years = (expiry - q_at) / (365*DAY)
        fitted = [option_iv(futures, k, sum(q)/2, quote_years, -1 if j < 2 else 1)
                  for j, (k, q) in enumerate(zip(strikes, legs, strict=True))]
        if any(x is None for x in fitted):
            yield detail, 'invalid model inputs', None
            continue
        ivs = [x[0] for x in fitted]
        steps = list(schedule(when, deadline, expiry))
        if not steps:
            yield detail, 'no remaining forecast horizon', None
            continue
        fields = (futures, credit, (ask-bid)/2, (ivs[0]+ivs[2])/2, RATE, IV_LOG_VOL, years,
                  (c.c_float*4)(*strikes), (c.c_float*4)(*ivs))
        side = 'put' if fitted[0][1] > fitted[2][1] else 'call'
        detail |= dict(pressured_side=side, short_put_delta=fitted[0][1], short_call_delta=fitted[2][1])
        # Use the first executable book at/after the ITI step for a simulated order.
        j = i if q_at == when else i+1
        if j < len(trace) and trace[j][0] < position['end'] and trace[j][0]-when <= MAX_QUOTE_AGE:
            detail |= dict(executable_at=et(trace[j][0]).isoformat(), executable_exit_pnl=round((credit-trace[j][3])*50, 2))
        yield detail, 'forecast', (fields, steps)


def replay(positions, engine):
    active = [(x, iter(checkpoints(x))) for x in positions]
    counts = Counter()
    while active:
        batch = []
        for position, stream in active:
            for detail, status, args in stream:
                counts[status] += 1
                detail['status'] = status
                position.setdefault('checks', []).append(detail)
                if args is not None:
                    batch.append((position, stream, detail, args))
                    break
        if not batch:
            break
        forecasts = engine.run([args for _, _, _, args in batch])
        active = []
        for (position, stream, detail, _), forecast in zip(batch, forecasts, strict=True):
            assert detail['midpoint_pnl'] < 0
            detail['forecast'] = forecast
            pressure = forecast[detail['pressured_side'] + '_stop']
            should_exit = (pressure >= RISK_EXIT_PROBABILITY and forecast['expected_hold_pnl'] < detail['close_now_pnl'])
            if should_exit and 'executable_exit_pnl' in detail:
                position['early_exit'] = detail
            else:
                if should_exit:
                    counts['exit decision without executable quote before original exit'] += 1
                active.append((position, stream))
    return counts


def summarize(positions):
    closed = [r for r in positions if 'early_exit' in r]
    for r in positions:
        r['policy_pnl'] = r['early_exit']['executable_exit_pnl'] if 'early_exit' in r else r['original_pnl']
    improvements = [r for r in closed if r['original_pnl'] < 0 and r['policy_pnl'] > r['original_pnl']]
    worsened = [r for r in closed if r['original_pnl'] < 0 and r['policy_pnl'] < r['original_pnl']]
    winners_cut = [r for r in closed if r['original_pnl'] > 0]
    return dict(trades=len(positions), early_exits=len(closed), original_pnl=sum(r['original_pnl'] for r in positions),
        policy_pnl=sum(r['policy_pnl'] for r in positions), losses_improved=len(improvements),
        loss_dollars_avoided=sum(r['policy_pnl']-r['original_pnl'] for r in improvements),
        losses_worsened=len(worsened), added_loss_dollars=sum(r['original_pnl']-r['policy_pnl'] for r in worsened),
        winners_cut_early=len(winners_cut), winners_turned_into_losses=sum(r['policy_pnl'] < 0 for r in winners_cut),
        winner_pnl_sacrificed=sum(r['original_pnl']-r['policy_pnl'] for r in winners_cut))


def call_sweep(positions, engine):
    checks = Counter()
    batch = []
    for position in positions:
        by_trade = position.setdefault('checks', [])
        for detail, status, args in checkpoints(position):
            if status == 'forecast' and detail['pressured_side'] != 'call':
                status = 'put side pressured'
            checks[status] += 1
            detail['status'] = status
            by_trade.append(detail)
            if status == 'forecast':
                assert detail['midpoint_pnl'] < 0
                batch.append((detail, args))

    if batch:
        for (detail, _), result in zip(batch, engine.run([args for _, args in batch]), strict=True):
            detail['forecast'] = result
    assert engine.forecasts == checks['forecast']

    runs = {}
    for threshold in CALL_SWEEP:
        trades = []
        for position in positions:
            row = position.copy()
            for detail in row['checks']:
                forecast = detail.get('forecast')
                if (forecast and forecast['call_stop'] >= threshold/100
                        and forecast['expected_hold_pnl'] < detail['close_now_pnl']
                        and 'executable_exit_pnl' in detail):
                    row['early_exit'] = detail
                    break
            trades.append(row)
        totals = summarize(trades)
        years = {str(y): summarize([r for r in trades if r['year'] == y]) for y in (2023, 2024, 2025)}
        assert sum(y['policy_pnl'] for y in years.values()) == totals['policy_pnl']
        runs[str(threshold)] = dict(results=totals, years=years, trades=[
            dict(day=r['day'], original_pnl=r['original_pnl'], policy_pnl=r['policy_pnl'],
                 early_exit=r.get('early_exit')) for r in trades])

    audit = [dict(day=r['day'], original_pnl=r['original_pnl'], end=et(r['end']).isoformat(), checks=r['checks'])
             for r in positions]
    return dict(checks=checks, runs=runs, positions=audit)


def main():
    parser = ArgumentParser()
    parser.add_argument('--dll', default=OUTPUT/'condor_mc.dll', type=Path)
    parser.add_argument('--output', default=OUTPUT/'replay.json', type=Path)
    parser.add_argument('--paths', default=16384, type=int)
    parser.add_argument('--seed', default=20261115, type=int)
    parser.add_argument('--verify-only', action='store_true')
    parser.add_argument('--call-sweep', action='store_true',
                        help='Replay call-side thresholds from 70 to 100 percent on the same forecasts.')
    args = parser.parse_args()
    engine = Engine(args.dll, args.paths, args.seed)
    engine.verify()
    print(f'CUDA ABI and deterministic fixtures passed on {engine.name}', flush=True)
    if args.verify_only:
        return
    positions = reconstruct()
    if args.call_sweep:
        report = call_sweep(positions, engine)
        report |= dict(device=engine.name, paths_per_forecast=engine.paths, seed=engine.seed, kernel_ms=engine.kernel_ms)
        output = OUTPUT/'call-sweep.json' if args.output == OUTPUT/'replay.json' else args.output
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2))
        print(json.dumps(dict(checks=report['checks'], kernel_ms=engine.kernel_ms,
            runs={k: {key: row[key] for key in ('results', 'years')} for k, row in report['runs'].items()}), indent=2),
            flush=True)
        return
    before_policy = Counter()
    for position in positions:
        before_policy.update(status for _, status, _ in checkpoints(position))
    counts = replay(positions, engine)
    totals = summarize(positions)
    years = {str(y): summarize([r for r in positions if r['year'] == y]) for y in (2023, 2024, 2025)}
    assert engine.forecasts == counts['forecast'] and totals['trades'] == 37
    for r in positions:
        del r['trace'], r['events']
    report = dict(device=engine.name, paths_per_forecast=engine.paths, seed=engine.seed,
                  kernel_ms=engine.kernel_ms, forecast_wall_seconds=engine.wall_seconds,
                  batches=engine.batches, steps_while_open=counts, original_position_steps=before_policy,
                  results=totals, years=years, positions=positions)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2))
    print(json.dumps({key: value for key, value in report.items() if key != 'positions'}, indent=2), flush=True)


if __name__ == '__main__':
    main()
