"""Independent array-based first-passage checks for the fixed continuation diagnostic."""
import argparse
import collections
import datetime as dt
import json
import math
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
import numpy as np
from analyze_daily_reversion import NY,NS,ny_time


def verify_day(job):
    source,f,trades,records=job
    tape=np.loadtxt(source/f['file'],delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows']
    assert np.all(tape['ns'][1:]>=tape['ns'][:-1])
    cutoff=int(dt.datetime.combine(dt.date.fromisoformat(f['value_date']),dt.time(15,45),NY).timestamp())*NS
    boundary=int(np.searchsorted(tape['ns'],cutoff,side='left'))
    by_id={t['trade_id']:t for t in trades}
    for r in records:
        t=by_id[r['trade_id']]
        exit_index=t['exit_ordinal']-1
        assert int(tape['ns'][exit_index])==int(t['exit_event_ns'])
        assert tape['price'][exit_index]==t['exit_price']
        start=exit_index+1
        sign=1 if t['side']=='Long' else -1
        scan=tape[start:boundary]
        stops=np.flatnonzero(sign*(scan['price']-t['stop_price'])<=0)
        if len(stops):
            stop_index=start+int(stops[0])
            end=stop_index+1
            assert r['terminal_reason']=='Original stop'
            assert r['terminal_time_ny']==ny_time(int(tape['ns'][stop_index]))
            assert r['terminal_price']==float(tape['price'][stop_index])
        else:
            end=boundary
            mark=boundary if boundary<len(tape) and tape['ns'][boundary]==cutoff else boundary-1
            assert r['terminal_reason']=='Session cutoff' and r['terminal_time_ny']==ny_time(cutoff)
            assert r['terminal_price']==float(tape['price'][mark])
        path=tape[start:end]
        gains=np.concatenate(([t['points']],sign*(path['price']-t['entry_price'])))
        drawdowns=np.maximum.accumulate(gains)-gains
        assert float(gains.min())==r['post_exit_minimum_points']
        assert float(gains.max())==r['post_exit_maximum_points']
        assert float(drawdowns.max())==r['post_exit_maximum_drawdown_points']
        assert bool(np.any(gains[1:]<=0))==r['revisited_entry_before_stop_or_cutoff']
        for target in (2,3):
            hits=np.flatnonzero(gains[1:]>=target*t['entry_band_size'])
            assert bool(len(hits))==r[f'target_{target}_hit']
            if len(hits):
                offset=int(hits[0])
                index=start+offset
                assert r[f'target_{target}_ordinal']==index+1
                assert r[f'target_{target}_event_ns']==str(int(tape['ns'][index]))
                assert r[f'target_{target}_price']==float(tape['price'][index])
                minimum=float(gains[:offset+2].min())
                assert r[f'target_{target}_minimum_points']==minimum
                assert r[f'target_{target}_giveback_from_original_exit_points']==max(0,t['points']-minimum)
                assert r[f'target_{target}_maximum_drawdown_points']==float(drawdowns[:offset+2].max())
                assert math.isclose(r[f'target_{target}_minutes_after_original_exit'],
                    (int(tape['ns'][index])-int(t['exit_event_ns']))/NS/60,abs_tol=1e-12)
        assert math.isclose(r['terminal_points'],sign*(r['terminal_price']-t['entry_price']),abs_tol=1e-12)
    return len(records),len(tape)


def main(output):
    report=json.loads((output/'analysis.json').read_text())
    baseline=json.loads((Path(report['protocol']['baseline_directory'])/'analysis.json').read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    records=collections.defaultdict(list)
    for r in report['paths']:
        records[r['trade_date']].append(r)
    trades=collections.defaultdict(list)
    for t in baseline['trades']:
        if t['exit_reason']=='Trailing watermark':
            trades[t['trade_date']].append(t)
    files=[f for f in manifest['files'] if f['value_date'] in records]
    checked=rows=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        for f,(n,count) in zip(files,pool.map(verify_day,[(source,f,trades[f['value_date']],records[f['value_date']]) for f in files])):
            checked+=n
            rows+=count
            print(f['value_date'],checked,flush=True)
    assert checked==len(report['paths'])==report['all_watermark']['exits']
    for label,population in [('all_watermark',report['paths']),('initial_one_band',[r for r in report['paths'] if r['original_watermark_bands']==1])]:
        assert len(population)==report[label]['exits']
        for target in (2,3):
            assert sum(r[f'target_{target}_hit'] for r in population)==report[label][f'target_{target}']['hits']
    proof=dict(complete=True,independent_array_first_passage_verification=True,verified_paths=checked,
        raw_rows_loaded_for_dates_with_watermark_exits=rows,original_exit_prices_and_ordinals_verified=True,
        first_stop_and_cutoff_verified=True,target_first_touches_verified=True,path_drawdowns_verified=True,
        primary_initial_one_band_paths=report['initial_one_band']['exits'],parameters_searched=False)
    (output/'independent-verification.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=Path('outputs/iti-nov2025-oct2026-watermark-continuation'))
    args=parser.parse_args()
    main(args.output_dir)
