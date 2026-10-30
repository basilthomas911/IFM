# Rust MarketData API Implementation Plan

## Explicit market-open clean start (2026-10-29)

Every explicit market-open start must initialize a fresh native session while the
API and Host processes remain running. Close may have completed, failed or been
missed; the previous native owner's state cannot decide the new trading date.

1. API `IValueDateProvider` explicitly initializes the opening date using its
   injected clock/calendar. Ordinary reads remain pinned. Custom backtest
   providers remain authoritative; Rust and scheduler workers derive no date.
2. The thin adapter submits `ifm_market_data_owner_reinitialize_v1` with that date.
   Rust serializes replacement, cancels and joins the old owner's three datasets,
   claims and notification subtree under one retirement budget, then installs a
   fresh owner pinned to the supplied date. This also applies to same-date starts.
3. Failed retirement does not admit a replacement over old resources. Accepted
   retirement continues if its C# caller stops observing; root shutdown still
   cancels all tracked work. Retired references reject subsequent date commands.
4. Copy new-session configuration and start futures/background discovery. Cold
   historical/Treasury clients and durable audit spool survive; old live claims,
   routes, caches and publication identities do not. Business orders and stored
   trade history remain unchanged. Previous-day live-feed intent is not revived.
5. Record old/new native owner IDs, API ValueDate, Host PID and elapsed time in
   structured Host logs. Market-open success still requires the scheduled task's
   terminal business receipt and session analytics/readiness checks.
6. Scheduled completion must distinguish native dataset ownership from managed
   process workers. The readiness contract's appended optional ownership flag
   selects Rust's healthy transport/publication proof with a nonempty generation
   and matching date. It does not fabricate expected/received subscription counts.
   Existing process-worker counter checks remain enforced. Regression tests must
   reject wrong dates, absent generation, dead producer/publication and recovery.
7. A fresh owner cannot rely on an old cached underlying quote. Its temporary
   bootstrap replays MBP-1 from the API ValueDate's exchange-session opening, and
   awaits the matching replay-completed control record. The temporary book is
   not published as executable live events. Existing quote age, ordered sides,
   provider event timestamps and decimal midpoint rules remain unchanged. The SDK
   fixture must require that exact replay start and finish setup without any
   subsequent live quote changes, then prove joined source retirement.

Verification: provider tests cover missed close, completed close, duplicate open,
DST weekend and late EOD projection. Native and actual managed/DLL tests cover
failed/recovering/stopped owners, same-date and next-day replacement, invalid
dates, cancellation, resource joins and old-date rejection. Live acceptance runs
the real scheduler market-open command against an already running application,
proving owner replacement, unchanged API/Host PIDs, the API-selected date and
fresh futures/signals. Deploying changed binaries requires a deployment session
restart; that deployment is separate from the market-open acceptance run.

Verified 2026-10-29: automatic opening occurrence
`28c2da3c-778e-dcd7-09a5-0743f49c8886` completed in 13.383 seconds with
BusinessCompleted/exit 0 and API ValueDate 2026-10-30. Native owner and all three
dataset identities changed while API/Host/UI/scheduler PIDs remained unchanged.
The subsequent background refresh recorded MonitoringRestored and 18/18 scopes
ready. ES outlook returned current data with ITI/daily analytics available.
See `MarketData-Feed-Recovery-Audit-Verification-v0.1.md` for the earlier failed
Options episode, corrected package hash, receipts and acceptance limits.



Date: 2026-10-25



Status (2026-10-27, revised ownership target): **Implementation incomplete; dataset ownership and streaming-publication phases reopened.** Existing native kernels, bounded decoding and parity fixtures remain useful evidence, but the current managed lease/recovery/delivery orchestration does not meet the required architecture. Rust must own the complete dataset lifecycle and publish streaming events directly to NATS. The C# adapter is commands/queries only. No production acceptance or automatic cutover is authorized by this revision. See [implementation evidence](../../TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md).



**Precedence:** the ownership and streaming requirements below supersede earlier instructions to retain managed feed orchestration, managed tick delivery or managed recovery authority. Historical checkpoints record what was built, not acceptance of the revised boundary.



## Current implementation evidence (2026-10-27)



Complete native reviewed reference preparation/refresh, native scope claims and

expiry, direct native futures publication, and copied option trade pricing /

persistence handoff components are implemented and qualified in isolation. Actual

C# adapter/DLL qualification passes. Real 120-second broker and reference stalls

fail under the original deadline and release native resources. Native evidence

pricing matches all 48 exported current C# outcomes. A broker PONG does not count

as evidence persistence acknowledgment. The external C# storage consumer owns

copied bytes only and cannot retain a dataset lease or block native reset.



**This is not the active-facade cutover.** Complete native options quote/cache and

control publication, desired-intent dataset coordination, retirement of managed

orchestration from the selected feed path and full Host/API/live acceptance remain

open. No component milestone closes those requirements. Detailed evolving results

and limitations are in [implementation evidence](../../TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md).



## Historical implementation checkpoints



2026-10-26: native record transport and three lane owners are implemented.

DBNMETA-001 is addressed for live, historical and file entry points by an incremental 65,536-byte

metadata parser and isolated SDK hook. See [qualification](../../TomasAI.IFM.Application.MarketData.Rust/docs/METADATA-STREAMING-QUALIFICATION.md).

The shared sync/async DBN decoders now stream metadata for all public entry points; complete facade,

Host composition and full feature/parity acceptance remain unfinished.



Continuation: phase-2 runtime/ABI/managed delivery foundation and the first trade

normalization kernel are implemented and qualified in isolation. Current evidence

includes 33 default tests, both real two-minute stall tests, 1,107 exact C# results

through async FFI, 1,000 native create/cancel/dispose cycles and exhaustive trade

normalization. The [operation matrix](../../TomasAI.IFM.Application.MarketData.Rust/docs/OperationMatrix.json)

assigns all 71 signatures and both properties. Full provider lanes, facade,

framework features, measured performance and live acceptance are still pending;

neither phase 2 as a whole nor production backend acceptance is marked complete.



## Implementation scope



Implementation root: `TomasAI.IFM.Application.MarketData.Rust`



Design: [MarketData Rust FFI lifecycle design](MarketData-Rust-FFI-Lifecycle-Design-v0.1.md)



Inventory: [source files, API operations and consumers](MarketData-Rust-FFI-Inventory-v0.1.md)



## 1. Required outcome



Implement a complete live Rust backend behind the existing `IMarketDataApi`, selectable alongside the current backend. Optimize the implementation for simple ownership, reliable cancellation/reset, low latency and bounded resource use. The current backend, its worker processes, existing Rust Databento FFI, DLLs and build paths remain unchanged.



The new implementation belongs under `TomasAI.IFM.Application.MarketData.Rust`; do not create the proposed runtime under `native.rust/MarketData.Rust`. This location supersedes that earlier proposal. The new library is `ifm_market_data_native`, with a distinct ABI and DLL filename. Its Rust provider implementation starts from copied, audited Databento bridge code; all changes to that copy stay in the new implementation.



No child processes are implemented or launched by the new backend. Tokio owns cancellable dataset tasks and their resources inside the existing MarketData Host. OS threads used by Tokio or explicitly owned runtime executors are permitted; worker executables are not.



Actors, API startup workflows, scheduled tasks and UI consumers retain their existing service calls and event contracts. Changes to select/load the backend, package its native library and provide backend-specific recovery verification belong to infrastructure composition and the new backend implementation.



Emulator/backtesting design and implementation are deferred until the live Rust backend is complete and accepted. Synthetic records, replay fixtures and fault injection in this plan are acceptance tools for the live backend, not an emulator product.



### 1.0 Required ownership and transport boundary (2026-10-27)



```text

Commands and functions:

IMarketDataApi -> thin C# FFI adapter -> Rust market-data owner

                                        -> independently owned dataset resources

Streaming:

Databento -> Rust processing/event encoding -> Rust NATS publisher -> existing C# consumers

```



- Rust owns each dataset/role's desired runtime subscriptions, leases, renewal, connections, tasks, bounded queues, cancellation, generations, health and one two-minute recovery episode. The lanes remain Futures/GLBX.MDP3, Futures/XCBF.PITCH and Options/GLBX.MDP3; options reset must leave both futures lanes progressing.

- FFI exposes commands, cancellation, operation results, status and snapshot queries. Continuous market ticks, quotes, trades, statistics, chain updates and feed lifecycle/error publications go directly from Rust to NATS. There is no managed wait/read/drain/republish loop for the market stream.

- A managed caller, reader, consumer, database write or broker publication cannot pin a dataset resource or gate native cancellation, joining, release or reconstruction. Copy data or use checked non-owning identities; never expose a resource-owning dataset handle to a downstream consumer.

- The C# facade must not instantiate `DatabentoMarketDataApi` as feed policy, manage dataset leases, run renewal/expiry loops, prepare context lifecycles, own feed recovery state or await arbitrary managed callbacks to reset a dataset.

- Thin hosting/configuration, ABI marshalling, external persistence and immutable reference inputs may remain managed. They are not dataset supervisors. C# trade/actor restoration and API-server restart are outside this dataset-recovery scope and are not success prerequisites for native cleanup.

- Existing C# raw tick consumers continue writing ScyllaDB through the existing event/storage path. Direct Rust ScyllaDB ingestion is not added in this revision. Likewise, existing PostgreSQL business intent/evidence storage is retained as an external boundary; its acknowledgement cannot hold a native dataset lease. Preserve persistence/idempotency contracts or report a documented compatibility blocker.



### 1.1 Acceptance baseline: mirror the current C# implementation



The current C# backend is the behavioral acceptance oracle. Run both implementations with identical recorded provider inputs, commands, policies, reference snapshots, initial persisted state, ValueDate and controlled business timestamps. Compare all observable API results, events, ordering guarantees, aggregate outputs, pricing/risk values, errors and lifecycle outcomes. Separate live subscriptions at different wall-clock times are not evidence of exact parity because they may receive different inputs.



**Default acceptance is exact equality. Every mismatch must either be fixed or have an evidence-backed, documented explanation and an accepted disposition. An unexplained mismatch fails acceptance.** A performance improvement or the use of Rust does not justify changing financial results or consumer behavior.



Maintain a difference register with test/fixture identity, input and baseline versions, expected C# result, actual Rust result, affected fields, reproducible cause, impact on consumers/trading, proposed disposition and approval reference. Include links to the regression tests and supporting traces. Previously agreed architecture differences (real Host PID instead of child PID, logical worker identity, generation-based reset proof) are recorded exceptions, not invented values or hidden test exclusions.



- Compare stable identifiers, prices, scaled quantities, business timestamps, ordering, status/error contracts and canonical hashes exactly where the contract requires them.

- Compare floating-point values exactly first. Any numerical tolerance requires a field-specific explanation, measured absolute/relative/ULP bounds as appropriate, and approval before it can make a failing comparison pass. Never introduce a blanket tolerance or round away a mismatch without reporting it.

- Nondeterministic runtime IDs, receipt/processing timestamps and scheduling details may differ. Record their semantic equivalence rules explicitly; compare relationships, monotonicity and required ordering instead of deleting entire fields from the test. Financial/source timestamps and ValueDate are not generally nondeterministic fields.

- Known C# defects may be corrected in Rust only as documented, approved parity exceptions with focused regression tests. Preserve the current implementation itself. Intended recovery fixes still need evidence and a recorded explanation.

- Internal task/thread layout and measured latency are assessed against their own requirements, not forced to equal the C# implementation. They cannot excuse different observable financial behavior.



Apply this rule at every phase exit and final acceptance. Do not count an unresolved difference as a pass; classify it as failed or awaiting exception approval. Final evidence separates exact matches, approved explained differences and outstanding failures.



### 1.2 Managed helper retirement inventory



The audit found 103 C# source files in the new `managed/` and `host/` package, including thin adapters/DTOs, and 38 responsibility groups including shared dependencies. These counts are an audit snapshot, not a count of independent orchestrators. Record every concrete helper's disposition and test in the operation/ownership matrix.



| Current managed responsibility | Required disposition in the new backend |

|---|---|

| `NativeOptionChainRuntime`, `NativeOptionConnection` | Move scope/lease/expiry/control/replacement authority into Rust. |

| `QualifiedCompositionDiscovery`, `NativeRecoveringOptionLeaseRegistry`, `QualifiedIndividualOptionFeeds` | Replace managed discovery registration, renewal, restore, owner/session tables and gates with native ownership. |

| `NativeMarketDataEpochFactory`, `NativeMarketDataEpoch`, `NativeEpochResources` | Move authoritative bootstrap, routes, cancellation trees, reset/join/disposal and dataset generations into Rust. |

| `NativeDatasetRecovery`, `NativeHostRecovery`, authoritative `NativeHostState`, `OptionFeedProcessingHealth` | Move incident/deadline/attempt/health authority into Rust; managed status is a projection only. |

| `DatasetWorkerAdmissionRegistry`, `DatasetDesiredSubscriptionRegistry`, durable subscription realization | Replace managed physical admission/manifest authority; retain business-intent storage outside. |

| `QualifiedEvaluatedOptionChain`, `OptionChainBackgroundUpdater`, `OptionFeedSessionCleanup`, chain/risk registrations | Move universe ownership, caches, scope gates, refresh, release and session retirement into Rust. |

| Pricing-context/Treasury preparation and composition helpers | Move feed context lifetime, eligibility/validation policy and refresh scheduling; supply immutable versioned external inputs where needed. Numerical pricing is already native and remains parity tested. |

| `NativeMarketDataDelivery`, quote/trade/futures delivery and observation publishers | Remove managed stream receivers, pulse queues, callback ownership and data publication scheduling after native replacements pass parity. |

| `NativeNatsPublisher`, session/cache publications | Port envelope construction, streaming serialization, ordering and catch-up ownership to the Rust NATS publisher; remove managed double publication. |

| Stable readers, catalogs, seed/replay/reference helpers | Rust owns provider resources, binding lifetime and feed scheduling; C# may marshal copied snapshots and external repository data. |

| `RustMarketDataApi`, `NativeCompositionMarketDataApi`, runtime loader/handle/completion bridge, Host dispatcher | Reduce to bounded command/query adaptation. A top-level opaque runtime handle is permitted; per-dataset lifetime authority and finalizer-dependent cleanup are not. |

| PostgreSQL/Redis/Scylla repositories, trade loading, API actors | Keep outside dataset ownership. Stalls cannot prevent native reset; actual persistence failure must remain observable. |

| `CurrentLeaseContract` compatibility responses | Keep explicitly disabled operations distinguishable from implemented native lease functionality; do not count a `Disabled` response as ownership coverage. |



Archive the detailed helper/timeout audit with qualification evidence. Interim gate bypasses or longer managed timeouts do not satisfy any native ownership exit gate.



## 2. Non-negotiable engineering rules



### Simplicity



- One authoritative owner for each dataset/role lane and its session generation.

- One owner-registration table and renewal authority; separate persistent desired business intent from disposable transport leases.

- One recovery episode coordinator, one monotonic deadline and one terminal result per episode. Do not reproduce the existing stack of independent quarantine/admission/restart latches.

- Use a small typed state machine: `Stopped`, `Starting`, `Running`, `Recovering`, `Failed`. Readiness details are evidence attached to state, not independently permanent gates.

- Start as one Rust crate plus the managed adapter. Introduce another crate, queue or abstraction only for a demonstrated ownership, dependency or testability requirement.

- Prefer explicit commands and typed results over reflection, service locators, dynamic dispatch and generic framework machinery inside the Rust runtime.



### Idiomatic Rust and safe FFI



- Use ownership, enums, newtypes, `Result`, bounded channels and explicit task collections. Apply `Arc` only where concurrent sharing is necessary; avoid reference cycles and unbounded retained snapshots.

- Keep unsafe code at the C ABI/buffer boundary. Add safety invariants to every unsafe block and exported operation; deny unsafe operations inside unsafe functions unless explicitly marked.

- No normal-path `unwrap`, `expect`, panics or unwinding across FFI for provider failures, malformed input or shutdown races. Translate failures into typed results with diagnostic context.

- Never hold a synchronous lock across `.await`, native I/O, callbacks or thread joins. CPU work must be bounded/chunked and cancellation aware.

- Do not use `spawn_blocking` for indefinitely running provider/session work. Any necessary blocking dependency needs a documented exit mechanism and qualification evidence.

- Normal shutdown uses explicit asynchronous close and awaited task completion. `Drop` and managed finalizers must not perform an unbounded join or free memory still used by a task.

- Treat cancellation of Databento `subscribe`/`start` as abandonment of the whole client/session. `next_record` cancellation behavior must be verified against the pinned SDK. Do not silently upgrade the SDK during migration.



### Low latency and bounded resources



- Provider records update native hot state directly; avoid per-record JSON, managed object graphs and repeated FFI calls for each field.

- Preallocate/reuse appropriately sized native stream/serialization buffers; use bounded copied command/query results at the FFI boundary. Define hard maximums and account for outstanding buffer ownership.

- Deliver updates and operation completions through signalled events, not timer polling. Batch only already available records; do not hold the latest update waiting to fill a batch or for an acknowledgement.

- Use latest-per-contract replacement for disposable option views. Preserve every record needed for volume, VWAP, bars and other authoritative aggregates, or mark/reseed incomplete aggregates explicitly.

- Reserve control/completion/failure capacity independently from quote traffic. A full data queue cannot prevent reset, cancellation, error notification or shutdown.

- Never log every quote on the hot path. Keep structured lifecycle/error logs, sampled counters and traces with correlation IDs and sanitized provider detail.

- Optimize measured bottlenecks. Lock-free data structures, zero-copy APIs and custom allocators are not prerequisites and need benchmark evidence before adding complexity.



## 3. Project and deployment layout



Proposed layout; phase 1 may simplify internal modules before committing their public interfaces:



```text

TomasAI.IFM.Application.MarketData.Rust/

  Cargo.toml

  Cargo.lock

  src/

    lib.rs

    ffi/             # C ABI, validated handles and buffer ownership

    runtime/         # supervisor, state machine, clocks, completion events

    dataset/         # lane/session ownership and cancellation

    databento/       # copied/adapted provider implementation

    subscriptions/   # desired runtime owners, leases and renewal

    catalog/         # definitions and provider identity mappings

    latest/          # coherent hot prices and risk observations

    aggregation/     # normalization, bars, session statistics and VWAP

    pricing/         # qualified contexts, supported IV/Greek engines

    option_chain/    # coverage, snapshots and background strategy cache

    historical/      # downloads, decoding, streaming and live startup replay

    reference/       # Treasury/calendar provider logic and conversions

    messaging/       # direct Tokio NATS publication, wire contracts and catch-up

    diagnostics/     # errors, traces, counters and cleanup evidence

  managed/

    TomasAI.IFM.Application.MarketData.Rust.csproj

    Api/             # RustMarketDataApi : existing IMarketDataApi

    Interop/         # distinct native loader, ABI records and SafeHandle

    Delivery/        # command completions only; no market tick streaming

    Recovery/        # read-only native recovery proof mapping; no coordinator

    Composition/     # optional backend registrations

  tests/             # Rust contract, lifecycle and integration tests

  benches/           # measured hot-path/recovery benchmarks

  docs/              # ownership, ABI and operation documentation

```



Managed integration tests can use a separate test project in the repository's normal test layout. The Rust backend's implementation code remains under the requested root. Do not move current implementation files there.



Runtime configuration selects `Current` or `RustFfi` when Host starts. `Current` remains the default. Exactly one backend owns production subscriptions and publishes live updates in a session. Switching backends is explicit; no automatic fallback, runtime hot switching or default cutover is implemented in this plan.



## 4. Recovery acceptance contract



### One original 120-second budget



Record the incident start and deadline when the recovery episode is accepted. Use monotonic elapsed time for the runtime budget; business ValueDate/time remain API supplied. Repeated attempts, lease reacquisition, cleanup, reconnect and verification all consume the same budget. Pass the remaining budget to dependencies; do not add independent recovery timers or restart the clock.



Provider heartbeat/freshness detection has its own detection policy. Measure detection latency separately; it must not conceal an extra recovery window. Caller cancellation and explicit market close are distinct lifecycle requests, not new recovery deadlines.



The flow is:



1. Capture current desired owners/policies and advance the dataset generation.

2. Cancel obsolete work immediately; discard obsolete replaceable queued observations without draining them.

3. Await old tasks/resources completing through the qualified cancellation path.

4. Create a clean session, subscribe exact underlying dependencies and reacquire fresh runtime leases from desired intent.

5. Verify the native session and the required dataset workload. Reuse a successful replacement while checking native freshness/coverage; downstream consumers are independently observed and cannot hold reset open.

6. Mark recovery successful only with the required cleanup/readiness evidence. Otherwise continue appropriate repair attempts within the remaining budget.

7. At the original deadline, record `Failed` and retain a terminal dataset error for direct Rust NATS notification and status queries. Cancellation, cleanup and broker outage cannot delay recording the result. No stale success, silent quarantine or leaked-owner replacement loop is acceptable. A UI/broker outage cannot guarantee dialog presentation at the exact deadline.



Reset is the native cancel/join/rebuild action inside this same 120-second episode; begin it promptly when needed. Do not spend 120 seconds in managed retries and then start a fresh two-minute dataset reset. Repeated reconnect attempts never renew the incident clock. An independently noncooperative native dependency is a qualification blocker, not permission to claim cleanup or spawn another leaked owner.



Routine failed steps must return to this coordinator rather than terminate the recovery loop. Separate episode identity from attempt identity so transport deduplication cannot replay an earlier attempt's result as a new attempt.



### Proof depends on workload



| Workload | Success proof |

|---|---|

| No live trade or background owner | Clean idle ownership and completed retirement; no quote/position wait. |

| Background strategy-chain owners | Restored subscriptions and usable required chain observations; no loaded-trade requirement. |

| Live trade subscriptions | Restored desired contracts, fresh required coherent observations/risk and publication state. Actor plan generation is a separate downstream acceptance check and cannot delay or own native dataset cleanup/recovery. |

| Quiet/closed session | API-session-aware transport/control evidence; no manufactured fresh prices or stale-data success. |



The Rust backend reports the real Host PID, logical worker identity, generation and desired manifest revision using existing public fields. Dataset reset keeps the same Host PID; internal Rust cleanup/new-generation evidence replaces the PID-change assertion only in its selected recovery adapter. The current backend's verification remains unchanged.



### Architectural limit and observable failure



Tokio cancellation cannot preempt synchronous code that never yields. RAII cannot safely kill a deadlocked OS thread. Qualification must prove cancellation for the supported network, SDK, processing and teardown paths. If a required dependency cannot meet the no-child-process design, record a blocker; do not quietly add Rust subprocesses or pretend a detached task has stopped.



An independent status/notification observer must expose the recorded two-minute failure if a dataset owner does not respond; it does not own or restart the dataset recovery clock. Measure separately: the native deadline, terminal-event publication, API receipt, and UI dialog presentation. Network/OS scheduling and an unavailable UI prevent an absolute guarantee of a visible dialog at exactly 120.000 seconds. The service must record failure at expiry, keep the notification available across reconnects through the existing error mechanism and never wait indefinitely for the failed owner. Existing external supervision is the final physical containment boundary; this plan adds no new automatic restart policy.



## 5. Implementation phases



### Phase 0 — Freeze behavior and establish measurable baselines



- Record the current commit, source checksums, native artifact hashes/provenance and runtime configuration. Preserve the existing backend and old native Rust crate/build inputs unchanged throughout implementation.

- Convert the inventory of 71 methods and two properties into an operation matrix: callers, current routing, Rust/managed assignment, units, error behavior, cancellation, wire identity and parity test.

- Identify all current event publications, synchronous readers, cache semantics and status-field consumers; record fixtures from the existing implementation.

- Inventory every currently used exercise/premium convention and numerical engine; do not equate the existing Rust Black-76 kernel with complete pricing coverage.

- Capture repeatable baseline throughput, p50/p95/p99/p99.9 latency, allocation/native memory, queue occupancy, CPU, task/thread/socket counts, first-subscription latency and reset duration.

- Freeze representative futures/options workloads and hardware/build profile. Require exact result matching by default; document and obtain approval for any field-specific numerical tolerance under section 1.1. Fix numeric latency/resource regression limits before optimizing or accepting the new backend; compare equivalent loads and report variance.



Exit: complete operation/feature matrix and approved reproducible benchmark/fault fixtures; no unassigned operation and no baseline modifications.



### Phase 1 — Prove cancellable Tokio lifecycle before the full port



- Create the new crate under the requested root and record provenance for copied Databento modules. Do not modify the source crate or reuse its DLL name.

- Implement one owner with explicit cancellation, task tracking, operation completions and cleanup evidence. Start with synthetic input/fake servers, then the pinned real SDK.

- Exercise cancellation during DNS/connect/authentication, subscribe, start, record read, replay, full-output queues and stop. Audit SDK/runtime dependencies that may create background or blocking tasks.

- Demonstrate cancellation selection without timer polling; discard partially cancelled setup clients. Await task/resource completion rather than dropping a join handle.

- Test an intentionally noncooperative task in an isolated test harness. The external observer must produce bounded failure without freeing live memory or creating repeated leaked owners. A test harness may be process-isolated for test-runner safety; the production Rust backend must not launch processes.



Exit: all supported paths have cancellation/cleanup proof; uncooperative behavior has an explicit bounded failure result. Stop full-port work if a necessary dependency cannot meet the agreed architecture.



### Phase 2 — Implement the ABI, managed adapter and runtime foundation



- Add explicit ABI version/size checks, fixed-width DTOs, typed errors, generation-checked handles, pooled copy operations and release semantics.

- Implement nonblocking command submission, request cancellation, signalled **command-completion** delivery and copied snapshot reads. Market streaming never traverses the FFI; provider tasks never invoke managed actors synchronously.

- Reduce the managed project to the distinct library resolver, validated opaque runtime handle, command/result marshalling and `RustMarketDataApi` facade. Move authoritative lifecycle policy into Rust; external persistence/reference adapters exchange immutable data and cannot own dataset leases.

- Add explicit business-clock/session inputs and a separate monotonic recovery budget. Centralize lifecycle state, error classification and episode/attempt handling.

- Implement the three ownership lanes: Futures/GLBX.MDP3, Futures/XCBF.PITCH and Options/GLBX.MDP3. No thread/process identity assumption substitutes for lane/generation ownership.



Exit: ABI fixtures, concurrent cancel/dispose/handle tests, deadline tests, malformed input and panic-boundary tests pass; native resources and control/output queues are bounded.



### Phase 3 — Port futures, catalog and aggregation



- Port live transport/normalization, definitions and instrument mapping, hot quotes/trades/readers, source replay handling and symbol/contract resolution integration.

- Implement current/next-contract reconciliation, API ValueDate/session rules and exact underlying dependencies using existing authoritative persistence.

- Port all market-data-required aggregation/session OHLC, statistics, volume, VWAP, bar output, correction and replay-overlap behavior.

- Implement framework routing, buffer pooling, publication versioning and disabled-output behavior in Rust. Publish existing event contracts directly to NATS from Rust; preserve their subjects and exact wire semantics.

- Preserve complete aggregates under pressure; if an input sequence is lost, produce explicit incomplete state and repair/reseed evidence.



Exit: fixtures match current contract/price/event/aggregate behavior and measured low-latency gates; one futures lane reset does not stop the other.



### Phase 4 — Port options, pricing, leases and strategy caches



- Implement exact-underlying readiness, individual option observation/risk, chain discovery, ownership/refcounts, fresh lease IDs, expiry/release/reacquire and one renewal authority.

- Port coherent pricing context validation and every currently required IV/Greek model/convention. Copy/adapt reusable numerical kernel code into the new dependency boundary without changing the current kernel; qualify unsupported model gaps explicitly.

- Port all provider-independent framework snapshot-store behavior: bounded scopes, immutable/coherent snapshots, monotonic versions, generation retirement and immediate reads.

- Implement published strategy-universe planning and background cache ownership independently of loaded trades. Preserve policy DTE/expiry/contract/strike limits and deduplicate physical subscriptions.

- Correct the observed incident paths in the new backend: ended lease reuse, old-generation refresh/release, no-trade proof, cached recovery responses and repeated process-replacement assertions.



Exit: pricing/model parity, no-trade background recovery, live-owner recovery, lease races and generation isolation pass; options reset leaves both futures lanes progressing.



### Phase 5 — Complete references, history and durable recovery integration



- Implement all `Framework.MarketData` Treasury acquisition/parsing/conversion behavior in Rust with approved units, conventions, date selection and identity/hash compatibility.

- Complete the FinancialModelingPrep framework behaviors required by current Treasury/calendar operations. Preserve application reference stores and publication policy.

- Implement historical estimates, metadata/download/DBN streaming, startup seed replay and all existing historical operation semantics. Keep durable loader checkpoints/manifests/storage in their application owners.

- Import committed current-ValueDate subscription intent and explicit enable/disable/market-close changes as native commands. Native reset restores its desired subscriptions without calling C# trade loading, lease adoption or actor recovery. Application startup may load saved business intent separately; reset cannot restore disabled owners.

- Complete all remaining interface operation/property mappings; explicitly supported production operations cannot fall through to default `NotSupportedException` behavior.



Exit: the entire API/framework matrix is covered, including references/history, session rollover, DST, checkpoint resume and saved intent. Emulator design remains deferred.



### Phase 6 — Compose both backends and preserve consumer behavior



- Add the opt-in backend selector and separate native packaging. Leave current backend registrations/behavior as the default path; keep its source/build implementation intact.

- Expose native-owned health/recovery outcomes through the same public status fields and terminal failure/UI contracts. Existing infrastructure observers must not become native reset prerequisites. Business consumers gain no Rust-specific branches.

- Verify the API read mirror and ongoing actor events remain current; test synchronous reader behavior before/after reset and while data is unavailable.

- Run the same contract/integration suites using each selected backend. Audit that Rust mode starts no worker executables and does not load the old provider lifecycle accidentally.



Exit: either backend can run the same actors/startup/scheduled tasks/UI unchanged; only one publisher/owner is active; startup selection and explicit rollback are proven.



### Phase 6A - Native control ownership and direct NATS publication (reopened; mandatory)



1. Freeze exact NATS subjects, deployment scope/configuration, authentication/TLS rules, envelope/DTO schemas, MessagePack encoding, timestamps, sequence/manifest rules, errors and current catch-up behaviour. Cover all three lanes and prepared option-chain publications. Keep Core NATS versus JetStream delivery semantics explicit; no silent switch to durable streaming.

2. Add a Tokio-compatible NATS client within the new crate. Share validated broker configuration through startup commands; do not expose credentials in logs or invent alternate per-language defaults. Native dataset owners produce bounded copied output into native publication ownership. One lane's stalled publisher cannot stall another lane or its control path.

3. Implement native owner registrations, lease renewal/expiry, context refresh, strategy-chain maintenance, complete desired subscription restoration and one monotonic recovery supervisor per dataset/role. Import policies/reference versions through explicit commands; no managed renewal thread or requalification callback is required for reset.

4. Implement Rust event construction, MessagePack encoding, sequence/generation fencing, latest-state catch-up and terminal-error retention. Preserve C# `Guid`/date/decimal/enum/extension representations, not merely structurally similar JSON values. Reset retires obsolete queued data without waiting for backlog drain. Required historical/aggregate/trade records cannot be silently dropped; explicitly report gaps and repair/reseed or fail the applicable contract.

5. Separate publish-task cancellation from dataset cleanup and the broker reconnect lifecycle. Bound queues, bytes, outstanding sends and retries. Reset can cancel/join old native publication work without a broker acknowledgement; terminal result remains queryable and retryable when NATS is down. A completed Core NATS client send is not a durable consumer/storage acknowledgement.

6. Decouple managed PostgreSQL evidence/publication callbacks from native scope lifetime. Preserve idempotency and existing persisted-data semantics through copied output and explicit acknowledgement identities where required. If this cannot preserve a required contract without pinning native resources, record the specific blocker instead of weakening correctness.

7. Retire managed ownership services from the **new backend's registrations and execution paths** only after replacement tests pass. Audit both DI and runtime traces: no `DatabentoMarketDataApi` feed-policy reuse, managed scope renewal/expiry, managed recovery coordinator, tick drain/republish loop or callback join prerequisite remains. Keep the current backend unchanged.

8. Publish an operation/ownership matrix for every existing `IMarketDataApi` capability and helper disposition: native module, thin adapter/external input if any, wire/parity test, cancellation/reset proof and remaining limitation.



Exit: native dataset cancellation/reconstruction is independent of managed consumers, persistence and NATS availability; all streaming is Rust -> NATS; only commands/functions/results cross FFI. Every isolation gate below passes before this phase is accepted.



For controlled downstream-stall tests with a healthy Databento fixture, **successful native cancel/join/rebuild and fresh source readiness within 120 seconds are mandatory**; merely timing out and reporting an error does not pass the isolation gate. Report publication/storage availability separately: rebuilding a dataset while NATS is down must not claim that consumers received data or persistence succeeded. Tests that deliberately prevent provider recovery must instead prove terminal dataset failure is recorded within the deadline, remains queryable and is delivered when notification transport returns. No new two-minute budget is granted for an inner retry or broker reconnect.





### Phase 7 — Recovery, performance and sustained acceptance



- Execute the matrix in section 6 through command/query FFI and direct Rust NATS streaming, including fault timing before/after command acknowledgement and while publications are congested.

- Run sustained equivalent-load tests and at least 1,000 deterministic reset cycles. Reconcile every active owner/task/socket/buffer/lease after each cycle; do not use eventual GC as native cleanup proof.

- Compare measured latency tails, throughput, CPU, allocation and peak/steady-state memory to phase-0 limits. Benchmark feed receipt, hot-state update, pricing completion, Rust NATS enqueue/send/receipt and downstream actor processing separately; measure command/query FFI separately from streaming.

- Validate the existing hybrid and standalone UI flows against each backend, including the two-minute failure dialog. Test live market open/close and recovery under a configured session only after deterministic qualification.

- Record the supported workload/hardware and test evidence. No lower-latency claim based solely on language choice, average-only measurements or an unloaded synthetic feed.



Exit: every required operation, recovery and performance gate passes with recorded evidence. If live market hours/environment are unavailable, record the live gate as blocked rather than accepted.



### Phase 8 — Package and hand over without automatic cutover



- Ship the new crate/managed adapter, distinct DLL/artifact manifest, symbols, sample configuration and operator runbook alongside current packages.

- Verify clean startup/shutdown with `Current` and `RustFfi`, missing/wrong ABI failures, source/artifact provenance, credentials redaction and deliberate rollback.

- Complete Rust documentation, API mapping, recovery evidence, benchmark report and remaining limitations.

- Keep `Current` default and retain the old backend/native FFI. User approval of a later cutover is separate from implementation acceptance; no baseline deletion or source consolidation is included.



Exit: selectable, documented, fully qualified live Rust backend with current backend unchanged and emulator work still out of scope.



## 6. Required test matrix



| Area | Required checks |

|---|---|

| Differential acceptance | Identical controlled inputs/state for C# and Rust; exact matching by default. Every difference is fixed or documented with reproducible cause, impact and accepted disposition; unexplained/unapproved mismatches fail. |

| Contract completeness | Every method/property and required error/cancellation result; wire subjects/keys/units; same consumers against both backends. |

| Framework completeness | Every responsibility in design sections 1.2/1.3 mapped to a Rust implementation and parity test, including reference conversion and publication behavior. |

| Ownership | Ended lease reacquisition, last-owner release, overlapping policies, simultaneous start/stop/reset, expired registration, late release and duplicate commands. |

| Setup/read cancellation | Disconnect, DNS/connect/auth stalls, partial subscribe/start, empty reads, replay delay and close/reset races through the actual SDK boundary where possible. |

| Recovery outcome | Happy path with time to spare; repeated failed steps continue within the original budget; success with no owners, background owners and live trades; deadline expiry produces one terminal result. |

| Deadline propagation | No retry, FFI/transport timeout or verifier starts a new budget; caller cancellation and market close work during recovery. |

| Replacement proof | Stable Host PID with fresh logical owner/generation; old tasks/resources completed; repeated attempt accepts already restored state; no cached earlier attempt result. |

| Isolation | Reset Options/GLBX while Futures/GLBX and Futures/XCBF keep progressing; obsolete generation cannot affect a replacement. |

| Pressure | Full quote/output queues, slow publication/consumer, concurrent history/reference work, memory caps and reserved reset/error capacity. |

| FFI safety | Invalid lengths/handles, stale/reused handles, duplicate release, retained snapshot/buffer, concurrent dispose, panic translation and no unwind/use-after-free. |

| Data correctness | Pricing conventions/Greeks, coherent exact-underlying snapshots, bars/OHLC/volume/VWAP, replay overlap, historical/checkpoint resume and policy eligibility. |

| Session/time | API ValueDate only, DST/weekends/holidays, quiet market, market close/open, startup and saved trade restoration. |

| Error visibility | Provider errors/warnings retain details; unknown exception reports unhealthy; deadline failure survives reconnect and appears through existing UI flow. |

| Noncooperative work | Independent observer reports failure; no false cleanup or repeated leaked replacements; documented external containment limitation. |

| Performance | Frozen equivalent workloads, latency tails and throughput; CPU/allocation/memory within phase-0 bounds; bounded native task/socket/buffer counts after repeated reset. |

| Native ownership audit | No managed dataset lease/renewal/recovery authority, callback lifetime lock or `DatabentoMarketDataApi` policy reuse in new-backend execution; verify actual registrations and traces. |

| Direct NATS parity | Rust publishes every required lane/event kind directly; C# consumers deserialize exact existing MessagePack fixtures and subjects; zero managed market-stream drain/republish calls and no duplicate publisher. |

| Stalled C# consumer | Hold or stop managed consumption while triggering reset; native cancellation, old-task/socket join, new generation and required dataset readiness complete inside the original 120s or retain an explicit terminal failure. |

| Stalled database | Hold managed PostgreSQL evidence and downstream Scylla writes beyond the recovery window; dataset reset neither awaits those writes nor retains native buffers through them; persisted contract/dedup and visible outage/gap results remain correct. |

| Stalled/down NATS | Blackhole writes, disconnect and reconnect, exhaust bounded output queues, reject auth and stall flush; dataset reset remains bounded and other lanes progress. Terminal result is queryable at expiry and is delivered after broker recovery without a new recovery clock. |

| All downstream stalled | Simultaneously stall C# consumer, persistence and broker under load; prove no retained native dataset references and bounded tasks/sockets/bytes across repeated replacements. |

| Deadline and failure injection | Inject failure at every dataset lifecycle stage and repeated failed attempts; use real production 120s expiry as well as virtual-time tests. No inner managed timeout or callback determines native recovery termination. |

| Preservation/package | Baseline source/artifact checksums, no Rust child processes, distinct DLL/ABI, default unchanged, explicit rollback and no duplicate publisher. |



Use virtual monotonic time for deterministic deadline branches plus wall-clock integrated expiry tests of the actual 120-second configuration. Virtual time alone is not sufficient to prove SDK/network cleanup. Test helpers may sample counters for assertions; production feed/event delivery remains signalled rather than polled.



## 7. Rust documentation and quality gates



Documentation is written with each implementation phase, not deferred until packaging:



- Crate-level `//!` explains purpose, module boundaries, threading/task model and how the managed API maps to the runtime.

- Every module documents ownership, cancellation, failure and resource limits relevant to it.

- Every public Rust type, function, field, enum variant and C ABI export has Rustdoc explaining semantics, units, valid states and errors. Use `# Safety`, `# Errors`, `# Panics` and examples where applicable; examples must compile when feasible.

- Nontrivial private algorithms and async state transitions have comments explaining invariants, cancellation boundaries, ordering, lock rules and why a tradeoff exists. Do not add comments that merely repeat trivial code.

- Every unsafe block has a nearby `SAFETY:` justification covering pointer validity, aliasing, ownership, sizes and concurrent use. The ABI header and managed records document the same buffer/handle contract.

- Document copied source provenance and intentional changes, provider cancellation guarantees, lease lifecycle, status-field mappings and recovery success/failure evidence.

- Maintain operator documentation for configuration, diagnosis, two-minute failure, packaging and explicit backend rollback. Include a worked no-trade cache recovery and a live-trade recovery trace.



Required new-crate gates: `cargo fmt --check`; Clippy with warnings denied; tests and doctests; `cargo doc --no-deps` with Rustdoc warnings denied. Enforce `missing_docs` and `unsafe_op_in_unsafe_fn` on the new public crate surface. Any narrowly scoped lint exception needs a documented reason. Run the gates for supported live/default feature combinations and the actual Windows x64 deployment target; expand platform scope only if explicitly required.



Gate commands and features are finalized when the new manifest exists; the requirements are not claims that these commands were run for this plan. Documentation checks do not replace functional, cancellation or performance evidence.



## 8. Evidence, completion and blockers



For each phase, record implementation commits, API/feature matrix entries, changed files, test commands/results, benchmark workload/build/host configuration and remaining risks. Recovery evidence includes incident/attempt IDs, deadline, cancellation requested/observed, last old-task completion, resources released, new generation, readiness proof, terminal publication and UI receipt/presentation timestamps.



Completion requires all live API/framework capabilities, parity tests, lifecycle fault tests, documentation gates, sustained measurements, packaging and configured live acceptance, with zero unexplained result differences and zero unapproved exceptions. A compile or successful start is not acceptance.



Material blockers include a required uncancellable dependency, missing pricing model parity, an API consumer that cannot be preserved through infrastructure adaptation, unavoidable baseline modification, or inability to meet measured resource/latency bounds without changing behavior. Report the specific evidence and smallest required design decision; do not silently weaken cleanup proof, extend the deadline, add a Rust worker process or change the current backend.





## Historical continuation evidence: 2026-10-26



The checkpoints below predate the 2026-10-27 native-ownership/direct-NATS revision. Passing managed-bridge tests is historical evidence only and cannot close the reopened phases above.



The current continuation adds real live-data control/status/event/hot-read exports

and managed delivery, dataset task tracking, per-lane bounded output, API business

clock injection and batch historical control/download streaming. See

`TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md` for the

qualification commands and limits. Full public API operations remain unqualified

in `docs/OperationMatrix.json`; component evidence must not mark them complete.



The first new large-download test exposed an async-future stack overflow from an

embedded 65 KiB array. Fixed scratch is now fallibly allocated on the heap, and a

64 MiB transfer passes byte/size/checksum checks without accumulating the file.

The authentication protocol's three text lines now have a separate 65,536-byte

bound as well. These changes are scoped to the new backend/vendor copies.



Compatibility review also distinguished stable public API readers from retired

native transport-book handles. Retained public futures reader objects now read a

replacement dataset while remaining bound to their original API date/runtime.

Exact snapshot comparison against the unchanged C# store passes through ten

resets, including bid/ask order counts, timestamps and final retirement.



### Required work still open



Update from the current continuation: the explicit `RustMarketDataApi` facade

now exists and compiles, routing 71 methods and both properties. Its native-only

epoch and 39 Host capabilities still require complete concrete owners before

registration. Contract qualification checks that no default interface body can

silently supply an unsupported operation.



Native exact configured-symbol bootstrap, parent discovery, atomic hydration and

catalog DTO mapping now pass HTTP/DLL differential tests. A 420-case CLR oracle

preserves the difference between stored-summary and live-query maturity rules.

Replay-only completion now has an ordered control publication; its session/VWAP

snapshot excludes later live records. HTTP warning delivery reaches the managed

bridge, including failure/cancellation outcomes and maximum retained header sizes.

Quote input qualification now runs in Rust using exact CLR decimal bits and

fixed-size FFI records. The managed pricing adapter uses it; 104 full pricing

comparisons and 10,000 direct unchanged-C# validator comparisons pass.



The catalog/publication paragraphs below record the earlier continuation and do

not describe the current remaining scope. The next concrete implementation work

is the complete native option scope/context/snapshot owner, futures publication

assembly and concrete epoch/Host composition, followed by reference/history

application completion and the full acceptance matrix. None of those gates is

waived by successful component tests. Full status and reproduction commands are

in `TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md`.



The next continuation adds atomic native catalog hydration and its managed/C ABI

boundary, then connects actual provider HTTP acquisition to the same RAII

transaction. Evidence: 444 unchanged C# definition cases, 1,000 cancelled refresh

cycles followed by successful reload, and real HTTP success/invalid/timeout/

cancellation tests with socket closure and retained committed state. The shared

control-result admission helper preserves result ownership across these new

operation kinds.



Authoritative output records now capture coherent market-price, session and VWAP

state under the book writer. A separate allocation-free 656-byte publication ABI

maps existing managed contracts without later hot-cache reads. DLL tests verify

separate trade/quote captures through ten resets, exact decimal VWAP conversion,

and preservation of incomplete-state flags. This does not complete replay-only

publication handling, option-risk events, Host/NATS delivery or the full facade.

Full regression before this publication extension: 113 passed, zero failed,

three explicitly excluded real-wall-clock tests already qualified separately.

All changed catalog/book/lane/ABI/VWAP tests were rerun after the extension;

the added queued-state test passed. Clippy/Rustdoc with warnings denied and the

managed build passed. No existing provider backend files were changed.



1. Complete the concrete epoch/Host owners behind the explicit managed `IMarketDataApi` facade and all native query/result paths;

   catalog hydration, option risk/context owners, preparation and snapshots must

   be integrated rather than replaced with unsupported/default results.

2. Complete reference acquisition/calendar/curve handling and historical durable

   staging/file-reader/checkpoint integration. Owned batch transport alone does

   not satisfy those full application operations.

3. Map native authoritative/derived outputs into existing publications and verify

   actor/trade-plan recovery evidence, separate from native transport readiness.

4. Wire physical ownership to the existing durable lease/restore authority,

   including current independent/separate connection modes where requested.

5. Add and qualify Host selection/composition, preserving Current as the default,

   then complete full differential, performance, live UI/open-close and packaging

   gates. The new backend must not be advertised as production-ready before these.



### Native option recovery continuation



The typed managed facade, native scoped controls and source-exact catalog bridge

exist. Native option selection/risk scheduling is now connected to provider quote

ingress and physical-source reset/retirement. Its tracked supervisor reports

independent processing faults into dataset recovery; only new physical-source

and pricing evidence clears that incident. Repeated numerical input faults do not

renew its two-minute deadline or repeatedly reconnect the gateway. The native

options owner accepts qualified contexts before connecting and joins pricing

alongside provider cleanup.



These are implementation components, not completed public API acceptance. Next:

carry the qualified contexts and immutable calculated caches across the DLL,

complete atomic reference refresh and durable per-trade evidence, implement the

concrete epoch/Host services and existing event publications, then finish remaining

reference/history, durable restore and full differential/live/packaging gates.

Keep the existing backend and its default registration unchanged throughout.



### Native publication and epoch-resource continuation



Completed component additions now include source-frozen trade context/evidence

delivery, the single event-driven receiver, official USTreasury acquisition,

futures event assembly and `NativeEpochResources` data-plane composition. ES and

VX wire fixtures match the unchanged C# event packets, normalizing independent

generated GUIDs only. The epoch-resource fixture performs real localhost HTTP

catalog bootstrap, SDK futures delivery, stable reader access and joined cleanup;

it also exercises stalled bootstrap under the original remaining deadline.



Stable option handles follow context refresh and physical replacement, lose

access after monitoring release/epoch retirement, and retain acknowledged trade

evidence across ownership-only lease changes. Cleanup components accept the

epoch's remaining deadline rather than starting a new two-minute timeout.

Uncooperative callback tests verify deadline expiry and no renewal on a second

shutdown call. These are offline component/FFI gates; see

`TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md` for details.



The public native epoch interface adapter/factory, concrete Host operations,

remaining FinancialModelingPrep/history/durable integration, production recovery

observers and optional selector remain to be completed. No public API operation

is promoted to accepted merely because its underlying component exists.





## Resolved integration blocker: API recovery assumed OS process replacement



The native Host can start, serve the existing NATS API and stop cleanly without

provider child processes. Before the approved change below, recovery was blocked by a consumer outside

`IMarketDataApi` implementation: `TomasAI.IFM.Application.Api.Server.Core/Actors/Recovery/OptionPipelineRecoveryRuntime.cs`,

lines 126?135. After `RecoverMarketDataAsync` succeeds, that caller rejects the

replacement unless its Options `ProcessId` is positive and differs from the old PID.

A correct Tokio subtree replacement retains the Host PID. Reporting a synthetic

PID, restarting the Host/futures, or falsely declaring recovery in progress would

not satisfy the approved architecture.



The existing caller can join a recovery that Host has already started, without the

PID check. That branch does not cover API actor/mailbox failures while Host remains

healthy, so it does not remove this blocker. Server Manager's separate PID checks

for an actual API process restart remain appropriate and must stay unchanged.



### Approved bounded compatibility change (2026-10-27)



1. Add explicit, backward-compatible backend ownership capability to the backend

   status contract (default: process ownership, preserving current C# semantics).

2. Keep the current PID replacement validation for the C# process backend.

3. For the Rust backend, verify actual native option-subtree retirement/join,

   replacement logical owner/generation, restored subscriptions and fresh progress.

   The existing cleanup flag and owner/generation fields carry the result; a

   capability identifies which lifecycle proof the API must validate.

4. Keep the shared original two-minute deadline, existing actor verification and

   terminal UI notification. No domain actor or UI changes are required.

5. Test both implementations: current C# same-PID rejection must still fail;

   verified Tokio replacement with unchanged Host PID must pass; missing join,

   unchanged generation, stale completion and incomplete restoration must fail.

   Test old/new MessagePack status shapes because the added capability is optional.



This requires an exception to the earlier requirement that callers outside the API

implementation remain unchanged. The user subsequently approved this exception;

the caller and additive contract change are now implemented and tested.

The caller itself is not in the 729-file frozen baseline; the shared backend DTO

is in the preserved backend source set and any additive change needs an explicit,

reviewed preservation exception. The current backend/default deployment remain

unchanged. Public operation acceptance remains open; basic startup/component gates

must not be mistaken for end-to-end recovery acceptance.



### Remaining work following that decision



- Complete logical option-owner/subtree replacement and reference/lease restoration,

  including expiry and failed-supervisor cases, without disturbing futures.

- Complete real-store, API actor and terminal UI integration acceptance.

- Finish all public operation differential tests, documented differences,

  performance/latency/memory gates and opt-in build/deployment packaging.

- Re-run the complete gate suite after those integrations. No production selector

  is enabled by the existing component or isolated Host executable tests.





### Compatibility decision implementation evidence (2026-10-27)



User authorized the narrow caller/contract exception. Additive status key 8

selects process or Tokio dataset-owner proof; absent values select process.

Current C# PID checks and Server Manager API process checks remain intact.

Fourteen proof/serialization tests passed. Original frozen source hashes remain

mandatory after reversing only the exact approved additive contract declaration.

A real localhost DLL test passed whole option-owner join/replacement, failure

mid-requalification, retained restore intent, retry, new logical/physical IDs,

old-source fencing, stable reader restoration and socket disposal. Full concrete

Host/store/actor acceptance and deployment selection remain pending.



### Additional live integration constraint found on 2026-10-27 (approved and implemented)



The opt-in native Host and updated API/eGui successfully start with real ES/VX

records, current analytics and background option scopes. Live recovery then

exposes a separate caller restriction at `OptionPipelineRecoveryRuntime.cs:137`:

after Host proves recovery, the caller unconditionally fails when there are no

live positions. The actual error is “No live position evidence exists to verify

end-to-end option recovery.” This is outside the approved changed-PID proof

exception. Do not fabricate a live position or weaken the four-leg/plan check for

loaded trades to bypass it.



Proposed narrow extension, requiring user approval before caller edits:



1. For the explicit Tokio backend only, classify the no-live-position workload

   as background option scopes or clean idle ownership.

2. Require completed native replacement/cleanup and restored ownership. For

   background scopes, require current-generation qualified cache/publication

   evidence; absence of a trade plan is not failure when no trade exists.

3. Preserve the original deadline, terminal UI error, and all existing live-trade

   four-leg/newer-plan/publication/mailbox checks. Preserve current C# behavior.

4. Test native idle, active background scopes, incomplete background evidence,

   stale publication, failed cleanup and live-trade regression cases. Run the

   actual API-supervised background-owner test afterward.



Independent backend corrections continue while that decision is pending:

retain completed same-operation recovery results so API retries do not reset a

successfully restored owner; classify explicitly missing coverage inputs as

not-ready data rather than an unknown processing exception; keep invalid policy,

definition conflicts and unexpected exceptions unhealthy.



### Native workload exception implementation evidence (2026-10-27)



User approved the native-only idle/background compatibility change. Optional key 7

on the read snapshot carries actual scope readiness and replacement state. Missing,

stale or incomplete evidence cannot pass. All subscriptions must be ready and

priced background work requires a current qualified cache publication. Existing

preferred-expiry policy leaves unused fallback scopes unpublished; those do not

require invented candidate coverage. Enabled trades cannot bypass the existing

four-leg/risk/new-plan/publication proof. C# process behavior remains unchanged.



57 focused tests pass, covering both MessagePack reader directions, idle/background/

fallback/underlying cases, invalid and stale proof, original 119/120-second deadline

boundaries with an injected clock, and previous loaded-trade/PID checks. Concrete

native Host/PostgreSQL recovery also passes using actual native idle evidence.

Frozen-source hashes remain unchanged; the integrity guard reverses only the exact

two approved additive contract fragments.





### 2026-10-27 native reference ownership continuation



Native typed convention/calendar/Treasury preparation, exact context/manifest

encoding and semantic identities are implemented and compared with independent

current C# oracles. A native reviewed scope retains reconstruction blueprints;

its tracked reference refresh owns HTTP/cancellation and restores fresh native

pricing/source proof without managed preparation callbacks. Reference and pricing

share the original incident deadline. Scope expiry cancellation bypasses unrelated

provider admission. See the current native ownership section in

[implementation status](../../TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md)

for evidence and remaining integration gates. This is not an active Host cutover.





### Revised native ownership implementation checkpoint



Native full trade evidence, actual ingress normalization, cached risk/quote actor

mapping, bounded durable handoff and owned NATS receipt/publication are now

component-qualified against independent current C# oracles. Native root-service

tracking and an event-driven dataset recovery authority implement the single

original deadline and stale-completion fencing. Native cancellation does not

await copied-data storage consumers. See the appended native ownership section

in `docs/IMPLEMENTATION-STATUS.md` for exact scope and evidence.



This checkpoint does not satisfy the final architecture: the persistent desired

intent/resource reconstruction controller, direct native option event task wiring,

thin command/query facade and selected Host registration still require completion.

No production cutover or full operation acceptance is asserted by these results.





### Native resource-group and prepared-cache implementation checkpoint



- Concrete futures/options native factories now run beneath the persistent Rust

  supervisor. The actual SDK/TCP options integration rebuilds the entire resource

  group and verifies a background chain using direct native cache publication.

- Native selection/composition projection and coherent capture are compared with

  the actual C# backend: eight context variants, selection/missing/full-risk paths,

  exact wire encodings and semantic identities. Additional offline boundary tests

  reject incomplete scopes, changed generations, invalid/stale quotes and canceled

  requests. Query capture uses the original caller budget, not an extra recovery timer.

- Quote and acknowledged trade events share a single native ordered actor writer.

  Prepared-cache chunks have a separate bounded latest-wins writer. A stalled

  broker flush does not block cache submission or dataset cancellation/join.

- The real C# assembler accepts a native 2,048-contract publication in five chunks

  and rejects missing/corrupt/mismatched frames. Native cache Host content stays

  plain MessagePack, with LZ4 compression only on the NATS frame.

- Completed failed reference-task joins are consumed before reporting their error;

  cleanup retry cannot poll a completed failed JoinHandle a second time.



Still required before completion: persistent native business claims/scope union,

complete discovery/ranking/cache policy, diagnostic and terminal UI integration,

all-command thin FFI/Host selection, aggregate regression and actual live acceptance.

The current managed prototype has not been silently cut over. Milestone percentages

are implementation estimates and do not replace operation-matrix acceptance.





### Subsequent native owner integration checkpoint



Native business claims, exact expiry and desired-scope union now run outside the

replaceable options resource group. Native owner command/query ABI and thin

managed marshaling are implemented and tested through the actual DLL, with

mutual exclusion against the older prototype owner. Numeric state queries reuse

the qualified 656-byte layout and retain data only. Futures can start without an

option catalog. Terminal failure journals survive retry/stop and require display

confirmation before acknowledgment.



Provider diagnostic delivery now starts before feed readiness, preserves original

worker identity and unknown codes, and uses hash-qualified external log acceptance.

The separate passive Host logger owns copied data only. Log saturation/retry cannot

pin a provider, prevent cancellation/join, extend a dataset recovery episode, or

serve as financial publication proof. Pending unconfirmed logs survive replacement;

bounded ring losses are counted explicitly. Actual C# wire/hash and SDK resource

integration checks pass.



The final facade/selected Host cutover, remaining native discovery/ranking/query

projections, terminal UI confirmation path and full acceptance matrix remain open.

Do not infer production activation or whole-plan completion from these component

milestones; continue implementation and verification without pausing at them.





### Implementation checkpoint: native bootstrap and policies (2026-10-27)



Overall estimate is 90%; this checkpoint is not final acceptance. The native

kernel now performs current-futures discovery inside its owned setup task,

strategy universe/shortlist planning, full price/Greek query construction and

prepared snapshot provenance. Native paged raw-reference lookup has a passive

C# database endpoint which holds copied facts, never provider leases or contexts.

Options setup can acquire those facts under its original native recovery deadline

and reuse immutable facts on reset. A real 120-second futures-bootstrap failure

reached terminal notification and joined in 120.02 seconds.



The active opt-in Host facade still contains the older managed orchestration and

must be routed to the complete native owner before selection/deployment. Do not

mark the operation matrix qualified based on component parity alone. Continue

through remaining routing, retention accounting, regression and live acceptance.





### Native readiness, reference-stall and current-contract checkpoint ? 90%



The overall estimate remains 90%; registered facade/Host orchestration cutover is

still outstanding and no production selection or full acceptance is claimed.



- An actual options raw-reference responder stall reached one terminal failure at

  the original 120-second deadline (120.04 seconds), joined the native tasks, and

  left the sibling dataset independent. After initial successful reference setup,

  repeated SDK/NATS dataset rebuilds reused native immutable facts while that

  external responder was stalled, with no additional database requests.

- Native event-driven readiness observation is exposed as an asynchronous FFI

  command. Cancelling the observer does not withdraw accepted feed intent. The

  native dataset's original deadline remains authoritative.

- Explicit bounded startup/reconnect queries now return copied current event

  envelopes, and the exact 32-key futures contract DTOs selected by dataset-owned

  bootstrap. This is a command/query catch-up facility, not an FFI streaming path.

  Replacement/failure fences contract queries; no second C# provider lookup is

  required. Actual SDK rebuild retained the models without retaining old tasks.

- Existing C# lossless legacy/exact strike translation was independently exported

  for 57,848 cases. Native results now match all cases, including halfway float

  formatting, scale preservation, conflicting exact values and invalid inputs.

  This uses shortest ties-to-even formatting, distinct from the existing

  fifteen-significant-digit double-to-decimal ranking conversion (8,373 cases).

- Actual DLL/C# owner-only and owner-inputs gates passed with awaited disposal.

  Unreachable input fixtures verify command acceptance only, not readiness.



Latest focused evidence: Native-options-reference-real120.log,

Native-contract-directory-sdk.log, Native-exact-strike-tests.log,

Native-contract-directory-clr-abi.log, Native-owner-current-inputs.log in

`.artifacts/rust-marketdata-offline`.





### Raw-input ownership and monitored-command checkpoint ? 92%



The estimate remains 92%. This checkpoint qualifies the native kernel and thin

command adapters; it does not claim that the registered Host/facade has cut over.



- Native public contract projections match the original C# catalog for configured

  futures/options and exact/parent/root/range/empty option queries, including full

  wire DTOs and semantic digests. Forty-eight independently exported input modes

  also cover invalid metadata and mapping failures.

- Raw reviewed option input is the complete existing 45-field read model. Ninety

  independently exported C# cases match native raw encoding, qualification error

  order and derived conventions, including non-UTC offsets and invalid strikes.

- Raw-only catalog commands contain copied contracts, gateway/broker endpoints and

  global policies. Rust derives calendars, underlyings, physical registrations and

  plans. Missing immutable references are staged through bounded data-only IO;

  dataset setup remains under the native cancellation/deadline authority.

- Actual DLL commands preserve a first trade while a second contract is merged.

  Single-call monitored acquisition accepts stable workflow/leg/contract intent;

  repeated acquisition/release and sharing two owners pass actual CLR/DLL tests.

  The SDK/NATS integration also proves shared monitoring and release do not restart

  the healthy provider, invalid commands preserve intent, and detached immutable

  facts can be reused while the database responder is stalled.

- Native prepared-cache queries match 376 C# request/status outcomes. All contract,

  calendar, price, Greek and eligibility decisions stay native; C# only marshals.

- Retired evidence no longer silently replaces the preceding outbox. Up to four

  retired directories / 64 MiB are retained separately from the 16 MiB active

  outbox, with exact evicted generation/record/byte counters exposed through a

  fixed 40-byte query. Old provider trade prints are not relabelled or replayed

  into new actor generations. The bounded retention/acknowledgment tests pass.

- Thin monitored-feed and session adapters contain no lease registry, renewal

  loop, reference preparation or independent recovery timeout. Native readiness

  is observed by completion notification; cancellation cannot erase accepted work.



Remaining implementation: replace the registered facade/Host managed epoch and

lease orchestration, integrate native automatic strategy discovery/planning and

scope demand, route complete status/current/recovery through the owner, then run

full parity/regression/live acceptance. The operation matrix remains unqualified.

The original C# backend/default and running user stack have not been changed.





### Native public control and complete catch-up checkpoint ? 94%



- The actual native public owner (not only the lower-level supervisor) passes

  loopback SDK/NATS recovery: successful source replacement, independent quiet

  sibling, complete source/manifest/model/current catch-up, stable old copied data,

  stale/repeated request idempotence and wrong-date rejection without another reset.

- The complete public backend-status DTO passed through the actual CLR/DLL

  interface. Idle health is distinct from running demand; the real PID and native

  API clock are reported. Native status revisions track state and RPC readiness.

- Native startup accepts raw root demands in one command and validates every

  dataset before acceptance. Rust constructs all three existing schemas. The

  actual CLR/DLL test proves an invalid second lane cannot partially start the first.

- Raw background policy/definition IO has happy, storage-error and cancellation

  integration coverage. Native policy range planning and complete source bounds

  are qualified; automatic strategy-loop/claim integration remains outstanding.

- Readiness observers now rely on the dataset's authoritative recovery deadline,

  removing the competing outer timer that could race terminal result delivery.

- The full regression checkpoint passed 290 tests / zero failures / nine default

  exclusions before the latest public-control additions. Focused tests cover the

  newer additions; a final whole-source regression is still required.



Still required: finish native automatic background planning/claims, replace the

registered managed-epoch facade/Host routes, remove resource orchestration from

that composition, and complete full parity and live acceptance. No production

selection, default backend change, running-stack change or commit has been made.





### Continued ownership qualification checkpoint (approximately 95%)



The public native owner now passes a real 120-second terminal-result/NATS error

notification test (120.02 seconds), ordinary SDK stop/restart, complete raw scope

commands over the actual CLR/DLL boundary, atomic invalid-command rollback and

native-clock scope expiry. Native temporary underlying lookup is now available

beneath the options dataset parent and original deadline; it cancels/joins before

returning detached prices. Its midpoint calculations match 105 actual CLR decimal

bit patterns. The shared native source-plan and reference-definition builders are

used by explicit commands and are ready for background discovery integration.



The implementation remains in progress: finish native automatic background

planning/claims, replace the prototype registered facade/Host orchestration, and

run final operation/parity/architecture/integration acceptance. No default backend

or running application has been switched by this checkpoint.





### 2026-10-27: native background maintenance checkpoint (87% estimate)



The earlier 95% estimates understated the remaining Host/facade work. The

revised overall estimate is 87%; this is an engineering estimate, not acceptance.

No new backend selector, deployment or default launch change has been made.



Completed and tested:

- Native published-policy discovery, exact underlying acquisition, coverage plan,

  complete subscription union, contexts and ranked lookup publication run below

  the options dataset's native resource parent.

- Empty policies prove idle without opening a Databento session. A populated

  policy starts native sources. Recovery reuses immutable reconstruction facts

  when the external database responder is stalled; no managed lease is retained.

- The actual SDK maintenance test preserved the physical source for unchanged

  policies and retired it to idle when the final published policy was removed.

- Native catch-up lists background lookup ownership as well as monitored scopes.

- Publication readiness now requires evidence from every monitored instrument,

  and remains independent of optional lookups with no executable candidates.



In progress (not yet accepted): coverage refresh with current qualified IV,

updated ABI/managed qualification, complete thin facade and Host registration

cutover, truthful background workload recovery proof, final operation-by-operation

parity and integrated actor/storage/UI qualification. The registered prototype

still uses managed epoch/qualification services and must not be represented as

the completed target architecture.



Evidence: Native-background-idle-and-cached-recovery-sdk.log,

Native-background-published-policy-and-recovery-sdk.log,

Native-background-refresh-sdk-regression.log,

Native-background-refresh-maintenance-sdk.log. The maintenance test will be rerun

with the latest IV/replan additions before acceptance.



## Native ownership continuation ? canonical monitored demand (2026-10-27)



Progress estimate: **89%**, approximate engineering estimate, not acceptance percentage.

The complete-owner facade/Host registration cutover is still pending; existing launch

selection and legacy backend remain unchanged.



Completed and qualified:

- Canonical option demand is accepted in Rust before raw database lookup. C# sends

  copied identity/configuration only; lookup/staging/context/source setup execute

  under the dataset parent and original recovery deadline.

- Native resolved monitored membership drives ticker/stream queries. Desired

  canonical scopes appear in version-1 native resource evidence.

- SDK integration proves first-use lookup, native reconstruction while raw storage

  is stalled, original sibling preservation, explicit release, and cancellation of

  a stalled first lookup without a late result resurrecting stopped work.

- Canonical metadata lock failures propagate; they are not silently treated as

  cache misses. Stable owner conflicts and bounded membership are explicit.

- Host build: zero warnings and errors after the canonical command change.



In progress (not accepted yet): compact 100,000-row background-source acquisition

using immutable 256-row pages, bounded copied-byte storage, native compact typed

coverage records, and full raw-model acquisition only after selection. Source

capacity, corruption, cancellation and maximum-size tests are being qualified.

Remaining: concrete complete-owner Host operations/DI cutover, operation-by-operation

parity, final actual-DLL interop/regression, and acceptance of the selected stack.



## Compact source qualification ? 90% estimate (2026-10-27)



Native coverage source acceptance: **5 passed, 0 failed**, including the complete

100,000-contract limit, zero/255/256/257/4,097-row boundaries, oversized declared

count, duplicate identities, bad full-source digest, incomplete final page and

cancellation during a stalled page. Latest maximum-size generated-source run

completed in 16.09 seconds (offline debug build; not a live-provider latency claim).



The actual C# passive fact responder also passed a complete 100,000-row transfer:

24 raw fields per compact row, immutable 256-row pages, aggregate SHA-256 integrity,

empty/partial boundaries, expired/invalid pages, oversized/concurrent/cancelled

capture, and no native runtime/resource reference. Full pricing models remain

limited to the selected <=2,048 contracts. Each raw response retains its existing

4 MiB bound; compact snapshot retention is capped at two snapshots / 64 MiB total,

plus one bounded capture. Rust retains compact typed coverage records, not the

full 45-field graph for all source contracts. DBN fixed-buffer decoding is unchanged.



All-target Clippy with warnings denied passed after these changes. Host build

passed with zero warnings/errors. Full native regression is being rerun. This

milestone does not mark the remaining Host cutover or live acceptance complete.





## Native discovery qualification and composition readiness (2026-10-27)



Engineering completion estimate: **92%**. This is not an acceptance percentage.

The complete-owner Host registration is still pending; the existing backend and

launch selection remain unchanged. There is no blocker at this milestone.



- Native discovery accepts copied normalized candidates, resolves exact immutable

  mapping versions, prepares pricing contexts, owns subscriptions and rebuilds

  the source without managed discovery leases or renewal loops.

- SDK tests passed both complete discovery/recovery and incomplete, duplicate and

  wrong-version rejection. Known business rejection does not stop sibling feeds.

- Actual C# -> DLL -> SDK/raw-reference/Treasury/NATS integration passed admission,

  coherent capture, cached reconstruction with fresh contexts, release and join.

  Evidence: Native-owned-discovery-managed-sdk.log (one test, zero failures).

- Thirty-four of thirty-nine Host operations have complete-owner adapters. These

  adapters are not registered yet. Remaining operations: chain evaluation, cached

  and uncached composition preparation, committed subscription reconciliation and

  committed release. Final full-operation parity and selected-stack acceptance

  remain outstanding.

- Composition capture now has an optional native event-driven quote-readiness

  wait. It subscribes before reading, retries only known quote readiness misses,

  observes source retirement directly and shares the original absolute request

  deadline. Neither notifications nor retries extend dataset recovery. Five

  deterministic tests passed: immediate outcomes, both event sources/lost-wakeup

  boundaries, no periodic retry, exact 120-second expiry, caller cancellation,

  retired/closed source, zero budget and stalled calculation cancellation.

  Evidence: Native-event-capture-wait.log. Integration/regression is in progress.





## Native composition ownership milestone (2026-10-28)



Engineering completion estimate: **94%**. No blocker found; continue through

remaining operations/cutover/acceptance. **36 of 39** Host operations have adapters;

these are still not the registered complete-owner Host implementation. Browser

operation, committed ownership reconciliation and committed release remain.



- Both cached and uncached composition preparation now have complete-owner

  adapters. The existing immutable business preparation/hash store remains the

  financial acceptance boundary. Native readiness uses observation events for

  options and futures, not managed polling or a shorter preparation timer.

- A stable preparation business owner derives from the exact workflow/revision/

  input-hash key. Native accepted intent survives physical replacement. Prepared

  snapshots remain immutable and are never silently recaptured on key replay.

- A data-only business plan writer stores reconstruction metadata after native

  qualification and binds its sealed plan name through a native alias command.

  It retains no resource handle, renewal state, provider/context preparation

  service, semaphore or recovery clock. Delayed/uncertain persistence does not

  let an acquisition observer withdraw a newer native desired activation.

- Actual **C# -> DLL -> SDK/Treasury/NATS/raw-reference** qualification passed with

  the **C# preparation commit deliberately blocked**. Native options cancellation,

  join and source reconstruction completed before the C# store was released.

  The test then validated the existing public preparation/hash contract, immutable

  replay without another commit, retained plan name, fresh recovered contexts,

  explicit release and joined shutdown. It observed exactly two SDK source

  connections (initial plus one physical recovery), with immutable reference

  facts reused. Evidence: Native-stalled-business-commit-sdk.log (1 passed).

- Native alias SDK tests passed stale/invalid identity rejection, idempotence,

  unchanged physical source during name binding and retained name through actual

  recovery. Boundary tests also passed shared accepted membership, conflicting

  immutable mapping rejection, released-owner name reuse and 4,096-owner churn

  without copied-alias accumulation. Later alias comparison performance changes

  are being requalified; no acceptance is inferred solely from compilation.



Current open review items: explicitly qualify complete-empty preparation's native

Host business-generation difference; qualify future-only readiness through the

actual adapter; review concurrent failure/release ordering for preparation; finish

native browse lifecycle; transfer committed subscription realization to native

ownership; remove prototype registrations at cutover; rerun the full operation

matrix and selected-stack integrated acceptance.





## Complete native ownership integration checkpoint (2026-10-28)



Engineering completion estimate: **95%**, not acceptance completion. Continue

without milestone pauses. No blocker is currently identified. **38 of 39** Host

operations have complete-owner adapters; these adapters are **not registered**

until native browsing and the integrated dependency graph are qualified.



Implemented since the preceding checkpoint:



- Rust reads a bounded, versioned committed business journal and immutable

  reconstruction recipes through passive copied-fact responders. The journal

  directory, owner membership, selected option monitoring, fixed future routes,

  conditional delivery and physical reconstruction are native-owned. Source

  snapshots are bounded to 32 MiB, transferred in 32 KiB chunks over bounded raw

  pages; complete byte hashes and monotonic authority watermarks are verified.

- A physical recovery reuses accepted immutable journal facts and acknowledged

  business delivery. SDK integration passed reconstruction while all subsequent

  managed journal/reference requests were blocked. Fixed futures dependencies

  are added/retired through their native dataset while ordinary demand survives.

- Committed release/reconcile use the existing retained-result ABI. Feature bit

  0x8000 advertises these commands (complete capability mask 0xffff). The C#

  adapter copies commands and results; it owns no physical leases or renewal.

- Reference calendars/publication/conversion recipes are copied exactly from

  accepted requests/plans. Native parsing and oracle tests preserve the existing

  context fields. Scoped frozen calendars cannot be overwritten by a later raw

  database read. Conflicting recipes for one shared physical instrument are

  rejected explicitly, rather than silently substituting another pricing basis.

- Selected full-model metadata is pruned to current desired membership. The

  4,096-window churn test passed without accumulating historical pricing models.

- Provisional workflow demand has a native monotonic lifetime, capped at 120

  seconds from its explicit deadline. Identical retries retain the first expiry.

  This is temporary preparation ownership, not an inner datafeed recovery timer.

  Committed and live-trade owners do not acquire this temporary expiry.

- Automatic expiry and internal resource replacement cannot reopen a failed

  episode. Atomic deadline checks preserve the original recovery deadline and

  terminal UI evidence. Only an explicit command can request another allowance.



Latest verified evidence:



- Native-passive-expiry-boundaries.log: 11 passed, zero failed.

- Native-preparation-expiry-boundaries.log: 7 passed, zero failed.

- Native-committed-reference-current-sdk.log: 2 SDK integration tests passed.

- Native-owner-current-host-build.log: zero warnings and zero errors.

- Native-frozen-reference-parity.log: complete journal, ownership and reference

  parity tests passed. The ownership fixture covers 81 unchanged C# hash cases.

- Native-raw-journal-delivery-clr.log: actual managed raw paging and conditional

  acknowledgment tests passed, including malformed/stale/oversized requests.



Remaining: native browsing lifecycle/result operation; complete-owner Host DI

and lifetime cutover; complete operation parity matrix; actual-DLL regression and

selected-stack acceptance. Latest release/reconcile and stalled-write actual-DLL

qualification is in progress and is not counted as accepted by this checkpoint.

Keep the current backend/default launch selection unchanged.





### Complete-owner integration progress (2026-10-28)



Estimated implementation completion: **98%**; acceptance gates are reported

separately. All 39 Host operations now route through the complete native owner

in the opt-in registration. Managed live epoch/lease/renewal/preparation services

have been removed from that graph. The actual public API/browser DLL integration

has passed, including shared view release and dataset reconstruction. The default

backend selection remains unchanged. Complete regression, production-duration

failure gates and development package/executable qualification continue without

milestone pauses. See the latest native `docs/IMPLEMENTATION-STATUS.md` checkpoint

for evidence and remaining gates; no blocker has been identified.





## Complete implementation / final offline qualification (2026-10-28)



Overall estimate: **99%**, with only real-store/API actor/UI acceptance blocked.

All complete-owner code and registration are implemented. Final full regression:

**385 passed, zero failed**. All **13 explicit integration/production-duration

checks passed**; the remaining ignored fixture case is regeneration only. The

optimized release DLL passed actual C# discovery/browser/preparation and catalog

parity, and its isolated Host passed executable startup/readiness/status/clean

shutdown with no dataset children. Strict all-target Clippy passed.



Final evidence, corrected defects, package paths, explained differences and the

recorded nonreproduced fixture failure are in

`TomasAI.IFM.Application.MarketData.Rust/docs/QUALIFICATION-REPORT.md`.

PostgreSQL, Redis and NATS are currently unavailable locally. The older native

Host still uses its earlier package; the new qualified release was written into

`.artifacts/rust-marketdata-qualification-package/Release` without stopping it.

The standard older native package hit loaded-file locks on an earlier publish

and is not a qualified artifact. A pre-publish running-process guard and explicit

output override now prevent that situation. Default C# selection is unchanged.

Do not mark full plan acceptance complete until actual live stack/UI verification

runs against the selected new package. The user has been asked to restore the

local services; no other implementation blocker remains.





## Mandatory startup and futures baseline gate (2026-10-28)



User-required prerequisite: complete this baseline before loaded-trade, fault

injection or final recovery acceptance. No recovery acceptance is inferred from

Host readiness alone.



1. Start the API, native Host and eGui against the actual configured local stores

   and broker. Record package identity, PIDs, authoritative API ValueDate, startup

   activity outcomes and readiness; no duplicate Host or dataset child processes.

2. Verify current ES and configured VX feeds, including their live futures EOD

   session processing. Observe advancing source timestamps and EOD OHLC/volume;

   distinguish current-session updates from previous-session history.

3. Trace current futures observations through the existing API actors, persisted

   projections and UI event consumers. Cover ITI, EMA, Bollinger, VWAP, TDI,

   MACD/RSI/ADX/ATR and market outlook where configured. A successful query or

   restored historical value alone is insufficient proof of current processing.

4. Observe the eGui UI across multiple updates: current prices/session values,

   current signal timestamps, ITI graph observations, analytics panels and current

   system-console market-outlook messages. ITI direction changes depend on actual

   threshold crossings; prove observations/processing rather than demand an

   artificial market change. Daily baselines may remain fixed by design; verify

   current previews and source provenance separately.

5. Investigate every missing/degraded/stale component and processing exception.

   Record expected market-dependent conditions explicitly. Do not accept silent

   failures or declare the gate passed solely from a green feed-health indicator.

6. Only after backend evidence and actual UI confirmation pass, continue normal

   loaded-trade parity, dataset recovery and terminal-error tests. No injected

   faults or API restart tests during this baseline phase.



Initial observation: startup activities completed and current ES market-outlook

messages reached eGui, but ITI observations were absent and VX/ITI were degraded.

An ITI snapshot read timed out near the controlled session shutdown; causality

requires investigation, not automatic attribution to native transport. Therefore

the baseline is OPEN. The overlapping option-definition range parity correction

passed its actual C# 100000-row paging regression and is packaged before retry.



## Unified dataset availability and recovery correction (2026-10-28)



The inventory found independent snapshot, ownership and reference timers that

could return unavailable or remove demand without a dataset failure. This section

supersedes earlier claims of complete timing-policy acceptance.



### Required behavior



- A required quote/output must become obtainable within 30 monotonic seconds.

  This is an availability allowance, not a maximum exchange quote age. Receiving

  unrelated quotes, heartbeats, retries or API clock updates cannot renew it.

- A known operational failure starts dataset recovery immediately. Required

  output remaining unavailable for 30 seconds reports a named workload failure

  through the same supervisor. Explicit closed/control-only workloads are exempt.

- One original 120-second recovery deadline includes cancellation, actual joins,

  rebuilding, subscription restoration and proof of usable output. No attempt,

  scope change, reference refresh or successful heartbeat grants extra time.

- A failed join retains ownership and cannot certify replacement. The terminal

  failure is retained independently of broker delivery; publication retry cannot

  block dataset retirement. UI delivery requires a functioning notification path.

- Remove the arbitrary one-second internal capture validity overlay. Use the

  remaining original operation budget and actual input/context validity. Preserve

  caller/strategy financial qualification policies and source timestamps; do not

  reinterpret 30-second availability as 30-second quote freshness or falsify prices.

- Explicit browser/live demand survives an idle UI and ends on explicit release,

  ValueDate/runtime teardown or a replacement command. Remove automatic 90-second

  browser expiry. Retain bounded directory capacities. Explicitly ephemeral

  preparation claims remain expiring and never represent committed live intent.

- Maintenance cadences and retry pacing remain scheduling intervals, not failure

  deadlines. Reference-service request/circuit policies are separate from the

  Databento socket and must propagate required-reference failures to its owner.



### Implementation and qualification



1. Add a bounded per-required-output native availability watchdog with event-driven

   wakeup at the earliest missing-output deadline; integrate monitored option legs

   and required background scope results, including stale/skipped captures.

2. Bound initial source readiness by 30 seconds without replacing the enclosing

   recovery deadline. Empty, explicitly proven no-candidate business results are

   distinct from missing/unqualified market input.

3. Preserve the original incident through availability failure, cleanup and retry;

   remove the capture overlay and automatic browser expiration.

4. Test exact boundaries, silence, unrelated activity, recovery/re-failure,

   control-only demand, multiple missing legs/scopes, cancellation, joining,

   retained terminal notification and explicit release/capacity handling.

5. Run Rust regression and strict Clippy; rebuild the native artifact. Live stack

   and UI acceptance remain subject to the mandatory baseline above. Do not inject

   faults until that baseline is accepted. No API-server restart change is in scope.



### Review corrections discovered during qualification



- A healthy options readiness report could retain its already-completed setup

  deadline for one iteration. Clear that deadline in the same report that proves

  setup, rather than letting a later failure inherit it.

- Report any options processing error before awaiting resource cleanup, preserving

  the same incident deadline through all joins.

- Health-channel closure after a long healthy idle wait must capture a new fault

  deadline at detection. Retaining the pre-wait timer caused immediate failures.

- Maintenance used a newly-created relative sleep after every health event;

  retain one scheduled instant and prioritize an elapsed maintenance wakeup.

- A fully valued scope with no eligible spread is a normal business outcome.

  Publish an explicit empty ranked snapshot instead of silently omitting the

  scope. Inspect later expiry choices before choosing an empty fallback; if any

  choice lacks usable input, do not certify an empty result. Bound an empty

  result by the earliest underlying input/context expiry across evaluated choices.

  This is an explained parity difference: the earlier native path exposed a

  missing scope; the corrected path exposes `NoEligibleCandidate`. Missing quotes,

  failed pricing and expired source data never become empty success.

- Explicit request/strategy age/skew policies remain unchanged. Removing those

  financial contract rules would require a separate contract/policy migration;

  this correction removes silent stalls and the extra internal 1-second/250-ms

  capture restriction without redefining caller trading policies.



### Correction status: implemented and offline qualified



Final evidence: **394 regular Rust tests passed, zero failed; seven actual-duration

gates passed; three actual release-DLL/C# interoperability gates passed; strict

all-target Clippy passed.** The changed quote-wait cancellation fixture also

passed. Facade routing/Host DI qualification and isolated packaged Host startup,

NATS status and joined shutdown passed. Release packaging completed with matching

source/package DLL hashes. Details and explained differences are in

`TomasAI.IFM.Application.MarketData.Rust/docs/QUALIFICATION-REPORT.md`.



The package at `.artifacts/rust-marketdata-qualification-package/Release` is ready

for the mandatory baseline. **Live acceptance is blocked by the previously found

futures trade-session-bar continuity problem and the outstanding signals/eGui

baseline.** No live faults were injected. The current C# backend/default launch

selection remains unchanged and production acceptance is not granted.

## Futures/signals baseline correction — 2026-10-28



Publish every accepted native futures execution with its captured price, size,

source time, epoch and ordinal. Keep query-cache price suppression independent.

Do not relax the bar accumulator's missing-ordinal validation. This deliberately

corrects the C# price-change-only behavior: exact parity would reproduce invalid

bar continuity and missing volume. No existing C# provider change is required.



Acceptance sequence: production Rust-to-C# OHLCV integration and analytics

regression; rebuild the isolated native package; start API/Host/eGui; verify

advancing ES/VX bars, EOD/ITI and dependent signals plus UI delivery. Do not inject

recovery faults until this baseline passes. Record any failure with evidence.



Also separate the API read mirror's snapshot and streaming watermarks. A query

snapshot may overtake queued executions, but must not consume their delivery.

Validate identity/contracts and deduplicate streaming frames independently;

older streaming frames must not overwrite newer cached query values. Verify this

race through the actual Rust wire -> C# read mirror -> bar accumulator boundary.



Baseline follow-up: scope snapshot-version aggregation to filtered stream/type

keys before computing max; install the covering event-log snapshot index through

an additive concurrent migration on large existing databases. Preserve all event

type compatibility and replay semantics. Verify generic prepared plans and empty

snapshot cases against real PostgreSQL with unrelated history.



During native futures replacement retain only copied canonical bindings. Clear

physical query/price access as before; report an unavailable price for a known

contract until the replacement is healthy. Unknown contracts stay unknown.



For required options risk, keep a completed immutable quote/calculation pair

publishable while newer quotes are pending. Never require ingress to become

quiet before a numerical result can commit. Preserve the exact source quote,

physical generation, reference/context identity and existing financial age

qualification. Strict current-quote queries retain `CalculationPending`;

event publication and readiness use the latest valid completed pair. Undefined

quotes, generation replacement, expired context and expired source still fence

publication. Test ingress advancing during numerical work, original sequence

preservation on actual actor-event encoding, deduplication and all retirement

boundaries. This prevents high-rate quote ingress from starving required output.


## In-process Tokio final dataset reset correction — 2026-10-28

This revision replaces terminal cancellation-and-wait with one native final reset.
The existing 120-second allowance remains the soft-recovery window. Its expiry
triggers a final reset inside the persistent Rust dataset supervisor, without a
C# query, command, actor replacement, UI acknowledgement or broker publication.
A fresh maximum 120-second allowance covers final physical retirement and
replacement readiness. It is explicitly a final-reset allowance; it does not
renew the preceding soft episode. A second failure removes/cancels the candidate
and retains a native terminal notification. No automatic endless reset loop is
permitted. A later independent incident after proven health receives a new
allowance. Explicit stop and withdrawn intent must not restart a dataset.

Implementation:

- Concrete resource groups signal parent cancellation before diagnostic locking.
  They provide emergency retirement that aborts/awaits the main Tokio task and
  awaits tracked descendants. Replacement is forbidden without retirement proof.
- Dataset removal fences detached numeric readers without acquiring their locks.
  Partial catalog rollback and committed-capture loading retirement use atomic
  signals. Buffer leases return through a bounded nonblocking queue.
- Keep broker workers owned through actual shutdown/abort/join; stalled durable
  receipts, copied publications and managed consumers cannot authorize or delay
  reset. Drop remains a signal/fence fallback, never a claimed async join.
- Collect cleanup errors and preserve root cause and missing readiness evidence.
- Native backend recovery capability disables C# pipeline inspection, repair,
  duplicate failure relays and dataset-triggered API restart escalation. Retain
  C# process-backend behavior. Rust publishes retained outcomes directly.
- Short synchronous financial calculations and in-memory critical sections are
  not made cancellable by adding `async`. Bound input/work, avoid I/O/await and
  user callbacks under those locks, and qualify worst-case cancellation latency.

Acceptance: offline tests must cover the exact 120-second boundary, unresponsive
async main tasks, final replacement success/failure, no overlapping generations,
retained query/buffer handles, cancellation while catalog/state locks are held,
blackholed Databento and NATS, full queues, stalled external durable receipts,
independent futures lanes, repeated reset without resource growth, and the C#
backend/native-backend supervision split. Complete the offline regression and
isolated package build before restarting a live session. Live acceptance remains
separate; no production acceptance is inferred from synthetic tests.


### In-process correction qualification - 2026-10-28

Implementation and offline qualification are complete for this correction:

- Full native regression: **404 passed, 0 failed, 16 explicitly ignored**, across
  108 test targets. The ignored set contains production-duration and separately
  provisioned bridge qualifications; it is not counted as passed.
- Separately executed actual wall-clock options reference stall: **1 passed**,
  **120.04 seconds**. It verifies the original deadline, cancellation/join and
  sibling isolation. Simulated-time supervisor tests additionally verify final
  rebuild success, final failure, no overlap and rearming after success or an
  explicit retry. These are distinct forms of evidence.
- Managed recovery integration: **45 passed, 0 failed** against an isolated NATS
  instance. Cached remote authority/mirror tests: **5 passed, 0 failed**.
- Strict Clippy (`--all-targets -- -D warnings`) passed. Debug native Host package
  rebuilt successfully; packaged and built DLL hashes match.
- Reviewed preservation checks retain original C# backend source identities,
  allowing only the documented backend-authority compatibility additions.
- Native authority is Unknown until the first accepted remote snapshot; C# must
  not repair datasets during that startup interval. Once native authority is
  known, C# recovery monitors and duplicate notification relays exit. The legacy
  managed fault-injection harness rejects native authority before injection;
  native lifecycle qualification uses Rust-owned fixtures.

Evidence: `.artifacts/rust-baseline-20261028/lifecycle-qualification.json` and its
referenced logs. Package:
`.artifacts/rust-marketdata-lifecycle-package/Debug`.

**Live acceptance is pending.** The currently running stack was not replaced or
faulted during this correction. Restart the rebuilt Host/API package, establish
ES/VX bar continuity plus signals/UI baseline, then qualify real-provider options
recovery and direct UI notification. Offline success does not mark production
accepted. The package manifest deliberately retains `productionAccepted: false`.

The final policy is two stages: up to 120 seconds of soft recovery, then one
final reset with up to 120 seconds for retirement plus replacement readiness.
Failure cleanup cannot grant another automatic restart. Health and a later
explicit retry rearm the allowance; passive membership maintenance does not.


### Reset notification and alert policy - 2026-10-28

At the first native 120-second deadline, the retained failure snapshot records
whether its supervisor will perform the one final full reset. Its UI message names
role/dataset, says "Resetting this dataset now", and includes original cause,
latest error, authoritative ValueDate and stable operation ID. This copied record
survives delayed broker delivery and subsequent successful replacement. A final
failure does not inherit the resetting action or announce another automatic reset.
The existing twenty-field UI event contract is retained. Native reset proceeds
without waiting for publication or UI acknowledgement.

The standalone UI's global dialog is reserved for terminal recovery failures,
explicit dataset-reset escalation and startup-blocking failures. Transient NATS,
console ACK, calendar, refresh, busy-action and controller diagnostics are console
and durable-log warnings. User-operation failures remain in their view. Historical
daily analytics retry status is console-only in both standalone and hybrid clients.
The historical 422 availability error itself still requires separate correction;
changing its presentation does not repair its requested date range.


Alert-policy qualification: 27 native supervisor/recovery/publication tests,
9 standalone notification tests, 2 isolated NATS delivery/audit tests (including
HTTP 503 console-ACK retry and no responders), and 31 managed daily-status tests
passed with zero failures. Strict native Clippy passed. The initial standalone
package copy encountered a running executable, so packaging uses the separate
`.artifacts/fatal-only-rust-ui/app` directory; no running session is terminated.
Evidence is recorded in `.artifacts/rust-baseline-20261028/fatal-only-alert-qualification.json`.


### Provider heartbeat health - 2026-10-28

The Rust live SDK explicitly requests a 30-second heartbeat interval. Every valid
incoming DBN record proves connection activity; Databento suppresses heartbeats
while other records arrive. Forty seconds without a record (interval plus ten
seconds) reports provider failure. Recovery receives its full 120-second budget
at detection, rather than consuming time since application startup. The existing
one final reset and terminal UI notification policy remains in effect.

Transport connectivity is observed separately from financial readiness. A quiet
connection can be healthy while reference/pricing/publication proofs are pending.
No timestamp, quote, price or financial proof is manufactured. Missing/expired
financial outputs remain unavailable to queries; quote absence alone no longer
resets the dataset. Native connection setup uses control acknowledgements while
actual pricing still requires real market data. Temporary underlying acquisition
remains cancellable and monitors the same SDK liveness signal while awaiting data.

Health observers copy only watch-channel state, never cancellation-owning guards,
sockets or dataset task registrations. Explicit provider errors, disconnection,
processing failures and heartbeat silence retain recovery handling. This change
is isolated to the Rust backend and its vendored SDK; the C# provider is unchanged.

Qualification includes a simulated 15-minute quote-free active connection,
first-failure timing, actual SDK heartbeat-only and silent-peer fixtures, existing
option/futures pipeline regressions and strict Clippy. A real application soak
requires restarting with the newly built native package; an older running process
cannot qualify these changes.

Heartbeat qualification: 406 regression tests passed after replacing the
initial network-target result with the final serial run (58 passed, zero failures).
The actual SDK fixtures separately passed: heartbeat-only quote absence for
46.01 seconds with clean cancellation, and silence detection at 40.02 seconds.
The simulated 15-minute quiet-provider test and full-budget-at-detection test pass.
Strict Clippy and development Host packaging pass. Initial parallel fixture races
and their final qualification are recorded, rather than omitted, in
`.artifacts/rust-baseline-20261028/heartbeat-health-qualification.json`.
The package is `.artifacts/rust-marketdata-heartbeat-package/Debug`.
The real 15-minute application soak remains pending restart with these binaries.


### Historical daily warmup availability boundary - 2026-10-28

Daily analytics still requires 200 valid completed sessions within a 365-calendar-
day lookback. It reads and publishes stored coverage before provider acquisition.
When history or the completed-session tail is missing, a new nonbillable operation
`IMarketDataApi.GetHistoricalAvailableEndAsync` delegates through the native facade
to Rust's cancellable `metadata.get_dataset_range` HTTP request. The exact schema
range is required: a newer dataset-wide or minute-schema end is not a substitute
for the `ohlcv-1d` boundary. Missing/empty/reversed/malformed schema coverage fails
explicitly without guessing a date or resetting live datasets.

Warmup intersects completed exchange sessions with that exclusive provider end,
rechecks stored coverage for the bounded window, then downloads only the missing
published tail (or the full bounded year when fewer than 200 sessions exist).
The effective dates determine acquisition identity; a later published range cannot
reuse an immutable completion for the earlier bounded request. Weekends and DST
use the existing market-session calendar. The authoritative API ValueDate remains
unchanged. Existing quote, indicator formulas and price normalization are unchanged.
Legacy providers can return unavailable capability (null) and retain their existing
warmup behavior. No new live-data timeout, recovery gate or process is introduced.

Qualification covers the logged October boundary, DST/weekend dates, advancement
of publication and missing-tail acquisition, schema-specific native HTTP decoding,
invalid responses, cancellation/socket retirement, the actual retained-result FFI,
and nullable availability round trips over isolated real NATS subjects. The public
native facade contract qualification also covers the added operation.

Historical-boundary qualification: 24 managed warmup/loader/NATS tests and 18 native
availability/batch/owned-HTTP tests passed with zero failures. Strict Clippy and
both native facade routing gates passed. The final Host package is
`.artifacts/rust-marketdata-historical-package/Debug`; the running session was not
restarted. Live provider verification requires restarting API and native Host with
these updated assemblies. Evidence:
`.artifacts/rust-baseline-20261028/historical-availability-qualification.json`.

### Recovery audit implementation plan

The next recovery audit changes are specified in
[Market Data Feed Recovery Audit Implementation Plan](MarketData-Feed-Recovery-Audit-Implementation-Plan-v0.1.md).
It preserves native recovery ownership and defines acknowledged per-record function
delivery, the requested `MarketDataFeedRecoveryCommandActor`,
`MarketDataFeedRecoveryStartedEvent` snapshot replay, durable event projection into
`market_data_service.marketdata_recovery_log`, and correlated server/OTLP logging.
This is a proposed plan, not a statement that recovery audit persistence is implemented.
