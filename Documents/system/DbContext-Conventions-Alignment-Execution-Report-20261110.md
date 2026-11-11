# DB context alignment execution report

Date: 2026-11-10. In progress; completion of all 13 contexts is not claimed.

## Coverage and evidence

The expanded audit inventories all 13 concrete contexts and applies 14 rule groups to each context and its supporting folders. Additional checks cover shared schema infrastructure, hidden Storage ownership violations and the auditor itself. Individual occurrences are retained; failed rule groups are not a count of unique defects. Functional tests remain necessary alongside source checks.

Baseline source and the pre-existing dirty workspace were preserved under `E:\IFM\DbContextConventions\20261110\baseline`. Baseline context source hashes are checked by the qualification launcher. Only the identical benchmark drivers are overlaid on frozen source; before production code is unchanged. Database fixtures are disposable and separate from Development.

## SequenceIdDb

Implementation and targeted correctness checks complete. Broader cross-context verification and final deployment remain pending.

- Added local combined/read/write contracts and capability properties returning the same repository instance; retained the shared sequence service contract and wired registrations/factory consumers consistently.
- Consolidated the provider-neutral binding into SequenceIdDbParameters; removed the confirmed unused non-tick CQL query.
- Centralized sequence schema/check SQL and moved shared schema technical helpers into C# 14 receiver blocks. Sequence allocation and transaction semantics are unchanged.
- Added concurrent allocation/read-purity and generator-restart BDD coverage. No Development sequence was allocated by these fixtures.

Tests: 2 unit, 2 actual-PostgreSQL BDD scenarios and 39 scoped integration/convention checks passed (the 39 include 14 Sequence rule groups, 4 shared-schema checks and 21 existing sequence checks). No skips.

### Before/after measurements

BenchmarkDotNet 0.15.8, fresh Release host per arm, PostgreSQL 17.2 in a dedicated loopback container with full WAL durability, identical 2-CPU/2-GiB limits. Five alternating pairs; eight measured iterations after warmup. Pooled and physical-new-connection cases use the same complete operations in both arms. One concurrent operation is an eight-allocation group. Values below are medians across five run means, with median allocated bytes per operation. These measurements do not establish a structural speedup.

| Operation | Connection | Before ms | After ms | Ratio | Before / after allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| ReadAllocationSize | Pooled | 1.531 | 1.490 | 0.973 | 14851 / 14792 |
| ReadCurrent | Pooled | 1.637 | 1.543 | 0.942 | 15061 / 15022 |
| AllocateBlock | Pooled | 1.534 | 1.831 | 1.194 | 15025 / 15025 |
| AllocateEightConcurrentBlocks | Pooled | 3.362 | 3.319 | 0.987 | 118966 / 118967 |
| ReadAllocationSize | New physical | 14.621 | 14.295 | 0.978 | 95030 / 95042 |
| ReadCurrent | New physical | 13.173 | 13.837 | 1.050 | 95046 / 95046 |
| AllocateBlock | New physical | 13.485 | 12.979 | 0.962 | 95046 / 94986 |
| AllocateEightConcurrentBlocks | New physical | 39.843 | 39.520 | 0.992 | 759824 / 759852 |

The short-run pooled AllocateBlock result exceeded the 10% investigation threshold. SQL, typed bindings and operation scope were compared with the frozen baseline, then the same complete operation was measured in five additional alternating warm pairs using 6 warmups, 20 measured iterations and 128 invocations. Median run means were 1.424 ms before and 1.410 ms after (0.990 ratio); median allocated bytes were 13,993 / 14,003. The longer runs did not reproduce the slowdown. Both datasets remain in the evidence; the short result has not been hidden.

Managed Monitor contention was not observed in these BDN runs. GC observations and iteration distributions remain in the raw JSON. Cold eight-client cases did collect Gen0/occasionally Gen1; this is not a zero-GC claim. PostgreSQL lock-wait attribution, sustained-tail p95/p99, CPU and working-set qualification are not established by these short measurements and remain part of broader profiling.

The initial noisy pair and a later pair overlapped by a benchmark-project build were retained but excluded from the five controlled pairs. The overlapping build was rejected by Windows file locking and repeated after the benchmark host exited. Controlled pair labels are repeat-1, repeat-2, repeat-3, repeat-4 and repeat-6.

Raw results: `E:\IFM\DbContextConventions\20261110\timings\{Before,After}\SequenceIdDb`. Paired summary: `timings\SequenceIdDb-paired-summary.json`.

## SystemAdminDb

Implementation and targeted correctness/performance comparisons complete; final all-context deployment remains pending.

- Added read/write capability properties to the combined interface, returning the same repository instance.
- Removed the delegated request validator and moved responsibility to the existing domain query handlers and authoritative event-to-projection preparation. Added a null-query boundary test for every database-backup query handler, proving invalid inputs do not reach persistence.
- Added complete parameter/return documentation and named static receipt/checkpoint mappers. Kept transaction-resource validation, hash-conflict rejection, receipts, checkpoints and SQL revision guards in Storage.
- Added actual PostgreSQL BDD scenarios for restart/idempotent retry and conflicting receipt payloads.

Checks passed: all 14 expanded context rule groups; 30 Storage integration/convention checks; the Domain.SystemAdmin unit suite (453 before the subsequent 4 scheduled-query cases, 457 with them); 144 domain BDD scenarios; 2 additional PostgreSQL Storage BDD scenarios; and 6 domain integration tests. No skips. Shared Framework.Storage unit suite: 403 passed. The strengthened first-three-context regression run passed 108 checks. API and managed Rust Host Debug builds both completed with zero warnings and zero errors.

Five complete alternating paired BDN runs used the same isolated PostgreSQL, durability and warm/cold configuration as SequenceIdDb. The projection commit includes the operation/phase rows, durable receipt and checkpoint; the retry verifies the existing receipt before returning. Values are medians across run means and median bytes/op.

| Operation | Connection | Before ms | After ms | Ratio | Before / after allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| ReadProtectionSets | Pooled | 1.485 | 1.583 | 1.066 | 15432 / 15405 |
| ReadOperation | Pooled | 1.377 | 1.323 | 0.961 | 16121 / 16108 |
| ReadCheckpoint | Pooled | 1.342 | 1.192 | 0.888 | 15156 / 15024 |
| ApplyNewRevision | Pooled | 11.654 | 10.726 | 0.920 | 36733 / 36671 |
| RetryAppliedRevision | Pooled | 6.977 | 6.725 | 0.964 | 14834 / 14912 |
| ReadProtectionSets | New physical | 12.624 | 12.685 | 1.005 | 96293 / 96296 |
| ReadOperation | New physical | 12.347 | 13.408 | 1.086 | 105644 / 105710 |
| ReadCheckpoint | New physical | 11.723 | 12.464 | 1.063 | 96628 / 96628 |
| ApplyNewRevision | New physical | 26.381 | 25.589 | 0.970 | 119828 / 119822 |
| RetryAppliedRevision | New physical | 17.490 | 18.989 | 1.086 | 95075 / 95176 |

The measurements vary in both directions. None exceeded the plan's 10% regression trigger; no general performance gain is claimed. Full distributions, allocations, GC and managed contention observations are retained in the raw BDN JSON. These short runs do not establish database lock-wait attribution or sustained p95/p99.

Final qualified labels: final-1 through final-5. Earlier partial runs are retained but are not mixed into the comparison. The disposable PostgreSQL readiness probe was corrected to require its TCP listener rather than its temporary initialization socket. No Development database was changed.

Raw results: `E:\IFM\DbContextConventions\20261110\timings\{Before,After}\SystemAdminDb`.

## ScheduledTaskDb

Implementation and targeted correctness/performance comparisons complete; final all-context deployment remains pending.

- Added matching read/write capability properties to the concrete context and combined contract.
- Moved client continuation validation, scope hashing, page-size preparation and successful-close interpretation into domain queries over projected run rows. The database accepts a typed selection and performs only the ordered read, revision-fenced writes and transaction handling.
- Retained the original opaque continuation layout, complete task/catalog/run snapshots and the rule that incomplete or failed closes cannot advance the authoritative completed-close date. No new completion table, event rewrite or Development data migration was introduced.
- Required projection payload checks now live in the domain projectors. Transaction-resource guards remain in Storage. Inherited persistence contracts have complete parameter and return documentation.

Verification: all 14 specific convention groups and existing PostgreSQL/snapshot cases pass in the 108-check strengthened first-three-context run. Domain.SystemAdmin: 457 unit tests, including 4 new projection-query boundary cases, and 144 BDD scenarios passed. API session-restoration tests: 4 passed. Framework.Storage: 403 unit tests passed. The API and managed Rust Host compiled in Debug with zero warnings/errors at this slice.

Five alternating pairs completed all 16 workloads per arm. Task adapters ensure the runner waits for every operation, including non-generic ValueTask writes. Values below are medians across run means; eight concurrent revisions are one complete reported group. The close-date query crosses two history pages, including domain validation and continuation preparation.

| Operation | Connection | Before ms | After ms | Ratio | Before / after allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| ReadDefinition | Pooled | 1.602 | 1.374 | 0.858 | 16272 / 16224 |
| ReadCatalog | Pooled | 1.296 | 1.260 | 0.972 | 15560 / 15668 |
| ReadRun | Pooled | 1.478 | 1.363 | 0.922 | 16842 / 16924 |
| ReadCompletedClose | Pooled | 2.839 | 2.960 | 1.043 | 46864 / 46944 |
| ReadHistoryPage | Pooled | 1.591 | 1.564 | 0.983 | 28410 / 28436 |
| UpdateRun | Pooled | 9.020 | 9.127 | 1.012 | 20668 / 20816 |
| RetryRun | Pooled | 8.653 | 8.807 | 1.018 | 20352 / 20322 |
| UpdateEightConcurrentRevisions | Pooled | 50.426 | 49.746 | 0.987 | 151465 / 151469 |
| ReadDefinition | New physical | 12.233 | 11.773 | 0.962 | 96560 / 96538 |
| ReadCatalog | New physical | 12.064 | 11.589 | 0.961 | 95754 / 95648 |
| ReadRun | New physical | 12.731 | 12.540 | 0.985 | 97074 / 97056 |
| ReadCompletedClose | New physical | 26.478 | 25.918 | 0.979 | 206944 / 207026 |
| ReadHistoryPage | New physical | 12.671 | 13.231 | 1.044 | 108004 / 108060 |
| UpdateRun | New physical | 20.286 | 19.105 | 0.942 | 100518 / 100554 |
| RetryRun | New physical | 18.630 | 18.959 | 1.018 | 100294 / 100252 |
| UpdateEightConcurrentRevisions | New physical | 64.334 | 61.794 | 0.961 | 792285 / 792478 |

No case exceeded the 10% regression trigger. The measurements vary in both directions and do not establish a general structural speedup. Raw allocation, GC and managed contention observations are retained. Database lock-wait attribution and sustained tail percentiles are not established by these short runs.

Qualified labels are awaited-1 through awaited-5 under `timings/{Before,After}/ScheduledTaskDb`. The earlier failed final-1 run is retained and excluded: two non-generic ValueTask workloads did not wait for I/O completion under this runner and exhausted fixture connections. Changing all adapters to Task made the complete comparisons finish; no production scheduler timeout is inferred from that harness failure.

## SecuritiesDb

- Consolidated the provider-neutral bindings, retired the unused Securities CQL catalog and its unreachable provider branches, and centralized PostgreSQL statements and static mappers. The context exposes separate read/write contracts; it no longer owns the application rollover service.
- Moved historical baseline capture, rebuild and rollover decisions into the Securities owner. The rollover command is fenced by the catalog revision; the event and canonical/symbol/pointer rows commit together. Existing source event identities remain replayable.
- Moved immutable reference and option-definition caches to domain readers. Each options read resolves the committed generation before accessing its immutable partitions. Publication preparation supplies the generation, coverage and timestamp; Storage commits the prepared rows and pointer. Failed publication leaves the previous generation readable.
- Native and compatibility market-data hosts prepare new futures definitions through the catalog command actor. Refreshing definitions cannot retire a currently assigned rollover contract. Maintenance copies canonical symbol rows under the existing PostgreSQL table locks after the domain owner qualifies the request.

Verified during implementation: 51 focused Securities checks passed. The actual NATS actor test passed, including a committed replacement, an identical retry without a second event, and rejection of a stale preparation. The owning BDD suite passed 3 scenarios. The broader unit fixture originally still selected Scylla; that and the new rollover projector inventory were updated. The full integration suite initially passed 16/17 and exposed concurrent cold-start creation of the same PostgreSQL namespace. The startup prerequisite fix was applied; the complete rerun passed all 17 integration tests, all 31 unit tests and all 3 BDD scenarios.

Five valid alternating BenchmarkDotNet pairs compare the same fixture (128 option definitions across four expiry partitions), full WAL durability and the frozen pre-change source. Before and after workloads include the generation lookup and the owning reader/publisher. The after arm includes the split cache and publication paths, with no timing claim for other Securities operations.

| Operation | Connection | Before ms | After ms | Ratio | Before / after allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| Read cached partition | Pooled | 1.182 | 1.196 | 1.011 | 17,630 / 17,651 |
| Stream cached range | Pooled | 3.034 | 3.327 | 1.097 | 92,450 / 92,458 |
| Publish generation | Pooled | 126.044 | 127.847 | 1.014 | 559,074 / 569,103 |
| Read cached partition | New physical | 11.461 | 11.651 | 1.017 | 99,332 / 99,286 |
| Stream cached range | New physical | 23.605 | 22.944 | 0.972 | 250,458 / 250,374 |
| Publish generation | New physical | 138.012 | 140.273 | 1.016 | 639,135 / 649,058 |

Labels: definition-1, -2, -3, -4, -6. The fifth run overlapped a build and is retained but excluded. The pooled range was 9.7% slower in the paired medians; none exceeded the 10% investigation trigger. The measurements do not establish a general speed improvement. Raw GC/managed contention evidence and the paired summary are in the E: timing archive. Exact pricing-reference and full actor rollover measurements remain open.

## MarketDataServiceDb measured recovery paths

The recovery owner now prepares the typed audit row. Storage owns positional C# binding, SQL, schema and persistence; ordinary reads and writes issue no DDL. API startup explicitly creates the additive recovery-log schema. Immutable digests, sequence predecessors and the original 120-second command budget remain intact. Actual PostgreSQL tests cover ordered paging, identical retries, conflicting facts and cold schema behavior. The isolated Feed recovery suite passed **8/8** after this split.

Five alternating BenchmarkDotNet pairs use the frozen baseline and disposable fully durable PostgreSQL. The recovery workload includes owner-side row preparation on the candidate arm. No build or test load overlapped these pairs.

| Operation | Connection | Before ms | After ms | Ratio | Before / after allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| ReadRecovery | Pooled | 1.332 | 1.338 | 1.005 | 20683 / 20106 |
| AppendExistingRecovery | Pooled | 6.281 | 6.112 | 0.973 | 21704 / 19724 |
| AppendNewRecovery | Pooled | 6.165 | 6.066 | 0.984 | 24376 / 22469 |
| ReplaceVxPair | Pooled | 10.081 | 9.617 | 0.954 | 56980 / 57116 |
| ReadRecovery | New physical | 12.373 | 12.086 | 0.977 | 147510 / 145658 |
| AppendExistingRecovery | New physical | 15.211 | 15.491 | 1.018 | 103244 / 98690 |
| AppendNewRecovery | New physical | 15.326 | 15.309 | 0.999 | 106694 / 102096 |
| ReplaceVxPair | New physical | 45.018 | 41.464 | 0.921 | 315158 / 315112 |

No case crossed the 10% regression threshold. These short measurements do not establish a broad speedup or sustained tail percentiles. GC and managed threading diagnostics remain in the raw exports. Durable intent publication and contended VX replacement remain outside this timing slice.

## PortfolioDb

Domain.Portfolio chooses which accepted Fund mandate versions to disable. Storage retains the advisory lock, locked exact-version reads, compare-and-set writes and source-event fence. Strategy recovery now uses a typed Storage query for the latest committed workflow source tails; Domain.Trade owns recovery qualification and reports invalid latest facts without replaying an older snapshot. Statement/schema catalogs and the shared query result types have been aligned.

The isolated Portfolio integration run passed **233/233 executed tests**. Five explicitly live-host cases were skipped. The focused PostgreSQL checks cover old and new Fund revisions and rollback, and the recovery cases exercise restart, out-of-allocation-order commits and invalid latest facts. Portfolio unit/BDD reruns and the final audit are still pending.

Five alternating BenchmarkDotNet pairs compare the frozen baseline and the candidate on fully durable isolated PostgreSQL. The accepted-mandate operation includes the owner, advisory lock, row reads and committed write in each arm. The fixture changes the source revision of one mandate. It does not measure large multi-version rebuilds or financial operations.

| Operation | Connection | Before ms | After ms | Ratio | Before / after allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| ReadFund | Pooled | 1.153 | 0.982 | 0.851 | 17883 / 17902 |
| ReadActiveFunds | Pooled | 1.228 | 1.211 | 0.986 | 19412 / 19371 |
| PersistAcceptedMandate | Pooled | 10.415 | 10.238 | 0.983 | 31935 / 31915 |
| ReadFund | New physical | 12.072 | 12.435 | 1.030 | 98183 / 98100 |
| ReadActiveFunds | New physical | 12.137 | 11.791 | 0.972 | 99420 / 99504 |
| PersistAcceptedMandate | New physical | 20.263 | 20.451 | 1.009 | 110378 / 110298 |

No case was more than 10% slower in the paired medians. Raw allocations, GC and threading measurements are retained with mandate-1 through mandate-5.

## Cross-context verification and open work

The focused historical rerun passed **12/12**. The historical diagnostics query reads only projection rows; each checkpoint and prepared progress write share one PostgreSQL command. API startup backfills older checkpoints in bounded pages and never overwrites newer progress. The integration case verifies no command-state fallback, rollback on an invalid manifest, immutable manifest retention and terminal-event precedence. The strategy catalog baseline event and dependency-ordered rebuild now run in Domain.Reference. Its full 32-test integration rerun passed.

The broader cross-context run passed **292 of 297**. Two failures used an invalid non-PostgreSQL validation fixture. Three exercised old chain-layout and documentation regex checks. The fixture and identified layout/documentation issues were corrected after that run; qualification still requires a rerun.

The 13-context inventory exercises 182 per-context rule groups. Additional tests cover shared schema, audit false positives and the repository-wide ownership boundary. The first complete candidate audit passed 145 of 196 tests. The 22:22 rerun passed **166 of 197** after consolidating SQL, splitting query contracts and improving the mapper audit; 30 context rule groups and one repository-wide ownership check remained open. The individual findings are in the timing archive's expanded-qualified-structure.json. This is a checkpoint before the latest mapper, layout and Portfolio recovery changes; it is not the final audit.

A subsequent focused rerun passed **92 of 92** tests. The run included 51 Securities cases, Portfolio accepted-projection and wire coverage, repeatable-read Trade Plan history and cache checks, the configuration baseline, PostgreSQL recovery log, event-source checkpoint and raw Scylla reads/binding ownership. The earlier failure expecting 22 rather than 23 Portfolio event types was corrected to require the new Fund Base Contracts event and round-trip its projection facts.

| Context | Changes made and verified evidence | Qualification still open |
| --- | --- | --- |
| ConfigurationDb | Baseline fact creation and validation moved to application persistence; technical table copy, schema allowlist and receipts remain in Storage. PostgreSQL baseline capture and rebuild passed. | Catalog graph and external validation under transaction, remaining bindings/docs/SQL, full suite and timings. |
| EventSourceDb | Combined lifecycle contract, shared checkpoint result models and static mappings; centralized exact SQL statements. PostgreSQL checkpoint case passed. | Full snapshot/concurrency/alias suites, remaining request validation/docs and timings with stream resolution. |
| MarketDataDb | Separated seed/statistics/evidence capabilities, consolidated the seed SQL catalog, and retired unreachable CQL paths for derived data. Raw Scylla ownership remains in TradeTickQuoteDb. | PostgreSQL historical persistence, progress backfill and reconciliation passed in the 12-test focused rerun. Composition ownership, remaining validation and full timing comparisons are open. |
| MarketDataServiceDb | Recovery projector supplies a typed audit row. Positional C# binding, one SQL/schema catalog, original 120-second command budget and idempotent digest/sequence fences retained. Added actual PostgreSQL ordered paging and conflicting evidence case. | Isolated recovery passed 8/8; recovery and VX timing pairs are above. Watchdog sequence and durable intent preparation and timings remain open. |
| PortfolioDb | Domain owner chooses disabled/active Fund projection versions while preserving original advisory lock, row locks and source-event fencing. PostgreSQL accepted projection tests and 23-event wire inventory passed. | 233 isolated integration passes; five paired Fund timing results above. Unit/BDD reruns and live-host scenarios remain. |
| ReferenceDb | Domain publisher owns instrument catalog event creation, duplicate/stale checks and atomic pointer publication. Typed selection DTO replaces public tuple. | Reference unit 282/282 and integration 32/32 (including the catalog source-event/rebuild rerun). Caller validation and timings remain open. |
| ScheduledTaskDb | Qualified targeted changes, tests and five timing pairs as recorded above. | Final cross-context and deployment verification. |
| SecuritiesDb | See the measured option definition paths and verified tests above. | 31 unit, 3 BDD and 17 integration passed; caller validation and exact reference and actor rollover timings remain open. |
| SequenceIdDb | Qualified targeted changes, tests and paired timings as recorded above. | Final cross-context and deployment verification. |
| SystemAdminDb | Qualified targeted changes, tests and paired timings as recorded above. | Final cross-context and deployment verification. |
| TradeDb | Direct class contract aligned; handoff SQL/DDL consolidated and the domain uses typed enlisted persistence. | Full handoff and financial/emulator suites; remaining helper/validation/docs and timings. |
| TradePlanDb | Domain owns chart caching. Page, fixed revision and optional chart share the same repeatable PostgreSQL snapshot and existing sampling rule. PostgreSQL history and cache cases passed. | All 14 source rules passed and focused PostgreSQL rerun passed 59/59 across Trade Plan, Portfolio and composition. Paired timings pending. |
| TradeTickQuoteDb | Streaming exposed through the read capability, raw result moved to shared Feed contracts, named mappers and a one-shot Scylla resource owner behind the readonly parameter model. Actual Scylla streaming and ownership tests passed. | Caller validation, broader suites and measured raw write/streaming comparisons. |

The deployed Development readiness endpoint still reported Healthy on the read-only check. This candidate has not been installed. Manual lifecycle testing remains paused and automatic openings remain disabled. No orders were submitted.


## Subsequent qualification

The Portfolio regression suites passed **538 unit** and **42 BDD** tests. Its integration run passed **233**, with five opt-in live-host cases skipped. Feed passed **618 unit** tests after moving raw-tick request validation to the application boundary. Trade passed **1,751 unit** tests after the strategy catalog projection split. The isolated strategy catalog suite passed **23 PostgreSQL** cases, including an external qualification check using a separate database connection and a stale publication proof rejection. Reference passed **32 integration** tests after that catalog change.

The last completed source audit passed **183 of 197** cases, with 14 failing rule groups, including the repository-wide ownership check. The machine-readable report lists each occurrence separately. This count is for that source revision and includes neither later edits nor functional test outcomes.

### TradePlanDb paired timings

The full source audit for TradePlanDb passed **14/14** rule groups at that checkpoint. The focused behavioral suite passed **59/59** cases, covering PostgreSQL repeatable reads, revision fences, rollbacks and cache behavior. Both versions measure the same 120-revision, 20-row workloads.

| Workload | Connection | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadLatestWithChart | Pooled | 5.711 | 5.389 | 143987 / 145037 |
| ReadAnchoredPage | Pooled | 5.221 | 5.478 | 143974 / 144924 |
| ReadLatestWithChart | New physical | 14.722 | 15.429 | 222224 / 223443 |
| ReadAnchoredPage | New physical | 13.706 | 13.967 | 222036 / 222943 |

Each row is the median of five alternating run means. The logs and BenchmarkDotNet GC and threading exports are retained alongside the raw results.


### Inventory benchmark preflight

The table records the first complete before/after run for each listed workload. Each cell is one BenchmarkDotNet run mean in milliseconds; these are **preflight results**, not the five-pair medians. Both versions use matching fixtures. EventSource includes stream resolution on every operation; Reference is the direct database adapter workload; Trade uses a synthetic handoff and does not place an order. Raw trades/quotes are measured in a dedicated Scylla keyspace.

| Context | Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | --- | ---: | ---: | ---: |
| EventSourceDb | ReadVersionWithResolution | Pooled | 2.189 | 1.968 | 28867 / 28906 |
| EventSourceDb | ReadSnapshotWithResolution | Pooled | 6.287 | 4.295 | 86152 / 85534 |
| EventSourceDb | ReadVersionWithResolution | New physical | 25.564 | 23.127 | 190594 / 190534 |
| EventSourceDb | ReadSnapshotWithResolution | New physical | 57.682 | 48.720 | 414988 / 415000 |
| ReferenceDb | ReadExactDefinition | Pooled | 1.082 | 1.190 | 16424 / 16462 |
| ReferenceDb | WriteIdempotentBatch | Pooled | 6.865 | 6.974 | 129009 / 129102 |
| ReferenceDb | ReadExactDefinition | New physical | 12.695 | 11.552 | 96772 / 96802 |
| ReferenceDb | WriteIdempotentBatch | New physical | 16.426 | 16.915 | 218632 / 218632 |
| ConfigurationDb | ReadPublishedFamily | Pooled | 8.731 | 8.328 | 107538 / 109101 |
| ConfigurationDb | PublishNewFamily | Pooled | 43.359 | 48.348 | 187399 / 285856 |
| ConfigurationDb | ReadPublishedFamily | New physical | 22.168 | 21.079 | 189078 / 190434 |
| ConfigurationDb | PublishNewFamily | New physical | 67.891 | 91.103 | 350086 / 530863 |
| TradeDb | ReadHandoff | Pooled | 3.656 | 3.447 | 18454 / 18474 |
| TradeDb | PersistRevision | Pooled | 8.294 | 7.819 | 18520 / 18436 |
| TradeDb | ReadHandoff | New physical | 12.478 | 13.409 | 97655 / 97686 |
| TradeDb | PersistRevision | New physical | 16.136 | 16.368 | 97276 / 97410 |
| TradeTickQuoteDb | WriteQuoteSegment | RowCount=64 | 1.511 | 1.531 | 22384 / 22384 |
| TradeTickQuoteDb | ReadTradeWindow | RowCount=64 | 1.900 | 2.196 | 69541 / 69685 |
| TradeTickQuoteDb | WriteQuoteSegment | RowCount=512 | 2.855 | 3.119 | 22822 / 22946 |
| TradeTickQuoteDb | ReadTradeWindow | RowCount=512 | 6.098 | 6.755 | 407336 / 406948 |

The Configuration publication preflight added a qualification read before the fenced write; external validation no longer runs under the publication locks. The extra database interaction is visible especially on new physical connections. The MarketData driver initially failed to construct a context in both versions; its invalid factory setup is corrected in the driver and the failed run supplies no measurement.

The next Storage run passed **215 of 224** checks (no skips). The nine failures were convention checks; the focused MarketDataService PostgreSQL behavior tests all passed. Its watchdog owner allocates a sequence only for new observations, and its durable intent owner qualifies requests before the database transaction. Scope, existing receipt, revision, immutable lease and outbox fences remain in persistence. The expanded audit at that checkpoint passed **189 of 197** cases; the subsequent code has not yet been fully qualified.


### MarketDataDb paired timings

The benchmark measures the same committed historical checkpoint and diagnostics projection. The candidate reads the explicit progress row instead of reconstructing it from checkpoint data. In the measured write, checkpoint and projected progress commit together.

| Workload | Connection | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadProgress | Pooled | 2.388 | 1.025 | 29630 / 15706 |
| AdvanceProgress | Pooled | 5.836 | 5.910 | 19112 / 20232 |
| ReadProgress | New physical | 23.941 | 12.024 | 195455 / 95944 |
| AdvanceProgress | New physical | 15.659 | 17.186 | 98640 / 99768 |

These are the medians of five run means. The new-connection write was 9.7% slower, while the pooled write was 1.3% slower. The new-connection read used one connection instead of two when the progress row exists.

The subsequent focused run passed **56 PostgreSQL/Scylla binding, transaction and MarketDataService checks**, **52 Download Log unit tests**, **6 durable delivery unit tests**, **282 Reference unit tests**, and **1,751 Trade unit tests**. The broad Storage run reached 719 passes before the runner aborted the dense 1.2-million-event timing case at its per-test deadline. Its reported failures included the old binding and layout checks later corrected; the run was not a clean pass. The focused convention/snapshot run passed 291 of 296. Four validation rule groups and an ordinal binder mapping remained in that run; the mapping passed in the subsequent 56-case run.


### ReferenceDb paired timings

Median of five alternating BenchmarkDotNet run means.

| Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadExactDefinition | Pooled | 1.082 | 1.190 | 16462 / 16462 |
| WriteIdempotentBatch | Pooled | 7.032 | 6.982 | 129009 / 129102 |
| ReadExactDefinition | New physical | 12.204 | 11.551 | 96744 / 96738 |
| WriteIdempotentBatch | New physical | 16.746 | 16.915 | 218632 / 218614 |


### TradeDb paired timings

Median of five alternating BenchmarkDotNet run means.

| Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadHandoff | Pooled | 3.305 | 2.989 | 18454 / 18435 |
| PersistRevision | Pooled | 7.886 | 7.758 | 18492 / 18436 |
| ReadHandoff | New physical | 12.711 | 13.375 | 97639 / 97640 |
| PersistRevision | New physical | 16.517 | 16.376 | 97421 / 97410 |


### TradeTickQuoteDb paired timings

Median of five alternating BenchmarkDotNet run means.

| Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| WriteQuoteSegment | RowCount=64 | 1.498 | 1.553 | 22384 / 22384 |
| ReadTradeWindow | RowCount=64 | 1.774 | 2.196 | 69664 / 69648 |
| WriteQuoteSegment | RowCount=512 | 2.839 | 2.847 | 22882 / 22952 |
| ReadTradeWindow | RowCount=512 | 6.281 | 6.528 | 406962 / 406910 |


### EventSourceDb paired timings

Median of five BenchmarkDotNet run means on a 512-event stream. Both versions resolve the stream and return the same 24-event snapshot tail.

| Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadVersionWithResolution | Pooled | 2.189 | 2.079 | 28867 / 28955 |
| ReadSnapshotWithResolution | Pooled | 5.156 | 4.651 | 86180 / 86172 |
| ReadVersionWithResolution | New physical | 24.232 | 24.388 | 190554 / 190594 |
| ReadSnapshotWithResolution | New physical | 50.361 | 49.464 | 414988 / 414945 |


## Context qualification ledger

Behavior results below are the latest completed targeted checkpoints for each changed path. The audit result is reported separately because a passing PostgreSQL scenario does not establish that every method follows the source convention. Timings use the frozen pre-refactor tree and an isolated provider. The candidate has not been installed in Development.

| Context | Changes and checked behavior | Representative median before → after |
| --- | --- | --- |
| SequenceIdDb | Combined capability and provider-neutral named sequence binding. PostgreSQL allocation and existing shared identity adapter verified, including concurrent allocation and retries. | Pooled single, longer run: 1.424 → 1.410 ms |
| SystemAdminDb | Domain prepares accepted backup/recovery facts. PostgreSQL receipt, checkpoint, atomic commit and retry remain fenced. SystemAdmin unit, BDD and isolated provider checks passed for this slice. | Pooled checkpoint: 1.342 → 1.192 ms |
| ScheduledTaskDb | Domain qualifies history scope and completed close semantics. PostgreSQL latest task/catalog/run snapshots, revision fencing and paging verified. | Pooled definition: 1.602 → 1.374 ms |
| PortfolioDb | Domain chooses Fund lifecycle versions and recovery order. PostgreSQL retains advisory/row locks and exact receipts. 538 unit, 42 BDD and 233 isolated integration passes; five opt-in live-host cases were skipped. | Pooled Fund read: 1.153 → 0.982 ms |
| TradePlanDb | Domain owns chart cache and material/disposable write policy. Revision-bound history and chart share one repeatable-read snapshot. 59 focused cases passed. | Pooled first page and chart: 5.711 → 5.389 ms |
| SecuritiesDb | Typed option definition generations, actor rollover ownership and reviewed identity handling. Earlier 31 unit, 3 BDD and 17 integration passed; the latest prepared-reference and projection split requires another provider run. | Pooled generation publication: 126.044 → 127.847 ms |
| MarketDataServiceDb | Feed supplies recovery fact rows; watchdog owner controls idempotent identifier allocation and durable intent owner supplies qualified commands. PostgreSQL rollback, receipts, outbox and watchdog retry passed in the focused provider run. | Pooled recovery log read: 1.332 → 1.338 ms |
| MarketDataDb | PostgreSQL owns historical projection/checkpoint rows; caller prepares progress and reconciles imported facts. Recent Download Log ownership passed 52 unit cases. New seed and evidence admission boundaries await the next focused run. | Pooled historical progress read: 2.388 → 1.025 ms |
| EventSourceDb | Centralized SQL and typed bindings include origin. The owner prepares audit evidence and decides audit-only vs event append, while PostgreSQL enforces duplicate and commit outcomes. Snapshot and ordinal checks passed on the focused builds. | Pooled snapshot with stream resolution: 5.156 → 4.651 ms |
| ReferenceDb | Caller owns legacy ID allocation and catalog event/pointer publication. Typed selection and batch bounds are enforced before Storage. 282 unit and 32 integration passes across the checked slices. | Pooled exact definition read: 1.082 → 1.190 ms |
| ConfigurationDb | Accepted graph facts and publication proof are separate from external validation; stale proof and concurrent write fences are enforced. PostgreSQL catalog qualification 23/23; the new five-pair results are recorded separately. | First cold publication pair: 67.891 → 91.103 ms |
| TradeDb | Typed enlisted handoff and SQL catalog preserve exact revisions. Caller prepares workflow history and scope. 1,751 unit tests passed before the latest history-scope changes. | Pooled handoff read: 3.305 → 2.989 ms |
| TradeTickQuoteDb | Raw-only Scylla CQL, typed Feed result and one-shot quote ownership. Exact 21-column order and provider round trips verified. Longer raw read measurements are needed for the 64-row result. | 512-row quote segment: 2.839 → 2.847 ms |


### ConfigurationDb paired timings

Median of five BenchmarkDotNet run means. Publication includes inserting a one-node draft and publishing it. The new path reads the exact graph before external validation and rechecks the fingerprint under the write fence.

| Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadPublishedFamily | Pooled | 8.731 | 8.899 | 107450 / 109006 |
| PublishNewFamily | Pooled | 39.588 | 47.570 | 187399 / 285856 |
| ReadPublishedFamily | New physical | 22.168 | 21.079 | 189078 / 190434 |
| PublishNewFamily | New physical | 67.891 | 91.226 | 350164 / 530863 |

The publication overhead is concentrated in the extra qualification read and new physical connection. Moving external validation out of the publication locks was verified by a separate-connection PostgreSQL check. These measurements cover the publication helper; they do not include source event append or model projection in the catalog actor.


### Latest focused verification checkpoint

The candidate built successfully. The focused run passed **248 of 252** checks with no skips. It included the new caller-prepared Securities version and projection writes, identity collision and rollback, option evidence and seed cache qualification, raw Scylla ordinal binding, trade atomic audit, and durable intent persistence. All failures in this run were source validation rule groups. The audit now recognizes named validation helpers as well as direct throws; it distinguishes the enlisted SQL executor's technical binding guards.

| Context | Failing rule | Individual findings |
| --- | --- | ---: |
| EventSourceDb | Validation | 55 |
| MarketDataDb | Validation | 114 |
| SecuritiesDb | Validation | 33 |
| TradeDb | Validation | 112 |

The remaining groups include checks for raw persistence requests and public compatibility methods. They require moving actual request qualification to the owning callers or documenting exact storage integrity exceptions; passing provider behavior tests does not remove those findings. The exhaustive per-file and per-line inventory is in expanded-next.json. Development cutover remains gated on qualification.

### Ownership and provider qualification follow-up

The audit now checks sequence allocation outside the dedicated sequence context, including supporting files. It also recognizes catalog paging validation and distinguishes PostgreSQL connection configuration and executor arguments from caller requests. The intermediate 191/199 run surfaced seven context rule failures plus a project-wide ownership failure that repeated the sequence findings. Those eight test failures were not eight individual violations.

| Context | Follow-up change | Completed verification at this checkpoint |
| --- | --- | --- |
| ConfigurationDb | First read uses committed facts without acquiring row locks; the locked publication proof and fingerprint comparison still fence the write. | 25 PostgreSQL checks passed, including reading while another transaction holds a row lock. The five-pair publication table above predates this last read change. |
| ReferenceDb | Seed Allocation Function now calls the original sequence generator directly. Legacy job preparation receives the generator from its caller; the context only exposes the high-water read. | 282 unit, 2 BDD, 32 integration on the domain suite and 6 focused API integration checks; Reference ownership findings cleared. |
| SecuritiesDb | Typed immutable reference staging and projection preserve checksum, canonical identity and rollback. | 31 unit, 3 BDD, 17 integration passed. Remaining audit findings still include request qualification and cursor guards to classify. |
| TradeDb | Spread, plan and ratio bindings now use the producer's sequence; UTC history requests qualify before storage. | 1,751 unit and 62 BDD passed; 177 integration passed with 3 skipped. The later TradeDb provider subset also passed; it exercises preservation of the caller's sequence. |
| MarketDataDb | Removed unused trade-signal bulk overloads. ITI, trade-signal, calculated option and raw-tick writes consume the supplied identities. The Feed EOD projector allocates history IDs before saving them to the spool; retry reuses the saved IDs. | 319 unit and 43 integration passed (3 skipped) before the EOD ownership change; 19 focused Feed tests passed afterward, including retry across writer restart. |

The existing Development API and Rust Market Data Host readiness endpoints both returned HTTP 200. The storage candidate has not been deployed.

The subsequent audit passed **196/200** checks. All 13 contexts passed ownership checks, including the project-wide scan for hidden policy. The four failing groups were request validation in EventSourceDb (50 occurrences), MarketDataDb (110), SecuritiesDb (31) and TradeDb (105) at this checkpoint. The report file `expanded-latest.json` identifies each location. This is a list of source findings, including technical guards that need individual classification, and does not imply 296 broken runtime paths.

Securities paging subsequently passed seven actor unit checks and both PostgreSQL tests for accepted requests, invalid and mismatched cursors, and reference history. Trade passed 1,753 unit tests including both reversed-range status queries and all 22 targeted TradeDb integration tests in the preceding identity run. MarketData passed six targeted PostgreSQL/Scylla storage checks and seven Feed EOD API integration tests after sequence ownership moved. The Feed Recovery audit harness passed all eight PostgreSQL/NATS tests. The broad Storage functional run finished with 889 passes and one separate Scylla local-serial write timeout in the 32-way command-log benchmark test. This run excluded the source audit and the dedicated dense timing fixture.

The next EventSource build removed the database-side fallback timestamps for creating a projector execution row. The projector already supplies the timestamp, actor name and persisted event identity; SQL still arbitrates competing claims and revisions. All eight targeted PostgreSQL projector and workflow snapshot tests passed on that build. Its `expanded-post-projector.json` audit reports **47 EventSource, 110 MarketData, 31 Securities and 105 Trade** validation findings. All other source rule groups pass. Until these findings are addressed and broad qualification is green, this is a candidate and not a Development cutover.

### ConfigurationDb follow-up after removing the qualification read locks

Three alternating isolated BenchmarkDotNet pairs, with no other test runners active. The frozen baseline was unchanged.

| Workload | Condition | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | --- | ---: | ---: | ---: |
| ReadPublishedFamily | Pooled | 8.541 | 4.067 | 107709 / 108702 |
| PublishNewFamily | Pooled | 37.097 | 41.776 | 187414 / 285730 |
| ReadPublishedFamily | New physical | 21.074 | 17.072 | 188959 / 190384 |
| PublishNewFamily | New physical | 66.325 | 87.667 | 350108 / 530736 |

Publication still costs more because it reads a committed graph separately before validating it outside the write transaction. The PostgreSQL test confirms that this initial read does not block behind an unrelated row lock; the second read under the write fence still rejects a changed graph. The allocation values above are from the run with the median timing for each arm.

### TradeTickQuoteDb longer Scylla follow-up

Three valid alternating BenchmarkDotNet pairs with 96 invocations per iteration and twelve measured iterations. A pair that overlapped a build was excluded and replaced. As above, the allocation values are from the median timing run in each arm.

| Workload | Rows | Before (ms) | After (ms) | Before / after allocated (B) |
| --- | ---: | ---: | ---: | ---: |
| ReadTradeWindow | 64 | 1.628 | 1.670 | 68405 / 68370 |
| ReadTradeWindow | 512 | 4.634 | 4.458 | 406550 / 406513 |
| WriteQuoteSegment | 64 | 1.720 | 1.534 | 20418 / 22177 |
| WriteQuoteSegment | 512 | 2.279 | 2.035 | 20796 / 20789 |

The longer measurements did not reproduce the earlier 23.8% slowdown on the 64-row read. The request still uses the same CQL, asynchronous paging, row ordering and persisted values.

### Final recorded source audit checkpoint

The last source checkpoint includes caller-qualified evidence lookups, a C# 14 binding helper shared with write confirmation, and futures rollover symbol preparation in the owning store. The expanded audit has **196 of 200 checks passing**. Each of the 13 contexts is checked against 14 rule groups; nine contexts pass every group. The four remaining groups contain these individual findings:

| Context | Rule | Findings |
| --- | --- | ---: |
| EventSourceDb | Request validation | 47 |
| MarketDataDb | Request validation | 108 |
| SecuritiesDb | Request validation | 29 |
| TradeDb | Request validation | 105 |

See [the latest machine-readable audit](/E:/IFM/DbContextConventions/20261110/expanded-final-owner-audit.json) for each location. Do not remove validation until its caller is identified and the invalid request is rejected before a database operation. Preserve storage constraints and exact concurrency, cursor and checksum fences. Development continues to use the existing build.

All five focused PostgreSQL evidence cases and all four Securities command integration cases passed on this checkpoint. The combined audit/evidence run was 201 passed, four failed, with the failures confined to the validation groups above. The isolated current command-log comparison reproduced the broad run's Scylla local-serial timeout on 32 concurrent same-key writes; the single-writer duplicate test passed. The command-log benchmark store, Scylla provider and test source have identical SHA-256 hashes on the candidate and frozen baseline.

Both command-log cases then passed on the frozen baseline and again on the candidate when rerun without other tests or builds active. The earlier timeout is intermittent; these reruns do not establish its cause or make the original broad run green. The broader functional count remains 889 passed and one failed on that run. The four source validation groups still prevent completion.

### Request validation follow-up

| Context | Initial findings | Findings after the refactor | Changes |
| --- | ---: | ---: | --- |
| EventSourceDb | 47 | 0 | Projector identity and lease requests, event append requests and durable projection requirements now carry validated values. Command persistence prepares command audit envelopes. Storage still checks committed stream heads, optimistic revisions and audit conflicts. |
| MarketDataDb | 108 | 0 | Market Data and Analytics owners qualify import batches, calendar queries, indicator requests, option volatility and VX tick evidence. Storage retains import ownership locks, cursor scope checks and immutable evidence checks. |
| SecuritiesDb | 29 | 0 | Callers qualify expiry ranges, exact partitions and reference versions. The Securities reference reader owns pricing convention queries. PostgreSQL still checks immutable content and publication identity. |
| TradeDb | 105 | 0 | Trade query handlers qualify exact identities and history ranges. Projectors and accepted-event writers own payload checks. Storage retains exact cursor, source hash and revision checks. |
| **Total** | **289** | **0** | |

The full convention suite passed 308 tests. The auditor distinguishes request validation from checks on transaction resources and storage-issued cursors. The MarketData and convention rerun passed all 444 selected tests. See the [machine-readable audit](/E:/IFM/DbContextConventions/20261110/expanded-request-validation-final.json) and the [MarketData and convention test results](/E:/IFM/DbContextConventions/20261110/request-validation-final-marketdata-and-conventions.trx).

The full Feed integration rerun passed 72 tests and skipped seven opt-in cases. The recovery tests exposed a shutdown failure in the Trade handoff actor. It now stops the projector instance resolved during startup. [Feed integration results](/E:/IFM/DbContextConventions/20261110/Feed-v3-validation.trx)

Trade integration passed 174 tests and skipped three opt-in cases in the full run. Three workflow cases hit financial authority checks during the concurrent run. All seven full-workflow risk scenarios passed in the isolated rerun, including those three cases. [Full Trade integration results](/E:/IFM/DbContextConventions/20261110/Trade-v2-validation.trx) · [Isolated risk workflow results](/E:/IFM/DbContextConventions/20261110/request-validation-trade-risk-isolated.trx)

The Scylla duplicate-write test again timed out at local serial consistency on the single-CPU integration fixture. The isolated fixture now gives conditional writes a 10-second contention budget. Both duplicate-write tests passed with the same 32 concurrent attempts and exactly-one-winner assertion. [Scylla contention results](/E:/IFM/DbContextConventions/20261110/request-validation-scylla-cas.trx)

The complete Storage test inventory contained **1,093 tests**. Comparing test IDs across the three partitions and the targeted Scylla rerun accounted for every test with a passing result. The EventSource partition passed all 276 tests, including the 1.2-million-event replay and binary-copy rollback cases. The final MarketData partition passed all 149 context tests alongside the complete convention suite. The remaining partition passed 667 of 668 on its first run, and the Scylla rerun passed the sole failure. [EventSource results](/E:/IFM/DbContextConventions/20261110/request-validation-final-shard-eventsource.trx) · [MarketData and convention results](/E:/IFM/DbContextConventions/20261110/request-validation-final-marketdata-and-conventions.trx) · [Remaining Storage results](/E:/IFM/DbContextConventions/20261110/request-validation-final-shard-remaining.trx)
