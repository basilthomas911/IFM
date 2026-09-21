# Event-log candidate: emulator and financial component qualification

## Result

**Follow-up:** the new joined accounting tests exposed a confirmed replay blocker after a successful first posting. See [Joined accounting replay blocker](Event-Log-V2-Joined-Accounting-Replay-Blocker-2026-09-20.md). The component results below remain valid, but do not qualify cutover.

**53 selected tests passed, zero failed.** No production cutover, live broker connection or market-feed activation occurred. These are component/storage gates, not a completed adapter-to-ledger end-to-end qualification.

The PostgreSQL-backed tests used only the retained synthetic candidate:

- Container: ifm-eventlog-benchmark-20260919 (postgres:17.2, eventlog-v2-benchmark ownership label).
- Endpoint: 127.0.0.1:25432.
- Database: ifm_eventlog_bench_092020260032_synthetic_host.
- IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION was explicitly set; the shared financial transaction helper and event-store fixture both honor it.

The adapter tests directly construct InteractiveBrokersEmulatorTradeBroker with in-memory emulator implementations and synthetic quotes. They do not instantiate a live IBKR adapter. The accounting-model tests are pure unit tests.

## Evidence

Reports are under BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/.

| Report | Passed | Scope |
| --- | ---: | --- |
| emulator-financial-storage.trx | 30 | EmulatorSubmissionIntegrationTests, GeneralLedgerPostingIntegrationTests, LedgerAccountingIntegrationTests, CapacityReservationIntegrationTests, CapacityPositionCloseIntegrationTests |
| emulator-adapter-boundary.trx | 20 | ManualOrderEmulatorTests and BrokerEventBoundaryTests |
| broker-accounting-model.trx | 3 | BrokerExecutionAccountingModelTests |

Verified storage behavior includes concurrent duplicate submission producing one durable emulator order, replay from a fresh store instance, rejection of changed orders/missing consumption/wrong environment, partial-fill and cancellation capacity accounting, reconciled partial/full position close, ledger posting replay, cash/fee/settlement signs, and valuation/realization without double counting.

Verified adapter behavior includes futures outright, vertical spreads and iron condors; complete/coherent quote requirements; insufficient cash; modification/cancellation; replay/recovery of persisted emulator evidence; partial fills; partial cancellation; ambiguous dispatch reconciliation; duplicated/out-of-order callbacks; cancel/fill races; and preventing Paper requests from reaching emulator ports.

The accounting model verifies opening settlement plus separate commission, closing settlement plus realized profit using exact opening basis, and fail-closed behavior for missing basis or invalid settlement rules.

Important distinctions:

- Fresh store/adapter reconstruction is not a separate operating-system process restart test.
- The PostgreSQL emulator-order store test is not itself a call through ITradeBroker.
- Emulator account cash assertions do not prove that the application's General Ledger actor received and persisted the same facts.
- Financial capacity tests use explicit synthetic reconciliation evidence; they do not obtain every fact from an actual adapter callback in one joined run.
- No new throughput benchmark was run and these results do not change the previously measured performance gains.

## Remaining joined acceptance gate

Code inspection located the production boundary in Domain.Portfolio/GeneralLedger/BrokerExecutionAccountingApi.cs:

1. Requires a completed Filled or Cancelled execution with nonzero fill evidence.
2. Queries the applicable financial posting configuration.
3. For closing execution, loads the established trade's opening basis.
4. Builds settlement, commission and realized-P&L items through BrokerExecutionAccountingModel.
5. Sends PostFundTransactionsCommand through IActorService to General Ledger.

No selected test above traverses all these steps from actual emulator observations to durable ledger posting. The next qualification should join them on the isolated host, covering:

- filled opening execution;
- cancelled execution with partial fills;
- zero-fill cancellation producing no posting at the workflow boundary;
- closing execution with persisted opening basis;
- duplicate delivery after posting and after host restart;
- changed fill/commission evidence failing closed;
- exact journal/cash/position assertions and unchanged event-log index/fence invariants.

Replay must be checked after financial revision advances: the API reads current posting configuration/revision while deriving stable operation identities. This is a test requirement, not a confirmed defect.

## Retained candidate state

Post-test inspection confirmed exactly three event-log indexes (ix_event_log_command_id, ux_event_log_event_version, ux_event_log_stream_version_v3) and the financial_legacy_event_fence trigger. Synthetic test data and reports remain available. No application source changes were needed for this increment.
