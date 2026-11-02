import datetime as dt
import hashlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import prepare_iti_year as exporter


class YearInputTests(unittest.TestCase):
    def test_sunday_evening_after_each_dst_transition_keeps_original_epoch(self):
        for date,opening,midnight in (
            ('2025-11-03','2025-11-02T23:00:00+00:00','2025-11-03T05:00:00+00:00'),
            ('2026-03-09','2026-03-08T22:00:00+00:00','2026-03-09T04:00:00+00:00'),
        ):
            with self.subTest(date=date),tempfile.TemporaryDirectory() as folder:
                root=Path(folder)
                (root/'ES').mkdir()
                expected=[int(dt.datetime.fromisoformat(t).timestamp())*exporter.NS for t in (opening,midnight)]
                payload=''.join(f'ES20260320,{date},{ns},{clock},6000.000000000,1\n' for ns,clock in zip(expected,('18:00:00.000000000','00:00:00.000000000'))).encode()
                (root/'ES'/'test.csv').write_bytes(payload)
                partition=dict(file='test.csv',value_date=date,rows=2,sha256=hashlib.sha256(payload).hexdigest(),min_event_ns=expected[0],max_event_ns=expected[1])
                with patch.object(exporter,'RAW',root):
                    observations=exporter.read_partition('ES',partition)
                self.assertEqual([r[0] for r in observations],expected)


if __name__=='__main__':
    unittest.main()
