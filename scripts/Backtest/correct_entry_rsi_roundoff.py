"""Retain original reports and reclassify only numerical-zero RSI slopes after reference verification."""
import csv
import datetime as dt
import json
from discover_iti_entry_signals import OUTPUT,RSI_ZERO_TOLERANCE,analyze


def main():
    report=json.loads((OUTPUT/'analysis.json').read_text())
    proof=json.loads((OUTPUT/'independent-verification.json').read_text())
    assert proof['complete'] and proof['tiny_rsi_slope_sign_disagreements']==8
    rows=json.loads((OUTPUT/'opportunities.json').read_text())
    for name in ('analysis','protocol','independent-verification'):
        original=OUTPUT/(name+'.json')
        retained=OUTPUT/(name+'-before-numerical-flat-correction.json')
        assert not retained.exists()
        retained.write_bytes(original.read_bytes())
    changed=[r for r in rows if r['aligned_rsi_slope'] is not None and 0<abs(r['aligned_rsi_slope'])<=RSI_ZERO_TOLERANCE]
    assert len(changed)==8
    correction=dict(timestamp_utc=dt.datetime.now(dt.timezone.utc).isoformat(),
        reason='Decimal reference found eight sign disagreements only at floating-point roundoff magnitude',
        rule='Classify absolute RSI slope <=1e-10 as flat; feature values and outcomes unchanged',
        maximum_affected_absolute_slope=max(abs(r['aligned_rsi_slope']) for r in changed),
        affected_opportunity_ids=[r['opportunity_id'] for r in changed],
        outcome_based_selection=False,original_reports_retained=True)
    report['protocol']['rsi_numerical_zero_tolerance']=RSI_ZERO_TOLERANCE
    report['protocol']['numerical_correction_after_reference_check']=correction
    report['analysis']=analyze(rows)
    (OUTPUT/'analysis.json').write_text(json.dumps(report,indent=2))
    (OUTPUT/'protocol.json').write_text(json.dumps(report['protocol'],indent=2))
    (OUTPUT/'numerical-correction.json').write_text(json.dumps(correction,indent=2))
    flat=[]
    for window in report['analysis']['windows']:
        for feature in window['features']:
            for group,summary in feature['groups'].items():
                flat.append(dict(window=window['window'],feature=feature['feature'],group=group,
                    opportunities=summary['opportunities'],target_hits=summary['target_hits'],target_rate=summary['target_rate'],
                    stop_outcomes=summary['outcomes'].get('Stop -1 band',0),cutoff_outcomes=summary['outcomes'].get('Session cutoff',0)))
    with (OUTPUT/'feature-comparison.csv').open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(flat[0]))
        writer.writeheader()
        writer.writerows(flat)
    print(json.dumps(correction,indent=2))


if __name__=='__main__':
    main()
