"""Causal original-trend entry and watermark-only exit regression checks."""
import unittest
from analyze_daily_reversion import NS
from analyze_two_reversal_two_band import ReversalSimulator
from test_two_reversal_two_band import e
from analyze_opposite_retracement import select_source_files


class OppositeRetracementTests(unittest.TestCase):
    def test_three_month_range_is_chronological_and_end_is_exclusive(self):
        source={'files':[{'value_date':d} for d in ('2026-11-01','2026-10-30','2026-07-31','2026-08-03','2026-09-01')]}
        files=select_source_files(source,'2026-08-01','2026-11-01')
        self.assertEqual([f['value_date'] for f in files],['2026-08-03','2026-09-01','2026-10-30'])
        with self.assertRaises(ValueError):
            select_source_files(source,'2026-11-01','2026-08-01')

    def setup_trade(self, trend=0, stop_bands=None, adverse_recovery=False,daily_loss_limit=None,date='2026-10-01'):
        sim=ReversalSimulator(date,watermark=True,stop_bands=stop_bands,
            daily_loss_limit=daily_loss_limit,entry_direction='original',reversal_profit_exit=False,adverse_recovery=adverse_recovery)
        direction=1 if trend==0 else -1
        t=sim.start
        sim.tick(t,440,e(2,trend,1,110))
        sim.tick(t+NS,440,e(7,trend,1,110))
        sim.tick(t+2*NS,440-direction*4,e(8,trend,1,110))
        self.assertIsNone(sim.pending_entry)
        sim.tick(t+3*NS,440-direction*8,e(8,trend,1,110))
        self.assertEqual(sim.pending_entry['direction'],direction)
        self.assertIsNone(sim.position)
        # A further adverse tick fills immediately: no bounce is required.
        sim.tick(t+4*NS,440-direction*9)
        self.assertEqual(sim.position['entry_ordinal'],sim.position['signal_ordinal']+1)
        if stop_bands is None:
            self.assertIsNone(sim.position['stop_ticks'])
        return sim,t,direction

    def test_mirror_entry_no_bounce_no_stop_and_session_close(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s,t,d=self.setup_trade(trend)
                fill=s.position['entry_ticks']
                s.tick(t+5*NS,fill-d*80)
                self.assertIsNotNone(s.position)
                s.tick(s.end+NS,fill+d*40)
                trade=s.finish()[0]
                self.assertEqual(trade['exit_reason'],'Session flatten')
                self.assertEqual(trade['points'],-20)
                self.assertEqual(trade['max_adverse_points'],-20)
                self.assertIsNone(trade['planned_risk_usd'])

    def test_mirror_watermark_strict_activation_ratchet_touch_and_cross(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s,t,d=self.setup_trade(trend)
                fill=s.position['entry_ticks']
                s.tick(t+5*NS,fill+d*8)
                self.assertNotIn('watermark_ticks',s.position)
                s.tick(t+6*NS,fill+d*9)
                self.assertEqual(s.position['watermark_bands'],2)
                s.tick(t+7*NS,fill+d*12)
                self.assertEqual(s.position['watermark_bands'],3)
                s.tick(t+8*NS,fill+d*12)
                self.assertIsNotNone(s.position)
                s.tick(t+9*NS,fill+d*11)
                trade=s.finish()[0]
                self.assertEqual(trade['exit_reason'],'Trailing watermark')
                self.assertEqual(trade['points'],2.75)

    def test_reversal_profit_exit_is_disabled_even_after_favorable_overshoot(self):
        s,t,d=self.setup_trade()
        fill=s.position['entry_ticks']
        s.tick(t+5*NS,fill+4,e(7,0,1,110))
        s.tick(t+6*NS,fill,e(8,0,1,110))
        self.assertIsNone(s.pending_exit)
        s.tick(t+7*NS,fill-4)
        self.assertIsNotNone(s.position)
        s.finish()

    def test_new_extreme_resets_two_reversal_count(self):
        s=ReversalSimulator('2026-10-01',True,None,None,entry_direction='original',reversal_profit_exit=False)
        t=s.start
        for n,price,mode,extreme in [(0,440,2,110),(1,440,7,110),
                (2,436,8,110),(3,444,7,111),(4,440,8,111)]:
            s.tick(t+n*NS,price,e(mode,0,1,extreme))
        self.assertIsNone(s.pending_entry)
        s.tick(t+5*NS,436,e(8,0,1,111))
        self.assertIsNotNone(s.pending_entry)

    def test_threshold_stop_mirrors_first_touch_and_gap_fill(self):
        for stop_bands in (4,5):
            for trend in (0,1):
                for gap_ticks in (0,3):
                    with self.subTest(stop_bands=stop_bands,trend=trend,gap_ticks=gap_ticks):
                        s,t,d=self.setup_trade(trend,stop_bands=stop_bands)
                        fill=s.position['entry_ticks']
                        distance=4*stop_bands
                        s.tick(t+5*NS,fill-d*(distance-1))
                        self.assertIsNotNone(s.position)
                        # Crossing prices are retained, rather than idealized stop fills.
                        s.tick(t+6*NS,fill-d*(distance+gap_ticks))
                        trade=s.finish()[0]
                        self.assertEqual(trade['exit_reason'],f'{stop_bands}-band stop')
                        self.assertEqual(trade['planned_risk_points'],stop_bands)
                        self.assertEqual(trade['points'],-stop_bands-gap_ticks/4)
                        self.assertEqual(trade['stop_gap_loss_usd'],gap_ticks*12.5)

    def test_threshold_stop_is_frozen_and_does_not_halt_after_a_loss(self):
        s,t,d=self.setup_trade(stop_bands=5)
        fill=s.position['entry_ticks']
        stop=s.position['stop_ticks']
        s.tick(t+5*NS,fill-4,e(2,1,2,fill/4,band=3))
        self.assertEqual(s.position['stop_ticks'],stop)
        s.tick(t+6*NS,stop)
        self.assertFalse(s.entry_blocked)
        s.tick(t+7*NS,fill-24,e(7,1,2,(fill-24)/4))
        s.tick(t+8*NS,fill-20,e(8,1,2,(fill-24)/4))
        s.tick(t+9*NS,fill-16,e(8,1,2,(fill-24)/4))
        self.assertIsNotNone(s.pending_entry)
        s.tick(t+10*NS,fill-15)
        self.assertIsNotNone(s.position)
        s.finish()

    def test_four_band_stop_first_loss_blocks_day_and_next_date_resumes(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s,t,d=self.setup_trade(trend,4,daily_loss_limit=1)
                fill=s.position['entry_ticks']
                s.tick(t+5*NS,fill-d*16)
                self.assertTrue(s.entry_blocked)
                self.assertEqual(s.losses,1)
                s.tick(t+6*NS,440,e(2,trend,2,110))
                s.tick(t+7*NS,440,e(7,trend,2,110))
                s.tick(t+8*NS,440-d*4,e(8,trend,2,110))
                s.tick(t+9*NS,440-d*8,e(8,trend,2,110))
                s.tick(t+10*NS,440-d*9)
                self.assertIsNone(s.pending_entry)
                self.assertIsNone(s.position)
                self.assertEqual(len(s.finish()),1)
                next_day,_,_=self.setup_trade(trend,4,daily_loss_limit=1,date='2026-10-02')
                self.assertFalse(next_day.entry_blocked)
                self.assertIsNotNone(next_day.position)
                self.assertEqual(next_day.losses,0)
                next_day.finish()

    def test_recovery_mirrors_breach_deep_bounce_and_strict_inside_arming(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s,t,d=self.setup_trade(trend,5,True)
                boundary=s.position['stop_ticks']
                s.tick(t+5*NS,boundary)
                self.assertNotIn('adverse_breached_ns',s.position)
                s.tick(t+6*NS,boundary-d*20)
                s.tick(t+7*NS,boundary-d*16)
                self.assertIsNotNone(s.position)
                self.assertNotIn('recovery_mark_ticks',s.position)
                s.tick(t+8*NS,boundary)
                self.assertNotIn('recovery_mark_ticks',s.position)
                s.tick(t+9*NS,boundary+d)
                self.assertEqual(s.position['recovery_mark_ticks'],boundary+d)
                self.assertEqual(s.position['adverse_extreme_ticks'],boundary-d*20)
                s.tick(t+10*NS,boundary)
                trade=s.finish()[0]
                self.assertEqual(trade['exit_reason'],'Recovered threshold recross')
                self.assertEqual(trade['points'],-5)

    def test_recovery_mirrors_favorable_band_steps_and_one_band_pullback(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s,t,d=self.setup_trade(trend,5,True)
                boundary=s.position['stop_ticks']
                s.tick(t+5*NS,boundary-d*20)
                s.tick(t+6*NS,boundary+d*5)
                s.tick(t+7*NS,boundary+d*8)
                self.assertEqual(s.position['recovery_mark_ticks'],boundary+d*5)
                s.tick(t+8*NS,boundary+d*9)
                self.assertEqual(s.position['recovery_mark_ticks'],boundary+d*9)
                s.tick(t+9*NS,boundary+d*6)
                self.assertIsNotNone(s.position)
                s.tick(t+10*NS,boundary+d*5)
                trade=s.finish()[0]
                self.assertEqual(trade['exit_reason'],'Recovery watermark pullback')
                self.assertEqual(trade['points'],-3.75)
                self.assertEqual(trade['max_adverse_points'],-10)

    def test_recovery_threshold_recross_preserves_gap_fill_and_priority(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s,t,d=self.setup_trade(trend,5,True)
                boundary=s.position['stop_ticks']
                s.tick(t+5*NS,boundary-d)
                s.tick(t+6*NS,boundary+d)
                s.tick(t+7*NS,boundary-d*7)
                trade=s.finish()[0]
                self.assertEqual(trade['exit_reason'],'Recovered threshold recross')
                self.assertEqual(trade['points'],-6.75)

    def test_recovery_gap_anchor_and_band_remain_frozen_across_signal_changes(self):
        s,t,d=self.setup_trade(stop_bands=5,adverse_recovery=True)
        boundary=s.position['stop_ticks']
        s.tick(t+5*NS,boundary-20,e(2,1,2,(boundary-20)/4,band=3))
        s.tick(t+6*NS,boundary+5)
        s.tick(t+7*NS,boundary+9)
        self.assertEqual(s.position['recovery_mark_ticks'],boundary+9)
        s.tick(t+8*NS,boundary+5)
        trade=s.finish()[0]
        self.assertEqual(trade['exit_reason'],'Recovery watermark pullback')
        self.assertEqual(trade['points'],-3.75)

    def test_deep_rebounds_without_inside_recovery_hold_until_session_cutoff(self):
        s,t,d=self.setup_trade(stop_bands=5,adverse_recovery=True)
        fill=s.position['entry_ticks']
        for n,move in enumerate((-48,-44,-52,-48,-40),5):
            s.tick(t+n*NS,fill+move)
            self.assertIsNotNone(s.position)
            self.assertNotIn('recovery_mark_ticks',s.position)
        s.tick(s.end+NS,fill)
        trade=s.finish()[0]
        self.assertEqual(trade['exit_reason'],'Session flatten')
        self.assertEqual(trade['points'],-10)
        self.assertIsNone(trade['recovery_watermark_armed_time_ny'])

    def test_recovery_keeps_existing_profit_watermark_exit(self):
        s,t,d=self.setup_trade(stop_bands=5,adverse_recovery=True)
        fill=s.position['entry_ticks']
        s.tick(t+5*NS,fill+9)
        s.tick(t+6*NS,fill+7)
        trade=s.finish()[0]
        self.assertEqual(trade['exit_reason'],'Trailing watermark')
        self.assertIsNone(trade['adverse_breached_time_ny'])
        self.assertIsNone(trade['recovery_watermark_armed_time_ny'])


if __name__=='__main__':
    unittest.main()
