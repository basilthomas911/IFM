"""Read-only Databento cost and record-count estimates; no market-data download."""
import argparse
import concurrent.futures
import json
from pathlib import Path
from import_es_october_backtest import request, save


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--start',required=True)
    parser.add_argument('--end',required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    def estimate(item):
        symbol,dataset,schema=item
        fields=dict(dataset=dataset,symbols=symbol+'.FUT',stype_in='parent',schema=schema,
                    start=args.start,end=args.end)
        with request('metadata.get_cost',fields) as response:
            cost=response.read().decode()
        with request('metadata.get_record_count',fields) as response:
            count=json.load(response)
        return dict(**fields,cost_usd=cost,records_estimate=count)
    jobs=[(symbol,dataset,schema) for symbol,dataset in [('ES','GLBX.MDP3'),('VX','XCBF.PITCH')]
          for schema in ('definition','trades')]
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        estimates=list(pool.map(estimate,jobs))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    save(args.output,dict(estimates=estimates,market_data_downloaded=False))
    print(json.dumps(estimates,indent=2))


if __name__=='__main__':
    main()
