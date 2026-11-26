# Actual-owner outcome evidence

`outcome_evidence.py` exports **actual persisted rows and receipts** from isolated qualification stores, or from original stores under the controller's currently held stopped-owner fence. It does not run trading models, manufacture financial receivers, activate an adapter or certify a complete session.

The PostgreSQL adapter discovers actual column types and primary keys or suitable non-null/NULLS-NOT-DISTINCT unique identities. It streams every selected column, including original IDs, timestamps and complete hexadecimal bytea payloads, through a REPEATABLE READ / READ ONLY transaction. It refuses missing relations or unsupported identities. `DEFAULT_RELATIONS` names come from the production Storage schema sources; custom subsets remain subsets.

The Scylla adapter uses a cached Cassandra image's bundled driver in a read-only, resource-bounded helper container on the isolated internal network. It performs paged SELECT JSON queries for explicitly listed contract/date/asset partitions; no full-ring scan or ALLOW FILTERING is used. Original JSON decimal text is parsed exactly. The helper does not acquire feeds or download provider data. It supports current `tick_trade_data` / `tick_quote_data` plus the existing futures/option tick tables.

The emulator adapter incrementally reads the actual FileEmulatorLedgerStore checkpoint, preserving order requests, operations, positions, cash journal, broker observations, quotes and match receipts. It checks the original head SHA before and after export. Legacy CommitRevision0 is reported as insufficient financial/source-floor evidence.

The source-proof adapter verifies the actual protocol2 ownership marker and authoritative 148-byte full-source / 128-byte destination receipt checksums. Full-source receipts bind source IDs/content and frozen recipient manifest hashes/counts; they supersede redundant partial receipts as the production owner does. The original source archive is still required to resolve exact recipient identities. Derived receipt indexes are not treated as authority.

All adapters write bounded, externally ordered canonical JSONL records and SHA-256 chains. Decimal values remain exact; original identities/clocks and binary content are not discarded to make comparisons pass. Record comparison detects identity, schema or content divergence, not only count/P&L differences. Incomplete or wrong-kind manifests are refused. A complete store export means its declared query finished; **every result retains `qualified:false`**.

## Configuration

A disposable configuration uses `role: DisposableQualification`, `backend: Original`, `Memory` or `File`, and a12-character lowercase-hex `runId`. Database containers must be named `ifm_session_<runId>_<suffix>` and carry the exact label `ifm.session-qualification=<runId>`.

The original final export instead uses `role: OriginalStoppedOwners`, `backend: Original`, and the actual controller-issued `originalFencePath` plus SHA. Every query checks the real loopback held-boundary endpoint, original owner/native/drain hashes and exact inspected PostgreSQL/Scylla container IDs and database/keyspace identity. Queries are SELECT-only; the PostgreSQL transaction is READ ONLY. A read-only, resource-limited CQL helper attaches only to the actual Scylla container's inspected network. A lost or changed owner fence prevents completion. No second full final database restore is required. No host connection secret or provider key is committed.

```json
{
  "role": "DisposableQualification",
  "runId": "012345abcdef",
  "backend": "Memory",
  "boundary": {
    "sessionId": "actual-captured-session",
    "initialSnapshotSha256": "<verified initial-owner manifest SHA-256>",
    "sourceCaptureSha256": "<verified original source capture SHA-256>",
    "calendarEvidenceSha256": "<calendar evidence SHA-256>"
  },
  "postgres": {"container": "ifm_session_012345abcdef_pg", "database": "ifm_qual_memory", "user": "postgres"},
  "scylla": {
    "container": "ifm_session_012345abcdef_scylla",
    "keyspace": "ifm_qual",
    "partitions": [{"table": "tick_trade_data", "assetType": 1, "contractId": "ES20261218", "valueDate": "2026-11-26"}]
  },
  "emulatorCheckpointPath": "<stopped isolated owner's actual checkpoint>",
  "realtimeJournalDirectory": "<isolated epoch>/sources",
  "limits": {"maximumBytes": 68719476736, "maximumRecordBytes": 16777216, "maximumRecords": 10000000, "chunkBytes": 4194304, "maximumSeconds": 600}
}
```

The source proof helper's temporary SQLite index stores32-byte binary identities in a WITHOUT ROWID table, separately capped at4GiB (or the smaller configured total) with a2-MiB cache. It remains bounded on disk instead of retaining full histories in memory. The external merge has a maximum512 sorted chunks; larger recordings require a reviewed larger chunk budget or split component exports. Temporary sort files and final ledger bytes are charged against the export's finite byte bound. Aggregate snapshot/source/database/replay storage remains a separate capacity gate. No arbitrary old evidence is pruned.

### Large exact comparisons

Set `limits.valueMode: ExactCanonicalDigest` for the original, Memory and File exports together. Each complete canonical row is read and SHA-256 hashed; the ledger retains its exact original category, owner, relation, authoritative identity, canonical byte length and value digest. Schema records remain complete. No column, payload, timestamp or identity is excluded from the digest. This mode cannot display the complete changed row: use the reported original key to inspect the separately retained authentic owner snapshot. Full and digest modes cannot silently compare equal.

Compact components enforce a16-GiB peak scratch/output bound and8-GiB final-ledger bound. A compact bundle accepts at most four components and merges their verified sorted streams directly into one32-GiB bounded ledger; it does not write a second external-sort copy. Four retained8-GiB components plus the32-GiB bundle bound total64GiB per owner, plus a separately reserved metadata allowance. Exceeding any bound fails the export rather than issuing a partial success. Use64-MiB sort chunks, an explicit512-GiB streamed-input limit,100,000,000 maximum records and finite21600-second provider/query deadlines for the large restored database. Input bytes and retained evidence bytes are independent limits. A configured32-GiB free-space floor is checked during output growth.

Actual isolated PostgreSQL measurement on256 rows containing32-KiB bytea values: full ledger16,862,423 bytes, peak scratch/output33,736,040 bytes; compact ledger87,363 bytes, peak185,920 bytes. The final recorded run took5.1005 versus2.0458 seconds. Every original field digest was checked against the full export; real PostgreSQL decimal changes and real Scylla trade/quote changes were also detected. This fixture is a storage measurement, not certification of the complete production dataset.

An original Core-only session has no durable-mailbox handler receipts. Do not export capture-only archive receipts as business-handler success. Original-versus-replay business-owner reconciliation and Memory/File transport-proof completeness require separate explicit qualification evidence; the existing full-owner coverage checks remain strict.

`export-original --config <actual-provider-config> --state <controller.json> --fence <held-evidence.json> --output <new-directory>` resolves the hash-pinned initial owner snapshot, final source archive copy and calendar. It invokes the frozen runtime's typed raw-partition inventory helper when needed; it never guesses future option subscriptions or relies on the rotating128-observation list. Its `result.json` identifies the completed business manifest and original typed partition manifest. `bundle --profile BusinessOnly` requires all11 business categories, every production PostgreSQL relation and the PostgreSQL/Scylla/emulator adapters; it excludes only sources/destinations, whose genuine Memory/File completion proofs are checked separately. Different comparison profiles cannot silently match.

Use new output directories; existing evidence is never overwritten:

```powershell
python scripts/RealtimeMailboxSessions/outcome_evidence.py export-postgres --config isolated-memory.json --output evidence/memory/postgres
python scripts/RealtimeMailboxSessions/outcome_evidence.py export-scylla --config isolated-memory.json --output evidence/memory/raw
python scripts/RealtimeMailboxSessions/outcome_evidence.py export-emulator --config isolated-memory.json --output evidence/memory/emulator
python scripts/RealtimeMailboxSessions/outcome_evidence.py export-proofs --config isolated-memory.json --output evidence/memory/proofs
python scripts/RealtimeMailboxSessions/outcome_evidence.py bundle --component evidence/memory/postgres/manifest.json evidence/memory/raw/manifest.json evidence/memory/emulator/manifest.json evidence/memory/proofs/manifest.json --output evidence/memory/all
python scripts/RealtimeMailboxSessions/outcome_evidence.py compare --expected evidence/memory/all/manifest.json --actual evidence/file/all/manifest.json --output evidence/comparison.json
```

The bundle derives category/adapter/relation coverage from the verified actual records and discovered schemas. Missing raw rows, production relations, emulator authority or any of the13 outcome categories produces explicit coverage failures; matching incomplete bundles cannot pass. All-family source coverage, native mapper checkpoint completeness, stopped-owner/multi-store consistency, original baseline/source-floor binding, actual handler replay and session qualification must be proved independently by the session orchestrator. These tools do not supply a missing actual replay host or treat diagnostic taps as authoritative tapes.

## Tests

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
python -m unittest discover -s scripts/RealtimeMailboxSessions -p test_outcome_evidence.py -v
$env:IFM_OUTCOME_DISPOSABLE_TESTS='1'
python -m unittest discover -s scripts/RealtimeMailboxSessions -p test_outcome_evidence.py -v
```

The explicit disposable fixture uses only cached PostgreSQL17.2 / Scylla6.2.2 / Cassandra5.0 images, an internal labeled network, no exposed ports and no live stores. It exports genuine production-schema order rows, exact large decimals/blobs, detects a cash change without changing counts, and verifies paged raw CQL values. These are actual-store adapter checks, not complete business-owner/session replay evidence.

Verified 2026-11-25: 12/12 tests passed with both actual disposable stores (55.974 seconds). The subsequent failure-cleanup regression passed all 11 pure tests (0.463 seconds; two actual-store cases explicitly skipped). Logs and exact coverage: .artifacts/realtime-mailboxes/20261125-safety/outcome-evidence-results.md. The helper is removed by its exact generated name in a finally block, including failed/timed-out queries; no broad Docker cleanup is used.
