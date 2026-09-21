# Event-log candidate: closing-basis qualification

## Outcome

Continuation of the owned `092020260032` accounting qualification found and fixed a production-code arithmetic defect. No production stores, event-log schema, broker settings, or running applications were changed. This is correctness qualification, not a throughput benchmark or deployment approval.

## Reproduction and narrow fix

`BrokerExecutionAccountingModel.Create` receives opening signed settlement as a **per-leg total**, but previously added that total for every closing fill. For two contracts opened at 100 with multiplier 50, then closed separately at 120 and 122:

- Opening basis: 10,000.
- Closing proceeds: 12,100.
- Correct gross realized profit: 2,100 (commissions posted separately).
- Observed before fix: **-7,900**, because opening basis was counted twice.

The model now tracks accounted opening leg IDs and adds each supplied basis once. Settlement and commission postings remain per fill; durable accounting intent, command identity, authorization, and replay handling are unchanged.

Regression cases cover long and short positions, gains and losses, split fills, and deterministic results with reversed input-fill ordering.

## Evidence

Reports are under `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/`:

| Report | Result |
| --- | --- |
| `closing-basis-before.trx` | 3 passed, 1 failed; actual -7,900 versus expected 2,100 |
| `closing-basis-after.trx` | 7 passed, 0 failed |
| `closing-basis-portfolio-regression.trx` | Full Portfolio unit suite: 233 passed, 0 failed |

The 7 focused cases are included in the 233-test suite, not additional independent coverage. Those unit tests use constructed execution evidence; joined coverage is recorded separately below.

## Follow-up: joined full-close qualification

`BrokerAccountingRuntimeTests` now extends both opening scenarios (fully filled and cancelled after a partial fill) with a full close of the one contract actually opened. Opening evidence is persisted through `TradeDb.UpsertEstablishedTradeAsync` into the owned Scylla fixture and loaded by the real `BrokerExecutionAccountingApi`. Posting uses the real GeneralLedger actor, NATS and candidate Postgres store.

- Missing opening trade rejects the close without advancing financial revision; persisting that evidence allows the same closing execution to proceed.
- Concurrent identical close deliveries advance financial revision only once.
- Profit scenario: opening 100, closing 120, multiplier 50; gross profit 1,000, total commissions 5, ending cash 11,095 (including the existing test's extra 100 deposit).
- Loss scenario: opening 100, closing 80; gross loss 1,000, total commissions 5, ending cash 9,095.
- Test book settlement posts against clearing, while realized P&L reclassifies clearing to P&L rather than cash. Clearing ends at zero; aggregate debits equal credits.
- Account balances use debit-minus-credit. A credit-normal profit account therefore stores -1,000 for the profit scenario. The initial joined run failed solely because the new assertion expected the opposite sign; the storage constraint and query contract were inspected before correcting the test.
- After host shutdown/recreation in the same process, both opening and closing execution replay succeed with changed delivery metadata and unchanged balances/revision.

Reports in the same artifact directory:

| Report | Result |
| --- | --- |
| `joined-full-close-accounting.trx` | 0/2; incorrect test expectation for credit-account balance sign |
| `joined-full-close-accounting-verified.trx` | 2/2 passed, including host-recreation replay |

Only the test fixture changed in this follow-up. Synthetic records remain in the owned stores; normal application stores were not touched. The build retained four existing generated MessagePack CS0436 warnings.

Limits: constructed executions, single-contract full closes, synthetic posting rules, same-process host recreation. This does not establish production account configuration, actual futures clearing economics, emulator callback integration, OS-process crash recovery, partial closes, or joined multi-fill/multi-leg close coverage. Split-fill arithmetic remains covered by unit tests.

## Integration requirements identified before durable allocation

The original `BrokerExecutionAccountingApi.LoadOpeningBasisAsync` summed **all original fills matching a contract**, without allocating basis to the actual quantity closed or subtracting basis already consumed by prior closing executions. This method has now been replaced by the durable allocation path described below.

Required next work:

1. Implement the approved weighted-average policy for partial closes and subsequent closing attempts, including unequal opening fill prices and rounding/remainder handling.
2. Carry quantity and consumed-basis evidence through the authoritative position/accounting boundary; validate direction, ownership, cumulative fill quantity, and over-closing.
3. Qualify missing/corrupt opening evidence, split/full/partial closes, subsequent attempts, and concurrent/replayed submissions using the isolated Scylla/Postgres fixture.
4. Verify production ledger account mappings: the synthetic clearing/P&L mappings above passed joined balance and host-restart tests, but production configuration and broader journal scenarios remain unqualified.

The user explicitly selected weighted-average cost per leg within each strategy position. The earlier policy question is resolved; separate trades and funds must not share basis.

## Weighted-average calculator qualification

Initially added `WeightedAverageClosingBasis` as a pure calculator; the subsequent durable integration below now invokes it from the posting API. Inputs are the immutable opening lots for a single position leg, the actual signed closing quantity, and previously consumed quantity and signed basis. Commissions remain separate.

- Basis is weighted by opening fill quantity, not fill count.
- Allocation is the difference between rounded cumulative basis at the new and previous closed quantities (USD cents, midpoint-to-even). This avoids accumulating independently rounded per-close errors; final close consumes every remaining cent.
- Long and short directions are supported. Zero closes, mixed opening directions/multipliers, non-cent opening settlement, over-closing, and inconsistent previously consumed basis are rejected.
- The opening lot set must remain fixed for this calculation. Late opening corrections require explicit reconciliation, not silently recalculating already consumed basis.
- Result includes this close's allocated basis, remaining signed quantity/basis, and cumulative consumed quantity/basis.

`weighted-average-portfolio-regression.trx`: **245 passed, 0 failed**, including 12 new calculator cases. Tests cover unequal opening prices/quantities, long/short partial and final closes, fragmented rounding conservation, and invalid evidence. No database or broker was used in this run.

The calculator stage alone did not provide concurrency safety. Persisted trade `ClosingFills` is not treated as an atomic financial consumption ledger.

## Durable weighted-average posting integration

Added `portfolio_financial.broker_closing_basis_claim`, keyed by portfolio, complete position identity, opening leg and accounting operation. A unique execution-attempt constraint also prevents the same attempt consuming the same leg twice. This additive schema was applied only to the owned qualification database.

`ClosingBasisIntentClaim` now:

- Validates position ownership and deterministic position identity, unique fill evidence, contract/component/attempt matching, multiplier, direction and aggregate leg quantities. Mapping a closing contract to multiple opening legs is rejected as ambiguous.
- Acquires a transaction-scoped advisory lock for the position; loads prior claims and their committed financial-operation receipts.
- Allocates actual filled quantity using weighted-average opening cost, verifies immutable opening evidence hashes, and inserts the per-leg claims in the **same transaction** as the exact immutable accounting command.
- Returns the original request for duplicate execution delivery. New attempts are rejected while an earlier claim has no posting receipt. Failed, timed-out, expired or revision-conflicted requests do not automatically release or rebase reservations: explicit reconciliation remains required.
- Rejects untracked legacy closing evidence rather than guessing historical consumption. Existing production history still requires a migration/reconciliation inventory; this is not a backfill implementation.

The initial joined run exposed another defect: every realized posting had source sequence 1, so the second partial close failed with `Valuation/realization source is stale`. Closing postings now identify the original trade/order, and the immutable request uses the next persisted valuation sequence. Financial revision concurrency validation remains intact.

Joined qualification uses two unequal-price opening fills (one at 100, one at 110, multiplier 50), followed by one-contract closes. Each consumes 5,250 of opening cost. Both profitable and loss-making cases are covered, with separate commissions. After final close the claims total two contracts and 10,500 basis, and clearing is zero.

Evidence under the same artifact directory:

| Report | Result |
| --- | --- |
| `weighted-average-durable-unit.trx` | 245/245 Portfolio unit tests passed |
| `weighted-average-durable-joined.trx` | 2/4 passed; subsequent close exposed stale realization sequence |
| `weighted-average-durable-joined-verified.trx` | 4/4 passed after sequence correction |
| `weighted-average-durable-concurrency.trx` | 4/4 passed with deterministic pending-claim concurrency test |
| `weighted-average-durable-final.trx` | 4/4 passed, additionally asserting persisted quantity/basis totals |
| `weighted-average-durable-storage.trx` | 10/10 selected intent/ledger storage regressions passed, including enlisted-intent rollback |
| `weighted-average-durable-unit-final.trx` | 245/245 Portfolio unit tests passed against final source |

The four final scenarios include opening Filled/Cancelled statuses, full and sequential partial closes, concurrent duplicate delivery, a distinct competing close blocked after reservation but before dispatch, over-close rejection, and unchanged balances on opening/closing replay after same-process host recreation. These reports repeat overlapping scenarios; they are not additive unique coverage.

### Remaining acceptance limits

- The initial nonzero-MTM partial-close guard is superseded by the [proportional MTM qualification](Event-Log-V2-Proportional-MTM-Qualification-2026-09-20.md). Proportional closes now restore the remaining valuation atomically; uneven leg closes still require per-leg MTM evidence.
- Pending/failed reservation reconciliation and historical allocation backfill are not automated. Earlier failed synthetic runs intentionally retain their unposted claims.
- Multi-leg joined accounting, short-position joined coverage, MTM/futures daily settlement, real broker callback flow, and process-kill recovery of this new allocation path remain unqualified. Calculator units cover short arithmetic.
- Opening evidence is read from Scylla before the PostgreSQL claim transaction. Hash checking detects changes against prior claims, but cross-store concurrent opening-evidence correction is not an atomic workflow; opening corrections require explicit coordination/reconciliation.
- Production account mappings and trade/position actor partial-close lifecycle remain separate acceptance work. Passing the accounting API scenarios is not an end-to-end trading deployment approval.

Read-only catalog verification still shows exactly the candidate's three event-log indexes and `financial_legacy_event_fence`. No event-log schema stripping, production database migration, deployment, or new throughput benchmark was performed.

No candidate cutover is approved by these results. No new performance improvement is claimed.
