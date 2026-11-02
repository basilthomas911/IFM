"""October 2026: reverse the retracement entry side, with watermark-only exits.

Both policies use the same setup rules, optional frozen stop, no daily loss cap,
next-tick fills and the 09:45–15:45 New York window. One live position per policy.
"""
import collections
import argparse
import csv
import hashlib
import json
import math
import time
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from analyze_daily_reversion import event_ns,price_ticks
from analyze_two_reversal_two_band import ReversalSimulator,SOURCE,RUN
from analyze_dc_to_overshoot import summarize

OUTPUT=Path('outputs/iti-october-2026-two-band-opposite-watermark-no-stop')
POLICIES=('original','retracement')


def analyze_day(job):
    """Stream each source tick once through both causally independent policies."""
    f,events,stop_bands,adverse_recovery=job
    sims={side:ReversalSimulator(f['value_date'],watermark=True,stop_bands=stop_bands,
        daily_loss_limit=None,entry_reversals=2,watermark_start_bands=2,
        entry_direction=side,reversal_profit_exit=False,adverse_recovery=adverse_recovery) for side in POLICIES}
    digest=hashlib.sha256()
    i=0
    with (SOURCE/f['file']).open('rb') as stream:
        for ordinal,line in enumerate(stream,1):
            digest.update(line)
            c=line.split(b',')
            ns,price=int(c[2]),price_ticks(c[4])
            event=events[i] if i<len(events) else None
            assert event is None or event['_ns100']>=ns//100
            if event is not None and event['_ns100']==ns//100 and price_ticks(event['IntrinsicPrice'])==price:
                i+=1
            else:
                event=None
            for sim in sims.values():
                sim.tick(ns,price,event)
    assert ordinal==f['rows'] and digest.hexdigest()==f['sha256'] and i==len(events)
    return f,{side:sim.finish() for side,sim in sims.items()},ordinal,i


def write_csv(path,records):
    """Write detached ledger rows without serializing nested exit summaries."""
    cleaned=[{k:v for k,v in r.items() if k!='exit_reasons'} for r in records]
    with path.open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(cleaned[0]) if cleaned else ['trade_id'])
        writer.writeheader()
        writer.writerows(cleaned)


def main(output=OUTPUT,stop_bands=None,adverse_recovery=False):
    """Retain the protocol before seeing outcomes and preserve previous evidence."""
    started=time.perf_counter()
    output.mkdir(parents=True,exist_ok=False)
    source=json.loads((SOURCE/'input.json').read_text())
    replay=json.loads((RUN/'input.json.results.json').read_text())
    assert replay['input_sha256']==source['input_sha256']
    files=[f for f in source['files'] if '2026-10-01'<=f['value_date']<'2026-11-01']
    assert len(files)==22 and all((SOURCE/f['file']).is_file() for f in files)
    assumptions=dict(timeframe='Daily',quantity=1,usd_per_point=50,
        session='09:45 <= entry < 15:45 America/New_York; flatten at last observed price at cutoff',
        entry_reversals=2,
        entry='After an observed overshoot, second progressive one-band reversal event; new extreme/DC resets count; next observed raw tick fill, without waiting for a bounce; one attempt per excursion',
        band_definition='BandSize from authoritative ITI replay, 20% of primary threshold; freeze entry-signal BandSize',
        protective_stop_bands=stop_bands,
        stop='No protective stop' if stop_bands is None else f'Freeze {stop_bands:g} entry bands from actual fill; round distance inward to an ES quarter point; exit first touching/crossing raw price, retaining gaps',daily_loss_limit=None,
        reversal_profit_exit=False,profit_exit='Trailing watermark only, plus session flatten',
        watermark_enabled=True,watermark_start_bands=2,watermark_lag_bands=0,watermark_buffer_bands=0,
        watermark_rule='Activate only after signed profit >2 entry bands; floor ratchets to each whole-band gain, never retreats; exit first raw price strictly below signed floor, retaining gap price',
        costs='Gross trade-price proxies; no commissions, quote spread, slippage model or latency',
        comparison='Only entry side differs; each policy owns its own position lifecycle, so subsequent entries may diverge')
    if adverse_recovery:
        assumptions.update(adverse_recovery=True,adverse_recovery_rule='inside-threshold-then-pullback-v2',threshold_is_loss_cap=False,
            stop='Threshold is the same frozen inward-rounded entry distance as the immediate-stop benchmark; strict adverse breach starts waiting, not an exit; only strict recovery inside threshold arms the exit',
            adverse_exit='Ignore all rebounds while outside threshold; arm at first observed price strictly inside; advance recovery mark to observed price only on a further full-band favorable move (including gaps); exit first full-band adverse pullback from recovery mark OR adverse touch/cross of original threshold; actual raw trade fill; threshold recross has priority; existing profit watermark and session cutoff remain')
    protocol=dict(start_date='2026-10-01',end_date_exclusive='2026-11-01',
        assumptions=assumptions,input_sha256=source['input_sha256'],
        source_run_directory=str(RUN),input_directory=str(SOURCE),
        source_files=files,scripts_sha256={name:hashlib.sha256((Path(__file__).parent/name).read_bytes()).hexdigest()
            for name in ('analyze_opposite_retracement.py','analyze_two_reversal_two_band.py','verify_two_reversal_two_band.py')})
    (output/'protocol.json').write_text(json.dumps(protocol,indent=2))
    dates={f['value_date'] for f in files}
    events=collections.defaultdict(list)
    for event in replay['signals']:
        if event['TimePeriod']==1 and event['ValueDate'] in dates:
            event['_ns100']=event_ns(event['IntrinsicTime'])//100
            events[event['ValueDate']].append(event)
    trades={side:[] for side in POLICIES}
    daily={side:[] for side in POLICIES}
    rows=matched=0
    with ProcessPoolExecutor(max_workers=4) as pool:
        for f,results,count,n in pool.map(analyze_day,[(f,events[f['value_date']],stop_bands,adverse_recovery) for f in files]):
            for side,ts in results.items():
                for t in ts:
                    t.update(trade_id=f'{f["value_date"]}-{len(trades[side])+1:03}',contract_id=f['contract_id'])
                    trades[side].append(t)
                daily[side].append(dict(trade_date=f['value_date'],**summarize(ts)))
            rows+=count
            matched+=n
            print(f['value_date'],{side:summarize(ts) for side,ts in results.items()},flush=True)
    assert rows==sum(f['rows'] for f in files) and matched==sum(map(len,events.values()))
    summary={}
    for side in POLICIES:
        folder=output/side
        folder.mkdir()
        totals=summarize(trades[side])
        assert totals['gross_pnl_usd']==sum(d['gross_pnl_usd'] for d in daily[side])
        risk=dict(worst_trade_usd=min((t['gross_pnl_usd'] for t in trades[side]),default=0),
            largest_intratrade_adverse_usd=max((-t['max_adverse_points']*50 for t in trades[side]),default=0),
            closed_trade_drawdown_is_not_intraday_equity_drawdown=True)
        report=dict(start_date='2026-10-01',end_date_exclusive='2026-11-01',
            input_sha256=source['input_sha256'],source_run_directory=str(RUN),input_directory=str(SOURCE),
            assumptions=dict(assumptions,entry_direction=side),totals=totals,risk=risk,
            checks=dict(raw_source_ticks=rows,daily_signals=matched,source_hashes_verified=True),
            daily=daily[side],trades=trades[side])
        (folder/'analysis.json').write_text(json.dumps(report,indent=2))
        write_csv(folder/'trades.csv',trades[side])
        write_csv(folder/'daily.csv',daily[side])
        summary[side]=dict(totals=totals,risk=risk)
    result=dict(policies=summary,analysis_seconds=time.perf_counter()-started,
        raw_ticks=rows,daily_signals=matched,
        gross_pnl_difference_usd=summary['original']['totals']['gross_pnl_usd']-summary['retracement']['totals']['gross_pnl_usd'])
    (output/'comparison.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=OUTPUT)
    parser.add_argument('--stop-bands',type=float,default=None)
    parser.add_argument('--adverse-recovery',action='store_true')
    args=parser.parse_args()
    if args.stop_bands is not None and (not math.isfinite(args.stop_bands) or args.stop_bands<=0):
        parser.error('Stop bands must be finite and positive')
    if args.adverse_recovery and args.stop_bands is None:
        parser.error('--adverse-recovery requires --stop-bands')
    main(args.output_dir,args.stop_bands,args.adverse_recovery)
