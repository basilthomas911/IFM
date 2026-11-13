# Application Storage query optimization implementation plan

The ScyllaDB latest tick-ID queries now use descending clustering reads with an identity-only projection. At 16,384 ticks per contract-day, both queries averaged about 1.06 ms after the change, down from about 31.6 ms. The RSI daily index experiment is complete. PostgreSQL already serves the query with a backward primary-key scan, so the candidate index stays out of the application schema.

The [query audit](Storage-Query-Performance-Review.md) contains the full folder inventory. This plan prioritizes the queries with a specific change to test and uses measured workload cost to choose from the remaining candidates.

## Implementation order

| Order | DbContext | Candidate change | BenchmarkDotNet comparison and correctness check |
| --- | --- | --- | --- |
| 1 | TradeTickQuoteDb | Replace MAX and GROUP BY in GetLastFuturesTickDataId and GetLastFuturesOptionTickDataId with the existing descending clustering order and LIMIT 1. Return the same three fields in mapper order. | Aggregate vs ordered read for futures and options with empty, typical and dense contract-days. Seed a newer tick on another date. Assert identical IDs and verify empty-day behavior through the context. Extend the Scylla benchmark fixture and reuse TradeTickQuoteRoutingTests. |
| 2 | MarketDataDb | The daily queries use backward primary-key scans. The four intraday indexes are part of the MarketDataDb schema and remove top-N sorts, with additional WAL for inserts and corrections. | Fresh creation, populated upgrade, repeat migration and query checks passed on disposable PostgreSQL 17.2. |
| 3 | SecuritiesDb | The paging index is part of the schema. Separate first-page and continuation queries give generic deep plans a full cursor seek. | Fresh install, populated upgrade, repeat migration and cursor continuity passed. The split cut the forced-generic deep page from 27.004 to 1.414 ms in the comparison and accepted the baseline tokens after application. |
| 4 | TradePlanDb | Sequential Iron Condor history now seeks by the last revision at the original revision ceiling. Arbitrary jumps use OFFSET and return a continuation when more rows exist. | The prototype reduced the complete deep page from 41.903 to 23.336 ms with a known cursor. After application, the disposable PostgreSQL fixture verified page results, revision ceilings and chart metadata. |
| 5 | TradeDb and MarketDataServiceDb | TradeDb uses a schema-managed order-fills index and separate first-page and continuation queries. MarketDataServiceDb now includes the recovery-log date/order index. | Both migrations and query plans passed on disposable PostgreSQL. The recovery-log benchmark cut broad reads by 27% and dataset-only reads by 62%, with 12.8% more WAL per append. |
| 6 | EventSourceDb | The schema now includes projector-scoped partial indexes for pending or retrying rows and publishing leases. | Claim latency fell from 9.939 to 9.007 ms in the benchmark. Fresh creation, upgrade, reapplication, concurrent claims and lease recovery passed on disposable PostgreSQL. |
| 7 | MarketDataDb, SecuritiesDb and TradeDb schema | Removed all nine duplicate secondary indexes from the schema catalogs and added idempotent drops for existing databases. | Fresh creation, populated upgrades, reapplication, primary-key read plans and uniqueness checks passed. The benchmark measured 20.1% to 33.8% less insert WAL. |

Use observed calls, total execution time and I/O to select the next items from the audit. Candidates such as receipt anti-joins, cash aggregates and option-chain winner selection warrant their own investigation when the workload shows material cost. [PostgreSQL pg_stat_statements](https://www.postgresql.org/docs/current/pgstatstatements.html)

## BenchmarkDotNet before and after timings

1. Use the existing Application.Storage.Benchmarks project in Release mode. Extend the closest fixture and seed deterministic data on isolated PostgreSQL and ScyllaDB instances. Record the source commit, .NET runtime, database image, dataset size, connection settings and concurrency.
2. Capture **Before** with the current query and schema. Capture **After** with exactly one candidate change. Use matched databases or schemas for index comparisons so the baseline cannot use the candidate index. Seed, analyze and verify results outside timed work.
3. Mark the existing implementation with [Benchmark(Baseline = true)] when both variants can run in the same BenchmarkDotNet job. For separate schema or commit runs, use identical job settings and report the after mean divided by the before mean. Run warm pooled connections as the main case and record cold-connection or concurrent cases separately where relevant. [BenchmarkDotNet baselines](https://benchmarkdotnet.org/articles/features/baselines.html)
4. Time one complete repository operation, including result materialization. Record mean, median, error, standard deviation, ratio and managed allocations where available. Use a separate per-request load sample for p95 and p99 when contention matters.
5. Save raw BenchmarkDotNet output and attach PostgreSQL execution plans or ScyllaDB traces. On PostgreSQL, compare actual rows visited, buffer work and sorts. On ScyllaDB, compare partitions and rows read. Run write-sensitive checks for any candidate index. [PostgreSQL EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html) · [ScyllaDB SELECT](https://docs.scylladb.com/manual/stable/cql/dml/select.html)

| DbContext and query | Data size and parameters | Before mean | After mean | After / before | Error and allocation | Correctness and server work |
| --- | --- | ---: | ---: | ---: | --- | --- |
| TradeTickQuoteDb GetLastFuturesTickDataId | 128 ticks per contract-day | 1.352 ms | 1.146 ms | 0.848 | ±0.135 to ±0.120 ms, 16.49 to 16.52 KiB | Full identity and empty day passed |
| TradeTickQuoteDb GetLastFuturesOptionTickDataId | 128 ticks per contract-day | 1.194 ms | 1.065 ms | 0.892 | ±0.049 to ±0.048 ms, 16.59 to 16.63 KiB | Full identity and empty day passed |
| TradeTickQuoteDb GetLastFuturesTickDataId | 16,384 ticks per contract-day | 31.604 ms | 1.065 ms | 0.034 | ±0.154 to ±0.043 ms, 16.54 to 16.54 KiB | Full identity and empty day passed |
| TradeTickQuoteDb GetLastFuturesOptionTickDataId | 16,384 ticks per contract-day | 31.688 ms | 1.062 ms | 0.034 | ±0.194 to ±0.059 ms, 16.61 to 16.62 KiB | Full identity and empty day passed |
| MarketDataDb GetLastFuturesRsiDailySignal | 256 rows per series | 0.977 ms | 1.006 ms | 1.030 | ±0.057 to ±0.074 ms | Full row and missing series passed. Three shared buffer hits in both plans. |
| MarketDataDb GetLastFuturesRsiDailySignal | 65,536 rows per series | 0.873 ms | 1.003 ms | 1.148 | ±0.047 to ±0.078 ms | Full row and missing series passed. Four shared buffer hits in both plans. |
| MarketDataDb InsertFuturesRsiSignal | 256 initial rows per series | 5.467 ms | 5.467 ms | 1.000 | ±0.114 to ±0.108 ms | 3,329 inserts verified per arm. WAL rose from 677.2 to 823.4 bytes per insert. |
| MarketDataDb InsertFuturesRsiSignal | 65,536 initial rows per series | 5.340 ms | 5.532 ms | 1.036 | ±0.058 to ±0.125 ms | 3,329 inserts verified per arm. WAL rose from 674.4 to 822.0 bytes per insert. |
| SecuritiesDb GetNativeOptionContractsPage | First page | 3.958 ms | 2.840 ms | 0.718 | ±0.713 to ±0.384 ms | Same page and token. Custom plan buffer hits fell from 55 to 8. |
| SecuritiesDb GetNativeOptionContractsPage | Deep page after 108,201 rows | 3.214 ms | 1.600 ms | 0.498 | ±0.144 to ±0.136 ms | Same page and token. Custom plan buffer hits fell from 57 to 9. |
| TradePlanDb anchored history | First page, metadata and rows | 22.581 ms | 22.018 ms | 0.975 | ±0.747 to ±0.451 ms | Same payloads, count, anchor and chart end. |
| TradePlanDb anchored history | Deep page after 60,000 rows, metadata and rows | 41.903 ms | 23.336 ms | 0.557 | ±0.690 to ±1.165 ms | Same page and metadata after two newer revisions. Cursor already available. |

Add one result row per scenario for the subsequent phases. Carry a candidate forward when it preserves results and a repeat run shows improvement beyond measurement noise on a representative workload. For an index change, also compare the write cost and document the rollout and rollback.

## Investigation status

The latest-ID comparison ran through the normal storage context using BenchmarkDotNet 0.15.8, .NET 10.0.10 and a disposable, single-node ScyllaDB 6.2.2 container on loopback. Both arms used warm connections, five warmup iterations and 20 measured iterations at 128 operations per iteration. Fixtures seeded synthetic key-only rows, including a higher tick ID on a neighboring date. The two dense cases reduced mean latency by 96.6%. At 128 rows, the differences were smaller and the futures baseline showed a bimodal distribution. [Before report](<C:/repos/IFM/.artifacts/latest-tick-id-20261112/before/results/TomasAI.IFM.Application.Storage.Benchmarks.LatestTickIdBenchmarks-report-github.md>) · [After report](<C:/repos/IFM/.artifacts/latest-tick-id-20261112/after/results/TomasAI.IFM.Application.Storage.Benchmarks.LatestTickIdBenchmarks-report-github.md>)

The benchmark entry point is `--latest-tick-id` in the existing storage benchmark executable. It uses the dedicated loopback ScyllaDB endpoint on port 59042 and the `ifm_dbcontext_benchmark` keyspace configured by `IFM_DBCONTEXT_BENCHMARK_SCYLLA`. The repository also has live-database fixtures for the pending trade-plan history and recovery-log comparisons.

All nine focused tests passed. The routing test verified both latest IDs through the MarketData context and confirmed that both return null after deleting the requested day's ticks. ScyllaDB traces also returned ID 32,768 for both CQL shapes on the dense fixtures. [Test results](<C:/repos/IFM/.artifacts/latest-tick-id-20261112/latest-tick-id.trx>) · [Futures trace](<C:/repos/IFM/.artifacts/latest-tick-id-20261112/futures_tick_data-trace.txt>) · [Futures options trace](<C:/repos/IFM/.artifacts/latest-tick-id-20261112/futures_option_tick_data-trace.txt>)

### MarketDataDb daily RSI results

The benchmark used warm connections through the normal MarketDataDb methods on disposable PostgreSQL 17.2. The two synthetic fixtures each seeded four series, with 256 or 65,536 rows per series. The first candidate run overlapped a WAL-triggered checkpoint. The comparison above uses a rerun with the same durability settings in both arms, a checkpoint before each arm and enough WAL headroom to complete each run without another checkpoint. [Before log](<C:/repos/IFM/.artifacts/rsi-daily-20261112/before-controlled.log>) · [After log](<C:/repos/IFM/.artifacts/rsi-daily-20261112/after-controlled.log>) · [PostgreSQL log](<C:/repos/IFM/.artifacts/rsi-daily-20261112/postgres.log>)

The candidate used `(contractid, timeperiod, periodlength, valuedate DESC, timestamp DESC)`. PostgreSQL used `futures_rsi_signal_pkey` backward in every captured plan and returned one row without a sort. The additional index occupied 25.9 MiB for the 262,144-row fixture and raised measured WAL per committed insert by 21.6% to 21.9%. The write cases inserted new keys through the normal upsert method. Conflict updates were outside the measurement. [Candidate DDL](<C:/repos/IFM/.artifacts/rsi-daily-20261112/candidate-index.sql>)

### MarketDataDb daily ADX ATR and MACD plans

The exact catalog SQL ran against the checked-in schema on isolated PostgreSQL 17.2. Each query fixes every key column preceding `valuedate` and `timestamp`. PostgreSQL scanned the corresponding primary key backward under `LIMIT 1` for custom and generic prepared plans. Matching reads returned one row with four shared buffer hits. Missing series returned zero rows with three hits. All 18 correctness checks passed, including newer rows in other scopes and an older date with a later time of day.

| Query | Dense custom median | Dense generic median | Access path |
| --- | ---: | ---: | --- |
| GetLastFuturesAdxDailySignal | 0.050 ms | 0.073 ms | Backward scan of futures_adx_signal_pkey |
| GetLastFuturesDailyAtrSignal | 0.090 ms | 0.082 ms | Backward scan of futures_atr_signal_pkey |
| GetLastFuturesMacdDailySignal | 0.066 ms | 0.077 ms | Backward scan of futures_macd_signal_pkey |

Times are server execution medians from five EXPLAIN ANALYZE samples per plan mode at 65,536 matching rows. The existing access paths already stop at the first match, so there was no candidate index to put through a before-and-after BenchmarkDotNet or write-cost comparison. [Plan captures](<C:/repos/IFM/.artifacts/daily-indicator-plans-20261112/plans.log>) · [Case summary](<C:/repos/IFM/.artifacts/daily-indicator-plans-20261112/summary.json>)

### MarketDataDb intraday RSI ADX ATR and MACD

Each query orders a fixed contract, timeframe, indicator parameters and value date by `marketdataasof DESC NULLS LAST, timestamp DESC`. The candidate B-tree puts those equality columns first and uses the exact requested ordering next. Baseline plans used a bitmap heap scan for 128 matching rows or a sequential scan for 8,192 matching rows, then a top-N sort. Each candidate plan returned one row directly. Shared buffer hits fell from 8 to 3 in the small case and from 882–923 to 4 in the dense case. [Candidate DDL](<C:/repos/IFM/.artifacts/intraday-indicators-20261112/candidate-indexes.sql>) · [Before plans](<C:/repos/IFM/.artifacts/intraday-indicators-20261112/before-plans.txt>) · [After plans](<C:/repos/IFM/.artifacts/intraday-indicators-20261112/after-plans.txt>)

| Indicator | 128-row read mean | 8,192-row read mean | Dense insert mean | Dense correction mean |
| --- | ---: | ---: | ---: | ---: |
| RSI | 1.423 → 1.122 ms | 7.785 → 0.946 ms | 5.832 → 5.583 ms | 5.543 → 5.547 ms |
| ADX | 0.990 → 0.894 ms | 3.300 → 1.006 ms | 5.638 → 5.499 ms | 5.558 → 5.630 ms |
| ATR | 1.164 → 0.910 ms | 7.360 → 1.101 ms | 5.312 → 5.561 ms | 5.451 → 5.571 ms |
| MACD | 1.035 → 0.926 ms | 7.959 → 0.956 ms | 5.420 → 5.440 ms | 5.502 → 5.344 ms |

Across both fixture sizes, WAL bytes per insert increased 24–29%. WAL bytes per correction increased 87–161% when the upsert changed the indexed as-of value. The dense fixture indexes occupied 4.6–5.1 MiB each. Write latency varied in both directions across cases. [Full timings, errors, allocations and WAL](<C:/repos/IFM/.artifacts/intraday-indicators-20261112/comparison.csv>)

The `--intraday-indicator-index` BenchmarkDotNet entry point ran the normal context methods with warm pooled connections against isolated PostgreSQL 17.2. Both arms kept full durability and completed without checkpoints during measurement. The synthetic fixture contained 128 or 8,192 matching ten-second observations with additional nonmatching scopes. Both arms verified scope, as-of precedence, timestamp tie-breaks, all-null fallback and missing dates. Each write case verified 1,089 committed inserts or corrections and the resulting latest signal. [Before run](<C:/repos/IFM/.artifacts/intraday-indicators-20261112/before.log>) · [After run](<C:/repos/IFM/.artifacts/intraday-indicators-20261112/after.log>)

The MarketDataDb schema now creates all four `_latest_intraday` indexes after the indicator metadata columns. On a populated PostgreSQL 17.2 fixture, the schema upgrade preserved all four table fingerprints. A second application retained the same index identities. Fresh schema creation also built four valid indexes. Each upgraded query used its new index, returned one row with three shared buffer hits and produced the same results as before migration. All 20 selected tests passed. [Migration test and plans](<C:/repos/IFM/.artifacts/intraday-index-migration-20261112/integration-tests.log>)

### SecuritiesDb native option paging

The `--securities-option-page-index` BenchmarkDotNet entry point used the normal paging and single-contract write methods with warm pools on disposable PostgreSQL 17.2. The synthetic fixture contained 120,037 ES rows across 76 expiries and 12,000 NQ rows. Each request returned up to 200 contracts. The deep continuation followed 108,201 rows and split a set of equal expiry, option type and strike keys. The database default collation was `en_US.utf8`. Both arms kept full durability and completed without a checkpoint during measurement. [Before run](<C:/repos/IFM/.artifacts/securities-option-page-20261112/before.log>) · [After run](<C:/repos/IFM/.artifacts/securities-option-page-20261112/after.log>)

The candidate matches `symbol, contractmonth, optiontype COLLATE "C", strikeprice, contractid COLLATE "C"`. It removed incremental sorts. The custom deep plan moved from a month-only range plus a residual filter to an index condition on the full cursor. The generic deep plan still filtered 108,201 rows because of the optional cursor predicate. Its buffer hits fell from 3,585 to 2,683. [Candidate DDL](<C:/repos/IFM/.artifacts/securities-option-page-20261112/candidate-index.sql>) · [Before plans](<C:/repos/IFM/.artifacts/securities-option-page-20261112/before-plans.txt>) · [After plans](<C:/repos/IFM/.artifacts/securities-option-page-20261112/after-plans.txt>)

| Workload | Before mean | With index mean | Change |
| --- | ---: | ---: | ---: |
| First page, default plan mode | 3.958 ms | 2.840 ms | −28.3% |
| Deep page, default plan mode | 3.214 ms | 1.600 ms | −50.2% |
| Deep page, forced custom plan | 3.125 ms | 1.454 ms | −53.5% |
| Deep page, forced generic plan | 29.722 ms | 27.168 ms | −8.6% |
| Insert a new contract | 8.194 ms | 8.294 ms | +1.2% |

Insert timing uncertainty overlapped across the two runs. WAL per committed insert rose from 1,086.6 to 1,233.3 bytes, and the index occupied 8.67 MiB. The two arms verified 1,089 inserts each in the canonical table and the symbol projection. The benchmark verified first, next, deep, replay and terminal pages against expected keys, checked missing symbols and rejected invalid cursors. Page and cursor fingerprints matched across arms. The existing paging integration test passed. [Full timing and allocation results](<C:/repos/IFM/.artifacts/securities-option-page-20261112/comparison.csv>) · [Integration test](<C:/repos/IFM/.artifacts/securities-option-page-20261112/securities-option-paging.trx>)

The SecuritiesDb schema creates `futures_option_contract_by_symbol_page_ordered`. On a populated disposable PostgreSQL 17.2 fixture, the schema upgrade preserved the canonical and symbol-projection table fingerprints. Reapplying the schema retained the same index identity, and fresh creation built a valid index. Both first and deep plans used the new index without a sort. The custom deep plan used the full cursor as an index condition. The generic deep plan still filtered 36,001 earlier rows. First and deep pages and tokens matched across the migration, and a pre-migration token continued to the correct next page afterward. All 13 selected tests passed. [Migration test and plans](<C:/repos/IFM/.artifacts/securities-option-page-migration-20261112/integration-tests.log>)

### SecuritiesDb first-page and continuation query experiment

Both arms used the installed paging index on a disposable PostgreSQL 17.2 fixture with 120,037 ES contracts. The split first-page statement took the symbol and limit. The continuation statement used the full cursor tuple as a mandatory range predicate. The forced-generic deep plan stopped filtering 108,201 earlier entries and used the tuple as an index condition. Shared buffer hits dropped from 2,683 to 9. [Before plans](<C:/repos/IFM/.artifacts/securities-option-query-split-20261112/before-plans.txt>) · [After plans](<C:/repos/IFM/.artifacts/securities-option-query-split-20261112/after-plans.txt>)

| BenchmarkDotNet workload | Combined query mean | Split query mean |
| --- | ---: | ---: |
| First page, default plan mode | 2.320 ms | 2.176 ms |
| Deep page, default plan mode | 1.685 ms | 1.574 ms |
| Deep page, forced custom plan | 1.492 ms | 1.559 ms |
| Deep page, forced generic plan | 27.004 ms | 1.414 ms |

The forced-generic deep case improved 94.8%. Timing uncertainty overlapped in the other three cases. After applying the split, a fresh disposable PostgreSQL fixture again replayed both captured baseline tokens, matched page and token fingerprints, and used the full cursor in the generic index condition with nine shared buffer hits. All 13 selected integration and convention tests passed. [Benchmark data](<C:/repos/IFM/.artifacts/securities-option-query-split-20261112/comparison.csv>) · [Verification plan](<C:/repos/IFM/.artifacts/securities-option-query-split-verification-20261112/generic-deep-plan.txt>) · [Verification test results](<C:/repos/IFM/.artifacts/securities-option-query-split-verification-20261112/integration-tests.log>)

### TradePlanDb anchored revision paging

The `--trade-plan-history-paging` BenchmarkDotNet fixture used disposable PostgreSQL 17.2 and one Iron Condor position-day with 65,537 sparse revisions. It compared the current OFFSET session with a revision-keyset prototype using the same repeatable-read transaction, count, maximum revision, chart-end metadata and payload deserialization. A page contained 50 snapshots. Chart rendering was outside the timed workload. [Benchmark data](<C:/repos/IFM/.artifacts/trade-plan-history-paging-20261112/comparison.csv>)

| Workload | OFFSET mean | Keyset mean |
| --- | ---: | ---: |
| First page with metadata | 22.581 ms | 22.018 ms |
| Deep page with metadata and a known cursor | 41.903 ms | 23.336 ms |
| First page, row query and deserialization | 1.579 ms | 1.849 ms |
| Deep page, row query and deserialization | 21.885 ms | 1.757 ms |

Both row paths scanned the existing primary key backward. At offset 60,000, OFFSET consumed 60,050 rows and 5,615 shared buffer hits. The keyset read 51 rows including lookahead and used eight hits. Both complete page methods still paid for the aggregate metadata scan. An index-only lookup of the preceding revision for a random jump took 9.049 ms in a separate benchmark. [Query plans](<C:/repos/IFM/.artifacts/trade-plan-history-paging-20261112/query-plans.txt>)

The original anchor was 131,074. After two newer revisions were committed, anchored first, deep, following and terminal pages matched in payload order, count and chart end. A refreshed first page advanced to revision 131,078. All four selected history and chart integration cases passed. [Benchmark verification](<C:/repos/IFM/.artifacts/trade-plan-history-paging-20261112/benchmark.log>) · [Integration test results](<C:/repos/IFM/.artifacts/trade-plan-history-paging-20261112/trade-plan-history-paging.trx>)

After application, 51 TradePlanDb tests passed. The query handler returned matching 50, 50 and 25-row pages across sparse revisions. The continuation held the ceiling at 250 after revision 252 was inserted, and refresh advanced to 252. A separate chart fixture verified that continuations preserved the original chart end and that a refresh included the newer sample. [Applied implementation test results](<C:/repos/IFM/.artifacts/trade-plan-history-paging-20261112/trade-plan-anchored-paging.trx>)

### TradeDb ReadFundOrderFills

BenchmarkDotNet compared the production read and write methods on two fresh PostgreSQL 17.2 fixtures, each with 328,002 order revisions, 81,000 execution attempts and eight rows per page. The candidate index keys were `(portfolioid, fundid, valuedate, orderid, revision DESC)`. [Benchmark data](<C:/repos/IFM/.artifacts/fund-order-fills-index-20261112/comparison.csv>)

| Workload | Baseline mean | With index | Mean change |
| --- | ---: | ---: | ---: |
| First page | 34.746 ms | 3.593 ms | 89.7% faster |
| Deep page after 14,998 result rows | 3.110 ms | 6.364 ms | 104.6% slower |
| Insert order revision | 7.179 ms | 7.098 ms | 99.9% confidence intervals overlapped |

For the first page, the candidate replaced a parallel sequential scan and revision sort with an ordered scan that emitted 19 revision rows. Shared buffer accesses fell from 7,839 to 78. For the deep page, the baseline sought through the primary key, while the candidate plan used a bitmap scan and sorted 6,003 revisions. Shared buffer accesses rose from 76 to 242. Both arms matched independently expected page payloads and produced the same fingerprint across first, next, deep, deep-next, terminal and empty pages. [Query plans and paging verification](<C:/repos/IFM/.artifacts/fund-order-fills-index-20261112/query-plans.txt>)

Both write arms persisted 1,665 revisions. WAL increased from 509.97 to 634.11 bytes per revision, or 24.3%, and the candidate index occupied 12.72 MiB before the write run. [Baseline log](<C:/repos/IFM/.artifacts/fund-order-fills-index-20261112/baseline.log>) · [Candidate log](<C:/repos/IFM/.artifacts/fund-order-fills-index-20261112/candidate.log>)

The follow-up query split used the candidate index for both arms on the same PostgreSQL fixture. The first-page mean moved from 3.396 to 3.010 ms with overlapping 99.9% confidence intervals. The deep-page mean fell from 6.529 to 3.344 ms. The continuation limits the order-key search to one more order than the requested row count, then fetches each needed latest revision and its execution attempts. Its deep plan read 16 revision index entries, while the current indexed query read and sorted 6,003 revisions. The existing first continuation `(48006, Guid.Empty)` and in-order attempt cursor `(58000, 00000000-0000-0000-0000-000000000001)` replayed with identical ordered payloads. [Split benchmark data](<C:/repos/IFM/.artifacts/fund-order-fills-query-split-20261112/comparison.csv>) · [Plans and cursor verification](<C:/repos/IFM/.artifacts/fund-order-fills-query-split-20261112/query-plans.txt>)

After application, the disposable PostgreSQL 17.2 fixture verified fresh creation and an upgrade with 328,002 order revisions. Reapplying the migration preserved the index identity and fixture row counts. The production first-page plan read 19 revision rows from the new index. The deep plan sought at order 58000 and read 16 index entries with zero heap fetches during order-key discovery. All six legacy requests replayed through the production context with byte-identical ordered payloads. [Migration, plan and cursor verification](<C:/repos/IFM/.artifacts/fund-order-fills-migration-20261112/verification.log>)

### MarketDataServiceDb ReadRecoveryLog

The BenchmarkDotNet fixture used 294,912 synthetic records across 12 dates and called the production context with the endpoint default limit of 256. The candidate keys were `(value_date, operation_id, sequence)`. Each arm matched independently expected payloads and ordering for all four filter combinations, a smaller limit, and empty or mismatched scopes. [Benchmark data](<C:/repos/IFM/.artifacts/recovery-log-index-20261112/comparison.csv>) · [Query plans and correctness](<C:/repos/IFM/.artifacts/recovery-log-index-20261112/query-plans.txt>)

| Scope | Baseline mean | With index |
| --- | ---: | ---: |
| Date only | 2.511 ms | 1.834 ms |
| Date and dataset | 4.903 ms | 1.861 ms |
| Date and operation | 1.324 ms | 1.077 ms |
| Date, dataset and operation | 1.318 ms | 1.105 ms |

The broad custom plan used the new date range in index order. The dataset-only custom plan kept the dataset filter and avoided the sort. Operation-scoped custom plans were already cheap on the existing operation/sequence index. Forced generic plans with an operation filter scanned the full table in index order before the change and scanned one date with the candidate. [Query plans](<C:/repos/IFM/.artifacts/recovery-log-index-20261112/query-plans.txt>)

WAL rose from 928.74 to about 1047.4 bytes per append across two runs. Each arm persisted 1,665 rows, and the index occupied 14.01 MiB before writes. Append means were 5.598 and 14.434 ms in the first run and 5.648 and 5.610 ms in the reverse-order repeat, respectively. The latency increase did not reproduce. [Initial write results](<C:/repos/IFM/.artifacts/recovery-log-index-20261112/comparison.csv>) · [Repeat baseline](<C:/repos/IFM/.artifacts/recovery-log-index-20261112/repeat-baseline.log>) · [Repeat candidate](<C:/repos/IFM/.artifacts/recovery-log-index-20261112/repeat-candidate.log>)

After application, the PostgreSQL 17.2 fixture verified fresh creation, upgrade with 294,912 existing rows, and reapplication with the same index identity. All nine before-and-after read cases had identical ordered payloads. The date-only and dataset-only plans used the new index without sorting under both custom and generic planning. Custom operation-scoped plans sought directly to the operation. Generic operation-scoped plans used the date range and filtered within it. [Migration, plans and correctness](<C:/repos/IFM/.artifacts/recovery-log-migration-20261112/verification.log>)

### EventSourceDb ClaimEventProjectorOutbox

The disposable PostgreSQL 17.2 fixture held 192,000 outbox rows across 16 projectors. The candidate added partial indexes on `(projectorname, nextattemptatutc)` for pending and retrying rows and `(projectorname, dispatchleaseexpiresatutc)` for publishing rows. The indexes occupied 384 KiB together. The benchmark called the production claim method for 32 rows and reset claimed rows outside timed iterations. Enqueue timing covered the outbox insert with its foreign key and conflict handling. [Benchmark data](<C:/repos/IFM/.artifacts/projector-outbox-index-20261112/comparison.csv>)

| Workload | Baseline mean | With indexes |
| --- | ---: | ---: |
| Claim 32 rows | 9.939 ms | 9.007 ms |
| Enqueue one row | 5.714 ms | 5.605 ms |

The baseline intersected the global eligibility scans with the projector primary-key range. The new bitmap scans scoped all eligibility branches to the projector and still sorted 1,080 qualifying rows by creation time, event ID and effect kind. In one EXPLAIN run, execution fell from 3.549 to 2.455 ms, and statement WAL increased from 22,304 to 24,968 bytes. Both enqueue arms persisted 833 rows and their latency intervals overlapped. Enqueue WAL rose from 3,596.54 to 3,689.89 bytes per row. [Query plans](<C:/repos/IFM/.artifacts/projector-outbox-index-20261112/query-plans.txt>) · [Baseline log](<C:/repos/IFM/.artifacts/projector-outbox-index-20261112/baseline.log>) · [Candidate log](<C:/repos/IFM/.artifacts/projector-outbox-index-20261112/candidate.log>)

Both fixtures verified claim priority and the effect tie-break, disjoint results from two concurrent workers while a row was locked, exclusion of future retries and active leases, reclaim at the expiry boundary, and rejection of the old token after ownership changed.

After application, the PostgreSQL 17.2 fixture verified both index definitions on fresh creation and a populated upgrade. Reapplication preserved both index identities and the fingerprint of all 192,000 queue rows. The post-migration claim plan used both indexes and kept the existing sort order. The production claim and publish methods passed the concurrency and lease-recovery checks. [Migration, plans and correctness](<C:/repos/IFM/.artifacts/projector-outbox-migration-20261112/verification.log>)

### Duplicate secondary indexes

The local development catalog showed nine nonunique B-trees with identical primary-key keys, ordering, collation and operator classes. None owned or backed a constraint, had a dependent object, was marked clustered or was a replica-identity index. Repository search found the names only in schema declarations. Both sets of development scan counters were zero, so they establish no workload history. [Development catalog](<C:/repos/IFM/.artifacts/duplicate-index-review-20261112/development-catalog.txt>)

On disposable PostgreSQL 17.2, each table began with 65,536 rows. PostgreSQL chose every secondary index in the baseline and used the corresponding primary key after removal. All nine ordered payload fingerprints matched and all nine primary keys continued to reject duplicate keys. BenchmarkDotNet measured representative SQL reads and committed 64-row insert batches. WAL fell 20.1% to 33.8% per row. The fixture's secondary indexes occupied 27.76 MiB in total. [Plans and correctness](<C:/repos/IFM/.artifacts/duplicate-index-review-20261112/query-plans.txt>) · [Timings and WAL](<C:/repos/IFM/.artifacts/duplicate-index-review-20261112/comparison.csv>) · [Recreate statements](<C:/repos/IFM/.artifacts/duplicate-index-review-20261112/recreate-indexes.sql>)

The initial country-code insert mean rose from 5.864 to 14.731 ms. The reverse-order repeat measured 5.879 and 5.592 ms with overlapping intervals, and WAL stayed at 225.22 and 149.05 bytes per row in both runs. [Repeat baseline](<C:/repos/IFM/.artifacts/duplicate-index-review-20261112/repeat-baseline.log>) · [Repeat removal](<C:/repos/IFM/.artifacts/duplicate-index-review-20261112/repeat-candidate.log>)

After application, fresh creation omitted all nine indexes. The populated upgrade and reapplication preserved the nine primary-key index identities, row fingerprints for 65,536 rows per table, and ordered read results. Each representative read used its primary-key index after migration and each primary key rejected a duplicate insert. [Migration, plans and correctness](<C:/repos/IFM/.artifacts/duplicate-index-migration-20261112/verification.log>)

### TradePlanDb exact count and chart end

Splitting metadata into an exact count and endpoint lookups on existing indexes reduced the latest metadata mean from 19.430 to 10.020 ms and the near-head anchored mean from 20.472 to 10.295 ms. First-page and deep sequential-page means moved from 22.231 to 13.170 ms and 22.774 to 12.882 ms, respectively. These page measurements cover metadata and rows. Chart samples were verified separately. The latest metadata plan moved from a parallel heap scan with 5,504 shared buffer hits to index-only work with 553 hits. [Benchmark log](<C:/repos/IFM/.artifacts/trade-plan-metadata-20261112/benchmark.log>)

For an older anchor with 1,024 qualifying revisions, a separate run measured 1.553 ms for the aggregate, 7.847 ms for the unrestricted split and 2.162 ms for a 128-entry time-ordered probe with an exact aggregate fallback. The bounded probe measured 10.631 ms near the latest revision. Revision anchors, exact totals, ordered page payloads, cursor bytes, and chart points matched, including out-of-order observation times and empty or all-null scopes. The candidates use existing indexes and were tested in the benchmark harness. [Guarded probe and correctness](<C:/repos/IFM/.artifacts/trade-plan-metadata-20261112/guarded.log>) · [Focused tests](<C:/repos/IFM/.artifacts/trade-plan-metadata-20261112/trade-plan-metadata.trx>)

The Iron Condor history session now uses the latest metadata split and the bounded anchored probe. The applied SQL reused the existing primary key and observation-time index. On the disposable fixture the near-head probe skipped the fallback, and the older anchor used the exact aggregate over 1,024 qualifying revisions. Production pages, cursors and chart samples matched the aggregate baseline, and the original anchored view stayed stable across two new writes. [Applied verification](<C:/repos/IFM/.artifacts/trade-plan-metadata-verification-20261112/verification.log>)

### MarketDataServiceDb recovery-log filter shapes

With the installed date/order index in both arms, splitting the optional filters into four prepared statements cut forced-generic operation-only reads from 7.902 to 0.823 ms and dataset-and-operation reads from 8.124 to 0.857 ms. The split used a direct operation seek and reduced shared buffer hits from 1,687 to four. The dataset-only split gained a direct dataset/date seek with the experimental `(dataset, value_date, operation_id, sequence)` index. Its forced-generic mean moved from 1.773 to 1.217 ms and the forced-custom mean from 1.755 to 1.637 ms with overlapping intervals. All twelve ordered result cases matched the independent expectations and had identical fingerprints. [Read benchmarks and plans](<C:/repos/IFM/.artifacts/recovery-log-filter-split-20261112/query-plans.txt>)

With the additional dataset index, append timing moved from 5.646 to 5.582 ms with overlapping intervals. Both arms persisted 609 rows and WAL rose from 1,067.24 to 1,201.51 bytes per append. The index occupied 16.63 MiB on the fixture. The split and extra index were tested in the benchmark harness. [Baseline append](<C:/repos/IFM/.artifacts/recovery-log-filter-split-20261112/write-baseline.log>) · [Indexed append](<C:/repos/IFM/.artifacts/recovery-log-filter-split-20261112/write-candidate.log>)

MarketDataServiceDb now routes reads to the four tested statements and creates the dataset-leading index. Fresh creation, upgrade with 294,912 existing rows, and reapplication passed on disposable PostgreSQL 17.2. Both index identities were stable on reapplication. All four production statements avoided sorting under custom and generic plans. All 12 result cases matched the legacy query through the production context and the prepared production statements in both plan modes. [Applied verification](<C:/repos/IFM/.artifacts/recovery-log-filter-migration-20261112/verification.log>)

### EventSourceDb ordered outbox claim

On the 192,000-row PostgreSQL 17.2 fixture, an explicit active-status predicate and a partial index on `(projectorname, createdatutc, eventid, effectkind)` eliminated the claim sort. The main plan changed from sorting 1,080 eligible rows to an ordered scan that passed 32 and filtered 51. A tighter eligibility cutoff also avoided the sort. BenchmarkDotNet claim latency was 8.918 to 8.903 ms and enqueue latency was 5.675 to 5.752 ms, with overlapping intervals. Captured claim WAL increased from 24,968 to 28,808 bytes per batch, and enqueue WAL from 3,695.58 to 3,819.90 bytes per row. The candidate index occupied 3.70 MiB. [Benchmark data](<C:/repos/IFM/.artifacts/projector-outbox-claim-order-20261112/comparison.csv>) · [Plans and correctness](<C:/repos/IFM/.artifacts/projector-outbox-claim-order-20261112/query-plans.txt>)

Both variants claimed the same first 32 IDs and passed tie-break, concurrent disjoint claim, row-lock skipping, expiry-boundary recovery and stale-token checks. The rewrite and ordered index remain in the benchmark harness.

### EventSourceDb receipt anti-joins

On the 96,000-event PostgreSQL 17.2 fixture, the per-event-name ID rewrite reduced business-event reads from 49.549 to 15.579 ms, named risk-journal reads from 52.131 to 10.306 ms, and workflow risk-history pages from 52.916 to 9.966 ms when pending rows followed a long acknowledged prefix. With pending rows near the start, the respective means were 2.033 to 1.919 ms, 1.575 to 1.795 ms, and 1.620 to 1.795 ms. A full ID set difference improved the long-prefix cases and took 14.140 to 28.804 ms on the early-backlog fixture. [Timings](<C:/repos/IFM/.artifacts/receipt-anti-joins-20261112/comparison.csv>)

The per-name variant uses the existing event-name/version and receipt indexes, limits each ordered ID scan to 32 pending rows, and materializes only the final 32 payloads. Both prototypes matched full ordered rows across a receipt gap, a lower-ID event committed after later events, unacknowledged retries, duplicate acknowledgements and separate risk projection receipts. The prototypes remain in the benchmark harness. [Plans and checks](<C:/repos/IFM/.artifacts/receipt-anti-joins-20261112/query-plans.txt>)

The per-name rewrite now serves all three production receipt readers. The long-history and early-backlog fixtures verified ordered results against the previous queries and exercised the business journal, enlisted named risk reader, and decoded risk-history page. All six production plans used the existing event-name/version index and fetched payloads only for the final 32 rows. [Long-history verification](<C:/repos/IFM/.artifacts/receipt-anti-join-verification-20261112/history.log>) · [Early-backlog verification](<C:/repos/IFM/.artifacts/receipt-anti-join-verification-20261112/early.log>)

### MarketDataDb option-chain statistics

`GetOptionChainStatistics` now seeks volume and open interest independently with two partial covering indexes. The prior BenchmarkDotNet means for the original query on the original schema and the rewrite on the indexed schema were 21.58 to 18.10 ms for 14 days of history and 229.53 to 69.18 ms for 180 days. [Benchmark results](<C:/repos/IFM/.artifacts/option-chain-statistics-seeks-20261112/comparison.csv>)

Fresh creation, upgrade with 290,829 existing rows, and reapplication passed on disposable PostgreSQL 17.2. Reapplication preserved index identities and the option quote fingerprint. Both applied plans used one index-only seek per metric per contract with zero heap fetches after vacuum. Results matched the previous SQL, including independent reference-date ordering and observation-date tie-breaks, zero values, one-sided results, historical cutoffs, and exclusion of future or ineligible rows. [Applied verification](<C:/repos/IFM/.artifacts/option-chain-statistics-migration-20261112/verification.log>)

The query now finds the winners with two ordered scans on the existing partial indexes. In BenchmarkDotNet, removing separate contract discovery and per-contract seeks reduced mean latency from 18.440 to 4.206 ms for 14 days and from 63.006 to 16.687 ms for 180 days. The applied plans each used two index-only scans feeding a merge full join, with zero heap fetches after vacuum. Both metric values and their reference dates matched the previous query in nine fixture cases. [Benchmark results](<C:/repos/IFM/.artifacts/option-chain-statistics-discovery-comparison-20261112/benchmark.log>) · [Applied verification](<C:/repos/IFM/.artifacts/option-chain-statistics-ordered-verification-20261112/verification.log>)

MarketDataDb now sets the option quote heap fillfactor to 80. Fresh creation, populated upgrade with 184,320 quotes, and reapplication passed with both partial indexes. The upgrade preserved the heap file, row locations, quote data, and index identities. For updates preserving the indexed metrics, HOT stayed at 17.38% and WAL stayed near 1,697 bytes per quote on the packed heap after the upgrade. A fixture-only heap rewrite brought HOT to 38.82% and WAL to 1,536 bytes per quote. The migration sets the storage option through ALTER TABLE. [Migration and measurements](<C:/repos/IFM/.artifacts/option-chain-quote-fillfactor-migration-20261112/verification.log>)

**Decision.** Keep fillfactor 80 on `futures_option_chain_quote_data` and skip the table rewrite.

The paced test ran 56 cycles at 13 seconds per cycle on separate disposable PostgreSQL 17.2 instances. Both heaps used fillfactor 80 and both partial indexes. Each cycle updated one of eight rotating groups of 8,192 quotes and preserved the indexed volume and open-interest fields. Each run recorded two timed checkpoints at cycles 23 and 46. The upgraded heap completed three autovacuums and the rewritten heap completed two. [Per-cycle measurements](<C:/repos/IFM/.artifacts/option-chain-quote-rotation-paced-20261112/cumulative-wal.csv>) · [PostgreSQL settings](<C:/repos/IFM/.artifacts/option-chain-quote-rotation-paced-20261112/packed-settings.txt>)

| Paced test metric | Upgraded heap | Rewritten heap |
|---|---:|---:|
| HOT rate, cycles 1–8 | 17.38% | 38.53% |
| HOT rate, cycles 25–32 | 95.81% | 99.02% |
| HOT rate, all 56 cycles | 76.96% | 84.89% |
| HOT rate, cycles 49–56 | 100% | 100% |
| Total WAL through cycle 56, including rewrite | 162.50 MiB | 193.26 MiB |

The rewrite saved 14.37 MiB of WAL during the workload, recovering about 32% of its 45.13 MiB one-time cost. It finished 30.76 MiB behind on cumulative WAL. The upgraded heap caught up to 100% HOT as the rotation revisited the same quote groups, and the rewrite's early WAL savings tapered. The earlier cycle-34 break-even came from forcing a checkpoint and running manual vacuum every cycle. Under default maintenance, the paced run never broke even.

### PortfolioDb available cash

`AvailableCash` now limits funding receipts to active reservations and aggregates reversal cash by original journal in the same statement snapshot. The combined candidate reduced BenchmarkDotNet mean latency from 63.739 to 6.113 ms for 24 reservations and from 2,622.042 to 5.878 ms for 2,048 reservations. Both funds had eight active reservations. [Benchmark report](<C:/repos/IFM/.artifacts/portfolio-available-cash-comparison-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.AvailableCashBenchmarks-report-github.md>)

Applied verification on disposable PostgreSQL 17.2 found 20 active funding receipts and one reversal-journal scan per plan. The previous plans scanned the reversal journal 60 times on sparse history and 3,545 times on dense history. Both applied histories returned exactly 9,750.76. The encumbrance-only, empty-fund, and separate-book cases also matched the previous query and independently calculated amounts. The change reused the existing schema. [Applied verification](<C:/repos/IFM/.artifacts/portfolio-available-cash-applied-20261112/verification.log>)

### EventSourceDb pending business handoffs

EventSourceDb now creates a partial index on receipt `event_id` where `NOT handoff_completed`. BenchmarkDotNet late-backlog reads improved from 17.881 to 1.490 ms. Early-backlog reads moved from 1.518 to 1.410 ms with overlapping confidence intervals. WAL per inserted receipt increased by 10.9% and WAL per completion by 25.2%. [Benchmark and WAL results](<C:/repos/IFM/.artifacts/pending-business-handoffs-20261112/verification.log>)

Fresh creation, upgrade with 119,863 receipts, and reapplication passed on both disposable PostgreSQL 17.2 fixtures. Both applied plans used the partial index with zero heap fetches after vacuum and no sort. The first batch and its retry returned the same 32 ordered events. Completing that batch exposed the next expected 32 events. [Early backlog](<C:/repos/IFM/.artifacts/pending-business-handoff-index-migration-20261112/early.log>) · [Late backlog](<C:/repos/IFM/.artifacts/pending-business-handoff-index-migration-20261112/late.log>)

### MarketDataServiceDb watchdog observation filters

`ListObservationsAsync` now selects one of four statements for unfiltered, date-only, status-only, and date-and-status reads. BenchmarkDotNet means under forced generic plans moved from 1.305 to 1.215 ms unfiltered, 74.013 to 1.118 ms date-only, 21.629 to 20.184 ms status-only, and 106.679 to 3.004 ms date-and-status. [Benchmark report](<C:/repos/IFM/.artifacts/watchdog-observation-filter-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.WatchdogObservationFilterBenchmarks-report-github.md>)

The applied verification used 480,000 observations across 48 days on disposable PostgreSQL 17.2. Both date-scoped plans used a direct condition on the existing date index. The status-only plan still used the timestamp index and discarded 99,900 rows for the rare status. All ten cases matched the previous query and independently expected order through the production context and prepared statements. The fixture used distinct timestamps. [Applied plans and results](<C:/repos/IFM/.artifacts/watchdog-observation-filter-applied-20261112/verification.log>)

Status-only reads now select a fixed statement for `Up`, `Resetting`, or `Down`. On the same fixture the generic plans used the status index for `Resetting` and `Down` and the timestamp index for common `Up`. BenchmarkDotNet means moved from 3.483 to 1.135 ms for `Resetting` and from 20.860 to 1.158 ms for `Down`. The `Up` timing intervals overlapped. The applied context and prepared statements passed all 12 ordered-result cases at limits of 0, 1, 100, and 500. [Benchmark report](<C:/repos/IFM/.artifacts/watchdog-status-specific-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.WatchdogStatusBenchmarks-report-github.md>) · [Applied verification](<C:/repos/IFM/.artifacts/watchdog-status-specific-applied-20261112/verification.log>)

### TradeDb risk history continuation

`ReadRiskHistoryAfter` now merges two bounded ranges using the existing indexes. BenchmarkDotNet deep-page mean fell from 27.933 to 1.621 ms. Early continuation moved from 1.548 to 1.458 ms with overlapping timing intervals. [Benchmark report](<C:/repos/IFM/.artifacts/trade-risk-history-bounded-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.RiskHistoryPagingBenchmarks-report-github.md>)

On disposable PostgreSQL 17.2, the applied custom and generic deep plans put both cursor ranges in index conditions. The prior plans discarded 140,026 rows. Shared buffer hits fell from 5,247 to 10. The production reader replayed six legacy cursors and returned the same ordered payloads and next-token bytes, including pages within and across timestamp ties, terminal pages, and empty pages. [Applied plans and verification](<C:/repos/IFM/.artifacts/trade-risk-history-bounded-applied-20261112/verification.log>)

### TradePlanDb chart buckets

The anchored one-minute chart and the multi-date 15-minute chart now seek to the latest eligible observation in each bucket and value date. The 15-minute query merges the per-date winners using the existing timestamp and revision order. In the dense fixture, BenchmarkDotNet means fell from 123.871 to 3.835 ms and from 277.594 to 1.823 ms, respectively. [Benchmark report](<C:/repos/IFM/.artifacts/trade-plan-chart-buckets-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.TradePlanChartBucketBenchmarks-report-github.md>)

The applied plans used the existing observation-time index with zero heap fetches after vacuum. Sort inputs fell from 109,017 to 200 rows for the one-minute chart and from 218,180 to 30 for the 15-minute chart. Both sorts fit in memory. The two production readers matched the previous SQL and independent bucket winners across nine cases on disposable PostgreSQL 17.2. [Applied plans and results](<C:/repos/IFM/.artifacts/trade-plan-chart-buckets-applied-20261112/verification.log>)

### ConfigurationDb parameter version paging

`GetParameterSets` and `VersionPage` now use bounded per-set version probes on the existing primary keys. Forced-generic BenchmarkDotNet means for the two deep-page queries fell from 377.918 to 2.085 ms and from 105.362 to 1.105 ms, respectively. The `VersionPage` benchmark measures SQL page selection and row materialization before the production context loads typed values. [Benchmark report](<C:/repos/IFM/.artifacts/configuration-parameter-version-probes-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.ParameterVersionPagingBenchmarks-report-github.md>)

On disposable PostgreSQL 17.2, all 12 applied plans used backward primary-key probes with set and version bounds in the index condition. All 22 ordered-page cases matched the previous SQL and independent expectations. The checks replayed existing cursors and cursors generated by the previous queries, including transitions between same-name sets and terminal pages. [Applied plans and cursor checks](<C:/repos/IFM/.artifacts/configuration-parameter-version-applied-20261112/verification.log>)

### ScheduledTaskDb run history continuation

`HistoryAfter` now merges two bounded ranges on the existing history index. The prior BenchmarkDotNet mean fell from 42.050 to 1.118 ms on the deep continuation. Early continuation moved from 1.294 to 1.198 ms with overlapping confidence intervals. [Benchmark report](<C:/repos/IFM/.artifacts/scheduled-task-history-split-20261112/results/TomasAI.IFM.Application.Storage.Benchmarks.ScheduledTaskHistoryBenchmarks-report-github.md>)

On disposable PostgreSQL 17.2, the applied custom and generic deep plans used both cursor bounds in index conditions. The prior plans discarded 140,026 rows, and shared buffer hits fell from 14,195 to 11. All 12 ordered-page checks matched the previous SQL, including timestamp ties and terminal and empty pages. The production reader accepted the existing cursor bytes and returned the same next-token bytes. [Applied plans and cursor checks](<C:/repos/IFM/.artifacts/scheduled-task-history-split-applied-20261112/verification.log>)

### SystemAdminDb recovery operation paging

`ListOperations` now uses explicit source and protection-set filters when supplied and a direct tuple bound on continuation pages. The schema creates global and scoped indexes ordered by descending creation timestamp and operation ID. With both changes, forced-generic BenchmarkDotNet means fell from 65.507 to 0.984 ms on the unscoped first page, 34.141 to 1.046 ms on the unscoped deep page, 26.036 to 1.055 ms on the scoped first page, and 23.579 to 1.134 ms on the scoped deep page. [Baseline benchmark](<C:/repos/IFM/.artifacts/system-admin-operation-paging-20261112/baseline.log>) · [Indexed benchmark](<C:/repos/IFM/.artifacts/system-admin-operation-paging-20261112/indexed.log>)

On disposable PostgreSQL 17.2, fresh creation, upgrade of 192,000 operations, and reapplication passed. The data fingerprint and both index identities were stable. All eight applied custom and generic plans used the intended paging index and avoided a page sort. Deep pages applied the full timestamp and operation-ID tuple as an index condition. All 28 ordered-page checks passed through the production context and prepared SQL, including replay of UUID cursors from the previous query. [Applied verification](<C:/repos/IFM/.artifacts/system-admin-operation-paging-applied-20261112/verification.log>)

### SystemAdminDb backup phase paging

`GetBackupPhases` now computes the exact maximum progress through the cursor and carries it into a bounded page. The inner page includes ties so the running maximum covers the entire final revision, and the outer limit preserves the requested page size. The new covering index keys are `(operation_id, event_revision)` and include `progress_percent`. Prior BenchmarkDotNet means moved from 52.61 to 0.968 ms on the first page and from 128.08 to 16.503 ms on the deep page, comparing the original schema and query with the indexed rewrite. [Baseline benchmark](<C:/repos/IFM/.artifacts/system-admin-backup-phase-prior-max-20261112/baseline.log>) · [Indexed benchmark](<C:/repos/IFM/.artifacts/system-admin-backup-phase-prior-max-20261112/indexed.log>)

Fresh creation, upgrade of 184,102 phase rows, and reapplication passed on disposable PostgreSQL 17.2. The row fingerprint and index identity were preserved. All four applied first and deep plans sought directly to the revision bound. The generic deep plan used an index-only scan over 150,000 prior entries for the exact seed, and the custom plan used a parallel heap scan for that aggregate. All 22 ordered-page checks matched through the production context and prepared statements. Existing revision cursors preserved monotonic progress across pages, including a page cut through a tied revision. [Applied verification](<C:/repos/IFM/.artifacts/system-admin-backup-phase-prior-max-applied-20261112/verification.log>)
