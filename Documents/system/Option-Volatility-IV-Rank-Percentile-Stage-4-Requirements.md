# Stage 4: Option Volatility, IV Rank/Percentile and Historical Evidence

Date: 2026-09-19. Status updated 2026-09-20: S4.2-S4.7 implementation and offline qualification are complete; S4.1 production activation remains blocked pending owner approval of the initial series and consumer policy. See the [Stage 4 implementation and verification record](ES-Trade-Blotter-Stage-4-Implementation-and-Verification-v1.0.md). Trading thresholds and runtime activation are not authorized until approval.

This extends the [implementation plan](ES-Trade-Blotter-Three-Stage-Implementation-Plan-v1.0.md) after Stages 1-3 and complements the [pricing-tier addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md). S1A supplies IV-only calculation; Stage 2 supplies qualified metadata/chain snapshots; Stage 3 supplies the trading consumers. Equity trading remains out of V1.

## 1. Objective and ownership

Provide a shared, versioned option-volatility analytics component that produces comparable IV observations, IV Rank and IV Percentile, persists their histories and supplies exact as-known-at-time evidence to option-strategy workflows.

IV Rank/Percentile are statistical context, not Greeks, probabilities of profit or standalone entry instructions. Their calculation does not require full Greeks or a new option pricer.

Verified baseline: the repository has option IV values but no identified C# IV Rank/Percentile implementation. Composer's existing averaged ImpliedVolatility feature is not IV Rank. Current MarketCondition v2 explicitly excludes option-chain/IV-surface inputs and leaves strategy suitability to TradeSelection. This stage must not silently restore superseded MarketCondition responsibilities.

## 2. Where the measures are used

| Consumer | Required use / boundary |
| --- | --- |
| Shared option-volatility analytics | Own consistent IV series, historical windows, Rank/Percentile, quality and provenance. Calculate once and publish reusable immutable results. |
| TradeSelection | Consume accepted volatility context in versioned strategy-suitability rules, including configured credit/debit or strategy-variant eligibility. Rank alone never selects a strategy. |
| Order Composer / spread engine | Use accepted context in explicitly allowed parameter rules, potentially Delta targets or widths. Keep leg selection based on each leg's qualified Delta and executable quotes. Replace no existing feature meaning in place. |
| Risk Manager | Use volatility context for configured admission, stress or exposure rules, alongside actual portfolio Greeks, margin and event risk. Record the exact accepted analytics snapshot. |
| Held-position / exit workflows | Observe changes in volatility context where a configured strategy requires it. Do not replace quote-based current Greeks or automatically hedge/close solely because Rank changes. Missing analytics must not silently prevent protective closing/cancellation. |
| Portfolio | Display market volatility context and its history next to position/portfolio exposures. Rank/Percentile are not additive: do not sum them across legs or funds. |
| Trade blotter | Show current comparable IV, Rank, Percentile, series/tenor, as-of time, lookback and quality; offer a history view and the exact entry/decision snapshot. |
| Historical testing / research | Query only values and versions available at the simulated decision time; compare entry/exit context without future-data leakage. |
| MarketCondition / RegimeDiscovery | Retain current contracts and ownership. Do not inject option-specific calculations into either operator implicitly. |

A separate OptionVolatilityAssessment pipeline operator may consume the shared analytics if an independently accepted workflow decision is needed. It is optional architecture, not permission to expand MarketCondition. The baseline requirement is typed input to TradeSelection and explicit downstream propagation, with a documented dependency/parameter version. Operator insertion must be separately specified and tested.

## 3. Comparable IV series and metadata

Do not rank an arbitrary visible-chain average, a changing selected spread, trade-price IV, or a single expiring contract against an incompatible historical series.

Define an immutable VolatilitySeriesDefinition in the existing versioned configuration/metadata boundary. A candidate first series is ES ATM constant-maturity quote-based IV; a 30-calendar-day tenor and a roughly one-year daily history are proposed starting profiles, not activated defaults. Additional short-dated or strategy-horizon series must be separately defined and qualified.

Each definition must identify:

- Stable SeriesId and methodology version; underlying root, venue, currency and data/environment identity.
- Target maturity, ATM/moneyness convention, call/put selection/combination and eligible product families.
- Exercise/premium/settlement conventions, applicable pricer versions and numerical policy.
- Quote/IV mark basis, liquidity/quality rules, exact-underlying matching and maximum quote/IV age/skew.
- Expiry selection and constant-tenor interpolation, including whether total variance is interpolated; no undocumented averaging of volatilities.
- Futures-roll/contract-replacement policy, exchange value-date/calendar/time zone and sampling/cutoff policy.
- Historical lookback, minimum valid observations/coverage, gap policy, Rank range convention and Percentile tie convention.
- Effective dates, approved configuration version, owner/evidence and compatibility of historical versions.

History continuity is by stable series/root methodology, not today's front-month ContractId. Every observation nevertheless preserves all actual contributing option and futures IDs. Rolling the future must not hide earlier history or stitch incompatible measures together. A methodology change creates a new series version; any comparable backfill must be explicitly qualified.

ITI Daily/Weekly/Monthly decision horizons are not option maturities. Bind a horizon to its volatility series explicitly; do not infer tenor from the horizon name.

Missing bracketing expiries, failed IV inversion, unknown conventions or incompatible sources produce unavailable/partial observations, never silent fallback to an arbitrary expiry or underlying.

## 4. Calculation conventions

Use annual decimal IV internally; publish Rank and Percentile in 0-100 units with explicit unit metadata. Do not use either metric as the volatility input to an option pricer.

For a versioned set of eligible IV observations:

- IV Rank = 100 * (current IV - low IV) / (high IV - low IV).
- IV Percentile = 100 * count(historical IV strictly below current IV) / count(valid historical observations).

The initial convention to qualify uses the prior configured exchange-session window for historical samples. Rank bounds include current IV as well as valid window samples, keeping a new high/low at 100/0. Percentile compares against prior samples only, with strict-less-than ties. Persist these conventions; do not mix them with previous-history-only unbounded Rank or another tie definition.

The lookback window does not silently expand backward to compensate for missing days. Persist expected and valid counts. Minimum coverage, lookback length and age limits must be approved configuration values before activation. A missing/insufficient history returns InsufficientHistory; a zero Rank range returns UndefinedRange, not numeric zero. Rank and Percentile have independently qualified status: Percentile can be defined on a flat history when coverage is sufficient.

Maintain one canonical completed observation per configured exchange value date/sampling slot for daily baselines. Intraday updates must not add hundreds of samples to a daily Percentile denominator. Current intraday IV may be compared with prior completed daily samples under the recorded policy. Intraday-distribution studies require a separate sampling-series definition.

Store daily finalized metrics plus bounded/coalesced intraday metric checkpoints and every exact snapshot referenced by a trading decision. This is not a requirement to persist calculations for every raw quote.

## 5. Persistence options and selected requirement

| Approach | Benefit | Limitation |
| --- | --- | --- |
| Persist source IV history; derive Rank/Percentile when queried | Compact, supports new lookbacks | Repeated work; cannot prove which calculation/version a live decision used unless that evidence is retained separately |
| Persist only derived Rank/Percentile history | Fast charts and retrieval | Cannot reliably audit/rebuild after correction or methodology change; insufficient on its own |
| Persist source IV, derived snapshots and decision references | Fast reads, reproducibility and audit | More storage/schema work; bounded sampling and retention are required |

Stage 4 requires the third approach. An in-memory/Redis latest-value cache may accelerate reads but is not the durable history or sole decision evidence.

Use existing PostgreSQL ConfigurationDb conventions for immutable series definitions and approved parameters. Use the existing ScyllaDB MarketDataDb boundary for time-series observations and metrics. These are proposed logical tables/read models, not existing schemas or final DDL:

| Logical record | Proposed access/key design | Required contents |
| --- | --- | --- |
| VolatilitySeriesDefinition | SeriesId + immutable version; effective-date lookup | Metadata and all calculation/sampling conventions above |
| OptionIvObservationHistory | Partition by environment, SeriesId/version and calendar bucket; cluster by ValueDate, sample slot/time and revision | IV, status, exact contributors, quote/underlying source times/sequences/generation, pricer/input digest, ObservedAtUtc, RecordedAtUtc, AvailableAtUtc, revision/supersedes ID |
| OptionIvMetricHistory | Same bounded series/date access, additionally keyed by metric policy/window version | Current IV, Rank/Percentile and separate statuses, historical low/high, below/tie/valid/expected counts, window boundaries, source observation IDs/digest, calculation version/time and availability time |
| OptionIvSnapshotById | Direct immutable SnapshotId lookup | Complete sealed metric payload plus exact source observation manifest/reference needed for replay |
| OptionIvLatest | One rebuildable pointer per environment/series/policy | Latest published snapshot identity, version and freshness; never the historical authority |
| Workflow volatility evidence | Existing versioned workflow evidence references | SnapshotId/digest, accepted policy/series version, decision time, freshness result and relevant rule outcome |

Choose monthly or yearly buckets only after estimating rows/partition for each sampling policy. Queries must use partition keys and bounded date ranges with paging; no unbounded root-wide scans or ALLOW FILTERING as a normal access path. Keep environment/simulation namespaces isolated.

### Write consistency, correction and retention

- Stable observation/snapshot identities make retries idempotent. A correction appends a new immutable revision linked to the old one; it must not overwrite accepted decision evidence.
- Scylla multi-table writes must not be treated as one relational transaction. Persist/verify source evidence and the sealed snapshot before advertising a published manifest/latest pointer. History readers expose only committed/published snapshots; incomplete writes remain invisible and recoverable.
- Latest-pointer updates must be monotonic and protected from older concurrent completions. Define single-writer or conditional-update semantics during implementation, with failure/concurrency tests.
- A restart reconstructs caches from published durable state and resumes bounded checkpoints. Use explicit idempotent retries for failed persistence/rebuild work; do not introduce a durable queue for every raw option quote.
- Retention must cover the configured lookback plus required replay/audit horizon. Referenced snapshots and their source evidence must not expire independently. No blanket TTL or destructive backfill until an approved retention/archival policy exists.
- Backfills write to a versioned staging namespace, validate counts, coverage and digests, then publish a qualified version. Never silently replace the historical record of what the live system knew.

## 6. Retrieval and historical use

Provide typed, cancellable application queries with bounded paging:

1. Latest qualified volatility snapshot for an exact series/policy and freshness requirement.
2. IV observation history by series, exchange ValueDate range, sampling slot and version.
3. Rank/Percentile history for charting/comparison, including status and coverage.
4. Exact immutable SnapshotId for a recorded decision.
5. As-known-at-time history/snapshot for a simulated decision time.

Distinguish ObservedAtUtc (market fact), CalculatedAtUtc (derived work), RecordedAtUtc (durable ingestion) and AvailableAtUtc (published usability). Revisions learned later must not appear in an earlier as-known query simply because their market ValueDate is old.

Offer two explicitly labeled historical modes:

- As-known replay: use only published revisions available at the original decision time; reproduce accepted evidence exactly.
- Restated research: use later qualified corrections/reconstruction under a declared methodology/data vintage. Never label this as what the live system knew.

Historical option-trade IV alone is not automatically a valid quote-midpoint ATM constant-tenor baseline. Backfill needs the required historical quotes/underlying/reference inputs or a qualified external series with matching methodology and provenance. A missing history cannot be manufactured from today's chain.

For market-closed testing, supply deterministic series fixtures with a virtual clock, versioned metadata and simulated roll/correction events. Fixtures may seed the full configured lookback in isolated test storage; they are explicitly synthetic, not live historical evidence.

## 7. Scheduling and integration

Consume Stage 2's qualified IV-only outputs for the reference contracts needed by the series. If additional ATM/expiry coverage is needed, acquire it through the existing shared subscription owner with bounded scope, not another independent feed. No full-chain/all-Greek pass is required just to calculate Rank/Percentile.

Update rolling statistics from qualified observations and publish at configured cadence/material-change boundaries. Keep option pricing, history reads and baseline warmup off UI and synchronous decision threads. A cold/unavailable service reports readiness/coverage explicitly; it must not stall unrelated futures workflows indefinitely.

Version application messages and TradeSelection/Composer feature allowlists explicitly. Record accepted volatility snapshots in the workflow context and propagate them to risk/decision evidence. Downstream freshness revalidation cannot silently replace upstream accepted context; a required reassessment is explicit.

Strategies declare whether this input is required or optional. Missing required evidence blocks new entry/suitability approval with a reason. Optional missing evidence is marked unavailable and cannot silently become a passing numeric threshold. Preserve independent protective-exit/cancel capabilities.

## 8. Implementation sequence and exit gate S4

1. Approve initial comparable-series definitions, histories, consumer rules, sampling and retention policies; inventory source-history availability.
2. Implement pure Rank/Percentile/window functions and versioned contracts with explicit units and failure/coverage states.
3. Implement series construction, historical import/qualification and bounded live IV-only enrichment.
4. Add additive configuration/Scylla schemas, idempotent publication, revision manifests, latest caches and query APIs.
5. Integrate typed volatility evidence into TradeSelection and approved Composer/Risk rules; keep MarketCondition scope unchanged.
6. Add blotter/Portfolio history and as-of displays; add deterministic market-closed simulation fixtures.
7. Complete the tests below, benchmark refresh/query/startup costs and record evidence before enabling dependent strategies.

### Required verification

- BDD happy paths: qualified IV history -> correct Rank/Percentile -> published snapshot -> strategy rule -> Composer/Risk evidence -> history/exact-snapshot retrieval; repeat using virtual time with markets closed.
- Unit cases: formula reference examples, strict ties, all-equal range, new extrema, rolling sample expiry, insufficient history, invalid/nonfinite IV, independent metric statuses and unit conversions.
- Series cases: futures roll, option expiry transitions, tenor bracketing, absent quotes, exercise/premium mismatch, ATM selection, explicit horizon mapping, gaps, session cutoffs, time zones/DST and version changes.
- Historical cases: late arrival/correction, backfill, sample deduplication, intraday versus daily denominator, as-known versus restated results and prevention of look-ahead.
- Integration cases: real application/storage boundaries, append-only revisions, duplicate delivery, failed/interrupted multi-table publication, concurrent writers, stale/latest fencing, process restart, cache loss, paging and retention/reference integrity.
- Consumer cases: required/optional missing input, no strategy enabled by fabricated zero, explicit feature-version migration, unchanged MarketCondition behavior and preserved protective exits.
- Performance: bounded partitions/queues, cache recovery, cold history readiness, query latency and allocations/GC. No per-tick full-chain pricing or UI blocking.

Run integration projects sequentially according to repository policy. Gate S4 requires all tests passing, reproducible source/metric histories, point-in-time decision replay, consumer evidence and approved activation parameters. Prior stage passes do not imply Stage 4 completion.
