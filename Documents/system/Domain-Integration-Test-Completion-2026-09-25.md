# Domain integration verification — 2026-09-25

All ten domain integration/integrated projects completed their applicable verification phases.
Final unique-case result: **556 passed, 0 failed, 8 explicitly skipped (564 total)**.
Skipped cases are listed below and are not counted as passing coverage.

| Project | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Application | 1 | 0 | 0 | 1 |
| Market Data Analytics | 46 | 0 | 0 | 46 |
| Market Data Feed | 60 | 0 | 4 | 64 |
| Market Data | 44 | 0 | 2 | 46 |
| Market Data Securities | 15 | 0 | 0 | 15 |
| Option Pricer | 16 | 0 | 0 | 16 |
| Portfolio | 202 | 0 | 0 | 202 |
| Reference | 17 | 0 | 0 | 17 |
| System Administration | 3 | 0 | 0 | 3 |
| Trade | 152 | 0 | 2 | 154 |
| **Total** | **556** | **0** | **8** | **564** |

## Verification phases and evidence

Integration projects ran sequentially, as required by `Integration-Test-Execution.md`.
Results for Application, Analytics, Feed, Market Data, Securities, Option Pricer, Reference,
and System Administration are in each project's `TestResults/domain-integration-final.trx`.

Feed's main run passed 59 cases and skipped five. Its optional disposable-broker outage test
then passed with `IFM_STAGE3_ISOLATED_NATS=1`; see
`TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TestResults/domain-outage-final.trx`.
The combined count is 60 passed and four skipped, without double-counting that case.

Portfolio evidence is under
`TomasAI.IFM.Domain.Portfolio.IntegrationTests/TestResults/domain-final/`:

| Phase | Passed | Evidence |
| --- | ---: | --- |
| Persistence, messaging, bootstrap, and startup verification | 187 | `portfolio-standard-final.trx` |
| Normal live API, authorization, catalog, workflow, pagination, and load | 10 | `portfolio-live-main.trx` |
| Portfolio, authorization, and 256-portfolio replay after API restart | 3 | `portfolio-live-restart.trx` |
| Workflow configuration and composition after restart | 1 | `portfolio-live-workflow-restart.trx` |
| Rollback mode: reject writes while allowing reads | 1 | `portfolio-live-rollback.trx` |

The live load qualification checks 256 creates, 512 concurrent duplicate requests,
256 rejected conflicting payloads, and exact authoritative/projected state. A new API process
then handles 256 retries while every Portfolio stays at business revision one.
This is bounded correctness/load qualification, not a sustained performance benchmark.

Trade evidence is under `TomasAI.IFM.Domain.Trade.IntegratedTests/TestResults/`:

- `trade-standard-final.trx`: 140 passed, two live-data skips; includes all eleven successive-stage,
  strategy-variant, and end-to-end tracing cases.
- `trade-qualified-final.trx`: 12 passed; covers broker accounting and long/short partial-close
  lifecycle persistence across host restart.

Additional focused regression evidence:
`TomasAI.IFM.Framework.Messaging.Nats.UnitTests/TestResults/durable-tracing.trx` — 24 passed.
These unit cases are additional to the 556 domain integration passes.

## Corrections verified

- Financial query replies now require schema version **2**, matching all 15 current financial
  query contracts. The user explicitly approved this change. Existing identity, scope,
  correlation, timestamp, and payload-size checks remain active.
- Durable projector publication carries W3C trace headers. Process and replay workers restore
  each delivery's context and clear inherited worker-startup context. The real five-stage
  workflow now stays in its initiating trace; queue tests also cover replay and untraced messages.
- Workflow starts retain their pinned selection binding. A pending composition reservation
  dispatches the reservation operation. Pricing snapshot conversion calculates the semantic
  digest for the destination representation. Regime-discovery initialization observes the
  workflow deadline.
- Workflow fixtures explicitly enable starts, publish uniquely identified profiles, and verify
  financial admission immediately after funding. Integration assertions and positional SQL
  parameters were updated for the current contracts and receipt-based history behavior.
- Qualification uses the approved, run-scoped Scylla keyspaces on port 9042. An older qualification
  API process sharing the test broker was stopped; both long and short close/restart cases then
  passed. No direction-specific trade-state relaxation was needed.

Earlier diagnostic failures from mismatched test broker settings and a misnamed qualification
artifact folder were corrected in the runner. They are superseded by the final successful runs.
The temporary Portfolio runner is `.tmp/Complete-PortfolioIntegration.ps1`; it stops its API
children between normal, restart, and rollback phases and on failure. Test stores and evidence
are retained. No qualification API is left running at completion.

## Explicitly unexecuted cases

Four Feed cases target retired direct tick-insertion paths; TickAggregation is now the
sole feed-tick persistence boundary:

- `InsertFuturesTickData_Ok`
- `InsertFuturesOptionTickData_Ok`
- `InsertFuturesOptionTickData_WithCallOptionContracts_Ok`
- `InsertFuturesOptionTickData_WithDifferentOptionContracts_Ok`

Four external live-data cases retain their existing opt-in requirements:

- Market Data: `Official_live_curve_import_reaches_durable_queryable_download_log`
- Market Data: `Observe_october_first_evaluated_chain_for_five_minutes`
- Trade: `Inspect_live_reference_definitions`
- Trade: `Published_reference_live_worker_pricing_durable_handoff_replacement_and_close`

This verification does not claim those external live-data cases passed. No test was newly
disabled or weakened to obtain the final result. `git diff --check` completed successfully.
