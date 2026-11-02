"""Compare natural strategy paths and their identical-entry exit outcomes."""
import csv
import json
import statistics
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[1]
WITH_STOP=REPO/'outputs/iti-may-oct-2026-threshold-minus-one-band'
sys.path.insert(0,str(REPO/'scripts/Backtest'))
from analyze_dc_to_overshoot import summarize


def read(path):
    return json.loads(path.read_text())


def metrics(trades):
    """Direction subsets retain chronological order; these are not standalone portfolios."""
    winners=[t['points'] for t in trades if t['points']>0]
    losers=[t['points'] for t in trades if t['points']<0]
    return dict(summarize(trades),win_rate=len(winners)/len(trades),
        average_winner_points=statistics.mean(winners) if winners else None,
        average_loser_points=-statistics.mean(losers) if losers else None,
        average_trade_points=statistics.mean(t['points'] for t in trades),
        worst_trade_points=min(t['points'] for t in trades),
        maximum_intratrade_adverse_points=max(-t['max_adverse_points'] for t in trades))


def write_csv(path,rows):
    with path.open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def entry_key(trade):
    return (trade['trade_date'],trade['contract_id'],trade['side'],trade['setup_group'],trade['entry_ordinal'])


def main():
    stopped=read(WITH_STOP/'original/analysis.json')
    unstopped=read(ROOT/'original/analysis.json')
    stop_proof=read(WITH_STOP/'original/independent-verification.json')
    no_stop_proof=read(ROOT/'original/independent-verification.json')
    assert stop_proof['complete'] and stop_proof['entry_threshold_minus_one_band_stop_verified']
    assert no_stop_proof['complete'] and no_stop_proof['no_stop_verified']
    assert stopped['input_sha256']==unstopped['input_sha256']
    assert stopped['start_date']==unstopped['start_date']=='2026-05-01'
    assert stopped['end_date_exclusive']==unstopped['end_date_exclusive']=='2026-11-01'
    excluded={'stop','protective_stop_bands'}
    assert {k:v for k,v in stopped['assumptions'].items() if k not in excluded}=={
        k:v for k,v in unstopped['assumptions'].items() if k not in excluded}
    assert read(WITH_STOP/'protocol.json')['source_files']==read(ROOT/'protocol.json')['source_files']
    months=sorted({d['trade_date'][:7] for d in unstopped['daily']})
    monthly=[]
    for month in months:
        for side in ('Long','Short'):
            a=[t for t in stopped['trades'] if t['side']==side and t['trade_date'].startswith(month)]
            b=[t for t in unstopped['trades'] if t['side']==side and t['trade_date'].startswith(month)]
            monthly.append(dict(month=month,side=side,with_stop=metrics(a),without_stop=metrics(b)))
    combined={side:dict(with_stop=metrics([t for t in stopped['trades'] if t['side']==side]),
        without_stop=metrics([t for t in unstopped['trades'] if t['side']==side])) for side in ('Long','Short')}
    a={entry_key(t):t for t in stopped['trades']}
    b={entry_key(t):t for t in unstopped['trades']}
    assert len(a)==len(stopped['trades']) and len(b)==len(unstopped['trades'])
    shared=a.keys() & b.keys()
    pairs=[]
    for key in sorted(shared):
        x,y=a[key],b[key]
        for field in ('entry_event_ns','entry_price','entry_signal_ordinal','entry_band_size','overshoot_extreme'):
            assert x[field]==y[field]
        if x['exit_reason']!='4-band stop':
            assert x['points']==y['points'] and x['exit_event_ns']==y['exit_event_ns']
        pairs.append(dict(date=x['trade_date'],side=x['side'],entry_ordinal=x['entry_ordinal'],
            entry_time_ny=x['entry_time_ny'],entry_price=x['entry_price'],
            with_stop_points=x['points'],without_stop_points=y['points'],
            stop_minus_no_stop_points=x['points']-y['points'],
            with_stop_exit=x['exit_reason'],without_stop_exit=y['exit_reason'],
            with_stop_exit_time=x['exit_time_ny'],without_stop_exit_time=y['exit_time_ny']))
    matched={}
    extra=[]
    for side in ('Long','Short'):
        keys={k for k in shared if k[2]==side}
        only_a={k for k in a.keys()-b.keys() if k[2]==side}
        only_b={k for k in b.keys()-a.keys() if k[2]==side}
        stopped_keys={k for k in keys if a[k]['exit_reason']=='4-band stop'}
        paired_delta=sum(a[k]['points']-b[k]['points'] for k in keys)
        extra_a=sum(a[k]['points'] for k in only_a)
        extra_b=sum(b[k]['points'] for k in only_b)
        assert paired_delta+extra_a-extra_b==combined[side]['with_stop']['points']-combined[side]['without_stop']['points']
        matched[side]=dict(shared_entries=len(keys),with_stop_points=sum(a[k]['points'] for k in keys),
            without_stop_points=sum(b[k]['points'] for k in keys),stop_minus_no_stop_points=paired_delta,
            stop_only_entries=len(only_a),stop_only_points=extra_a,
            no_stop_only_entries=len(only_b),no_stop_only_points=extra_b,
            shared_stop_outs=len(stopped_keys),would_have_won_without_stop=sum(b[k]['points']>0 for k in stopped_keys),
            stop_improved_outcome=sum(a[k]['points']>b[k]['points'] for k in stopped_keys),
            stop_worsened_outcome=sum(a[k]['points']<b[k]['points'] for k in stopped_keys))
        for label,keys,ledger in [('with_stop_only',only_a,a),('no_stop_only',only_b,b)]:
            for k in sorted(keys):
                t=ledger[k]
                extra.append(dict(policy=label,date=t['trade_date'],side=side,
                    entry_ordinal=t['entry_ordinal'],entry_time_ny=t['entry_time_ny'],points=t['points'],exit=t['exit_reason']))
    october=[t for t in unstopped['trades'] if t['trade_date'].startswith('2026-10')]
    prior=read(REPO/'outputs/iti-october-2026-two-band-opposite-watermark-no-stop/original/analysis.json')
    assert len(october)==len(prior['trades'])
    for x,y in zip(october,prior['trades']):
        assert {k:v for k,v in x.items() if k!='trade_id'}=={k:v for k,v in y.items() if k!='trade_id'}
    assert summarize(october)==prior['totals']
    report=dict(complete=True,period='2026-05-01 through 2026-10-31',
        comparison='Protective stop at frozen threshold minus one band versus no protective stop; all other rules identical',
        monthly=monthly,combined=combined,all_trades=dict(with_stop=metrics(stopped['trades']),without_stop=metrics(unstopped['trades'])),
        matched_entries=matched,matched_entry_definition='Same date, contract, side, setup group and raw entry ordinal; entry prices, timestamps and band sizes verified identical',
        entry_availability_changes_are_reported_separately=True,directional_subsets_are_not_standalone_long_only_or_short_only_runs=True,
        source_ticks_verified_per_policy=no_stop_proof['raw_ticks'],source_trading_dates=len(unstopped['daily']),
        verification=dict(with_stop=stop_proof,without_stop=no_stop_proof),october_no_stop_exact_reconciliation=True,costs_included=False)
    (ROOT/'stop-versus-no-stop-analysis.json').write_text(json.dumps(report,indent=2))
    rows=[dict(month=m['month'],side=m['side'],with_stop_trades=m['with_stop']['trades'],without_stop_trades=m['without_stop']['trades'],
        with_stop_points=m['with_stop']['points'],without_stop_points=m['without_stop']['points'],
        with_stop_gross_usd=m['with_stop']['gross_pnl_usd'],without_stop_gross_usd=m['without_stop']['gross_pnl_usd']) for m in monthly]
    write_csv(ROOT/'monthly-direction-comparison.csv',rows)
    write_csv(ROOT/'matched-entry-trades.csv',pairs)
    write_csv(ROOT/'unmatched-entry-trades.csv',extra)
    print(json.dumps(dict(complete=True,combined=combined,matched_entries=matched),indent=2))


if __name__=='__main__':
    main()
