# Stage 2 implementation and verification

Date: 2026-09-19. Scope: Stage 2 of the three-stage blotter implementation plan, including the approved calculation-tier integration. Production activation remains gated. Stage 3 trading/blotter and Stage 4 volatility-history workflows are not implemented by this change.

## Delivered behavior

- Futures and futures-option references retain append-only metadata, exact decimal strikes, provider instrument/publisher identity, raw-definition evidence, UTC expiry/last-trading times, reviewed conventions and effective mapping versions. Existing wire keys and integral contract IDs remain compatible; fractional IDs use ordinary decimal notation.
- Both Add/Change editors have an asynchronous, cancellable, filtered and snapshot-pinned Databento selector. Imported facts are read-only, reviewed conventions are separate, confirmation previews the IFM ID, and Change cannot replace an established instrument. Provider references no longer depend on legacy exchange/multiplier lookup aliases. Legacy references retain their lookup validation.
- Options require the exact existing underlying future, including provider instrument/publisher IDs and compatible expiry. Reviewed options additionally require a qualified, completely published underlying version.
- Reference versions and their derived pricing conventions share one immutable payload. Identity claims prevent conflicting provider/IFM bindings. Interrupted publication stays unavailable to qualified pricing; idempotent retry completes publication. Exact historical/effective-version reads and bounded history enumeration preserve prior evidence.
- Qualified European/American futures references route through the Stage 1 calculator. Equity trading remains disabled. Existing American execution/assignment lifecycle gates are not removed by model availability.
- Quote ingress updates bounded latest state without pricing or durable quote replay. Independent background selection/risk sweeps yield between bounded batches. Every sweep covers its captured contract set; it does not wait a full refresh interval for each batch. Timer ticks coalesce instead of queuing overlapping sweeps.
- Selection uses price/Delta and separately refreshed IV with the original IV quote, underlying, calculation time and context. Selected/held ownership requests full-risk scheduling. Context changes, stale observations and retired generations invalidate results.
- `CompositionSnapshotRequest.SelectionOnly` exports schema-2 selection snapshots through the existing host/worker composition API, including exact IV provenance and no fabricated full Greeks. Full valuation retains schema 1. Stage 3 consumers must explicitly request and understand selection snapshots.
- Every trade delivered by the qualified option-chain session gets its own actual-Trade-price full-Greek attempt before latest-value/out-of-order filtering. The durable evidence includes exact native nanoseconds, value date, price, size, sequence, provider identity, generation, pricing context, underlying and either all Greeks or explicit failure. Historical trade Greeks are not current position risk.
- Worker publication uses a dedicated acknowledgment pipe. Completion means host Scylla retention/readback succeeded, not an in-memory enqueue. Wrong-generation/rejected acknowledgments fail closed. A bounded 15-second retention deadline stops the chain on unresolved delivery. Idempotent source identities preserve the first attempt and reject changed economics.

## Retention and compatibility boundaries

`option_trade_evidence` is the authoritative qualified-chain source/Greek evidence, partitioned by contract and value date. It is not a quote replay queue. Duplicate delivery with a new worker generation/receive time does not replace the original pricing attempt. A pricing failure still retains its source trade. No implicit TTL or deletion policy was introduced.

Database acknowledgment is not a promise that unacknowledged provider records survive a process crash: recovery of records not yet retained still depends on provider replay/reconciliation. A write that completed before an acknowledgment was lost is safe to retry by source identity.

Legacy tick projections remain compatibility views; consumers of qualified Trade-basis evidence must use the evidence contract, not assume old quote-derived/zero-default Greek fields are equivalent. The Stage 3 blotter must use the new qualified snapshots/evidence.

Additive startup schemas contain the new columns/tables. Migration and backfill qualification used disposable test keyspaces. Existing application reference data was not automatically relabeled as reviewed, and no application-keyspace migration or live API/UI restart was performed.

## Test evidence

Windows/.NET SDK 10.0.302; .NET runtime 10.0.10. Integration projects ran sequentially. Actor acceptance used disposable NATS 24222, Redis 26379 and PostgreSQL 25432, plus a unique Scylla keyspace. Provider responses, clocks, calendars, rates and lookup entries are fixtures, not a live Databento qualification.

TRX files reside in each named project's `TestResults` directory.

| Scope | Result | Evidence |
| --- | --- | --- |
| Domain market-data full unit suite: identity, old/new payloads, exact strikes, import and validation | 225 passed | `stage2-domain-final.trx` |
| Application market-data full suite: American/European routing, scheduler, snapshots, retention acknowledgment, worker lifecycle | 516 passed, 5 skipped | `stage2-marketdata-final.trx` |
| Securities command-model regression | 20 passed | `stage2-securities-final.trx` |
| Selector and existing editor view-model regressions | 19 passed | `stage2-editors-final.trx` |
| WinForms populated Add/Change, validation failure and rendering | 8 passed | `stage2-ui-workflows.trx` |
| Scylla migration/backfill, immutable versions, concurrent claims, crash/retry, trade retention/reopen/deduplication | 3 passed | `stage2-durable-storage.trx` |
| Real HTTP/NATS actors after schema creation; paging errors; futures/decimal-option Add, Change and query | 1 acceptance scenario passed | `stage2-actor-api.trx` |
| Stage 1 full option-pricer unit regression | 122 passed | `stage2-stage1-regression.trx` |
| Stage 1 full BDD regression | 16 passed | `stage2-stage1-bdd.trx` |
| Stage 1 affected numerical/serialization integration | 8 passed | `stage2-stage1-integration.trx` |
| Maximum-chain managed load and 100 reconstructions | 1 passed | `stage2-load-final.trx` |
| Final API server and full UI builds | zero warnings/errors | final build output |

The five full-suite skips are the live Treasury probe, opt-in composition load, two opt-in supervised-worker extended tests and a platform-specific containment case. Composition load was separately enabled and passed. The other skips are not represented as successful tests. The prior-stage unrelated spread-distribution actor tests were not rerun; the affected numerical integration and market-data consumer regressions were.

Executable scenarios cover provider Add/Change previews in both editors, real command/event projection and readback, explicit missing-field rejection, nearest exact decimal preservation, wrong underlying, concurrent identity collision, unpublished versions, corrected historical versions, retained out-of-order trades, pricing-failure retention, wrong-generation acknowledgment, stale selection and 512-contract yielding sweeps with IV reuse. Existing fixture suites cover time/quote/rate failures and legacy payload rejection.

UI artifacts are under `TomasAI.IFM.UI.Net.SystemTests/bin/Debug/net10.0-windows10.0.17763.0/artifacts/stage2`: `import-False-False.png`, `import-False-True.png`, `import-True-False.png`, `import-True-True.png`, `import-validation-failure.png` and selector layout images. Populated option and validation-failure images were visually inspected. UI and API verification is component-composed: actual selector controls plus editor command tests and real actor transport; it is not one automated desktop-to-database session.

Initial acceptance failures were corrected rather than excluded: test-host connection-setting precedence, missing isolated JetStream/lookup setup, repeat-run event identities and bounded schema-cleanup timeout handling. The earlier native worker-reset intermittency is retained in the previous progress record; the final full market-data run passed that test.

## Load evidence and production gate

Final 30-second synthetic run: 512-contract scope, 244,736 quotes, approximately 8,152 quotes/second, 100 managed runtime reconstructions, reset p95 110.2 ms / p99 463.4 ms, full-snapshot reported p95/p99 139.6 ms, approximately 41.3 MB/s allocated. The 20-second observation reported collection counters 169/35/2 and 197.5 ms cumulative GC pause. See `Application.MarketData.UnitTests/TestResults/stage2-load-final.json` for exact samples (project directory has the `TomasAI.IFM.` prefix).

This is a short correctness/boundedness measurement, not a 30-minute soak, an American-full-chain latency guarantee or market-hours production approval. Full-sweep work increased allocation relative to the earlier one-batch-per-interval run; the earlier faster number did not refresh the entire chain at the requested cadence. Profile allocations and representative American-chain capacity before production review. If calculations exceed freshness limits, results remain unavailable rather than silently appearing current.

Approved configurable test defaults remain price/Delta 250 ms, IV 5 seconds and selected/held risk 1 second. `MaximumContractsPerPass` bounds a yielding batch within a sweep. Production requires a separately versioned approved policy with review evidence; no such approval was enabled here.

## Reproduction

Run the named unit/UI projects with their listed filters/TRX names. Run storage and actor integration projects sequentially. The actor acceptance scenario requires the disposable services described above; pass its test-only PostgreSQL credentials through process-local `POSTGRES_DEV_KEY`, not inline in a base connection string.

For the bounded load run, set process-local `IFM_OCP_LOAD=1`, `IFM_OCP_LOAD_SECONDS=30` and `IFM_OCP_LOAD_EVIDENCE` to an absolute output path, then run the `Maximum_scope_sustained` test. These variables do not activate production feeds.

## Gate outcome

Stage 2 implementation and the scoped fixture-based verification gate passed. The final storage crash/retry run passed all three targeted scenarios. Both application builds passed. No known failing test remains in the executed Stage 2 scope.

This outcome does not claim a live provider probe, a restart of the user's entire deployed system, market-hours performance qualification, completion of unrelated integration suites, Stage 3 broker execution or production activation. Those boundaries and the skipped opt-in/platform tests above remain explicit.
