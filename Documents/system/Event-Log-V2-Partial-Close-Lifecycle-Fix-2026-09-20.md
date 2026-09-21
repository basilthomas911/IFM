# Partial-close lifecycle implementation and qualification

## Implemented behavior

This implements the lifecycle fix authorized after the partial-close blocker report. No production deployment, event-log migration, account configuration change or live broker action was performed.

- Futures and option trade close handlers now accumulate closing fills against **actual original fill quantities**, not requested leg quantities. Partial closes return the trade to Open, retain all accepted closing evidence, and leave ClosedAtUtc unset. Full consumption marks the trade Closed.
- Stable fill identities deduplicate repeated evidence; changed duplicate payloads, reused external execution identities, wrong direction, ambiguous contract mapping and over-closing are rejected.
- The execution projector includes closing fills in futures outright, iron-condor and vertical-spread position close commands.
- Position calculations reduce each leg by its actual closing quantity, accumulate realized price P&L from execution prices, and retain the remaining open position. Partial closes preserve route generation; final closes zero remaining quantities and advance route generation.
- Position snapshots retain accepted closing-fill evidence so replay after serialization/rehydration cannot consume quantity twice. Price updates and EOD snapshots carry that history forward.
- Existing time-only position close commands remain supported. New fill-aware close commands use appended MessagePack key 5; snapshots append key 12. Missing historical fields deserialize as null, so the new properties explicitly normalize null to empty arrays. Existing serialized key numbers are unchanged.

Position P&L retains the existing price-unit calculation convention; this does not redefine it as cash-multiplier-adjusted ledger P&L. Financial accounting continues through its separate qualified boundary.

## Evidence

Reports are in `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/`.

| Report | Result |
| --- | --- |
| `partial-close-lifecycle-initial.trx` | 29 passed, 2 old blocker-characterization expectations failed because partial closes now succeeded |
| `partial-close-trade-unit.trx` | Full Trade unit suite: 1,107 passed, 1 unrelated option-pricing manifest expectation failed |
| `partial-close-wire-recovery.trx` | 10 passed, 1 historical missing-field normalization test failed; corrected in source |
| `partial-close-lifecycle-joined.trx` | 22 passed, 1 same historical missing-field failure; includes passing real-actor lifecycle and accounting scenarios |
| `partial-close-lifecycle-unit-final.trx` | 35/35 focused lifecycle tests passed after corrections |
| `partial-close-lifecycle-joined-verified.trx` | 23/23 passed: 2 real-actor lifecycle, 10 accounting, 11 wire/recovery cases |
| `partial-close-wire-recovery-final.trx` | 11/11 passed, including final historical-snapshot compatibility assertion |
| `partial-close-trade-unit-verified.trx` | Full Trade unit suite: 1,108/1,108 passed after correcting the stale manifest assertion |
| `partial-close-trade-bdd.trx` | 5/5 selected TradeFlowBehaviorTests passed |
| `partial-close-trade-verification.trx` | 12/12 selected TradeFlowQualificationTests passed, including deterministic replay and tick-routing allocation checks |
| `partial-close-accounting-qualification.trx` | 12/12 isolated partial-close actor and broker-accounting cases passed |
| `partial-close-emulator-registration.trx` | Emulator composition registration passed |
| `partial-close-emulator-unit.trx` | Complete trade-broker emulator unit suite: 38/38 passed |
| `partial-close-portfolio-unit-full.trx` | Complete Portfolio unit suite: 261/261 passed |
| `partial-close-portfolio-integration-final.trx` | 13/13 isolated ledger-accounting and emulator-submission integration cases passed |
| `partial-close-ui-full-verified.trx` | Complete UI system suite: 231/231 passed using retained MC-R08 runtime evidence |

The 11 wire/recovery cases overlap the combined 23-case run; counts are not additive. Four existing generated MessagePack CS0436 warnings remain. Scoped diff checks passed.

New pure tests cover futures and option trade partial/final transitions, long/short single- and multi-leg position reductions, requested-versus-filled opening quantities, changed duplicate evidence, over-closing and rehydrated replay.

Two new real-actor tests use isolated NATS/Postgres/Scylla to create long/short futures trades, let the actual trade projector establish positions, apply partial closes, verify persisted remaining quantities, recreate the host, replay a committed trade command, and complete final closure. They dispatch trade/position commands directly in the execution handoff order; they do not simulate the entire broker callback pipeline. Host restart is in the same OS process, not a process-kill test.

Wire tests cover the three close-command types, historical time-only payloads, old position snapshots and serialized partial-position deduplication.

The option-pricing manifest failure is resolved. The test now asserts all 29 field names in key order, including existing appended fields PremiumStyle, UnderlyingKind, Strike and Right (keys 25–28), instead of expecting 25 fields. No production pricing contract changed. The BDD and verification runs above are selected local suites, not complete broker or database acceptance runs.

The complete UI system run initially identified theme-convention and stale expectation failures. The broker account dialog and provider selector now inherit DarkTradingForm; the broker evidence, manual order, blotter and workflow-details controls inherit DarkTradingView. Stale reference-selector, reflection-argument, and broker-evidence-tab assertions were aligned with the current UI. The repaired complete suite passes. The MC-R09 observation cases consumed the retained Daily, Weekly and Monthly MessagePack artifacts generated by MC-R08; no synthetic replacement was introduced.

## Outstanding qualification

- UI behavior and the emulator/ledger component suites are now qualified by the complete runs above. Joined option-position actor/route integration, a complete callback-to-projector trade execution, concurrent distinct closing attempts across accounting/trade/position boundaries, and process-kill recovery between handoff stages still need dedicated acceptance harnesses.
- Historical payload readability is tested; byte-identical replay of old audited position commands across the schema upgrade is not qualified. Do not infer mixed-version deployment safety from append-only keys alone.
- Existing projection ordering, manual correction after partial realization, historical reconciliation, uneven-leg MTM and daily futures settlement remain outside this change.
- The prior two-trades-per-order policy is unchanged. Additional hedge trades or automatic hedge execution are not introduced.

No candidate production cutover is approved by these results.

## Qualification host correction

The initial MC-R08 refresh failed before its test body because the Test profile supplied no TradeDb connection and the generic storage fallback selected an obsolete provider. The integration host now binds TradeDb explicitly to `IFM_TEST_TRADE_CONNECTION` or a configured ScyllaDB connection and fails with a ScyllaDB-specific configuration error if neither exists. Obsolete provider-specific tests and migration helpers were removed; IFM qualification requires only PostgreSQL and ScyllaDB persistence.
