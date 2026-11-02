"""Verify each capped day equals its uncapped trade sequence through loss two."""
import csv
import json
from pathlib import Path
from analyze_dc_to_overshoot import summarize

OUTPUT=Path('outputs/iti-june-2026-two-loss-daily-limit')
BASE=Path('outputs/iti-june-2026-opposite-dc-exit-comparison')


def main():
    report=json.loads((OUTPUT/'analysis.json').read_text())
    original=json.loads((BASE/'analysis.json').read_text())
    capped=report['policies']['exit-dc']
    baseline=original['policies']['exit-dc']
    assert report['input_sha256']==original['input_sha256']
    daily=[]
    halted=[]
    for day in capped['daily']:
        date=day['trade_date']
        old=[t for t in baseline['trades'] if t['trade_date']==date]
        actual=[t for t in capped['trades'] if t['trade_date']==date]
        expected=[]
        losses=0
        for t in old:
            expected.append(t)
            losses+=t['gross_pnl_usd']<0
            if losses==2:
                break
        assert len(actual)==len(expected)
        for a,e in zip(actual,expected):
            assert {k:v for k,v in a.items() if k!='trade_id'}=={k:v for k,v in e.items() if k!='trade_id'}
        assert losses<=2 and day['losses']==losses
        if losses==2:
            assert actual[-1]['gross_pnl_usd']<0
            halted.append(dict(trade_date=date,halt_time_ny=actual[-1]['exit_time_ny'],trades_skipped=len(old)-len(actual)))
        old_pnl=sum(t['gross_pnl_usd'] for t in old)
        daily.append(dict(trade_date=date,uncapped_trades=len(old),capped_trades=len(actual),
                          uncapped_pnl_usd=old_pnl,capped_pnl_usd=day['gross_pnl_usd'],
                          improvement_usd=day['gross_pnl_usd']-old_pnl,losses=losses,loss_limit_reached=losses==2))
    assert summarize(capped['trades'])==capped['totals']
    proof=json.loads((OUTPUT/'exit-dc-independent-verification.json').read_text())
    assert proof['verified_source_ticks']==11003263 and proof['verified_trades']==len(capped['trades']) and proof['daily_loss_limit_verified']
    result=dict(complete=True,month='2026-06',daily_loss_limit=2,
        loss_definition='Any trade closed with negative gross P&L; losses need not be consecutive; no fixed-price stop added',
        uncapped=baseline['totals'],capped=capped['totals'],
        improvement_usd=capped['totals']['gross_pnl_usd']-baseline['totals']['gross_pnl_usd'],
        halted_days=len(halted),skipped_trades=len(baseline['trades'])-len(capped['trades']),
        checks=dict(each_day_matches_uncapped_sequence_through_second_loss=True,raw_tick_verification=proof),
        daily=daily,halts=halted)
    with (OUTPUT/'daily-comparison.csv').open('w',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(daily[0]))
        writer.writeheader()
        writer.writerows(daily)
    (OUTPUT/'comparison.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k not in ('daily','halts')}))
    print(json.dumps(daily))


if __name__=='__main__':
    main()
