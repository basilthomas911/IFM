# Rust UI Trade Bridge Allocation Optimization Implementation Plan

Date: 2026-10-21
Status: Implemented and verified in an offline native trade monitor; live-feed process profiling pending
Scope: Live trade monitor presentation in the managed Rust UI bridge

Reduce managed allocations during live trade monitoring by encoding typed display fields directly, reusing immutable history rows, and detecting changes before reconstructing publications. Preserve the existing Rust screens, displayed values, backend trade-plan generation and persistence, and live-versus-database history selection.

## 1. Current allocation path

A 15-second trace of the running UI process attributed approximately 71% of sampled allocation volume to `BridgeDocument.Project` and 6% to `BridgeWire.Serialize`. The trace recorded approximately 1.79 GB of sampled allocation volume; this measures allocation churn, not retained memory. Trace and analysis: `.artifacts/diagnostics/ui-bridge-alloc-current.nettrace` and `.artifacts/diagnostics/ui-bridge-alloc-current-analysis.tsv`.

The current path is:

1. `TradeMonitorUpdates.OnChanged` marks the monitor changed for every property notification and retains up to 100 live plans.
2. `TradeWorkspaceSession.RefreshPending` allows refreshes at intervals of at least 100 milliseconds while dirty.
3. `TradeWorkspaceSession.Snapshot` reconstructs history rows for all retained plans.
4. `TradePlanDisplay.EncodeHistory` projects the entire plan into generic dictionaries and arrays before removing position, leg and parameter evidence.
5. `Session.TradePublication` serializes the changed workspace into a new owned byte array.
6. `PublicationTransfer.Read` compares completed publications and can produce a smaller patch.

Incremental transfer already exists. Its patch generation happens after the allocation-heavy projection and full-publication encoding. The initial changes preserve this transfer mechanism and MessagePack compatibility.

## 2. Implementation stages

| Stage | Work | Completion gate |
| --- | --- | --- |
| 1 | Capture repeatable baselines and wire fixtures | Matching workload, values and refresh counts can be replayed |
| 2 | Encode history fields directly with generated typed accessors | Decoded old/new history documents match; excluded evidence is never traversed |
| 3 | Cache encoded rows by immutable plan identity | Unchanged rows reuse owned encoded bytes; bounds and invalidation are verified |
| 4 | Track changes independently for history and live sections | Price updates do not re-encode history; all displayed state changes still reach Rust |
| 5 | Reuse and improve existing incremental publication | Unchanged sections are reused before serialization; patch/full recovery remains correct |
| 6 | Validate performance and prolonged operation | Functional suites pass and repeatable allocation targets are met |

### Stage 1 Establish reproducible measurements

Extend `TomasAI.IFM.UI.Rust.Benchmarks` with a trade-monitor workload rather than relying solely on its existing generic option-grid benchmark. Replay fixed captured or fixture plans with complete position evidence. Use identical data, event counts and refresh intervals for both implementations.

Cover:

- One plan and a full 100-row live history window.
- Idle polling, position-only updates, quote-only updates and one new plan per refresh.
- Bursts of plans between refreshes, including trimming the oldest visible rows.
- Database history pages with live feed off.
- Multiple open trades, repeated tab switching and closing/reopening monitors.

Record bytes allocated per refresh and per added plan, encoding counts, refresh and encode p50/p95/p99 duration, payload/patch bytes, collection counts, GC pauses and retained cache size. Keep forced collection, if needed for controlled retained-memory comparisons, confined to the benchmark process. Do not force collection or restart the user's running apps during profiling.

Freeze representative MessagePack fixtures and the required history field set, including decimal string formatting, enum names, null handling, Eastern display timestamps, and the current millisecond precision.

### Stage 2 Encode typed history fields directly

Replace `TradePlanDisplay.EncodeHistory`'s call to `BridgeDocument.Map` with a writer that uses the existing generated `DisplayProperty.Write` accessors and `MessagePackWriter`.

Build and cache the allowed descriptor lists once per supported plan/detail type. Determine map member counts before writing. Skip these members before reading their values:

- Plan root: `position` and `parameters`.
- Iron Condor history detail: `position`, `legs`, `legacyLegs` and `strategyRiskParameterSet`.

Apply exclusions only to the history display document. Full position details and backend evidence retain their existing representation. Preserve the current public encoding entry point and Rust map keys. Avoid a new generic object graph, per-row dictionary construction, per-refresh LINQ closures, and boxing on typed scalar writes.

Gate:

- Compare decoded baseline and optimized documents for populated and nullable plans.
- Verify exact scalar formats and all existing displayed valuation, PnL, action and error fields.
- Prove excluded evidence is never read by the history encoder; removing it after projection is insufficient.
- Keep `TradePlanDisplayTests` and existing cross-language fixtures passing.

### Stage 3 Reuse bounded immutable history rows

Add a cache owned by the trade workspace lifecycle. Cache the complete history row, including its grid fields and owned MessagePack `Data`, so repeated refreshes reuse the row instead of reconstructing it.

Use a structured identity containing the established trade/position identity, value date, route generation, plan revision and display schema version. Include display configuration that affects bytes in cache invalidation. Do not create formatted string keys on every lookup. Plan publications must be treated as immutable; a replacement under the same identity must be checked and invalidated rather than silently displaying stale bytes.

Match the existing 100-row live window and the current database page. Add an aggregate encoded-byte budget across open trades; use an initial configurable internal limit of 8 MiB and evict inactive/old entries first. An oversized row can be displayed without caching. Eviction must never discard a backend plan or remove a row from the visible history. Cache entries retain encoded display rows, not entire plan evidence graphs.

Clear relevant entries on history-window eviction, trade close, session disposal, value-date/page changes and generation replacement. Separate live and saved-history cache scopes. Coalesce duplicate notifications of the same immutable revision. Build newly encoded rows outside subscription locks and publish the completed row atomically. Never retain memory borrowed from a returned pool buffer.

Gate:

- An unchanged 100-row history performs zero history-row encodes on refresh.
- Adding one new plan encodes one new row; other rows reuse their encoded data.
- Verify same revision numbers in different trades, dates and generations cannot collide.
- Verify cache bounds, replacement invalidation and disposal under repeated navigation.

### Stage 4 Separate history changes from live display changes

Replace the single broad monitor invalidation path with revisions for history, current position/quotes, limits/status, and chart/editor sections as required by the actual displayed fields.

`TradeMonitorUpdates` should advance history state for new plan identities, trimming, live-mode transitions and database page changes. Price or status notifications should refresh their affected display section without invalidating history rows. Null/empty property names mean all properties changed; handle these correctly. Map every displayed property to its dependent section, with a safe full-refresh fallback for unclassified changes.

Preserve the existing 100-millisecond refresh cadence initially. A burst may coalesce visual refreshes, but every unique received plan within the retained window must remain available. Preserve live-feed-on selection from live notifications and live-feed-off selection from persisted history. Changing the display pipeline must not issue additional database polling or restart feed leases.

Capture section revisions before snapshotting. A change arriving during publication must leave that section dirty for the next refresh. Fence late callbacks from a closed or replaced monitor. Keep confirmation dialogs and editor state independent of background live updates.

Gate:

- Live price changes update their cells while cached history rows remain untouched.
- Plans, action/error fields, feed stop notifications and mode switches appear correctly.
- Concurrent updates during refresh are not lost.
- Validate two simultaneously open trades and tab switching.

### Stage 5 Reuse existing incremental publication

Continue using the existing `CopyChanged` and `PublicationTransfer` patch/full-snapshot contract. First reuse unchanged sections and row bytes while constructing a changed publication. Keep idle polling on the existing cached-byte fast path.

Measure the remaining complete-publication allocation after stages 2 through 4. If it remains material, add a producer path that derives existing transfer operations from section changes and stable row identities, avoiding complete reserialization of unchanged sections. Resolve row prepend/trim, ordering and current index-based row identifiers explicitly before emitting producer-generated changes.

Maintain one coherent publication sequence per stream, immutable payload ownership, full snapshots on first attachment or missed sequence, and fresh full state after trade/generation changes. Preserve the current fallback when a patch is unsuitable or larger than a full publication. Any ABI change requires an explicit versioned compatibility design; none is assumed for the first four stages.

Gate:

- Extend `PublicationTransferTests` for history insertion, trimming, unchanged nested row bytes and missed-sequence recovery.
- Decode and apply fixtures in Rust and compare reconstructed state to the full publication.
- Verify ordering, selection and detail views remain stable.
- Keep ordinary quote polling independent from database history and chart sampling.

### Stage 6 Verify and roll out

Run the affected managed bridge/system tests, Rust unit tests and cross-language fixtures. Build the solution and Rust UI using existing repository scripts. Extend `TradeMonitorUpdatesTests`, `TradePlanDisplayTests` and `PublicationTransferTests` with the cases above.

Performance gates for the fixed 100-row workload:

- At least 80% less managed allocation during repeated live refreshes than the captured baseline. This is an acceptance target, not a measured result yet.
- Zero history-row encodes during unchanged or position-only refreshes after warm-up.
- One history-row encode for one new immutable plan.
- No material p95/p99 refresh-latency regression; account for host measurement noise and repeat any inconclusive run.
- Bounded cache retention after repeated window eviction, trade switching and closure.

Do a manual UI pass and use the repository's supported FlaUI/UI Automation route for accessible controls where available. Verify rendered Rust grids visually where native controls are not exposed to UI Automation. Check history selection, cell formatting and colours, feed on/off transitions, chart cadence, multiple tabs and confirmation dialogs.

After the user restarts the built version, capture another short UI allocation trace under the same live-monitor workload. Compare allocation stacks, allocation rate, Gen2 frequency and GC pause time to the baseline. Retain the source traces and benchmark results under ignored `.artifacts` paths.

## 3. Files and responsibilities

| Area | Primary files/projects |
| --- | --- |
| Typed history encoding | `TomasAI.IFM.UI.Rust.Bridge.Net/TradePlanDisplay.cs`, `BridgeDocument.cs`, `DisplayDescriptors.cs` |
| Row cache and section revisions | `TradeWorkspaceSession.cs`, `TradeMonitorUpdates.cs` |
| Publication reuse and transfer | `BridgeBootstrap.cs`, `BridgeWire.cs`, `PublicationTransfer.cs` |
| Managed correctness | `TomasAI.IFM.UI.Rust.SystemTests` |
| Rust compatibility | `TomasAI.IFM.UI.Rust` decoder and publication/patch tests |
| Performance evidence | `TomasAI.IFM.UI.Rust.Benchmarks` and ignored allocation traces |

## 4. Completion criteria

The bridge retains its current visible behaviour and data-source rules, reproduces the established MessagePack history schema, reuses unchanged rows, and meets the measured allocation gate. Backend plan generation, valuation, event logging, persistence and broker execution remain unaffected. Running applications are restarted only as part of an explicitly coordinated deployment or verification step.


## 5. Implementation and measured results (2026-10-21)

Implemented direct typed history writing in `TradePlanDisplay`, the workspace-owned `TradePlanRowCache`, independent monitor revisions and `TradeMonitorRowSections`. History cache entries are limited to 8 MiB of encoded data and 4,096 entries across tabs; oversized rows remain visible without caching. Replacement objects under the same identity are re-encoded. Cached source plans are weakly referenced, and history encodes happen outside the monitor subscription lock.

The established trade, saved positions, limits, chart and detail sections now reuse unchanged encoded rows. Live notifications retain the existing 100-plan window, database history remains exclusive to stopped mode, and the existing refresh cadence and chart sampling are preserved.

### Repeatable performance gate

A new `--trade-probe` benchmark replays identical four-leg synthetic plans with full evidence through the frozen previous projection and the optimized history-publication path. These are controlled presentation measurements, not live market prices or an end-to-end process allocation trace. Both paths serialize complete history publications with byte-identical data. Each scenario runs 100 refreshes; inputs and evidence are constructed before measurement. Two Release runs passed the 80% allocation gate.

| History rows | Scenario | Previous allocation/refresh | Optimized allocation/refresh | Reduction | History encodes/refresh after warm-up |
| --- | --- | --- | --- | --- | --- |
| 1 | Unchanged history | 59,476 bytes | 2,528 bytes | 95.75% | 0 |
| 1 | One new plan | 59,476 bytes | 7,027 bytes | 88.18% | 1 |
| 100 | Unchanged history | 5,870,250 bytes | 187,256 bytes | 96.81% | 0 |
| 100 | One new plan | 5,869,586 bytes | 258,092 bytes | 95.60% | 1 |

For 100 rows, optimized p95 was 1.09?1.27 ms for unchanged history and 1.45?1.68 ms when adding a plan. The first run's previous p95 values were 35.19 ms and 9.12 ms respectively. Timing is host-sensitive; allocation and encode-count gates were repeatable. The measured 100-row cache retained approximately 178 KiB of encoded data.

Evidence: `.artifacts/trade-bridge-performance.json`, `.artifacts/trade-bridge-performance-repeat.json`, and the corresponding benchmark logs. Build the Release bridge first, then run the benchmark project with `BridgeBenchmarkAssemblyRoot` pointing at that build and arguments `--trade-probe <output.json>`.

### Incremental transfer decision and regression fix

The existing ABI and patch/full recovery contract remain in use. Sections and row data are reused before full serialization. The measured history workload exceeds the allocation acceptance target, so a second producer-generated patch protocol was not introduced. Full changed publications still require owned byte arrays and can allocate on the LOH; these changes do not eliminate all Gen2 collections. Benchmark collection counts must not be treated as live-process GC rates.

The 100-row regression uncovered an existing pooled-tree growth bug: assigning a nested node directly from a recursive parse could write to the old array after the pool grew. Parsing now completes before assigning into the current array. C# tests and Rust fixture reconstruction verify history prepend/trim, nested binary documents, duplicate/missed sequences and full-state recovery.

### Verification and remaining rollout work

- Focused managed tests: 24 passed, including byte compatibility, excluded evidence, row reuse, cache bounds, replacement invalidation, live/saved isolation, two monitors, disposal and updates during capture.
- Rust suite: 143 passed, 8 ignored; the three generated cross-language transfer fixtures additionally passed when explicitly enabled.
- Release bridge build: zero warnings and zero errors.
- Complete solution build: zero warnings and zero errors; `.artifacts/trade-bridge-solution-build.log` (2 minutes 7 seconds).
- An unfiltered managed run encountered desktop/live-host tests without their required fixture environment; it is not a clean full-suite result.
- Automatic approval review initially rejected staging/launching the isolated desktop build. After the user stopped API/UI and explicitly authorized verification, staging and native desktop verification were permitted and completed. See the verification follow-up below.
- A post-restart live-feed trace and prolonged observation remain pending. Offline verification starts and closes only its own fixture windows; the development API was left stopped.


### Offline native verification follow-up

The isolated production shell launched successfully with the updated Release managed bridge. Its older `PilotTests.Real_host_exposes_AccessKit_and_updates_from_managed_bridge` test expects a removed ?Refresh fixture? button and therefore remains obsolete; its failure must not be described as a cached-history regression or a full desktop-suite pass.

Added `TradeHistoryDesktopTests` using the existing `trade_realtime_cache` Rust example and managed-generated publications. The fixture now contains 100 rows encoded by `TradePlanRowCache`, followed by a prepend and trim. The generator verifies exactly 101 encodes for both windows. The test regenerates its own input, starts only a disposable native process, verifies the history header and Eastern timestamps for revisions 100 and 101, checks responsiveness, saves screenshots, and terminates its fixture. The native example supports an optional transition interval to allow UI Automation startup without racing the second publication. Early AccessKit activation timeouts are handled with foreground activation and bounded UI Automation retries.

Verification evidence:

- `.artifacts/trade-bridge-history-desktop.log`: trade-specific FlaUI verification passed.
- `.artifacts/trade-bridge-history-desktop-final.log`: final combined managed/native verification run.
- `.artifacts/trade-bridge-verification/tests/history-initial.png` and `history-updated.png`: observed native grid order changes from revision 100 to 101, with revision 100 retained beneath it.
- `.artifacts/trade-bridge-verification/history.json`: generated native presentation input; missing pricing fields in this fixture intentionally remain N/A.

The verified Release bridge dependencies were staged into `.artifacts/rust-ui/app/managed` for the next normal UI startup. The staged bridge DLL SHA-256 matches the verified build: `1C2C105E04510E8501D1B4885DF9743B5A0D886B4DDB7A1C27F8DB4A6779FD76`.

A live-feed GC comparison still requires an API/UI session with an actively monitored trade; the offline fixture is not presented as a live-process allocation trace.
