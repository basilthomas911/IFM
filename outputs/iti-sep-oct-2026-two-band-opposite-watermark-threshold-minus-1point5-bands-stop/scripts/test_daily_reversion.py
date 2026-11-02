import unittest
from analyze_daily_reversion import DaySimulator, NS, verify_ledger


def event(mode, trend=0, group=1, extreme=110):
    return dict(IntrinsicTimeMode=mode, IntrinsicTimeTrend=trend,
                IntrinsicTimeGroupId=group, TrendExtreme=extreme)


class ReversionTests(unittest.TestCase):
    def setup_short(self):
        s = DaySimulator('2026-10-01')
        t = s.start
        s.tick(t, 440, event(2))
        s.tick(t+NS, 440, event(7))
        s.tick(t+2*NS, 436, event(3))
        s.tick(t+3*NS, 432, event(8))
        s.tick(t+3*NS, 431)  # Separate next trade can have identical nanoseconds.
        self.assertEqual(s.position['entry_ticks'], 431)
        return s, t+4*NS

    def test_stop_gap_and_no_second_attempt(self):
        s,t = self.setup_short()
        s.tick(t, 442)
        self.assertEqual(s.trades[0]['exit_reason'], 'Protective stop')
        self.assertEqual(s.trades[0]['points'], -2.75)
        s.tick(t+NS, 440, event(7))
        s.tick(t+2*NS, 436, event(3))
        s.tick(t+3*NS, 432, event(8))
        self.assertIsNone(s.pending_entry)
        verify_ledger(s.trades)

    def test_dc_return_and_same_timestamp_ordering(self):
        s,t = self.setup_short()
        s.tick(t, 420, event(2, 1, 2, 105))
        s.tick(t, 421)
        self.assertEqual(s.trades[0]['exit_reason'], 'DC price return')
        self.assertEqual(s.trades[0]['points'], 2.5)

    def test_overshoot_exit_next_trade(self):
        s,t = self.setup_short()
        s.tick(t, 420, event(2, 1, 2, 105))
        s.tick(t+NS, 410, event(7, 1, 2, 102.5))
        self.assertIsNotNone(s.position)
        s.tick(t+NS, 409)
        self.assertEqual(s.trades[0]['exit_reason'], 'Overshoot extended')
        self.assertEqual(s.trades[0]['exit_price'], 102.25)

    def test_flatten_uses_last_known_price_and_blocks_entries(self):
        s,t = self.setup_short()
        s.tick(s.end-NS, 425)
        s.tick(s.end+NS, 400, event(7))
        s.tick(s.end+2*NS, 430, event(3))
        s.tick(s.end+3*NS, 432, event(8))
        self.assertEqual(s.trades[0]['exit_price'], 106.25)
        self.assertEqual(s.trades[0]['exit_reason'], 'Session flatten')
        self.assertIsNone(s.pending_entry)
        verify_ledger(s.finish())

    def test_overnight_reversals_not_counted_and_extreme_resets(self):
        s = DaySimulator('2026-10-01')
        t = s.start
        s.tick(t-4*NS, 440, event(2))
        s.tick(t-3*NS, 440, event(7))
        s.tick(t-2*NS, 436, event(3))
        s.tick(t-NS, 432, event(8))
        s.tick(t, 428, event(3))
        self.assertIsNone(s.pending_entry)
        s.tick(t+NS, 444, event(7, extreme=111))
        s.tick(t+2*NS, 440, event(3))
        self.assertIsNone(s.pending_entry)
        s.tick(t+3*NS, 436, event(8))
        self.assertIsNotNone(s.pending_entry)

    def test_long_mirror_stop(self):
        s = DaySimulator('2026-10-01')
        t = s.start
        s.tick(t, 400, event(2,1,1,100))
        s.tick(t+NS, 400, event(7,1,1,100))
        s.tick(t+2*NS, 404, event(3,1,1,100))
        s.tick(t+3*NS, 408, event(8,1,1,100))
        s.tick(t+4*NS, 409)
        s.tick(t+5*NS, 399)
        self.assertEqual(s.trades[0]['side'], 'Long')
        self.assertEqual(s.trades[0]['points'], -2.5)
        verify_ledger(s.trades)


if __name__ == '__main__':
    unittest.main()
