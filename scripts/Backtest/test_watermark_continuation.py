import unittest
from examine_watermark_continuation import Continuation,NS


class ContinuationTests(unittest.TestCase):
    def make_path(self,side):
        sign=1 if side=='Long' else -1
        t=dict(trade_id='example',trade_date='2026-01-02',side=side,entry_time_ny='entry',
               entry_price=100,entry_band_size=2,exit_time_ny='exit',exit_price=100+sign*1.75,
               exit_event_ns=str(10*NS),exit_ordinal=10,points=1.75,watermark_bands=1,stop_price=100-sign*2)
        return Continuation(t,100*NS),sign

    def test_target_order_and_drawdown_mirror(self):
        for side in ('Long','Short'):
            with self.subTest(side=side):
                p,sign=self.make_path(side)
                p.observe(10*NS,100-sign*.5,11)  # Later tick with identical timestamp.
                p.observe(11*NS,100+sign*4,12)
                p.observe(12*NS,100+sign*3,13)
                p.observe(13*NS,100+sign*6.25,14)
                p.observe(14*NS,100-sign*2.25,15)
                r=p.result()
                self.assertTrue(r['target_2_hit'] and r['target_3_hit'])
                self.assertEqual(r['target_2_minimum_points'],-.5)
                self.assertEqual(r['target_2_giveback_from_original_exit_points'],2.25)
                self.assertEqual(r['target_2_maximum_drawdown_points'],2.25)
                self.assertEqual(r['target_2_ordinal'],12)
                self.assertEqual(r['terminal_reason'],'Original stop')
                self.assertEqual(r['terminal_points'],-2.25)

    def test_original_stop_ends_path_before_later_recovery(self):
        p,sign=self.make_path('Long')
        p.observe(11*NS,98,11)
        self.assertTrue(p.finished)
        self.assertFalse(p.result()['target_2_hit'])
        with self.assertRaises(AssertionError):
            p.observe(12*NS,110,12)

    def test_cutoff_does_not_count_after_hours_price(self):
        p,sign=self.make_path('Long')
        p.observe(11*NS,103,11)
        p.finish(p.cutoff,103,11,'Session cutoff')
        self.assertFalse(p.result()['target_2_hit'])
        with self.assertRaises(AssertionError):
            p.observe(p.cutoff,110,12)


if __name__=='__main__':
    unittest.main()
