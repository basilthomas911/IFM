# Isolated live-host acceptance progress — 2026-09-20

## Verified results

Latest follow-up: [current-policy lifecycle qualification](Event-Log-V2-Current-Policy-Lifecycle-2026-09-20.md) now passes Portfolio/Fund state transitions and fresh-process recovery using published deployment/profile fixtures. This is protocol/persistence acceptance, not execution of the strategy engines or broker workflow. Earlier failures below are retained as history.

- Docker was restarted and the authorized AIO limit reapplied: `fs.aio-max-nr=1048576`. With the disposable Scylla running, `aio-nr=816416` (232160 slots available). This runtime setting must be reapplied after another Docker restart.
- Run `092020260032` uses disposable PostgreSQL, Scylla, Redis and NATS endpoints. Normal application stores were not migrated.
- The real API reached Healthy bootstrap twice, reporting 185 registered actor types and open consumer intake, with the three-index candidate event log.
- The launcher now supports `-AcceptanceSuite Basic`, running live-host tests before stopping its owned API child.
- Typed business identity allocation passed. Legacy reference-family acceptance failed: 1 passed / 1 failed.

## Initial acceptance mismatch

The failed test `Production_Reference_actor_returns_the_exact_read_only_v1_family_catalog` queries the legacy Scylla `trade_strategy_family_v3` table and expects exactly three definitions. The isolated table has zero rows. Current normal startup calls `StrategyCatalogMigration.EnsureAsync()` for the ConfigurationDb catalog; it does not call `TradeStrategyFamilyBootstrapper.EnsureV1Async()`. That initializer is restricted to the explicit bootstrap-only branch. Therefore the old test does not match current normal startup behavior. No legacy seeding was added to production startup to satisfy it.

The follow-up below qualifies the current catalog separately. The failed legacy test remains unchanged and is not counted as a pass.

## Follow-up: current catalog, writes, and process restart

Four tests passed in three isolated runs:

| Suite | Passed | Verified |
| --- | --- | --- |
| Basic | 2/2 | Real NATS catalog queries return all 22 default definitions with exact identities and content hashes; all remain unpublished drafts. Typed business identity allocation also passes. |
| PortfolioWrite | 1/1 | Real portfolio create/read/update/read, PostgreSQL event authority and Scylla projection visibility. |
| PortfolioRestart | 1/1 | A new API process reads the same active Portfolio; projection equals rehydrated event authority at revision 2. |

Builds completed with zero warnings/errors. The launcher builds before starting the host, then tests with `--no-build` to avoid changing loaded binaries. Tests route to the disposable NATS endpoint and exact run-specific PostgreSQL database. Portfolio write/restart use synthetic Portfolio ID `1909202601`. Run PortfolioWrite once on a fresh fixture, then PortfolioRestart against the retained stores; repeat restart checks are read-only, but repeating PortfolioWrite requires a fresh fixture because the identity already exists.

After these tests, the isolated event log contains 7 total rows and retains exactly `ix_event_log_command_id`, `ux_event_log_event_version`, and `ux_event_log_stream_version_v3`, plus `financial_legacy_event_fence`. No production event-log schema was changed.

The larger configuration/reservation/composition/risk test also has legacy assumptions: it creates a policy with an integer family permission. Current `PortfolioFinancialPolicyCommandActor` rejects new legacy permissions and requires an exact ConfigurationDb deployment; enabled activation validates publication. This test must be migrated with explicit qualified deployment fixtures, not by disabling validation or restoring obsolete startup seeding. Its restart assertion also expects revision 7 while its current write test expects 8 after the manual-order addition; that pair needs consistent current-policy coverage before use.

Next remaining gates: current-policy full workflow, sustained live-host writes, broader recovery and UI acceptance. The passed portfolio restart check covers a stopped API process, not PostgreSQL power loss or graceful-shutdown behavior.

Reproduce (in order on a fresh prepared fixture, replacing the run ID as appropriate):

```powershell
scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Run -AcceptanceSuite Basic
scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Run -AcceptanceSuite PortfolioWrite
scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Run -AcceptanceSuite PortfolioRestart
```

## Evidence and limits

### Bounded concurrent-write qualification: failed, rollout blocked

Update: the retry failure below has now been corrected and verified. See [Retry identity correction and verification](Event-Log-V2-Retry-Identity-Fix-2026-09-20.md) for 47 passing selected tests, including concurrent and fresh-process replay acceptance. The original failure evidence below is retained; broader rollout gates remain open.

Added opt-in `-AcceptanceSuite Load` and `EventLogQualificationLoadTests`. The test guards both NATS and PostgreSQL routing, allocates 256 distinct Portfolio identities, and runs eight concurrent workers. Each worker must create a draft, replay the identical business request successfully, and reject a changed-payload replay. Final checks require exactly one business event per Portfolio and matching projections. Timing output is emitted only after all correctness checks pass; this is a bounded smoke test, not a sustained soak.

The build passed with zero warnings/errors. The live-host test failed on an identical replay before completing the load: `Command ID ... is already associated with a different command payload.` No valid throughput result was produced. Partial synthetic data is retained for diagnosis. The three-index shape remains unchanged; the owned API process was stopped.

Verified mechanism:

1. `PortfolioCommandApi.CreatePortfolioAsync` computes a deterministic command ID from the business idempotency key and Portfolio payload (`IdempotentCommandId.Create`).
2. Each `Send` creates fresh `CorrelationId` and `RequestedOnUtc` metadata.
3. `PortfolioCommand` serializes both metadata fields at MessagePack keys 7 and 8.
4. `CommandAuditMessagePackCodec.Serialize` hashes the complete command bytes. `EventSourceActorDbContext` supplies that hash to duplicate coordination.
5. The same command ID therefore arrives with different audit bytes and is rejected before the actor's business replay handling.

This is an integration contract incompatibility; the failed run alone does not establish that index consolidation caused it. Do not remove payload-conflict checks, force success in the test, or activate the candidate in production.

Required next fix: define stable retry identity separately from volatile per-attempt correlation/time while preserving the original complete audit payload, validating authorization for every attempt, and detecting changed business payload/route/access identity. Review existing persisted audit hashes and other Portfolio/Fund/policy clients before choosing the compatibility mechanism. Add same-payload retry, changed-payload conflict, changed-principal, concurrent duplicate, and post-restart tests, then rerun this load gate. This audit/identity change has not been implemented in this increment.

The full strategy workflow gate also remains open: its current fixture requires a synthetic authoritative instrument snapshot, published current-capability strategy/variant/deployment graph, and published versioned trade-selection/order-composition profiles with exact matching bindings. Legacy integer family permissions and arbitrary profile IDs cannot stand in for those dependencies.

Failure evidence: `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/Load-20260920014505.trx` and `api-20260920014450.log` in the same directory.

- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/api-20260920011655.log`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/api-20260920011947.log`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/basic-20260920012002.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/Basic-20260920013507.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/PortfolioWrite-20260920013542.trx`
- `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/PortfolioRestart-20260920013621.trx`

The launcher terminates its owned API process after the check; this is not graceful-shutdown acceptance. Disposable stores are retained for further verification. No new throughput benchmark was run in this step, and no production cutover is authorized by these results. Full UI, workflow, sustained live-host writes and recovery acceptance remain incomplete. This candidate retains global event identity and financial safeguards; it is not the original fully stripped-down schema.
