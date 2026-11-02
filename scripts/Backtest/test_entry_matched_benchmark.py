import unittest
import numpy as np
from benchmark_iti_entry_timing import match_indices,volatility_asof,volatility_bars,MINUTE,NS
from benchmark_iti_crossday import select_controls


class MatchedBenchmarkTests(unittest.TestCase):
    def test_crossday_controls_exclude_same_future_and_other_contract_dates(self):
        event=dict(id='case',day_index=25,clock_minutes=50,contract_id='ES',volatility=2,band=4)
        pools={i:[dict(pool_id=str(i),day_index=i,clock_minutes=50,contract_id='ES',volatility=2,band=4)] for i in range(30)}
        selected,count=select_controls(event,pools)
        self.assertEqual(count,20)
        self.assertEqual(len(selected),5)
        self.assertTrue(all(5<=r['day_index']<25 for r in selected))
        pools[5][0]['contract_id']='OTHER'
        pools[6][0]['clock_minutes']=66
        self.assertEqual(select_controls(event,pools)[1],18)

    def test_matching_is_fixed_prior_only_without_outcomes(self):
        event=dict(id='example',ns=20*MINUTE,band=4,volatility=2)
        pool=[dict(ns=i*MINUTE,band=4,volatility=2) for i in range(1,25)]
        selected,count=match_indices(event,pool)
        self.assertEqual(count,15)
        self.assertEqual(len(selected),5)
        self.assertEqual(len(set(selected)),5)
        self.assertEqual(selected,match_indices(event,pool)[0])
        self.assertTrue(all(0<event['ns']-pool[i]['ns']<=15*MINUTE for i in selected))
        for c in pool:
            c['future_outcome']='arbitrary future label'
        self.assertEqual(selected,match_indices(event,pool)[0])

    def test_calipers_not_relaxed_when_insufficient_controls(self):
        event=dict(id='example',ns=20*MINUTE,band=4,volatility=2)
        pool=[dict(ns=i*MINUTE,band=4,volatility=2) for i in range(16,20)]
        pool.extend([dict(ns=15*MINUTE,band=4.5,volatility=2),dict(ns=14*MINUTE,band=4,volatility=2.6)])
        self.assertEqual(match_indices(event,pool),([],4))

    def test_volatility_uses_only_completed_bars(self):
        tape=np.array([(i*MINUTE+NS,100+i*.25) for i in range(22)],dtype=[('ns','i8'),('price','f8')])
        first=volatility_asof(17,tape,volatility_bars(tape))
        self.assertEqual(first['volatility'],.25)
        self.assertEqual(first['bar_end_ns'],str(17*MINUTE))
        changed=tape.copy()
        changed['price'][17:]=999
        self.assertEqual(first,volatility_asof(17,changed,volatility_bars(changed)))


if __name__=='__main__':
    unittest.main()
