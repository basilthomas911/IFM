import unittest
import hashlib
import decimal
from types import SimpleNamespace
from import_es_october_backtest import verification_partitions, verify_partition


def partition(low,high,rows=2,date='2026-10-01'):
    return dict(contract_id='ES20261218',value_date=date,file='csv/test.csv',rows=rows,
                volume=10,price_raw_sum=100,row_hash_sum=(1<<256)-1,
                min_tick_id=low,max_tick_id=high,min_event_ns=low,max_event_ns=high)


class PreserveTests(unittest.TestCase):
    def fingerprint_fixture(self):
        rows=[SimpleNamespace(tickid=i,ticktime=f'12:00:0{i}.000000000',price=decimal.Decimal('1'),size=1) for i in (1,2)]
        p=partition(1,2)
        p.update(volume=2,price_raw_sum=2_000_000_000,file='test.csv')
        p['row_hash_sum']=sum(int.from_bytes(hashlib.sha256(
            f"{p['contract_id']},{p['value_date']},{r.tickid},{r.ticktime},{r.price:.9f},{r.size}\n".encode('ascii')).digest(),'big') for r in rows)%(1<<256)
        return rows,p

    def test_recovered_copy_requires_full_matching_fingerprint(self):
        rows,p=self.fingerprint_fixture()
        session=SimpleNamespace(execute=lambda *args:rows)
        _, actual=verify_partition('futures_tick_data',p,session)
        self.assertEqual(actual['rows'],2)

    def test_matching_counts_and_totals_cannot_hide_changed_tick_times(self):
        rows,p=self.fingerprint_fixture()
        rows[0].ticktime,rows[1].ticktime=rows[1].ticktime,rows[0].ticktime
        session=SimpleNamespace(execute=lambda *args:rows)
        with self.assertRaisesRegex(RuntimeError,'row_hash_sum'):
            verify_partition('futures_tick_data',p,session)

    def test_boundary_merge_preserves_all_october_fingerprints(self):
        new=partition(1,2)
        prior=partition(3,4)
        later=partition(5,6,date='2026-10-02')
        manifest=dict(partitions=[new],preserved_partitions=[prior,later])
        combined=verification_partitions(manifest)
        self.assertEqual(sum(p['rows'] for p in combined),6)
        merged=next(p for p in combined if p['value_date']=='2026-10-01')
        self.assertEqual((merged['min_tick_id'],merged['max_tick_id']),(1,4))
        self.assertEqual(merged['row_hash_sum'],(1<<256)-2)
        self.assertEqual(merged['volume'],20)
        self.assertTrue(merged['load_required'])
        self.assertFalse(next(p for p in combined if p['value_date']=='2026-10-02')['load_required'])
        self.assertEqual(new['rows'],2)
        self.assertEqual(prior['min_tick_id'],3)

    def test_overlap_rejected_before_any_write(self):
        with self.assertRaisesRegex(RuntimeError,'Overlapping tick-key ranges'):
            verification_partitions(dict(partitions=[partition(1,3)],preserved_partitions=[partition(3,5)]))

    def test_original_single_slice_still_supported(self):
        result=verification_partitions(dict(partitions=[partition(1,2)]))
        self.assertEqual(result[0]['rows'],2)


if __name__=='__main__':
    unittest.main()
