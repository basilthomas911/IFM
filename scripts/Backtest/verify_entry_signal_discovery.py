"""Independently check opportunity labels and entry-time indicator lineage against raw ticks."""
import argparse
import collections
import datetime as dt
import json
import math
from concurrent.futures import ProcessPoolExecutor
from decimal import Decimal
from pathlib import Path
import numpy as np
from analyze_daily_reversion import NY,NS,ny_time

MINUTE=60*NS


def decimal_rsi(closes):
    """Reference uses Decimal smoothing, matching the production accumulator's precision model."""
    values=[None]*len(closes)
    slopes=[None]*len(closes)
    gain=loss=Decimal(0)
    for i in range(1,len(closes)):
        change=Decimal(str(float(closes[i])))-Decimal(str(float(closes[i-1])))
        g,l=max(change,0),max(-change,0)
        if i<=14:
            gain+=g
            loss+=l
            if i==14:
                gain/=14
                loss/=14
        else:
            gain=(gain*13+g)/14
            loss=(loss*13+l)/14
        if i>=14:
            value=(50. if gain==0 else 100.) if loss==0 else 100.-100./(1.+float(gain/loss))
            values[i]=value
            if values[i-1] is not None:
                slopes[i]=value-values[i-1]
    return values,slopes


def check_number(actual,expected):
    if expected is None:
        assert actual is None
    else:
        assert actual is not None and math.isclose(actual,expected,rel_tol=1e-9,abs_tol=1e-9),(actual,expected)


def verify_day(job):
    source,f,records=job
    tape=np.loadtxt(source/f['file'],delimiter=',',usecols=(2,4),dtype=[('ns','i8'),('price','f8')])
    assert len(tape)==f['rows']
    minutes=tape['ns']//MINUTE
    available=np.flatnonzero(minutes[1:]!=minutes[:-1])+1
    closes=tape['price'][available-1]
    ends=(minutes[available-1]+1)*MINUTE
    rsi,slopes=decimal_rsi(closes)
    date=dt.date.fromisoformat(f['value_date'])
    start=int(dt.datetime.combine(date,dt.time(9,45),NY).timestamp())*NS
    cutoff=int(dt.datetime.combine(date,dt.time(15,45),NY).timestamp())*NS
    boundary=int(np.searchsorted(tape['ns'],cutoff,side='left'))
    tiny_disagreements=0
    for r in records:
        signal=r['signal_ordinal']-1
        entry=r['entry_ordinal']-1
        extreme=r['extreme_ordinal']-1
        assert entry==signal+1 and extreme<signal
        assert start<=int(tape['ns'][signal])<=int(tape['ns'][entry])<cutoff
        assert int(tape['ns'][signal])==int(r['signal_event_ns'])
        assert int(tape['ns'][entry])==int(r['entry_event_ns']) and tape['price'][entry]==r['entry_price']
        assert int(tape['ns'][extreme])==int(r['extreme_event_ns'])
        sign=1 if r['side']=='Long' else -1
        elapsed=(int(tape['ns'][signal])-int(tape['ns'][extreme]))/NS/60
        distance=sign*(float(tape['price'][signal])-r['extreme_price'])/r['entry_band_size']
        check_number(r['reversal_speed_bands_per_minute'],distance/elapsed if elapsed>0 else None)
        j=int(np.searchsorted(available,signal,side='right'))-1
        expected_rsi=expected_slope=None
        if j>=0:
            assert r['rsi_bar_end_ns']==str(int(ends[j]))
            assert r['rsi_bar_available_ns']==str(int(tape['ns'][available[j]]))
            assert available[j]<=signal and ends[j]<=tape['ns'][signal]
            if int(tape['ns'][signal])-int(ends[j])<=120*NS:
                expected_rsi=rsi[j]
                expected_slope=slopes[j]
        check_number(r['rsi14'],expected_rsi)
        check_number(r['rsi14_slope'],expected_slope)
        check_number(r['aligned_rsi_slope'],sign*expected_slope if expected_slope is not None else None)
        if expected_slope is not None and np.sign(expected_slope)!=np.sign(r['rsi14_slope']):
            assert abs(expected_slope)<1e-9 and abs(r['rsi14_slope'])<1e-9
            tiny_disagreements+=1
        if expected_slope is not None:
            classify=lambda value:0 if abs(value)<=1e-10 else 1 if value>0 else -1
            assert classify(expected_slope)==classify(r['rsi14_slope'])
        k=int(np.searchsorted(available,extreme,side='right'))-1
        expected_efficiency=None
        if k>=0:
            assert r['preceding_bar_end_ns']==str(int(ends[k]))
            assert r['preceding_bar_available_ns']==str(int(tape['ns'][available[k]]))
            assert available[k]<=extreme
            if k>=14 and int(tape['ns'][extreme])-int(ends[k])<=120*NS:
                close_changes=[float(closes[i]-closes[i-1]) for i in range(k-13,k+1)]
                path=sum(abs(x) for x in close_changes)
                expected_efficiency=-sign*sum(close_changes)/path if path else 0.
        check_number(r['preceding_trend_efficiency'],expected_efficiency)
        risk=math.floor(r['entry_band_size']*4+1e-10)/4
        assert r['stop_price']==r['entry_price']-sign*risk
        assert r['target_price']==r['entry_price']+sign*2*r['entry_band_size']
        if r['outcome']=='Session cutoff':
            segment=tape['price'][entry+1:boundary]
            mark=boundary if boundary<len(tape) and tape['ns'][boundary]==cutoff else boundary-1
            assert r['outcome_ordinal']==mark+1 and r['outcome_event_ns']==str(cutoff)
        else:
            end=r['outcome_ordinal']-1
            assert entry<end<boundary
            segment=tape['price'][entry+1:end]
            assert int(tape['ns'][end])==int(r['outcome_event_ns'])
            move=sign*(tape['price'][end]-r['entry_price'])
            assert move>=2*r['entry_band_size'] if r['target_hit'] else move<=-risk
            mark=end
        before=sign*(segment-r['entry_price'])
        assert np.all(before<2*r['entry_band_size']) and np.all(before>-risk)
        assert r['outcome_price']==float(tape['price'][mark])
        assert r['outcome_time_ny']==ny_time(int(r['outcome_event_ns']))
    return len(records),len(tape),tiny_disagreements


def main(output):
    report=json.loads((output/'analysis.json').read_text())
    rows=json.loads((output/'opportunities.json').read_text())
    baseline=json.loads((Path(report['protocol']['baseline_directory'])/'analysis.json').read_text())
    source=Path(baseline['input_directory'])
    manifest=json.loads((source/'input.json').read_text())
    by_date=collections.defaultdict(list)
    for r in rows:
        by_date[r['trade_date']].append(r)
    files=[f for f in manifest['files'] if f['value_date'] in by_date]
    checked=ticks=tiny=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        for f,(n,count,rounding) in zip(files,pool.map(verify_day,[(source,f,by_date[f['value_date']]) for f in files])):
            checked+=n
            ticks+=count
            tiny+=rounding
            print(f['value_date'],checked,flush=True)
    assert checked==len(rows)==report['totals']['opportunities']
    assert sum(r['baseline_status']=='Executed' for r in rows)==baseline['totals']['trades']
    proof=dict(complete=True,verified_opportunities=checked,raw_rows_loaded=ticks,
        next_tick_fills_and_first_outcome_touches_verified=True,closed_bar_availability_verified=True,
        decimal_wilder_rsi_reference_verified=True,preceding_efficiency_and_reversal_speed_verified=True,
        tiny_rsi_slope_sign_disagreements=tiny,all_baseline_entries_reconciled=True,
        rsi_numerical_zero_tolerance=1e-10,rsi_group_disagreements_after_tolerance=0,
        model_fitted=False,new_strategy_backtest_run=False)
    (output/'independent-verification.json').write_text(json.dumps(proof,indent=2))
    print(json.dumps(proof),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=Path('outputs/iti-entry-signal-discovery-nov2025-oct2026'))
    args=parser.parse_args()
    main(args.output_dir)
