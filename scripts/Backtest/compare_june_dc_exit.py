"""Reconcile June's unchanged baseline and summarize the opposite-DC exit trial."""
import csv
import json
from pathlib import Path

OUTPUT=Path('outputs/iti-june-2026-opposite-dc-exit-comparison')
ORIGINAL=Path('outputs/iti-dc-first-reversal-no-stop-0945-1545-nov2025-oct2026')


def metrics(policy):
    ts=policy['trades']
    gains=[t['gross_pnl_usd'] for t in ts if t['gross_pnl_usd']>0]
    losses=[t['gross_pnl_usd'] for t in ts if t['gross_pnl_usd']<0]
    return dict(policy['totals'],win_rate_percent=len(gains)/len(ts)*100,
                average_winner_usd=sum(gains)/len(gains),average_loser_usd=sum(losses)/len(losses),
                largest_trade_loss_usd=min(t['gross_pnl_usd'] for t in ts),
                worst_daily_pnl_usd=min(d['gross_pnl_usd'] for d in policy['daily']))


def main():
    report=json.loads((OUTPUT/'analysis.json').read_text())
    original=json.loads((ORIGINAL/'analysis.json').read_text())['policies']['hold']
    baseline=report['policies']['hold']
    trial=report['policies']['exit-dc']
    old_trades=[t for t in original['trades'] if t['trade_date'].startswith('2026-06')]
    assert len(old_trades)==len(baseline['trades'])==54
    for old,new in zip(old_trades,baseline['trades']):
        assert {k:v for k,v in old.items() if k!='trade_id'}=={k:v for k,v in new.items() if k!='trade_id'}
    assert [d for d in original['daily'] if d['trade_date'].startswith('2026-06')]==baseline['daily']
    for policy,filename in [('hold','independent-verification.json'),('exit-dc','exit-dc-independent-verification.json')]:
        proof=json.loads((OUTPUT/filename).read_text())
        assert proof['verified_trades']==report['policies'][policy]['totals']['trades']
        assert proof['verified_source_ticks']==report['checks']['source_rows']==11003263
    old_days={d['trade_date']:d for d in baseline['daily']}
    daily=[]
    for d in trial['daily']:
        old=old_days[d['trade_date']]
        daily.append(dict(trade_date=d['trade_date'],current_trades=old['trades'],trial_trades=d['trades'],
                          current_pnl_usd=old['gross_pnl_usd'],trial_pnl_usd=d['gross_pnl_usd'],
                          improvement_usd=d['gross_pnl_usd']-old['gross_pnl_usd']))
    with (OUTPUT/'daily-comparison.csv').open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(daily[0]))
        writer.writeheader()
        writer.writerows(daily)
    result=dict(complete=True,month='2026-06',current=metrics(baseline),trial=metrics(trial),
                gross_pnl_improvement_usd=trial['totals']['gross_pnl_usd']-baseline['totals']['gross_pnl_usd'],
                closed_trade_drawdown_reduction_usd=baseline['totals']['max_closed_trade_drawdown_usd']-trial['totals']['max_closed_trade_drawdown_usd'],
                checks=dict(baseline_trades_and_daily_pnl_identical=True,both_policies_independently_verified=True,
                            raw_ticks=report['checks']['source_rows'],daily_signals=report['checks']['matched_daily_events']),
                scope='Daily ES only; same replay, source hashes, 09:45-15:45 NY, one ES at $50/point; no protective stop; gross before costs',
                trial_rule=report['assumptions']['exit_dc'],
                note='Re-entry is simulated after later DC events; this is a full policy comparison, not frozen original entries.',daily=daily)
    assert sum(d['improvement_usd'] for d in daily)==result['gross_pnl_improvement_usd']
    (OUTPUT/'comparison.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='daily'}))
    print(json.dumps(daily))


if __name__=='__main__':
    main()
