"""Compare a single watermark change with identical fixed round-trip cost scenarios."""
import argparse
import csv
import json
from pathlib import Path


def performance(trades,cost):
    pnl=[t['gross_pnl_usd']-cost for t in trades]
    wins=[x for x in pnl if x>0]
    losses=[x for x in pnl if x<0]
    equity=peak=drawdown=0
    for x in pnl:
        equity+=x
        peak=max(peak,equity)
        drawdown=max(drawdown,peak-equity)
    return dict(trades=len(pnl),wins=len(wins),losses=len(losses),flats=pnl.count(0),
                gross_pnl_usd=sum(t['gross_pnl_usd'] for t in trades),
                cost_per_round_trip_usd=cost,total_cost_usd=len(pnl)*cost,net_pnl_usd=sum(pnl),
                average_net_trade_usd=sum(pnl)/len(pnl) if pnl else None,
                average_win_points=sum(wins)/len(wins)/50 if wins else None,
                average_loss_points=-sum(losses)/len(losses)/50 if losses else None,
                profit_factor=sum(wins)/-sum(losses) if losses else None,
                max_closed_trade_drawdown_usd=drawdown)


def main(baseline_dir,candidate_dir):
    baseline=json.loads((baseline_dir/'analysis.json').read_text())
    candidate=json.loads((candidate_dir/'analysis.json').read_text())
    assert baseline['input_sha256']==candidate['input_sha256']
    assert baseline['checks']==candidate['checks']
    assert [d['trade_date'] for d in baseline['daily']]==[d['trade_date'] for d in candidate['daily']]
    ignored={'watermark_lag_bands','watermark_buffer_bands','watermark_rule'}
    assert {k:v for k,v in baseline['assumptions'].items() if k not in ignored}=={
        k:v for k,v in candidate['assumptions'].items() if k not in ignored}
    changes={k:dict(baseline=baseline['assumptions'].get(k,0),candidate=candidate['assumptions'].get(k,0))
             for k in ('watermark_lag_bands','watermark_buffer_bands')
             if baseline['assumptions'].get(k,0)!=candidate['assumptions'].get(k,0)}
    assert len(changes)==1,'Only one watermark parameter may change'
    scenarios=[]
    for cost in (0,5,10,20):
        old=performance(baseline['trades'],cost)
        new=performance(candidate['trades'],cost)
        scenarios.append(dict(cost_per_round_trip_usd=cost,baseline=old,candidate=new,
                              net_improvement_usd=new['net_pnl_usd']-old['net_pnl_usd']))
    monthly=[]
    for m in candidate['monthly']:
        month=m['month']
        old=performance([t for t in baseline['trades'] if t['trade_date'].startswith(month)],10)
        new=performance([t for t in candidate['trades'] if t['trade_date'].startswith(month)],10)
        monthly.append(dict(month=month,baseline_trades=old['trades'],candidate_trades=new['trades'],
            baseline_gross_usd=old['gross_pnl_usd'],candidate_gross_usd=new['gross_pnl_usd'],
            baseline_net_usd=old['net_pnl_usd'],candidate_net_usd=new['net_pnl_usd'],
            net_improvement_usd=new['net_pnl_usd']-old['net_pnl_usd']))
    entry_key=lambda t:(t['trade_date'],t['entry_ordinal'],t['side'],t['entry_price'])
    old_entries={entry_key(t):t for t in baseline['trades']}
    new_entries={entry_key(t):t for t in candidate['trades']}
    changed=[]
    for key,old in old_entries.items():
        new=new_entries.get(key)
        if new and (old['exit_ordinal'],old['exit_price'])!=(new['exit_ordinal'],new['exit_price']):
            changed.append(dict(trade_date=old['trade_date'],entry_time_ny=old['entry_time_ny'],side=old['side'],
                                baseline_points=old['points'],candidate_points=new['points'],
                                improvement_usd=new['gross_pnl_usd']-old['gross_pnl_usd']))
    report=dict(baseline_directory=str(baseline_dir),candidate_directory=str(candidate_dir),
        primary_cost_per_round_trip_usd=10,
        cost_model='Illustrative fixed all-in commission/fees/spread/slippage deduction per completed one-contract trade; no bid/ask reconstruction; source trade-price stop gaps already retained',
        daily_loss_halt='Unchanged: first gross losing trade; cost deductions are reporting only',
        only_strategy_change=changes,
        identical_entry_sets=old_entries.keys()==new_entries.keys(),changed_exits=changed,
        baseline_trades_reaching_two_bands_before_exit=sum(t['max_favorable_points']>=2*t['entry_band_size'] for t in baseline['trades']),
        scenarios=scenarios,monthly=monthly)
    assert sum(m['candidate_net_usd'] for m in monthly)==scenarios[2]['candidate']['net_pnl_usd']
    assert sum(m['baseline_net_usd'] for m in monthly)==scenarios[2]['baseline']['net_pnl_usd']
    (candidate_dir/'cost-comparison.json').write_text(json.dumps(report,indent=2))
    for name,records in [('monthly-comparison',monthly),('trades-with-costs',[
            dict(t,assumed_round_trip_cost_usd=10,net_pnl_usd=t['gross_pnl_usd']-10) for t in candidate['trades']])]:
        with (candidate_dir/(name+'.csv')).open('w',newline='') as stream:
            writer=csv.DictWriter(stream,fieldnames=list(records[0]))
            writer.writeheader()
            writer.writerows(records)
    print(json.dumps(report,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-dir',type=Path,required=True)
    parser.add_argument('--candidate-dir',type=Path,required=True)
    args=parser.parse_args()
    main(args.baseline_dir,args.candidate_dir)
