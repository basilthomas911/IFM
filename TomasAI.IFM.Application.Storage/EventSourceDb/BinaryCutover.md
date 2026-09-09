# Binary-only event-log cutover

The event log now stores a required, non-empty `EventPayload bytea` column. Ordinary appends, optimistic-version appends, enlisted financial transactions, replay/snapshot reads, projector recovery, business-subscription journals, financial history/receipt recovery and Risk history all use the shared MessagePack event-log codec. There is no JSON read fallback or dual write.

The runtime writes uncompressed MessagePack, selected for the faster measured snapshot decode. The version-1 envelope preserves null AggregateId metadata. A compatible one-field formatter also preserves a default ActorEntityId without normalizing it to `none`. The ITI trigger event serialization constructor preserves null AggregateId, EventSource and CreatedBy values inside workflow snapshots. The registry supplies the concrete type, and the reader restores the authoritative global EventId. Corrupt known-event payloads fail replay; unavailable event types remain visible as UnknownEvent with base64 diagnostic bytes.

## Database reset performed

On 2026-09-09, the user explicitly limited deletion to `localhost:5432/event-source-test-db`.

- Before reset: 1,009,440 events, 42 financial history receipts and 4,441 financial operation receipts.
- The explicit transaction cleared events, their projector/outbox/subscription/financial receipt metadata and event-linked command deduplication records, reset stream positions and removed projector checkpoints.
- It dropped `eventdata` and added required `eventpayload bytea` with a non-empty constraint. Post-reset inspection confirmed zero events and zero financial receipts before tests.
- Global event and stream identities/sequences were retained. Financial projection/business tables were not reset. This was an event-history reset, not a clean financial sandbox; old projection rows no longer have their deleted event history available for replay.
- No other database was changed. Integration tests subsequently appended new binary events.

The first attempt exceeded the regular provider timeout and rolled back. The maintenance attempt completed with a longer timeout and progress notices. The reset SQL uses an explicit dependency list without CASCADE, rejects other database names and connected clients, and refuses to run again once the legacy column is absent.

Ordinary schema creation rejects a legacy JSON event table with an actionable error. Other environments require an explicit cutover before running this version; startup never deletes their history automatically.

## Reproduction and evidence

From the repository root, inspection is read-only:

```powershell
dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- --event-log-inspect-test
```

The completed one-time reset used `--event-log-reset-test-to-binary` and [the guarded SQL](../../scripts/Reset-TestEventLogToBinary.sql). Do not change that script's database guard to apply it to another environment.

Test results are saved under `TestResults/BinaryCutover`; see the validation table below. The frozen 20-event JSON fixtures and original timings are unchanged. JSON decoding is retained only in the benchmark project's legacy comparison reader. The MessagePack benchmark now calls the production shared codec.

## Validation results

| Suite | Passed | Skipped |
|---|---:|---:|
| Shared event-codec/read-model unit tests | 7 | 0 |
| Framework.Storage unit tests | 398 | 0 |
| Event-source and PostgreSQL provider integration tests | 58 | 0 |
| Portfolio event history, atomic writes, workflow recovery and commit-uncertainty integration tests | 15 | 0 |
| Portfolio event round-trip verification | 2 | 0 |
| Fund projector/replay unit tests | 31 | 0 |
| Analytics replay unit tests | 5 | 0 |
| SystemAdmin replay unit tests | 13 | 0 |
| Trade workflow/option replay unit tests | 12 | 0 |
| Portfolio history BDD tests | 2 | 0 |
| Business-journal/projection integration tests | 9 | 2 |
| **Total** | **552** | **2** |

The two skipped tests are existing opt-in live reference/pricing checks requiring `IFM_OCP_LIVE=1`, live credentials and an evidence directory. No event-storage test was skipped. Initial failures from old JSON test fixtures and constructor normalization were corrected and their affected suites rerun successfully. Concurrent test-project builds initially conflicted on shared output files; subsequent builds ran sequentially.

TRX evidence: `binary-event-codec-final.trx`, `binary-framework-storage.trx`, `storage-final.trx`, `binary-financial-storage.trx`, `binary-portfolio-roundtrip.trx`, `fund-replay.trx`, `analytics-replay.trx`, `admin-replay.trx`, `trade-replay-final.trx`, `portfolio-history.trx`, and `business-journal-final.trx`, all under `TestResults/BinaryCutover`.

The unchanged frozen 20-event corpus passed semantic round trips with the production codec in both uncompressed and LZ4 modes, plus 80 malformed-payload rejection checks per mode (160 total). All original fixture byte lengths and SHA-256 hashes were reverified. Both affected replay benchmark projects build without warnings or errors. Final database inspection again confirmed `eventpayload bytea` and no `eventdata` column. `git diff --check` passed.
