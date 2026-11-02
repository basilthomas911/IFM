"""Read-only progress from staged import manifests, checkpoints and proofs."""
import argparse
import json
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    args=parser.parse_args()
    status={}
    for symbol in ('ES','VX'):
        root=args.root/symbol
        manifest=json.loads((root/'import-manifest.json').read_text())
        done=json.loads((root/'load-checkpoint.json').read_text()) if (root/'load-checkpoint.json').exists() else {}
        by_date={}
        for p in manifest['partitions']:
            by_date[p['value_date']]=by_date.get(p['value_date'],0)+p['rows']
        completed={table:sum(rows for date,rows in by_date.items() if table+'/'+date in done)
                   for table in manifest['tables']}
        row=dict(new_trades_target_per_table=manifest['total_rows'],
                 new_trades_loaded_by_table=completed,
                 load_percent=round(100*sum(completed.values())/(manifest['total_rows']*len(completed)),2),
                 complete_table_dates=len(done))
        if (root/'verification.json').exists():
            proof=json.loads((root/'verification.json').read_text())
            row.update(full_row_verification_complete=proof['verified'],
                       total_trades_per_table_including_october=proof['expected_rows_per_table'])
        status[symbol]=row
    print(json.dumps(status,indent=2))


if __name__=='__main__':
    main()
