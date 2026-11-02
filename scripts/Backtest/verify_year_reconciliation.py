"""Reconcile annual input coverage, October regression and monthly P&L."""
import argparse
import json
import hashlib
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',required=True,type=Path)
    args=parser.parse_args()
    report=json.loads((args.output_dir/'analysis.json').read_text())
    manifest=json.loads((Path(report['assumptions']['input_directory'])/'input.json').read_text())
    run=Path(report['source_run_directory'])
    result=json.loads((run/'input.json.results.json').read_text())
    validation=json.loads((Path(report['assumptions']['input_directory'])/'input-validation.json').read_text())
    independent=json.loads((args.output_dir/'independent-verification.json').read_text())
    assert validation['validated'] and validation['input_sha256']==manifest['input_sha256']==result['input_sha256']
    assert result['accepted_es_ticks']==manifest['es_rows']==independent['verified_source_ticks']
    assert independent['verified_trades']==report['policies']['hold']['totals']['trades']
    dates=[f['value_date'] for f in manifest['files']]
    assert dates==sorted(set(dates)) and dates[0]=='2025-11-03' and dates[-1]=='2026-10-30'
    assert manifest['start']=='2025-11-01T00:00:00Z' and manifest['end']=='2026-11-01T00:00:00Z'
    assert manifest['input_sha256']==hashlib.sha256(json.dumps(manifest['files'],sort_keys=True,separators=(',',':')).encode()).hexdigest()
    raw=json.loads(Path('.artifacts/backtest-nov2025-sep2026/ES/import-manifest.json').read_text())
    contracts=sorted({p['contract_id'] for p in raw['partitions']})
    expected_dates={p['value_date'] for p in raw['partitions'] if p['value_date']<'2026-10-01'}
    assert set(d for d in dates if d<'2026-10-01')==expected_dates
    selected={(p['value_date'],p['contract_id']):p for p in raw['partitions']}
    for f in manifest['files']:
        if f['value_date']<'2026-10-01':
            date=f['value_date'].replace('-','')
            front=next(c for c in contracts if c[-8:]>date and int(c[-4:-2]) in (3,6,9,12))
            assert front==f['contract_id']
            assert selected[(f['value_date'],front)]['rows']==f['source_rows']
            assert f['rows']+f['skipped_no_prior_vx']==f['source_rows']
    prior=json.loads(Path('.artifacts/iti-backtest/e1ce5fbc17b7/input.json.results.json').read_text())
    old=[e for e in prior['signals'] if e['TimePeriod']==1]
    new=[e for e in result['signals'] if e['TimePeriod']==1 and e['ValueDate'].startswith('2026-10')]
    assert old==new,'October production Daily signals changed'
    old_report=json.loads(Path('outputs/iti-dc-first-reversal-no-stop-0945-1545-october-2026/analysis.json').read_text())
    annual=report['policies']['hold']
    old_trades=old_report['policies']['hold']['trades']
    new_trades=[t for t in annual['trades'] if t['trade_date'].startswith('2026-10')]
    assert len(old_trades)==len(new_trades)==35
    for old_trade,new_trade in zip(old_trades,new_trades):
        for key,value in old_trade.items():
            if key!='trade_id':
                assert value==new_trade[key],(key,old_trade['trade_id'])
    assert sum(d['gross_pnl_usd'] for d in annual['daily'])==sum(m['gross_pnl_usd'] for m in annual['monthly'])==annual['totals']['gross_pnl_usd']
    assert len(annual['monthly'])==12
    proof=dict(complete=True,trading_dates=len(dates),raw_front_contract_date_coverage_verified=True,
               october_daily_signals_identical=len(new),october_trades_identical=len(new_trades),
               october_gross_pnl_usd=sum(t['gross_pnl_usd'] for t in new_trades),
               monthly_daily_trade_pnl_reconciled=True,replay_mode=result['replay_mode'])
    (args.output_dir/'year-reconciliation.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof))


if __name__=='__main__':
    main()
