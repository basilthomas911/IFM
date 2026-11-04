# State repository qualification follow-up

Date: 2026-11-03

Status: Complete. All required qualification blockers are resolved. Full Trade and Portfolio integration suites pass; the existing externally orchestrated/live opt-in gates remain explicitly skipped.

## Corrections

- Migrated 93 legacy Trade SQL statements from named CQL parameters to PostgreSQL positional parameters in the actual C# binding order. Qualified tables, replaced CQL conditional inserts with explicit conflict behavior, restored upserts, corrected an invalid option-leg query and removed a nonexistent fill column. Added a dedicated update binding that preserves creation metadata. Six additional risk-history statements now use native SQL; exact invocation writes, duplicate/conflict checks and summary advancement share one transaction and cancellation token.
- Removed five duplicated domain query statements entirely. Option history, limits, position and quantity reads now use typed TradeDbContext methods. Non-tick Trade storage and opt-in fixture configuration use PostgreSQL; historical Scylla terminology was corrected in test names and diagnostics. ScyllaDB remains the tick-data provider.
- Standalone test processes receive a deployment manifest generated from the three actual copied assemblies. Production manifest validation remains enforced. API-only startup verification resolves the remote Market Data client; feed recovery workers belong to Market Data Host. Development, Production/DatabentoLive and Production/Synthetic API composition are valid without starting feeds.
- Strategy catalog product validation and symbol queries read one published Reference instrument-definition snapshot. Publication and Portfolio configuration no longer require a live Market Data NATS responder to validate durable product identities.
- Portfolio batch posting reuses period, rule and account evidence only within its existing financial authority transaction. Each line/balance update uses one SQL statement; available cash uses one statement; non-valuation postings avoid unnecessary valuation reads. Source identity and current cash checks remain per posting. Financial deadlines and reconciliation assertions are unchanged.
- Configured test hosts use one broker consistently, apply common lifecycle overrides and await application-owned actor/container cleanup before disposing DI dependencies. Disposal is idempotent. Trade fixtures with frozen inputs suppress the remote receiver because those hosts deliberately omit Feed actors. Runtime startup failures now fail the readiness signal even before actor registration, preserving the actual cause.
- Complete frozen decision requests use lossless LZ4 evidence encoding when beneficial. Existing MessagePack and JSON evidence remain readable and hash checked. All original request fields survive reconstruction. Composer/Risk ingress uses their declared 512 KiB stage budget; producer and transport budgets remain enforced. Fault injection for independent projectors uses a compatibility repository; native repositories commit source and projection atomically. A wall-clock pricing fixture now keeps synthetic expiry inside its trading date after the exchange rollover.
- Composer Function commands now derive a distinct deterministic identity from the accepted preparation, workflow and revision. Previously, acceptance and execution reused one ID for different command-audit payloads. Conflict checks remain strict. Function persistence/projection failures now record their actual exception and operational failure before returning a typed failed result.
- Risk preparation and financial handoff commands explicitly support resuming an identical audit reservation without committed source events. Changed payloads remain conflicts. Position actors acknowledge identical committed retries, while rejecting changed close evidence. Beginning a close advances the immutable trade evidence revision for both futures and options, preventing status changes from overwriting a prior history revision.
- Native PostgreSQL workflow projection spans now measure serialization, detail/history/timeline writes and active-state changes inside the actual transaction. They are registered with OpenTelemetry and tested in the initiating workflow trace. A cancellation test uses explicit processing/admission signals instead of racing 25 ms and 250 ms timers.

## Verification

Final results are recorded in [qualification-results.json](../../.artifacts/qualification-blockers-20261103/qualification-results.json), with the underlying build/test logs:

| Suite | Result |
| --- | --- |
| PostgreSQL legacy SQL catalog | All 99 statements prepare against the real schema |
| PostgreSQL Trade storage / manifest / wrapped-hosted-factory regressions | 20 passed |
| Reference unit | 272 passed |
| Application unit | 75 passed |
| Portfolio unit | 481 passed |
| Trade unit | 1,633 passed |
| Function lifecycle regressions | 36 passed |
| Full Shared unit suite | 340 passed |
| Telemetry unit | 9 passed |
| Portfolio wire verification | 79 passed |
| Full Portfolio integration | 208 passed, five existing opt-in skips |
| Full Trade integration | 163 passed, three existing opt-in skips |

The Trade calculation matrix exercises all 12 variants over Daily, Weekly and Monthly horizons. Compression tests prove full input reconstruction, legacy decoding and tamper detection. Ingress tests accept content above 64 KiB within the stage budget and reject content above 512 KiB.

The six relevant unit suites passed **2,810 tests** in total; the 36 Function lifecycle tests are included in the 340 Shared tests. Both full integration runs exited successfully, including teardown. All five complete workflow/authorization cases, the trace qualification, strict audit recovery, both partial-close restart directions and all legacy query routes pass. Changed-payload retries are rejected.

Five Portfolio skips require externally orchestrated live IDs or restart/rollback gates. Two Trade skips require live-feed credentials/evidence; one requires a dedicated disposable PostgreSQL position-audit database. These are not counted as passes, and no live-feed or externally orchestrated gate was enabled by this task. The final full Trade run took 7.18 minutes and Portfolio took 3.18 minutes; these are suite wall times, not application latency measurements.

## Before/after financial measurements

These are development workloads against isolated PostgreSQL on the same machine, not production SLAs. Before and after are separate runs; the after measurements below come from full qualification with other work active. Batch p99 is the maximum of five observations. Admission includes expected revision conflicts.

| Workload | Before p50 | After p50 | Before p99 | After p99 |
| --- | ---: | ---: | ---: | ---: |
| Single posting | 50.73 ms | 54.29 ms | 62.83 ms | 71.42 ms |
| Independent books | 60.25 ms | 59.54 ms | 96.02 ms | 71.55 ms |
| Shared admission | 177.11 ms | 74.38 ms | 206.00 ms | 101.55 ms |
| Batch of 100 | Cancelled at 2,000 ms deadline | 1,233.12 ms | No completed baseline sample | 1,498.07 ms |

All five measured 100-entry batches committed and reconciled, with no rollback retries and approximately 11.54 MB allocated per batch. A focused earlier after run measured 1,064.36 ms p50 and 1,232.40 ms maximum. No numerical speedup is claimed for a cancelled baseline, and single-posting performance did not improve in this run.

Raw logs, baseline/after JSON and the timing comparison are in [.artifacts/qualification-blockers-20261103](../../.artifacts/qualification-blockers-20261103). Creation replay measurements remain in the [original implementation results](State-Repository-Creation-Snapshot-Implementation-Results-2026-11-03.md).

Development services were not stopped or redeployed. Builds and test binaries use E: outputs. Only exact run-labelled disposable fixture containers are cleaned up. No historical event rewrite, commit or push is part of this follow-up.
