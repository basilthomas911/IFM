# Durable accounting replay fix

## Outcome

The reproduced completed-execution replay defect is fixed in source and verified on the isolated candidate. **40 selected tests passed**: two expanded joined scenarios, 33 financial storage tests and five unit regressions. API Release build and isolated startup-composition verification also passed.

No production database migration, application launch, live broker connection or event-log cutover occurred. Closing-execution basis, a full emulator-callback-to-ledger run, separate-process restart and UI qualification remain outstanding.

## Implementation

BrokerExecutionAccountingApi now computes a canonical fingerprint of the complete execution evidence and checks for a durable original intent before querying current rules/revision. Matching retries dispatch the original command, preserving its revision, rule references, timestamps, identities and payload. Delivery source-event/confirmation-time differences do not rebuild a matching intent. Changed execution evidence fails closed.

New BrokerAccountingIntentStore uses the same authoritative PostgreSQL database and transaction abstraction. An execution-scoped transaction advisory lock serializes competing initial claims. The first claim wins; subsequent matching claims return it. No process-local cache is needed for recovery.

The additive portfolio_financial.broker_accounting_intent table stores:

- portfolio/operation primary key;
- canonical execution evidence hash;
- versioned MessagePack command payload and SHA-256 integrity hash, wrapped in JSON for storage;
- creation timestamp.

The exact binary command representation matters: the initial JSON-only command experiment preserved semantic values but still failed the audit byte comparison. The final implementation uses CommandAuditMessagePackCodec, verifies the stored bytes/hash, validates the semantic financial input hash and checks that reserialization matches.

The existing command log cannot alone supply the new intent contract: it does not contain the original complete execution fingerprint for a pre-dispatch first-writer-wins claim. No existing audit/receipt table was repurposed or overwritten.

FinancialCanonicalHash, expected-revision checks, command audit conflict checks, authorization, posting validation, financial receipts and event-log indexes/fence remain unchanged. Original commands still go through the real actor on replay; intent lookup is not a replacement success acknowledgement.

## Compatibility and recovery limits

- Existing audited or receipted operations without an intent are refused with RequestMismatch and an explicit reconciliation-required message. No automatic legacy reconstruction/backfill is performed.
- The table is additive in PortfolioFinancialSchema initialization. Production must initialize the new schema before enabling this code.
- Retain and back up intents with financial records. No cleanup/retention deletion has been added.
- A claimed command that never posted and later becomes expired or loses its expected revision still fails normal financial validation. It requires reconciliation; this change does not silently rebase or extend it.
- Evidence fingerprint/schema evolution requires deliberate compatibility handling. Unknown or corrupted stored transport fails closed.
- Qualification left its synthetic rows in place, including intermediate JSON-format experiment rows and one deliberately corrupted test-owned intent. These are not production records.
- The tests recreate the actor host inside one test process; they do not prove recovery after an operating-system process crash.
- This adds a read per accounting API invocation and one durable intent claim per new execution. No new performance benchmark was run; previous append throughput figures do not measure this added accounting work.

## Verification

Artifacts: BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/.

| Report | Result |
| --- | --- |
| joined-accounting-intent-first.trx | 0/2; intermediate JSON-only experiment retained the audit conflict |
| joined-accounting-intent-binary.trx | 2/2; exact transport preservation fixed original replay cases |
| joined-accounting-intent-expanded.trx | 2/2; expanded final scenarios |
| accounting-intent-storage-regression.trx | 33/33 |
| accounting-intent-unit-regression.trx | 5/5 |

Each expanded joined scenario (Filled, or Cancelled with a partial fill) verifies:

1. Concurrent first delivery through the real accounting API and General Ledger actor posts only once.
2. Funding 10,000 minus settlement 5,000 minus commission 2.50 leaves cash 4,997.50.
3. Identical replay leaves balance and financial revision unchanged.
4. Changed price, commission, quantity or external execution ID is rejected.
5. An unrelated 100 deposit advances revision; recreating the API and changing delivery metadata still replays without posting again, leaving cash 5,097.50.
6. A zero-fill completed cancellation is rejected at the accounting API boundary.
7. After disposing/recreating the actor host, replay still preserves cash and revision.

Storage tests additionally verify competing proposals choose one exact command before any actor dispatch, byte-identical recovery from a fresh store instance, changed evidence refusal, legacy-audit refusal without creating an intent, and transport-integrity rejection.

The original 30 emulator/ledger/capacity storage tests also pass. Unit regressions cover accounting translation and financial error classification.

API startup verification explicitly completed without starting schemas, actors, feeds or HTTP listeners. Post-test inspection found the same three event-log indexes and financial_legacy_event_fence, plus the new intent table in the isolated candidate only.
