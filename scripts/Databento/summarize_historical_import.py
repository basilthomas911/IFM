"""Publish completion only after both futures imports pass full row verification."""
import decimal
import json
from pathlib import Path
from import_es_october_backtest import save


def main():
    root=Path('.artifacts/backtest-nov2025-sep2026')
    proof=dict(target='trade_tick_quote_backtest_db',
        new_window_utc=['2025-11-01T00:00:00Z','2026-10-01T00:00:00Z'],
        combined_window_utc=['2025-11-01T00:00:00Z','2026-11-01T00:00:00Z'],
        tables=['futures_tick_data','futures_tick_data_by_time'],
        scope='Outright ES and VX futures trades only; no options or calendar spreads',
        products={},new_rows_per_table=0,combined_rows_per_table=0,
        source_keyspace_modified_by_importer=False)
    cost=decimal.Decimal(0)
    for symbol in ('ES','VX'):
        folder=root/symbol
        manifest=json.loads((folder/'import-manifest.json').read_text())
        verification=json.loads((folder/'verification.json').read_text())
        acquisition=json.loads((folder/'acquisition.json').read_text())
        assert verification['verified'] and manifest['target']==proof['target']
        assert [manifest['start'],manifest['end']]==proof['new_window_utc']
        expected=manifest['total_rows']+sum(p['rows'] for p in manifest['preserved_partitions'])
        assert expected==verification['expected_rows_per_table']
        assert all(verification['tables'][table]['total_rows']==expected for table in proof['tables'])
        assert manifest['instrument_class_counts']=={'F':manifest['total_rows']}
        assert manifest['total_rows']+manifest['excluded_spread_records']==acquisition['trades']['records_estimate']
        for receipt in acquisition.values():
            cost+=decimal.Decimal(receipt['cost_usd'])
            assert [receipt['start'],receipt['end']]==proof['new_window_utc']
        proof['products'][symbol]=dict(new_rows_per_table=manifest['total_rows'],
            preserved_october_rows_per_table=expected-manifest['total_rows'],
            combined_rows_per_table=expected,excluded_spread_records=manifest['excluded_spread_records'],
            source_sha256=manifest['raw_trade_archive_sha256'],
            contract_ids=sorted(set(p['contract_id'] for p in manifest['partitions']+manifest['preserved_partitions'])),
            earliest_new_value_date=min(p['value_date'] for p in manifest['partitions']),
            latest_new_value_date=max(p['value_date'] for p in manifest['partitions']),
            full_row_checksums_verified=True,october_preservation_verified=True)
        proof['new_rows_per_table']+=manifest['total_rows']
        proof['combined_rows_per_table']+=expected
    proof['databento_acquisition_estimate_usd']=str(cost)
    tuning=json.loads((root/'ES'/'temporary-write-timeout.json').read_text())
    assert tuning['restored'], 'Temporary Scylla timeout must be restored before publishing completion'
    proof['temporary_runtime_setting_restored']=True
    proof['original_write_timeout_seconds']=tuning['original_seconds']
    proof['complete']=True
    save(root/'completion-proof.json',proof)
    print(json.dumps(proof,indent=2))


if __name__=='__main__':
    main()
