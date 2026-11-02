import unittest
from decimal import Decimal
import numpy as np
from discover_iti_entry_signals import indicator_arrays,closed_bars,asof_features,label_path,group_name,MINUTE,NS


class EntrySignalDiscoveryTests(unittest.TestCase):
    def test_flat_rsi_roundoff_is_not_directional_confirmation(self):
        for value in (0,1.4210854715202004e-14,-1.4210854715202004e-14):
            self.assertEqual(group_name(dict(aligned_rsi_slope=value),'aligned_rsi_slope',{}),'Flat')
        self.assertEqual(group_name(dict(aligned_rsi_slope=1e-8),'aligned_rsi_slope',{}),'Aligned')
        self.assertEqual(group_name(dict(aligned_rsi_slope=-1e-8),'aligned_rsi_slope',{}),'Opposed')

    def tape(self,items):
        return np.array(items,dtype=[('ns','i8'),('price','f8')])

    def test_wilder_rsi_matches_decimal_reference(self):
        prices=[100+(i%7)*.25+(i//9)*.5 for i in range(65)]
        values,slopes,efficiency=indicator_arrays(np.array(prices))
        changes=[Decimal(str(b))-Decimal(str(a)) for a,b in zip(prices,prices[1:])]
        gain=sum(max(x,0) for x in changes[:14])/14
        loss=sum(max(-x,0) for x in changes[:14])/14
        previous=None
        for i in range(14,len(prices)):
            if i>14:
                gain=(gain*13+max(changes[i-1],0))/14
                loss=(loss*13+max(-changes[i-1],0))/14
            rsi=float(Decimal(100)-Decimal(100)/(1+gain/loss))
            self.assertAlmostEqual(values[i],rsi,places=10)
            if previous is not None:
                self.assertAlmostEqual(slopes[i],rsi-previous,places=10)
            previous=rsi

    def test_indicators_warmup_and_trend_efficiency(self):
        rsi,slope,eff=indicator_arrays(np.arange(20,dtype=float))
        self.assertTrue(np.isnan(rsi[13]) and np.isnan(slope[14]))
        self.assertEqual(rsi[14],100)
        self.assertEqual(slope[15],0)
        self.assertEqual(eff[14],1)
        self.assertEqual(indicator_arrays(np.ones(20))[0][14],50)

    def test_incomplete_minute_and_future_prices_cannot_change_features(self):
        tape=self.tape([(i*MINUTE+NS,100+i*.25) for i in range(20)])
        bars=closed_bars(tape)
        first=asof_features(17,tape,bars)
        changed=tape.copy()
        changed['price'][18:]=1000
        second=asof_features(17,changed,closed_bars(changed))
        self.assertEqual(first,second)
        self.assertEqual(first['bar_end_ns'],str(17*MINUTE))
        self.assertEqual(first['bar_available_ns'],str(17*MINUTE+NS))

    def test_first_touch_stop_target_mirror_and_cutoff(self):
        for sign in (1,-1):
            tape=self.tape([(0,100),(NS,100+sign*4),(2*NS,100-sign*2)])
            self.assertTrue(label_path(tape,0,sign,2,3*NS)['target_hit'])
            tape=self.tape([(0,100),(NS,100-sign*2),(2*NS,100+sign*4)])
            self.assertEqual(label_path(tape,0,sign,2,3*NS)['outcome'],'Stop -1 band')
            tape=self.tape([(0,100),(NS,100+sign*4)])
            self.assertEqual(label_path(tape,0,sign,2,NS)['outcome'],'Session cutoff')


if __name__=='__main__':
    unittest.main()
