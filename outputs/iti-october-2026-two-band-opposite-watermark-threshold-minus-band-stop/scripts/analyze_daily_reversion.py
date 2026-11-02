"""Causal Daily ES velocity-reversion estimate from verified October ITI replay.

One ES, two progressive reversal events after an observed extended overshoot,
03:00 <= entry < 15:45 New York. Signals warm overnight; counts start at 03:00.
Trade-price proxies only, without quote fills, commissions or broker simulation.
"""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
from zoneinfo import ZoneInfo

NY = ZoneInfo('America/New_York')
MODES = {0: 'Trending', 1: 'TrendExtremeChanged', 2: 'TrendDirectionChanged',
         3: 'TrendReversalChanged', 7: 'TrendOvershootExtended', 8: 'TrendOvershootReversal'}
NS = 1_000_000_000


def event_ns(text):
    body = text.removesuffix('Z')
    seconds, _, fraction = body.partition('.')
    stamp = dt.datetime.fromisoformat(seconds).replace(tzinfo=dt.timezone.utc)
    return int(stamp.timestamp()) * NS + int((fraction + '000000000')[:9])


def ny_time(ns):
    seconds, fraction = divmod(ns, NS)
    return (dt.datetime.fromtimestamp(seconds, dt.timezone.utc) + dt.timedelta(microseconds=fraction//1000)).astimezone(NY).isoformat(timespec='microseconds')


def price_ticks(value):
    ticks = round(float(value) * 4)
    if not math.isclose(ticks / 4, float(value), abs_tol=1e-8):
        raise ValueError('ES price is not an exact quarter point')
    return ticks


class DaySimulator:
    def __init__(self, date):
        self.date = date
        day = dt.date.fromisoformat(date)
        self.start = int(dt.datetime.combine(day, dt.time(3), NY).timestamp()) * NS
        self.end = int(dt.datetime.combine(day, dt.time(15, 45), NY).timestamp()) * NS
        self.group = None
        self.trend = None
        self.extreme = None
        self.qualified = False
        self.reversals = []
        self.used = set()
        self.position = None
        self.pending_entry = None
        self.pending_exit = None
        self.previous = None
        self.finished = False
        self.trades = []
        self.decisions = []
        self.counters = collections.Counter()
        self.ordinal = 0

    def decision(self, kind, ns, **fields):
        self.decisions.append(dict(date=self.date, kind=kind, time_ny=ny_time(ns), event_ns=str(ns), **fields))

    def close(self, ns, price, reason, reference_ns=None):
        p = self.position
        if p is None:
            raise AssertionError('Cannot close a flat position')
        pnl_ticks = p['direction'] * (price - p['entry_ticks'])
        row = dict(trade_date=self.date, side='Long' if p['direction'] == 1 else 'Short',
            quantity=1, multiplier=50, setup_group=p['setup_group'],
            qualification_time_ny=ny_time(p['qualification_ns']),
            first_reversal_time_ny=ny_time(p['first_reversal_ns']),
            entry_signal_time_ny=ny_time(p['signal_ns']), entry_time_ny=ny_time(p['entry_ns']),
            entry_price=p['entry_ticks']/4, stop_price=p['stop_ticks']/4,
            initial_risk_points=abs(p['entry_ticks']-p['stop_ticks'])/4,
            dc_time_ny=ny_time(p['dc_ns']) if p['dc_ns'] is not None else None,
            dc_price=p['dc_ticks']/4 if p['dc_ticks'] is not None else None,
            dc_group=p['dc_group'],
            exit_signal_time_ny=ny_time(p['exit_signal_ns']) if p.get('exit_signal_ns') is not None else None,
            exit_time_ny=ny_time(ns), exit_price=price/4, exit_reason=reason,
            exit_price_reference_time_ny=ny_time(reference_ns if reference_ns is not None else ns),
            points=pnl_ticks/4, gross_pnl_usd=pnl_ticks*12.5,
            outcome='Win' if pnl_ticks > 0 else 'Loss' if pnl_ticks < 0 else 'Flat',
            entry_event_ns=str(p['entry_ns']), exit_event_ns=str(ns),
            entry_signal_event_ns=str(p['signal_ns']),
            entry_source_ordinal=p['entry_ordinal'], entry_signal_source_ordinal=p['signal_ordinal'],
            exit_source_ordinal=self.ordinal,
            max_favorable_points=p['mfe_ticks']/4, max_adverse_points=p['mae_ticks']/4,
            holding_minutes=(ns-p['entry_ns'])/NS/60)
        self.trades.append(row)
        self.decision('Exit', ns, reason=reason, price=price/4, points=pnl_ticks/4)
        self.position = self.pending_exit = None

    def tick(self, ns, price, event=None):
        self.ordinal += 1
        if self.previous is not None and ns < self.previous[0]:
            raise AssertionError('Nonchronological input')
        # The scheduled cutoff uses only the last price observable at that time.
        if not self.finished and ns >= self.end:
            if self.position is not None:
                reference = (ns, price) if ns == self.end else self.previous
                if reference is None or reference[0] > self.end:
                    raise AssertionError('No causal price for scheduled flatten')
                self.close(self.end, reference[1], 'Session flatten', reference[0])
            if self.pending_entry is not None:
                self.counters['pending_entry_canceled_at_close'] += 1
            self.pending_entry = None
            self.finished = True

        active = self.start <= ns < self.end and not self.finished
        if active and self.position is not None:
            p = self.position
            move = p['direction'] * (price-p['entry_ticks'])
            p['mfe_ticks'] = max(p['mfe_ticks'], move)
            p['mae_ticks'] = min(p['mae_ticks'], move)
            if p['direction'] * (price-p['stop_ticks']) <= 0:
                self.close(ns, price, 'Protective stop')
            elif self.pending_exit is not None:
                self.close(ns, price, 'Overshoot extended')
            elif p['dc_ns'] is not None and self.ordinal > p['dc_ordinal'] and self.previous is not None:
                previous_distance = p['direction'] * (self.previous[1]-p['dc_ticks'])
                distance = p['direction'] * (price-p['dc_ticks'])
                # Ignore repeats at the DC price; exit on a genuine return/touch
                # from the favorable side, or an immediate adverse cross after DC.
                if previous_distance >= 0 and distance <= 0 and (previous_distance > 0 or distance < 0):
                    self.close(ns, price, 'DC price return')

        if active and self.position is None and self.pending_entry is not None:
            pending = self.pending_entry
            self.pending_entry = None
            if pending['direction'] * (price-pending['stop_ticks']) <= 0:
                self.counters['entry_skipped_invalid_stop_distance'] += 1
                self.decision('Entry canceled', ns, reason='Entry price reached/passed frozen stop', price=price/4)
            else:
                self.position = dict(pending, entry_ns=ns, entry_ticks=price, entry_ordinal=self.ordinal,
                    dc_ns=None, dc_ticks=None, dc_group=None, mfe_ticks=0, mae_ticks=0)
                self.decision('Entry filled', ns, side='Long' if pending['direction'] == 1 else 'Short',
                    price=price/4, stop=pending['stop_ticks']/4, setup_group=pending['setup_group'])

        if event is not None:
            self.signal(ns, price, event, active)
        self.previous = (ns, price)

    def signal(self, ns, price, e, active):
        mode = e['IntrinsicTimeMode']
        group = e['IntrinsicTimeGroupId']
        trend = e['IntrinsicTimeTrend']
        direction = 1 if trend == 0 else -1
        if self.position is not None:
            p = self.position
            if mode == 2 and direction == p['direction'] and group != p['setup_group'] and p['dc_ns'] is None:
                p.update(dc_ns=ns, dc_ticks=price, dc_group=group, dc_ordinal=self.ordinal)
                self.decision('DC confirmed', ns, price=price/4, group=group)
            elif mode == 7 and p['dc_group'] == group and direction == p['direction']:
                p['exit_signal_ns'] = ns
                self.pending_exit = ns
                self.decision('Overshoot exit signaled', ns, price=price/4, group=group)

        if mode == 2 or self.group != group:
            self.group, self.trend = group, trend
            self.extreme = price_ticks(e['TrendExtreme'])
            self.qualified = False
            self.reversals = []
            self.qualification_ns = None
        elif mode in (1, 7):
            self.extreme = price_ticks(e['TrendExtreme'])
            self.reversals = []
            if mode == 7:
                self.qualified = True
                self.qualification_ns = ns
        elif mode in (3, 8) and self.qualified and active:
            self.reversals.append(ns)
            if len(self.reversals) == 2 and self.group not in self.used and self.position is None and self.pending_entry is None:
                self.used.add(self.group)
                self.pending_entry = dict(direction=-direction, setup_group=group, stop_ticks=self.extreme,
                    signal_ns=ns, signal_ordinal=self.ordinal, qualification_ns=self.qualification_ns, first_reversal_ns=self.reversals[0])
                self.decision('Entry signaled', ns, side='Long' if direction == -1 else 'Short',
                    price=price/4, stop=self.extreme/4, group=group)

    def finish(self):
        if not self.finished:
            raise AssertionError('Tick coverage does not reach the scheduled cutoff')
        if self.position is not None or self.pending_entry is not None:
            raise AssertionError('Day did not finish flat')
        return self.trades


def verify_ledger(trades):
    seen = set()
    for trade in trades:
        key = (trade['trade_date'], trade['setup_group'])
        assert key not in seen, 'More than one attempt per excursion'
        seen.add(key)
        entry = dt.datetime.fromisoformat(trade['entry_time_ny'])
        exit = dt.datetime.fromisoformat(trade['exit_time_ny'])
        assert entry.date() == exit.date() == dt.date.fromisoformat(trade['trade_date'])
        assert dt.time(3) <= entry.time() < dt.time(15, 45)
        assert entry <= exit and exit.time() <= dt.time(15, 45)
        assert int(trade['entry_event_ns']) >= int(trade['entry_signal_event_ns'])
        assert trade['entry_source_ordinal'] == trade['entry_signal_source_ordinal'] + 1
        sign = 1 if trade['side'] == 'Long' else -1
        points = sign * (trade['exit_price']-trade['entry_price'])
        assert points == trade['points'] and points * 50 == trade['gross_pnl_usd']
        assert sign * (trade['entry_price']-trade['stop_price']) > 0
        if trade['exit_reason'] == 'Protective stop':
            assert sign * (trade['exit_price']-trade['stop_price']) <= 0
        if trade['exit_reason'] in ('Overshoot extended', 'DC price return'):
            assert trade['dc_time_ny'] is not None
        if trade['exit_reason'] == 'Overshoot extended':
            assert trade['exit_signal_time_ny'] is not None
        if trade['exit_reason'] == 'Session flatten':
            assert exit.time() == dt.time(15, 45)
    for previous, current in zip(trades, trades[1:]):
        assert int(previous['exit_event_ns']) <= int(current['entry_event_ns']), 'Overlapping positions'
        if previous['trade_date'] == current['trade_date']:
            assert previous['exit_source_ordinal'] < current['entry_source_ordinal']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-dir', type=Path, required=True)
    parser.add_argument('--input-dir', type=Path, required=True)
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    if (args.output_dir / 'analysis.json').exists():
        raise ValueError('Choose a new output directory; existing analysis is retained')
    results = json.loads((args.run_dir/'input.json.results.json').read_text())
    source = json.loads((args.input_dir/'input.json').read_text())
    assert results['input_sha256'] == source['input_sha256']
    daily_events = collections.defaultdict(list)
    for event in results['signals']:
        if event['TimePeriod'] == 1:
            event['_ns100'] = event_ns(event['IntrinsicTime']) // 100
            daily_events[event['ValueDate']].append(event)
    assert sum(map(len, daily_events.values())) == results['generated_signals']['Daily'] == 5288
    all_trades, daily_summary, decisions = [], [], []
    source_tick_count = matched = 0
    for item in source['files']:
        date = item['value_date']
        sim = DaySimulator(date)
        events = daily_events[date]
        index = count = 0
        digest = hashlib.sha256()
        with (args.input_dir/item['file']).open('rb') as stream:
            for line in stream:
                digest.update(line)
                columns = line.split(b',')
                ns, price = int(columns[2]), price_ticks(columns[4])
                e = events[index] if index < len(events) else None
                if e is not None and e['_ns100'] < ns//100:
                    raise AssertionError(f'Unmatched ITI event {date}/{index}: {e["IntrinsicTime"]}')
                if e is not None and e['_ns100'] == ns//100 and price_ticks(e['IntrinsicPrice']) == price:
                    event = e
                    index += 1
                else:
                    event = None
                sim.tick(ns, price, event)
                count += 1
        assert index == len(events)
        assert count == item['rows']
        assert digest.hexdigest() == item['sha256']
        matched += index
        source_tick_count += count
        trades = sim.finish()
        verify_ledger(trades)
        for trade in trades:
            trade['trade_id'] = f'ES-DVR-{date}-{len(all_trades)+1:03d}'
            all_trades.append(trade)
        decisions.extend(sim.decisions)
        daily = dict(trade_date=date, trades=len(trades), wins=sum(t['points']>0 for t in trades),
            losses=sum(t['points']<0 for t in trades), flats=sum(t['points']==0 for t in trades),
            stops=sum(t['exit_reason']=='Protective stop' for t in trades),
            overshoot_exits=sum(t['exit_reason']=='Overshoot extended' for t in trades),
            dc_return_exits=sum(t['exit_reason']=='DC price return' for t in trades),
            session_exits=sum(t['exit_reason']=='Session flatten' for t in trades),
            points=sum(t['points'] for t in trades), gross_pnl_usd=sum(t['gross_pnl_usd'] for t in trades),
            matched_daily_events=index, source_ticks=count)
        daily_summary.append(daily)
        print(json.dumps(daily), flush=True)
    verify_ledger(all_trades)
    assert source_tick_count == source['es_rows'] and matched == 5288
    total = sum(t['gross_pnl_usd'] for t in all_trades)
    assert total == sum(d['gross_pnl_usd'] for d in daily_summary)
    profit = sum(t['gross_pnl_usd'] for t in all_trades if t['gross_pnl_usd']>0)
    loss = sum(t['gross_pnl_usd'] for t in all_trades if t['gross_pnl_usd']<0)
    cumulative = peak = drawdown = 0
    for trade in all_trades:
        cumulative += trade['gross_pnl_usd']
        peak = max(peak, cumulative)
        drawdown = max(drawdown, peak-cumulative)
    report = dict(strategy='Daily single-leg velocity reversion, October 2026',
        source_run=args.run_dir.name, input_sha256=source['input_sha256'],
        assumptions=dict(instrument='ES20261218', quantity=1, point_multiplier_usd=50,
            session='03:00 <= entries < 15:45 America/New_York; flat at 15:45',
            warmup='Use overnight signal state; reversal counting begins at 03:00',
            qualification='Observed TrendOvershootExtended; no additional extension minimum',
            entry='Second progressive reversal (ordinary or overshoot); next observed tick price',
            resets='New published extreme resets reversal count; DC starts a new excursion; Trending ignored',
            stop='Frozen published extreme; first reaching/crossing trade price; conservative priority on gaps',
            profit_exit='Next same-trend overshoot extended after trade-direction DC; next observed tick price',
            fallback_exit='Return/touch across DC confirmation price; first observed crossing tick; no exit on DC tick itself',
            flatten='Scheduled 15:45 exit marked at last trade observable at/before cutoff',
            limits='One position, one attempt per excursion; no fees, spread, liquidity, latency or risk-budget simulation'),
        checks=dict(matched_daily_events=matched, verified_source_ticks=source_tick_count,
            source_file_hashes_verified=True, session_and_stop_rules_verified=True,
            daily_and_trade_pnl_reconciled=True, maximum_open_positions=1),
        totals=dict(trades=len(all_trades), wins=sum(t['points']>0 for t in all_trades),
            losses=sum(t['points']<0 for t in all_trades), flats=sum(t['points']==0 for t in all_trades),
            points=sum(t['points'] for t in all_trades), gross_pnl_usd=total,
            gross_winning_pnl_usd=profit, gross_losing_pnl_usd=loss,
            profit_factor=profit/-loss if loss else None,
            max_closed_trade_drawdown_usd=drawdown,
            exit_reasons=dict(collections.Counter(t['exit_reason'] for t in all_trades))),
        daily=daily_summary, trades=all_trades)
    (args.output_dir/'analysis.json').write_text(json.dumps(report, indent=2))
    (args.output_dir/'decision-audit.json').write_text(json.dumps(decisions, indent=2))
    for name, records in (('daily-pnl.csv',daily_summary), ('trade-ledger.csv', all_trades)):
        with (args.output_dir/name).open('w',newline='',encoding='utf-8') as stream:
            writer = csv.DictWriter(stream, fieldnames=list(records[0]) if records else ['trade_id'])
            writer.writeheader()
            writer.writerows(records)
    print(json.dumps(report['totals']), flush=True)


if __name__ == '__main__':
    main()
