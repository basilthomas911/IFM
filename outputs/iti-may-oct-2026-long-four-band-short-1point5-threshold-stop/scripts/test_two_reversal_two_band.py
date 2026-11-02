import unittest
from analyze_daily_reversion import NS
from analyze_two_reversal_two_band import ReversalSimulator,stop_distance_ticks
from test_daily_reversion import event


def e(mode,trend=0,group=1,extreme=110,band=1):
    return dict(event(mode,trend,group,extreme),BandSize=band)


class TwoReversalTests(unittest.TestCase):
    def test_half_band_buffer_mirror_activation_steps_touch_and_first_cross(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s=ReversalSimulator('2026-06-09',True,1,1,1,1,watermark_buffer_bands=.5)
                t=s.start
                high=440 if trend==0 else 400
                sign=-1 if trend==0 else 1
                s.tick(t,high,e(2,trend,1,high/4))
                s.tick(t+NS,high,e(7,trend,1,high/4))
                s.tick(t+2*NS,high+sign*4,e(8,trend,1,high/4))
                fill=high+sign*5
                s.tick(t+3*NS,fill)
                self.assertEqual(s.position['stop_ticks'],fill-sign*4)
                for seconds,gain,level in ((4,4,None),(5,5,.5),(6,3,.5),(7,2,.5),
                                           (8,8,1.5),(9,6,1.5),(10,12,2.5),(11,10,2.5)):
                    s.tick(t+seconds*NS,fill+sign*gain)
                    self.assertEqual(s.position.get('watermark_bands'),level)
                s.tick(t+12*NS,fill+sign*9)
                self.assertEqual(s.trades[0]['exit_reason'],'Trailing watermark')
                self.assertEqual(s.trades[0]['points'],2.25)
                s.tick(s.end,fill)
                s.finish()

    def test_half_band_buffer_does_not_override_protective_stop_on_gap(self):
        s=ReversalSimulator('2026-06-09',True,1,1,1,1,watermark_buffer_bands=.5)
        t=s.start
        s.tick(t,440,e(2))
        s.tick(t+NS,440,e(7))
        s.tick(t+2*NS,436,e(8))
        s.tick(t+3*NS,435)
        s.tick(t+4*NS,430)
        s.tick(t+5*NS,440)
        self.assertEqual(s.trades[0]['exit_reason'],'One-band stop')
        self.assertEqual(s.trades[0]['points'],-1.25)
        self.assertTrue(s.entry_blocked)

    def test_delayed_watermark_mirrors_holds_second_band_and_ratchets_without_retreat(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s=ReversalSimulator('2026-06-09',True,1,1,1,1,watermark_lag_bands=1)
                t=s.start
                high=440 if trend==0 else 400
                sign=-1 if trend==0 else 1
                s.tick(t,high,e(2,trend,1,high/4))
                s.tick(t+NS,high,e(7,trend,1,high/4))
                s.tick(t+2*NS,high+sign*4,e(8,trend,1,high/4))
                fill=high+sign*5
                s.tick(t+3*NS,fill)
                for seconds,gain,level in ((4,4,None),(5,5,1),(6,8,1),(7,7,1),(8,12,2),(9,8,2),(10,20,4),(11,16,4)):
                    s.tick(t+seconds*NS,fill+sign*gain)
                    self.assertEqual(s.position.get('watermark_bands'),level)
                s.tick(t+12*NS,fill+sign*15)
                self.assertEqual(s.trades[0]['exit_reason'],'Trailing watermark')
                self.assertEqual(s.trades[0]['points'],3.75)
                s.tick(s.end,fill)
                s.finish()

    def test_first_reversal_entry_and_one_band_watermark_mirror(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s=ReversalSimulator('2026-06-09',watermark=True,stop_bands=1,daily_loss_limit=1,
                                    entry_reversals=1,watermark_start_bands=1)
                t=s.start
                high=440 if trend==0 else 400
                sign=-1 if trend==0 else 1
                extreme=high/4
                s.tick(t,high,e(2,trend,1,extreme))
                s.tick(t+NS,high,e(7,trend,1,extreme))
                s.tick(t+2*NS,high+sign*4,e(8,trend,1,extreme))
                self.assertIsNone(s.position)
                self.assertIsNotNone(s.pending_entry)
                fill=high+sign*5
                s.tick(t+2*NS,fill)
                self.assertEqual(s.position['entry_ordinal'],s.position['signal_ordinal']+1)
                self.assertEqual(s.position['stop_ticks'],fill-sign*4)
                s.tick(t+3*NS,fill+sign*4)
                self.assertNotIn('watermark_bands',s.position)
                s.tick(t+4*NS,fill+sign*5)
                self.assertEqual(s.position['watermark_bands'],1)
                s.tick(t+5*NS,fill+sign*8)
                self.assertEqual(s.position['watermark_bands'],2)
                s.tick(t+6*NS,fill+sign*7)
                self.assertEqual(s.trades[0]['exit_reason'],'Trailing watermark')
                self.assertEqual(s.trades[0]['points'],1.75)
                s.tick(s.end,fill)
                s.finish()

    def test_one_reversal_requires_overshoot_and_first_loss_blocks_day(self):
        s=ReversalSimulator('2026-06-09',True,1,1,1,1)
        t=s.start
        s.tick(t,440,e(2))
        s.tick(t+NS,436,e(3))
        self.assertIsNone(s.pending_entry)
        s.tick(t+2*NS,444,e(7,extreme=111))
        s.tick(t+3*NS,440,e(3,extreme=111))
        s.tick(t+4*NS,439)
        s.tick(t+5*NS,443)
        self.assertTrue(s.entry_blocked)
        self.assertEqual(s.trades[0]['gross_pnl_usd'],-50)
        s.tick(t+6*NS,448,e(2,0,2,112))
        s.tick(t+7*NS,448,e(7,0,2,112))
        s.tick(t+8*NS,444,e(8,0,2,112))
        self.assertIsNone(s.pending_entry)
        s.tick(s.end,444)
        self.assertEqual(len(s.finish()),1)

    def test_no_daily_loss_limit_allows_two_losses_then_later_win(self):
        s=ReversalSimulator('2026-06-09',watermark=True,stop_bands=1,daily_loss_limit=None)
        t=s.start
        for group,high in ((1,440),(2,444)):
            s.tick(t,high,e(2,0,group,high/4))
            s.tick(t+NS,high,e(7,0,group,high/4))
            s.tick(t+2*NS,high-4,e(3,0,group,high/4))
            s.tick(t+3*NS,high-8,e(8,0,group,high/4))
            s.tick(t+4*NS,high-9)
            s.tick(t+5*NS,high-5)
            t+=6*NS
        self.assertEqual(s.losses,2)
        self.assertFalse(s.entry_blocked)
        s.tick(t,448,e(2,0,3,112))
        s.tick(t+NS,448,e(7,0,3,112))
        s.tick(t+2*NS,444,e(3,0,3,112))
        s.tick(t+3*NS,440,e(8,0,3,112))
        s.tick(t+4*NS,439)
        s.tick(t+5*NS,430)
        s.tick(t+6*NS,432)
        self.assertEqual(s.trades[-1]['exit_reason'],'Trailing watermark')
        self.assertGreater(s.trades[-1]['gross_pnl_usd'],0)
        s.tick(s.end,432)
        self.assertEqual(len(s.finish()),3)

    def test_one_band_protective_stop_mirrors_and_halts_day(self):
        for trend in (0,1):
            with self.subTest(trend=trend):
                s=ReversalSimulator('2026-06-09',watermark=True,stop_bands=1)
                t=s.start
                prices=(440,436,432,431,433,435) if trend==0 else (400,404,408,409,407,405)
                extreme=110 if trend==0 else 100
                s.tick(t,prices[0],e(2,trend,1,extreme))
                s.tick(t+NS,prices[0],e(7,trend,1,extreme))
                s.tick(t+2*NS,prices[1],e(3,trend,1,extreme))
                s.tick(t+3*NS,prices[2],e(8,trend,1,extreme))
                s.tick(t+4*NS,prices[3])
                s.tick(t+5*NS,prices[4],e(0,trend,1,extreme,10))
                self.assertIsNotNone(s.position)
                self.assertEqual(s.position['distance'],4)
                s.tick(t+6*NS,prices[5])
                self.assertEqual(s.trades[0]['exit_reason'],'One-band stop')
                self.assertEqual(s.trades[0]['gross_pnl_usd'],-50)
                self.assertEqual(s.losses,1)
                s.tick(s.end,prices[5])
                s.finish()

    def test_one_band_stop_keeps_two_band_watermark_activation(self):
        s=ReversalSimulator('2026-06-09',watermark=True,stop_bands=1)
        t=s.start
        s.tick(t,440,e(2))
        s.tick(t+NS,440,e(7))
        s.tick(t+2*NS,436,e(3))
        s.tick(t+3*NS,432,e(8))
        s.tick(t+4*NS,431)
        self.assertEqual(s.position['stop_ticks'],435)
        s.tick(t+5*NS,427)  # One band of profit leaves the watermark unarmed.
        self.assertNotIn('watermark_bands',s.position)
        s.tick(t+6*NS,423)
        self.assertNotIn('watermark_bands',s.position)
        s.tick(t+7*NS,422)
        self.assertEqual(s.position['watermark_bands'],2)
        s.tick(t+8*NS,424)
        self.assertEqual(s.trades[0]['exit_reason'],'Trailing watermark')
        s.tick(s.end,424)
        s.finish()
        self.assertEqual(stop_distance_ticks(1.1,1),4)

    def short(self,watermark=False):
        s=ReversalSimulator('2026-06-09',watermark)
        t=s.start
        s.tick(t,440,e(2))
        s.tick(t+NS,440,e(7))
        s.tick(t+2*NS,436,e(8))
        s.tick(t+3*NS,432,e(3))
        self.assertIsNone(s.position)
        s.tick(t+3*NS,431)
        self.assertEqual(s.position['stop_ticks'],439)
        return s,t+4*NS

    def test_mixed_reversals_next_tick_and_stop_gap_halt_day(self):
        s,t=self.short()
        s.tick(t,442)
        self.assertEqual(s.trades[0]['gross_pnl_usd'],-137.5)
        self.assertEqual(s.trades[0]['ideal_stop_fill_pnl_usd'],-100)
        self.assertEqual(s.trades[0]['stop_gap_loss_usd'],37.5)
        s.tick(t+NS,444,e(2,0,2,111))
        s.tick(t+2*NS,444,e(7,0,2,111))
        s.tick(t+3*NS,440,e(3,0,2,111))
        s.tick(t+4*NS,436,e(8,0,2,111))
        self.assertIsNone(s.pending_entry)
        s.tick(s.end,436)
        self.assertEqual(len(s.finish()),1)

    def test_short_watermark_strict_activation_ratchet_and_retreat(self):
        s,t=self.short(watermark=True)
        s.tick(t,423)  # Exactly two bands does not activate.
        self.assertNotIn('watermark_bands',s.position)
        s.tick(t+NS,422)
        self.assertEqual(s.position['watermark_bands'],2)
        self.assertEqual(s.position['watermark_ticks'],423)
        s.tick(t+2*NS,423)  # A touch does not exit.
        self.assertIsNotNone(s.position)
        s.tick(t+3*NS,419)  # Full third band raises the signed watermark.
        self.assertEqual(s.position['watermark_bands'],3)
        s.tick(t+4*NS,418)
        s.tick(t+5*NS,419)
        self.assertIsNotNone(s.position)
        s.tick(t+6*NS,420)
        self.assertEqual(s.trades[0]['exit_reason'],'Trailing watermark')
        self.assertEqual(s.trades[0]['points'],2.75)
        self.assertEqual(s.losses,0)
        s.tick(s.end,420)
        s.finish()

    def test_long_watermark_mirror_and_first_cross_gap_fill(self):
        s=ReversalSimulator('2026-06-09',watermark=True)
        t=s.start
        s.tick(t,400,e(2,1,1,100))
        s.tick(t+NS,400,e(7,1,1,100))
        s.tick(t+2*NS,404,e(3,1,1,100))
        s.tick(t+3*NS,408,e(8,1,1,100))
        s.tick(t+4*NS,409)
        s.tick(t+5*NS,417)
        self.assertNotIn('watermark_bands',s.position)
        s.tick(t+6*NS,418)
        self.assertEqual(s.position['watermark_ticks'],417)
        s.tick(t+7*NS,429)  # Gap across several levels: raise directly to five bands.
        self.assertEqual(s.position['watermark_bands'],5)
        s.tick(t+8*NS,420)
        self.assertEqual(s.trades[0]['exit_price'],105)
        self.assertEqual(s.trades[0]['points'],2.75)
        s.tick(s.end,420)
        s.finish()

    def test_protective_stop_has_priority_over_watermark_on_large_gap(self):
        s,t=self.short(watermark=True)
        s.tick(t,422)
        s.tick(t+NS,442)
        self.assertEqual(s.trades[0]['exit_reason'],'Two-band stop')
        self.assertEqual(s.losses,1)
        s.tick(s.end,442)
        s.finish()

    def test_long_mirror_and_frozen_band(self):
        s=ReversalSimulator('2026-06-09')
        t=s.start
        s.tick(t,400,e(2,1,1,100))
        s.tick(t+NS,400,e(7,1,1,100))
        s.tick(t+2*NS,404,e(3,1,1,100))
        s.tick(t+3*NS,408,e(8,1,1,100))
        s.tick(t+4*NS,409)
        s.tick(t+5*NS,405,e(0,1,1,100,10))
        self.assertEqual(s.position['stop_ticks'],401)
        s.tick(t+6*NS,401)
        self.assertEqual(s.trades[0]['gross_pnl_usd'],-100)
        s.tick(s.end,400)
        s.finish()

    def test_profit_needs_favorable_overshoot_then_reversal(self):
        s,t=self.short()
        s.tick(t,420,e(2,1,2,105))
        s.tick(t+NS,416,e(3,1,2,105))
        self.assertIsNone(s.pending_exit)
        s.tick(t+2*NS,410,e(7,1,2,102.5))
        self.assertIsNone(s.pending_exit)
        s.tick(t+3*NS,414,e(8,1,2,102.5))
        s.tick(t+3*NS,415)
        self.assertEqual(s.trades[0]['gross_pnl_usd'],200)
        self.assertEqual(s.losses,0)
        s.tick(s.end,415)
        s.finish()

    def test_new_extreme_resets_sequence_and_stop_rounds_inside_budget(self):
        s=ReversalSimulator('2026-06-09')
        t=s.start
        s.tick(t,440,e(2))
        s.tick(t+NS,440,e(7))
        s.tick(t+2*NS,436,e(3))
        s.tick(t+3*NS,444,e(7,extreme=111))
        s.tick(t+4*NS,440,e(8,extreme=111))
        self.assertIsNone(s.pending_entry)
        s.tick(t+5*NS,436,e(3,extreme=111))
        self.assertIsNotNone(s.pending_entry)
        self.assertEqual(stop_distance_ticks(1.1),8)


if __name__=='__main__':
    unittest.main()
