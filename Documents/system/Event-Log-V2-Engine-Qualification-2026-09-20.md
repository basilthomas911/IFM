# Event-log candidate: isolated engine qualification

## Status

The isolated engine/financial correctness gate now **passes: 46/46**, with **74/74 default-policy unit regressions** also passing. No production cutover was performed. Broker execution/fills, fill accounting and UI qualification remain separate gates.

Follow-up component/storage qualification passed another 53 selected tests; see [Emulator and financial qualification](Event-Log-V2-Emulator-Financial-Qualification-2026-09-20.md). The joined adapter-to-General-Ledger acceptance path remains outstanding.

Initially, after updating an outdated synthetic snapshot fixture, 35 of 36 five-stage cases passed. The remaining Daily / LongBullishIronCondor case was rejected correctly because its authorization expired during cold fixture setup. The resolution below supersedes the initial failure analysis retained later in this report.

This is correctness qualification, not another throughput benchmark. The earlier paired performance results remain unchanged.

## Completed expiry diagnosis and correction

The diagnostic rerun reproduced the cold-case failure. In engine-five-stage-diagnostic.trx:

- Synthetic portfolio/fund and authorization portfolio/fund all matched: 793940170.
- Both authority epochs were 1; a read-only PostgreSQL query confirmed operating state Active.
- Authorization expiry was 2026-09-20T02:42:59.4336474Z.
- The test reached the append boundary at 2026-09-20T02:42:59.5017775Z: already about 68 ms expired.

The production guard was correct. Further diagnostics in engine-cold-bound-diagnostic.trx proved the effective expiry came from the Market Condition assessment. Its required ReferenceQuote source has a two-second maximum age; downstream selection, composition and risk correctly preserve that earlier expiry. Creating/funding a synthetic financial book and cold execution overhead were inside that window.

Fixture-only correction:

- Before calculation, the five-stage correctness suite freezes an explicit synthetic assessment policy with source freshness ceilings of at least 30 seconds. Larger existing source ceilings remain unchanged. Its parameter hash and workflow binding are recomputed consistently.
- The suite explicitly requests a 20-second composition lifetime/window ceiling. Every earlier inherited bound, quote-age bound and production minimum calculation still applies: this does not guarantee 20 seconds of effective validity. The successful cold diagnostic retained only about 3.6 seconds at the financial boundary.
- CompositionFixture retains its previous five-second integration-window default for other tests; the new window argument is opt-in.
- No already-calculated result or committed receipt is extended, and no production policy/default/expiry check changes.
- Each successful five-stage case first submits an explicitly expired authorization and asserts AuthorityRevoked / NotCommitted. It then appends the actual valid authorization at the same expected revision, proving the rejected attempt did not advance the stream. Existing receipt equality/reload assertions still run.

Final evidence in the same artifact directory:

| Report | Result |
| --- | --- |
| engine-five-stage-diagnostic.trx | 35/36; reproduced and proved pre-append expiration |
| engine-financial-final.trx | 45/46; intermediate composition-only adjustment still inherited the two-second assessment limit |
| engine-cold-bound-diagnostic.trx | 0/1; identified the upstream minimum |
| engine-cold-fixed.trx | 1/1; corrected cold case, including expiry rejection |
| engine-financial-qualified.trx | 46/46: 36 five-stage cases, nine routing guards, one risk persistence/replay case |
| engine-default-policy-regressions.trx | 74/74: composer edge/policy/calendar and default assessment calculation/selection-consumer regressions |

The 36 five-stage cases include Daily, Weekly and Monthly for all 12 variants. This certifies the explicitly configured synthetic correctness path, not the production two-second cold-start latency target. Production-timing performance acceptance remains necessary; this result must not be used to justify relaxing production freshness.

Post-run checks confirmed all three candidate indexes and financial_legacy_event_fence remain present. Failed synthetic cases and receipts were retained for diagnosis.

## Test-only implementation changes

- TradeDatabaseFixture now accepts explicit PostgreSQL, Scylla trade and Redis endpoints instead of always connecting to normal localhost ports.
- FiveStageFinancialBoundary uses the same explicit PostgreSQL endpoint.
- EventLogEngineQualification is opt-in through IFM_ENGINE_QUALIFICATION_RUN. It requires Test, a twelve-character run ID, and exact matching synthetic endpoints. Validation runs before fixture connections.
- The shared calculation-test host routes all six Scylla keyspaces, PostgreSQL, Redis and NATS to the retained owned fixture. Distributed caching is process-local; HTTP requests through IHttpClientFactory are rejected. The existing host already uses a synthetic historical provider and an in-memory broker emulator.
- Nine offline tests verify valid routing, unchanged non-opt-in behaviour and rejection of invalid qualification settings.
- FiveStageFinancialRuntimeTests now obtains the immutable regime snapshot through the real IRegimeDiscoveryMarketSignalSnapshotProvider and includes it in the command. Production command validation was not changed.

Run: 092020260032. PostgreSQL database: ifm_eventlog_bench_092020260032_synthetic_host on 127.0.0.1:25432. Scylla: 29042; Redis: 26379; NATS: 24223.

Post-run inspection still found exactly:

- ix_event_log_command_id
- ux_event_log_event_version
- ux_event_log_stream_version_v3

## Evidence

Artifact directory: BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/engine-qualification/

| Report | Result | Meaning |
| --- | --- | --- |
| engine-risk-replay.trx | 0/1 | Initial host configuration lacked the REST base URI; shutdown's disposed-provider exception masked the original startup error. |
| engine-risk-replay-2.trx | 1/1 | Actual risk actor persisted original and resized results, loaded state and replayed both. |
| engine-five-stage.trx | 0/36 | Existing fixture omitted the now-required initialized regime snapshot. |
| engine-five-stage-2.trx | 35/36 | Snapshot correction allowed all calculation stages; one Fund authorization failure remained. |
| engine-guards-and-risk-final.trx | 10/10 | Nine isolation guard cases plus the risk persistence/replay case, using final plumbing. |

Build completed with no errors and four generated MessagePack resolver type-conflict warnings.

The five-stage suite covers 12 variants across Daily, Weekly and Monthly. It passes actual result envelopes between Regime Discovery, Market Condition, Trade Selection, Order Composition and Risk Management. Successful cases also exercise synthetic funding, committed capacity reservation, Fund risk authorization and persisted receipt reload.

Limitations: market observations and portfolio authority are labelled synthetic fixtures; the Portfolio query dependency is substituted in this test host. This does not certify live feed ingestion, full Portfolio lifecycle, broker execution/fills, fill accounting, UI operation or process-restart recovery. Separate earlier lifecycle/restart reports remain applicable.

## Initial financial failure (resolved above)

Failed case: Daily / LongBullishIronCondor.

- Risk invocation: dead468c-42c5-4304-a4e8-28b4e281042f.
- Synthetic portfolio/fund: 130074680.
- Reservation granted: 2026-09-20T02:37:09.4227554Z.
- Reservation valid until: 2026-09-20T02:37:09.4739238Z.
- Only about 51 ms remained at grant.
- FundRiskAuthorizationStore.ValidateAsync rejected the later Fund append with: Fund financial authorization is no longer current.

The short remaining lifetime strongly points to expiration during the cold first-case handoff. The rejection combines identity, operating-state, epoch and time checks, so the exact failing predicate still needs explicit diagnostic evidence before calling the cause proven. The receipt was read directly from the isolated PostgreSQL store. No expiry or authority check was loosened and no production TTL was changed.

Original follow-up checklist (items 1-3 are now addressed by the resolution above):

1. Add test diagnostics at the Fund append boundary for UTC time, expiry and authority identity/state.
2. Separate synthetic fixture setup/JIT cost from the time-bounded execution path, while preserving the expiry safety checks.
3. Require the full 36-case gate to pass; retain an explicit expired-authorization rejection test.
4. Continue emulator execution/fills and accounting acceptance, then the isolated UI gate.

All synthetic rows, including failed-case receipts, remain available for diagnosis. Normal application stores were not targeted.

## Reproduction

Use the owned containers listed in the isolated acceptance report; verify their ownership labels and loopback ports before running. Do not run the entire integration assembly: unrelated tests still have their own routing conventions.

In a fresh PowerShell process, set DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT to Test; set the existing fixture-only POSTGRES_TEST_KEY and SCYLLADB_TEST_KEY credentials, then:

```powershell
$env:IFM_ENGINE_QUALIFICATION_RUN='092020260032'
$env:IFM_TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25432;Database=ifm_eventlog_bench_092020260032_synthetic_host'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION=$env:IFM_TEST_POSTGRES_CONNECTION
$env:IFM_TEST_TRADE_CONNECTION='Contact Points=127.0.0.1;Port=29042;Default Keyspace=ifm_synthetic_092020260032_trade'
$env:IFM_TEST_REDIS_URL='127.0.0.1:26379'
$env:IFM_FINANCIAL_TEST_NATS_URL='nats://127.0.0.1:24223'
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj -c Release --no-restore --filter "Category=PortfolioFinancialFiveStage" --blame-hang-timeout 90s --logger trx
```
