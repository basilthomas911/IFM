import unittest
from analyze_dc_to_overshoot import DcSimulator
from analyze_daily_reversion import NS
from test_daily_reversion import event


class DcTests(unittest.TestCase):
    def test_two_daily_losses_with_win_between_block_same_tick_and_later_entries(self):
        s=DcSimulator('2026-06-09','exit-dc',exit_rule='first-reversal',start_time='09:45',daily_loss_limit=2)
        t=s.start
        s.tick(t,400,event(2))
        s.tick(t+NS,401)
        s.tick(t+2*NS,380,event(2,1,2,95))
        s.tick(t+3*NS,379)
        self.assertEqual(s.losses,1)
        s.tick(t+4*NS,400,event(2,0,3,100))
        s.tick(t+5*NS,401)
        s.tick(t+6*NS,440,event(7,0,3,110))
        s.tick(t+7*NS,435,event(8,0,3,110))
        s.tick(t+8*NS,434)
        self.assertEqual(s.losses,1)  # A win must not reset the daily count.
        s.tick(t+9*NS,400,event(2,0,4,100))
        s.tick(t+10*NS,401)
        s.tick(t+11*NS,380,event(2,1,5,95))
        s.tick(t+12*NS,379,event(2,0,6,94.75))
        self.assertTrue(s.entry_blocked)
        self.assertIsNone(s.pending_entry)
        s.tick(t+13*NS,400,event(2,1,7,100))
        s.tick(t+14*NS,399)
        self.assertIsNone(s.position)
        s.tick(s.end,399)
        self.assertEqual(len(s.finish()),3)
        self.assertEqual(s.losses,2)
        following=DcSimulator('2026-06-10','exit-dc',daily_loss_limit=2)
        following.tick(following.start,400,event(2))
        self.assertIsNotNone(following.pending_entry)
        self.assertEqual(following.losses,0)

    def test_flat_does_not_count_and_losing_reversal_fill_counts(self):
        s=DcSimulator('2026-06-09','exit-dc',exit_rule='first-reversal',daily_loss_limit=1)
        t=s.start
        s.tick(t,400,event(2))
        s.tick(t+NS,400)
        s.tick(t+2*NS,400,event(2,1,2,100))
        s.tick(t+3*NS,400)
        self.assertEqual(s.losses,0)
        s.tick(t+4*NS,400,event(2,0,3,100))
        s.tick(t+5*NS,401)
        s.tick(t+6*NS,440,event(7,0,3,110))
        s.tick(t+7*NS,435,event(8,0,3,110))
        s.tick(t+8*NS,390,event(2,1,4,97.5))  # Adverse next-tick fill on profit exit.
        self.assertEqual(s.trades[-1]['exit_reason'],'First reversal after overshoot')
        self.assertTrue(s.entry_blocked)
        self.assertIsNone(s.pending_entry)
        s.tick(s.end,390)
        self.assertEqual(len(s.finish()),2)

    def test_dc_exit_consumes_signal_then_waits_for_later_dc(self):
        for direction in (0,1):
            with self.subTest(direction=direction):
                s=DcSimulator('2026-06-09','exit-dc',exit_rule='first-reversal',start_time='09:45')
                t=s.start
                s.tick(t,400,event(2,direction,1,100))
                s.tick(t,401)
                s.tick(t+NS,380,event(2,1-direction,2,95))
                self.assertIsNone(s.pending_entry)
                self.assertEqual(len(s.trades),0)
                s.tick(t+NS,379)
                self.assertIsNone(s.position)
                self.assertEqual(s.trades[0]['exit_reason'],'Opposite DC')
                self.assertEqual(s.trades[0]['exit_signal_ordinal']+1,s.trades[0]['exit_ordinal'])
                s.tick(t+2*NS,340,event(7,1-direction,2,85))
                s.tick(t+3*NS,350,event(8,1-direction,2,85))
                self.assertIsNone(s.position)
                s.tick(t+4*NS,400,event(2,direction,3,100))
                s.tick(t+5*NS,401)
                self.assertEqual(s.position['group'],3)
                s.tick(t+6*NS,440,event(7,direction,3,110))
                s.tick(t+7*NS,435,event(8,direction,3,110))
                s.tick(t+8*NS,434)
                self.assertEqual(s.trades[1]['exit_reason'],'First reversal after overshoot')
                self.assertEqual(s.trades[1]['setup_group'],s.trades[1]['overshoot_group'])
                s.tick(s.end,434)
                s.finish()

    def test_dc_exit_at_cutoff_uses_scheduled_flatten(self):
        s=DcSimulator('2026-06-09','exit-dc',exit_rule='first-reversal',start_time='09:45')
        s.tick(s.start,400,event(2))
        s.tick(s.start+NS,401)
        s.tick(s.end-NS,380,event(2,1,2,95))
        s.tick(s.end+NS,370)
        self.assertEqual(s.finish()[0]['exit_reason'],'Session flatten')
        self.assertEqual(s.trades[0]['exit_price'],95)

    def test_short_session_last_available_price_mark(self):
        s=DcSimulator('2025-11-28','hold',exit_rule='first-reversal',start_time='09:45')
        t=s.start
        s.tick(t,400,event(2))
        s.tick(t+NS,401)
        s.tick(t+2*NS,420)
        trades=s.finish()
        self.assertEqual(trades[0]['exit_price'],105)
        self.assertEqual(int(trades[0]['exit_event_ns']),s.end)
        self.assertEqual(trades[0]['exit_ordinal'],3)

    def test_late_session_no_stop_and_cutoff(self):
        s=DcSimulator('2026-10-01','hold',exit_rule='first-reversal',start_time='09:45')
        t=s.start
        s.tick(t-NS,400,event(2))
        s.tick(t,401)
        self.assertIsNone(s.position)
        self.assertIsNone(s.pending_entry)
        s.tick(t+NS,400,event(2))
        s.tick(t+2*NS,401)
        s.tick(t+3*NS,300)  # Adverse move must not stop the position.
        self.assertIsNotNone(s.position)
        s.tick(s.end-NS,390)
        s.tick(s.end+NS,380,event(2))
        s.tick(s.end+2*NS,370)
        trades=s.finish()
        self.assertEqual(len(trades),1)
        self.assertIsNone(trades[0]['stop_price'])
        self.assertEqual(trades[0]['exit_price'],97.5)
        self.assertEqual(int(trades[0]['exit_event_ns']),s.end)
        self.assertEqual(trades[0]['exit_reason'],'Session flatten')

    def test_reversal_exit_waits_for_overshoot_and_first_reversal(self):
        s=DcSimulator('2026-10-01','hold',2,'first-reversal')
        t=s.start
        s.tick(t,400,dict(event(2),BandSize=1))
        s.tick(t+NS,401)
        s.tick(t+2*NS,404,event(3))  # Not armed yet.
        self.assertIsNone(s.pending_exit)
        s.tick(t+3*NS,440,event(7))
        self.assertIsNone(s.pending_exit)
        s.tick(t+4*NS,450,event(7))  # Further extension does not exit.
        s.tick(t+5*NS,445,event(8))
        self.assertEqual(len(s.trades),0)
        s.tick(t+5*NS,444)
        self.assertEqual(s.trades[0]['exit_reason'],'First reversal after overshoot')
        self.assertEqual(s.trades[0]['points'],10.75)
        s.tick(s.end,400)
        s.finish()

    def test_short_ordinary_reversal_after_overshoot(self):
        s=DcSimulator('2026-10-01','hold',2,'first-reversal')
        t=s.start
        s.tick(t,400,dict(event(2,1,1,100),BandSize=1))
        s.tick(t+NS,399)
        s.tick(t+2*NS,350,event(7,1,1,87.5))
        s.tick(t+3*NS,365,event(3,1,1,87.5))
        s.tick(t+4*NS,366)
        self.assertEqual(s.trades[0]['points'],8.25)
        s.tick(s.end,400)
        s.finish()

    def test_dc_resets_reversal_exit_qualification(self):
        s=DcSimulator('2026-10-01','hold',2,'first-reversal')
        t=s.start
        s.tick(t,400,dict(event(2),BandSize=10))
        s.tick(t+NS,401)
        s.tick(t+2*NS,440,event(7))
        s.tick(t+3*NS,410,event(2,1,2,102.5))
        s.tick(t+4*NS,415,event(8,1,2,102.5))
        self.assertIsNone(s.pending_exit)
        s.tick(s.end,415)
        self.assertEqual(s.trades[0]['exit_reason'],'Session flatten')
        s.finish()

    def test_long_two_band_stop_frozen_rounded_and_gap_fill(self):
        s=DcSimulator('2026-10-01','hold',2)
        t=s.start
        e=dict(event(2),BandSize=1.1)
        s.tick(t,400,e)
        s.tick(t+NS,401)
        self.assertEqual(s.position['stop_ticks'],391)  # 100 - 2.2, rounded down to 97.75.
        s.tick(t+2*NS,392)
        self.assertIsNotNone(s.position)
        s.tick(t+3*NS,389)
        self.assertEqual(s.trades[0]['exit_price'],97.25)
        self.assertEqual(s.trades[0]['exit_reason'],'Two-band stop')
        s.tick(s.end,400)
        s.finish()

    def test_short_stop_and_skip_entry_already_beyond_stop(self):
        s=DcSimulator('2026-10-01','hold',2)
        t=s.start
        s.tick(t,400,dict(event(2,1,1,100),BandSize=1.1))
        s.tick(t+NS,399)
        self.assertEqual(s.position['stop_ticks'],409)
        s.tick(t+2*NS,409)
        self.assertEqual(s.trades[0]['exit_reason'],'Two-band stop')
        s.tick(t+3*NS,400,dict(event(2),BandSize=1))
        s.tick(t+4*NS,390)
        self.assertIsNone(s.position)
        self.assertEqual(s.canceled_entries,1)
        s.tick(s.end,400)
        s.finish()

    def test_stop_has_priority_over_pending_overshoot_exit(self):
        s=DcSimulator('2026-10-01','hold',2)
        t=s.start
        s.tick(t,400,dict(event(2),BandSize=1))
        s.tick(t+NS,401)
        s.tick(t+2*NS,440,event(7))
        s.tick(t+3*NS,390)
        self.assertEqual(s.trades[0]['exit_reason'],'Two-band stop')
        s.tick(s.end,400)
        s.finish()

    def test_entry_first_overshoot_and_next_tick_fills(self):
        s=DcSimulator('2026-10-01','hold')
        t=s.start
        s.tick(t,400,event(2))
        s.tick(t,401)
        s.tick(t+NS,440,event(7))
        self.assertEqual(len(s.trades),0)
        s.tick(t+NS,439)
        s.tick(t+2*NS,450,event(7))
        self.assertEqual(len(s.trades),1)
        self.assertEqual(s.trades[0]['points'],9.5)
        s.tick(s.end+NS,400)
        s.finish()

    def test_reverse_on_opposite_dc(self):
        s=DcSimulator('2026-10-01','reverse')
        t=s.start
        s.tick(t,400,event(2))
        s.tick(t+NS,401)
        s.tick(t+2*NS,380,event(2,1,2,95))
        s.tick(t+3*NS,379)
        self.assertEqual(s.trades[0]['points'],-5.5)
        self.assertEqual(s.position['direction'],-1)
        s.tick(t+4*NS,340,event(7,1,2,85))
        s.tick(t+5*NS,339)
        self.assertEqual(s.trades[1]['points'],10)
        s.tick(s.end,400)
        s.finish()

    def test_hold_ignores_opposite_overshoot_and_flattens(self):
        s=DcSimulator('2026-10-01','hold')
        t=s.start
        s.tick(t-NS,400,event(2))
        self.assertIsNone(s.pending_entry)
        s.tick(t,400,event(2))
        s.tick(t+NS,401)
        s.tick(t+2*NS,380,event(2,1,2,95))
        s.tick(t+3*NS,340,event(7,1,2,85))
        s.tick(s.end-NS,350)
        s.tick(s.end+NS,300)
        self.assertEqual(s.trades[0]['exit_reason'],'Session flatten')
        self.assertEqual(s.trades[0]['exit_price'],87.5)
        s.tick(s.end+2*NS,400,event(2))
        self.assertIsNone(s.pending_entry)
        s.finish()


if __name__=='__main__':
    unittest.main()
