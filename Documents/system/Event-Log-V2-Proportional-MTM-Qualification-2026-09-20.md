# Event-log candidate: proportional MTM close qualification

## Scope and implementation

Continues the weighted-average closing-basis qualification. Only repository code and the owned synthetic `092020260032` fixtures are in scope. No production migration, broker connection, event-log cutover or throughput claim is made.

The closing-accounting API now supports proportional release of recorded unrealized P&L (MTM) for a single-leg partial close, and calculates a uniform remaining-quantity ratio for multi-leg positions. Opening cost allocation remains weighted-average per position/leg; commissions remain separate.

The existing ledger realization operation reverses all prior valuation. Instead of altering serialized financial contracts and old immutable request hashes, the API constructs one atomic batch:

1. Post settlement and commission from confirmed fills.
2. Reverse prior MTM and post realized P&L using the existing realization rule.
3. When MTM remains, restore only the remaining position's valuation using the configured valuation rule.

All items commit inside the existing fenced ledger transaction. No committed intermediate all-cleared MTM state is exposed. This creates additional audit/journal entries compared with a dedicated partial-realization primitive; it is a correctness implementation, not a write-performance optimization.

The MTM portion reversed is `Round(previous MTM * closed quantity / quantity remaining before this close, 2, ToEven)`. The retained amount is the exact difference, leaving any residual cent for later closes. Final close clears the full remainder. Positive and negative MTM follow the same signed calculation.

`BrokerExecutionAccountingModel` now emits a realization item even when gross realized P&L is zero: break-even closes must still reverse existing MTM. Existing immutable intents are replayed unchanged, not rebuilt with the new behavior.

## Safety and compatibility

- No MessagePack fields, financial request schemas, canonical hash algorithms, event-log indexes or triggers changed.
- The restoration rule must be an unconfirmed-movement valuation rule whose debit/credit account bindings exactly match the realization rule's valuation-asset/unrealized-P&L bindings. Otherwise request creation fails before a claim is persisted.
- Realization uses the next stored valuation sequence; restoration uses the following sequence. Existing financial revision validation rejects concurrent state changes after request preparation.
- Multi-leg aggregate MTM can only be apportioned when every remaining leg closes by the same ratio. Uneven leg closes raise `PORTFOLIO_ACCOUNTING.NON_PROPORTIONAL_CLOSE_MTM_RECONCILIATION_REQUIRED` and need per-leg valuation evidence. Even a recorded zero net valuation can hide offsetting leg gains and losses, so it does not bypass this guard.
- Failed or uncertain postings retain durable basis reservations. Existing reconciliation requirements remain; this stage does not add automatic intent rebasing or release.

## Tests and evidence

Reports are under `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/`.

| Report | Result |
| --- | --- |
| `proportional-mtm-unit.trx` | 259/259 Portfolio unit tests passed before the additional net-zero guard tests |
| `proportional-mtm-storage.trx` | 17/17 selected ledger/intent storage tests passed |
| `proportional-mtm-joined.trx` | 8/10 passed; two break-even assertions assumed a P&L balance row exists for zero P&L |
| `proportional-mtm-joined-verified.trx` | 10/10 passed, including MTM balances and restart replay |
| `proportional-mtm-unit-final.trx` | 261/261 Portfolio unit tests passed, including net-zero multi-leg guard |

Final joined build retained the four existing generated MessagePack CS0436 warnings. Scoped diff checks passed. Read-only candidate catalog inspection still shows three event-log indexes (`ux_event_log_stream_version_v3`, `ix_event_log_command_id`, `ux_event_log_event_version`) and `financial_legacy_event_fence`.

The storage failure test throws during the restoration calculation after realization has already written inside the transaction. It verifies unchanged balances and absent committed operation receipt, then retries the same batch and verifies realized and retained unrealized balances. This is transaction rollback qualification, not an OS-process crash test.

The joined scenarios use the real accounting API, ledger actor, NATS, isolated PostgreSQL and persisted opening evidence in isolated Scylla. They cover zero/positive/negative starting MTM, partial and final closes, break-even closes, a one-cent rounding remainder, pending-claim concurrency, over-close rejection and replay after same-process host recreation. The break-even assertion was corrected to sum the relevant balance rows, correctly allowing no row when no realized P&L was posted.

## Remaining qualification limits

**Verified release blocker:** the [partial-close lifecycle audit](Event-Log-V2-Partial-Close-Lifecycle-Blocker-2026-09-20.md) reproduces full-close-only rejection in both trade handlers. Accounting boundary qualification is not end-to-end trading acceptance.

- Joined MTM tests use a single long futures leg and synthetic account mappings. Multi-leg ratio calculation and rejection are unit-tested; joined multi-leg/short strategy qualification remains.
- Portfolio MTM allocation is not exchange variation-margin or daily futures settlement accounting. Those workflows require separate qualification.
- Existing historical intents and positions need migration/reconciliation inventory, especially older break-even closes prepared without a realization item.
- Broker callback flow, UI/position actor partial-close lifecycle, coordinated opening-evidence corrections, pending-intent reconciliation and process-kill recovery remain outside this completed calculation/storage stage.
- Production account configuration and performance overhead of the additional MTM journal item have not been benchmarked or approved for deployment.
