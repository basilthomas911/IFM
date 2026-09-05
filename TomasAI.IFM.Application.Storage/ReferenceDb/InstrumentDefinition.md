# Instrument definitions

ReferenceDb owns these schema-managed tables:

- `instrument_definition`: complete, unmodified Databento JSON records plus queryable identity/product columns. Primary key `((snapshot_id, dataset, bucket), record_index)`; 128 buckets per dataset prevent a single oversized partition.
- `instrument_definition_product`: distinct current futures/futures-option products, keyed by `((snapshot_id, family), symbol, exchange, currency)`, with the stable IFM symbol ID.
- `instrument_definition_snapshot`: one `current` pointer, completion time, ingested record count and dataset list. It is published only after all raw and product writes complete.

Access through `IReferenceDbContext.InstrumentDefinitions`. `ReadJsonAsync(snapshot, dataset, bucket)` streams the original records. `GetSymbolsAsync(snapshot, family)` reads the compact product partition, without `ALLOW FILTERING`, a raw-table scan, a provider call or ID allocation.

The public API uses `StoredInstrumentDefinitionSymbolCatalog`; `InstrumentDefinitionRefresh.RefreshAsync` handles imports and can later be called by a scheduler. The explicit API Server mode `--refresh-instrument-definitions-only` populates the configured database without starting normal application services. Scheduling and old/incomplete snapshot cleanup are not enabled in this change. Existing completed data stays queryable during a refresh.

Live verification on 2026-09-05 populated Development `reference_test_db` from `GLBX.MDP3`: snapshot `62764663-2e6f-41e5-bbfe-0a87c40d18fe`, 1,567,072 raw records, 2,735 Futures products and 1,023 FuturesOption products. Independent reads counted all 128 partitions, validated 256 JSON samples and verified the metadata and stable IDs of every indexed product. The live Scylla integration test also passed. See `.tmp/instrument-definition-verification.json` and `.tmp/instrument-definition-load.log` for the recorded results.
