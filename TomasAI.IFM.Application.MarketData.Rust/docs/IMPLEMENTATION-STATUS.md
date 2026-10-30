# Implementation and lifecycle qualification status

## 2026-10-30: background coverage no longer waits in options startup

Missing underlying quotes are observed by a separate, cancellable native worker.
Monitored option subscriptions resolve without awaiting that worker. Background
planning admits independently priced underlying groups and leaves missing groups
pending; the explicit complete-chain planner keeps its existing failure contract.
Timestamp validation remains mandatory. Previously acquired background contracts
are retained while their underlying price is unavailable, subject to current
source membership. Provider setup failures retry within the optional observer;
quiet market data does not create a dataset recovery deadline.

Availability changes wake coalesced background planning; ordinary price drift
uses the existing maintenance cadence. Idle-to-active coverage carries its
resolved reconstruction recipe across the supervisor handoff.

Limitation: provider subscription membership is still immutable. An actual change
in selected contracts uses the existing controlled physical-session replacement.
This is not an implementation of incremental subscribe/unsubscribe on a live
socket. Live deployment and provider qualification are separate from isolated
native tests.

Date: 2026-10-27

Status: **DBNMETA-001 addressed for live/historical/file metadata paths by bounded streaming.
SDKDNS-001 remains resolved for the explicitly configured async DNS path. Not a
production-accepted backend. Concrete facade and opt-in native Host are implemented;
the current backend remains the default.**

See [streaming qualification](METADATA-STREAMING-QUALIFICATION.md). This supersedes
the historical blocker below. Shared public metadata entry points are bounded, including standard SDK live/history
reads and sync/async dynamic/compressed readers. Low-level push and legacy DBZ
entry points explicitly enforce their bounded capacities.

## Reopened architecture gates - 2026-10-27

The user requires complete native dataset ownership and direct Rust NATS streaming. The current managed control/delivery implementation does **not** satisfy this target. Lease/renewal/context lifetime, epoch/reset/recovery ownership and managed publication scheduling must move into Rust; `RustMarketDataApi` becomes command/query interop only. See the revised [implementation plan](../../Documents/system/MarketData-Rust-FFI-Implementation-Plan-v0.1.md) for helper dispositions, phase 6A and acceptance gates. Existing component/parity passes remain evidence for reuse, not acceptance of the new boundary. No new ownership/publication code or tests were implemented by this documentation revision; production acceptance remains false. Native reset must be demonstrated with C# consumption, database writes and broker sends deliberately stalled, including all three at once. API restart is not native dataset recovery proof. Scylla persistence remains downstream in C#.

## Implemented

### Native ownership correction in progress (2026-10-27)

The new `messaging` modules construct envelopes and publish directly from Rust.
They are not yet wired as the active Host feed path. The scoped async-nats fork
owns and joins its core socket/event tasks; reset cancellation uses neither a
broker flush acknowledgement nor managed callbacks. DNS and TLS setup use the
existing cancellable native resolver and preloaded in-memory trust configuration.
Seven real TCP tests cover publication, withheld broker replies, cancelled INFO
handshake, bounded queued/in-flight bytes, and independent-lane progress.

Twenty-one production C# envelope/subject fixtures pass native comparison and
reverse decoding through the actual C# formatter. LZ4 representation differences
are recorded in `PARITY-DIFFERENCES.md`. Native monotonic lease authority has five
tests, including expiry, stale renewal/release, preserved deadlines and 1,000
generation rotations. Neither that authority nor the publisher has replaced the
managed execution paths yet. The required full ownership, integration and live
acceptance gates remain open; earlier historical passes do not close them.

### Native evidence ownership continuation (2026-10-27)

- Native actual-trade pricing now freezes the exact original reference, underlying,
  price and API time. All 48 independent current C# pricing/evidence results match
  complete wire bytes, including full semantic provenance. Eight context schemas /
  exercise / rollover combinations cover success, absent underlying, empty size,
  stale source, invalid price and unbracketable price. Cancellation escapes to
  native recovery rather than becoming manufactured financial output.
- The native evidence outbox is bounded by item count and encoded bytes. It stores
  the first attempt as copied bytes, original ValueDate/generation and a non-owning
  receipt. Duplicate receive times/generations cannot rewrite it. Conflicting
  economic source content is rejected. Durable receipt is required before actor
  publication; NATS PONG is explicitly insufficient. Late receipts cannot remove
  replacement work. Retained external copies do not hold native feed resources.
- A separate copied-data C# storage endpoint delegates only to the existing durable
  evidence writer. It performs no lease, renewal, reference or native reset work.
  Its queue is bounded to 64 and four IO jobs; overflowing requests remain retained
  by the native outbox for retry rather than blocking shared Host control input.
  This new endpoint is compiled but not registered by active Host composition yet.
- Native evidence request/reply owns its bounded subscription and connection. Its
  opt-in subscription Drop closes the receiver without spawning a detached
  unsubscribe task; the native connection owner explicitly joins socket workers.
- Real TCP checks pass for positive durable receipt, negative storage receipt and
  silent writer. The native spool posts kind-6 evidence only after storage confirms
  the first attempt, then releases staging after broker publication proof. A failed
  write is retried under its original deadline, without a fresh request window.
  Cancellation joins socket/subscription workers while a writer remains silent.
- The actual C# copied-evidence processor passed blocked-writer no-ack, failure,
  cancellation, bounded-input and immutable-source checks. Native request/receipt
  bytes match its exported DTOs. Receipt parsing rejects every truncation, invalid
  schema/UTF-8/hash/outcome, oversized declared lengths and trailing data without
  allocating from untrusted lengths.

The broad regression before the last spool addition passed 230 tests with no
failures across 65 result blocks; the expensive unchanged full metadata parity
case was excluded from that repeat after passing the previous complete gate
(227 tests, zero failures). Six production-duration cases remained explicitly
ignored in the broad runs; the broker/reference 120-second cases passed separately.
The new spool's production-duration test has a separate log. Strict checks must be
rerun after later additions. These components do not close the active-facade gate.

Remaining integration is substantive: wire complete native option quote/cache and
control publication, retain desired intent and coordinate complete dataset rebuild
in Rust, replace the managed facade/orchestration selection, then qualify the full
Host/API/consumer boundary and live recovery. The currently running stack has not
been changed or presented as acceptance of the revised architecture.

### Current qualification corrections (2026-10-27)

- Two actual production-duration tests passed in 120.08 seconds: a silent broker
  withholding PONG and a stalled native reference HTTP request. Each uses the
  original 120-second incident, reaches failure, closes owned resources and does
  not grant another recovery window. Healthy unrelated futures receive real
  fixture heartbeats and remain in their original generation.
- Found and corrected an important broker-proof assumption: async-nats `flush`
  proves local TCP output, not a server PONG. Native publication now uses an owned
  protocol roundtrip. Ordered heartbeat/explicit PING observers prevent a heartbeat
  PONG from completing the wrong proof. Server rejection cannot count as success.
- Seven native TCP publisher tests cover real acknowledgments, withheld PONG,
  cancellation/socket EOF, authentication, malformed protocol sizes/CRLF/control
  lines, server rejection and credential redaction. Broker acknowledgment is still
  not PostgreSQL persistence acknowledgment.
- Provider, reference and pricing status preserve the earliest original deadline.
  Fresh-generation proof is required to clear recovery. Reference recovery cannot
  leave pricing falsely Recovering after an already completed native replacement.
- The actual managed-thread DLL test exposed Windows stack exhaustion in deeply
  nested cold async HTTP preparation. Bounded heap-pinned cold futures corrected
  it; no stack-size increase or blocking worker was introduced. Actual adapter/DLL
  native reviewed preparation and native claims qualification then passed.
- Source-time trades now capture full immutable native reference contexts as well
  as numerical inputs and manifests. Context refresh cannot rewrite prior evidence.
  Stable stale-reference/tenor error identities match the existing pricer, including
  rejecting observations from the future when API-controlled time moves backward.
- Ten independent C# option-source fixtures match exact MessagePack bytes,
  persistence identities, economic content digests and full typed semantic hashes.
  Receive-time/generation changes preserve the source identity; economic changes
  preserve the key but change its content digest, as required by current storage.

These results are component evidence. The active Host/facade still has managed
feed orchestration. Native options publication, persistence acknowledgment,
diagnostic/control publication and the complete native dataset intent coordinator
remain required before revised architecture or live acceptance can pass.

### Native ownership implementation continuation (2026-10-27)

These are new native component/integration results, not deployment or complete
facade acceptance. The running Host has not been switched to this revised path.

- Direct native futures delivery now uses the real pinned Databento SDK, native
  event construction and an owned TCP NATS publisher without stream FFI. Its
  integration test replaces provider resources twice while broker acknowledgements
  are withheld and a downstream copy is retained; the other futures lane progresses.
  Broker workers are tracked/joined, with bounded queued and in-flight bytes.
- All 20 current futures event-content oracle fixtures match exactly; all 21 outer
  envelope/role/kind fixtures pass, including reverse C# formatter decoding. Inner
  event payloads are plain MessagePack; outer envelopes use extension-98 LZ4.
  The publisher flushes at most 32 already-ready frames together, without a timer.
- Native scope claims renew/release without waiting behind provider control I/O.
  Expiry cancels its resources immediately, even while another scope's admission
  is blocked. The strengthened actual SDK/socket test proves the old socket closes
  before the unrelated pending authentication is released.
- Calendar qualification matches 1,728 independently exported C# boundary/DST
  cases and 138 frozen Windows timezone aliases. Exact canonical scalar hashing
  matches 5,075 C# fixtures, including decimal and binary64 boundary values.
- Native full Treasury source/rate qualification matches 21 C# cases, preserving
  exact rate DTO bytes, declared-order curve digests and typed semantic hashes.
- Complete native context preparation matches all eight C# context/manifests and
  semantic digests across schema versions, exercise styles and fractional rollover
  times. The native cache rejects a tenor cutover even before the stored context's
  publication-validity boundary. A complete atomic typed refresh restores it;
  numeric-only refresh cannot remove native reference authority.
- Native HTTP preparation cancellation closes the socket and releases cache-gate
  waiters. Reusing an expired original deadline opens no replacement HTTP request.
- A reviewed native scope now retains its own immutable rebuild inputs, prepares
  its contexts/manifests, opens the SDK feed, calculates valid risk, and performs
  automatic native reference/tenor refresh with fresh physical-source proof.
  Scope release cancels and joins reference/pricing/provider tasks and releases
  retained manifest memory. Its reference and pricing paths share the original
  incident deadline and have one reference reconnect authority.

Focused regression before automatic-refresh integration: 65 tests passed across
11 binaries, zero failures (two production-duration tests excluded by their explicit
ignore attributes). The later native-context/coalesced gate passed 14 tests.
Updated whole-suite and real production-duration results are recorded after the
remaining integration changes; earlier results do not qualify those later changes.

Still open: complete native options evidence/cache event mapping and direct NATS
publication; diagnostics/control/error publication; durable persistence protocol
that transfers copied data without pinning native resources; shared dataset-wide
recovery proof; command/query facade and Host selection that retire the managed
lease/context/delivery orchestration. The new native reviewed-scope path is not
selected by the active Host yet. Native reviewed-scope and claim commands are now exposed and qualified through the actual C# adapter/DLL; that is not an active-facade cutover. No live/full backend acceptance is
claimed by these tests.

### Latest integration continuation (2026-10-27)

**Native workload compatibility approved and implemented (2026-10-27).**
The API accepts explicit native idle/background evidence after joined owner
replacement, retaining C# behavior and every loaded-trade check. Optional key 7
on the read snapshot carries actual scope ownership, ready scopes, replacement
state, generation and API-clock observation. Missing evidence is never idle.
All scopes, including underlying dependencies, must be ready. Priced background
work also needs a fresh nonempty cache publication from an owned current-generation
scope. Existing policy intentionally leaves unused fallback expiries unpublished.
Enabled trades without position evidence cannot use this exception. Stale dates,
identities, local failures or closed mailboxes prevent success. Verification uses
the original 120-second deadline without renewal.

The focused API suite passes **57 tests**, including both MessagePack reader
directions and all prior loaded-trade/PID cases. Actual native Host/PostgreSQL
recovery passes with the concrete idle scope evidence. Release API and native
Host builds succeed with zero warnings/errors. Frozen-source preservation passes;
only the two exact approved additive fragments are reversed before original hashes
are checked. No baseline hash was regenerated.

**Expanded rerun complete:** 54 stages, zero failures, recorded at
`2026-10-27T10:54:44Z`. This reran all normal Rust/managed/NATS/PostgreSQL gates;
the unchanged five production-deadline cases and 1,000-reset soak are recorded in
the earlier 60-stage run below. The rerun exposed a qualification-fixture race:
server header write was mistaken for client receipt before cancellation. The
catalog test now waits for parsed warnings before canceling the stalled body;
warning retention and redaction pass. No provider warning behavior was weakened.
Busy first acquisition and incomplete replacement also pass actual native lease
checks and cannot be reported as idle ownership.

**Live continuation:** user stopped the original stack; opt-in native Manager PID
67204 launched Host PID 66840 and API PID 72364. Host reports RustFFI readiness;
API reports all eight startup activities satisfied, ValueDate 2026-10-27. eGui
launched. The user confirmed the trade open with Live Feed enabled. Happy recovery
operation `35dcd6ac-98bd-42e5-bf0c-645a9def8858` passed. It fired at
2026-10-27T11:01:51.3778041Z after the fixed five-minute delay; the actual API
coordinator returned Healthy after **9.3497014 seconds**. Options owner changed
from `0c35c205-b46c-4bc9-ad79-a140f32d6975` to
`21576bd4-ff48-452b-87b9-8811cf5a97d9`, generation changed, and Host PID 66840 stayed
unchanged. Both futures owners/generations stayed healthy and unchanged. Saved
trade `101.701.1901.1501` remained enabled and current option scopes remained ready.
Because enabled trade intent was present, this could not use the no-position
native exception; the unchanged loaded-trade proof required four fresh legs,
qualified risk, newer complete plan, and independently recorded publication.
Read-only eGui accessibility was truncated after loading the large trade view;
UI rendering is not inferred from that capture.

Forced-deadline operation `f2981515-38bb-4c81-b992-5762d5229703` is armed with the
same fixed 300-second delay. Expected injection about 11:07:58Z, deadline about
11:09:58Z. This Development harness deliberately holds API verification completion
open after fault injection; it qualifies deadline/UI escalation, not a claim that
the real provider became permanently unrecoverable. Terminal/UI acceptance is
pending.


The full expanded offline run completed **60 stages, zero failed**, recorded at
`2026-10-27T07:18:32Z`. The report has since been replaced by the newer
54-stage run; individual production-deadline and soak logs remain available.
This includes all Rust regression/lint/documentation gates, exact reference and
financial/event parity, native underlying dependencies, real PostgreSQL/NATS,
1,000 joined resets and all five actual two-minute timeout cases. Subsequent
managed same-incident idempotency changes were separately rerun through the
concrete Host/native/PostgreSQL integration test; both duplicate terminal-result
handling and a new genuine failure after a prior success pass.

Real Development integration exposed and corrected:

- XCBF's valid publisher pair **105 (XCBF)** / **106 (XOFF)** within one dataset.
  Mixed-publisher SDK records now produce byte-identical C# market/trade/quote/
  statistics events and live routes; unrelated publishers remain rejected.
- Batch download authentication: match the pinned SDK by using the descriptor's
  path on the configured authenticated historical API origin. Descriptor hosts,
  signed queries and double-slash paths cannot redirect credentials. Four batch
  tests pass, including fixed-buffer streaming and invalid/failure cases.
- Additional ES underlying quote dependencies (for example March 2027) needed by
  the published option-cache expiry range. They are bounded option-owned native
  scopes, restored after option-owner replacement, never primary futures routes.
  Initial failed dependencies remain visible rather than becoming healthy idle.
- Stale temporary discovery-lease release now retires the exact restored lease
  instead of blocking the background owner's generation transition. Acquire,
  read, and revision-based business-ownership checks retain generation fencing.
- Repeated requests for one completed incident retain its terminal result; they
  do not repeatedly replace a recovered owner or grant a failed incident another
  deadline. A genuinely new observed failure is still repaired using the original
  caller deadline rather than being hidden behind cached success.
- Known missing underlying quotes log an explicit not-ready coverage condition
  and publish no coverage/price. They retry on normal policy refresh; invalid
  definitions/policies and unknown processing exceptions still report unhealthy.

The actual API reached launch readiness with current ES/VX analytics visible in
eGui, Feed Health Green, current ValueDate **2026-10-27**, and current console
messages. Its live Host loaded `ifm_market_data_native.dll` without the legacy
provider/pricer DLLs. A later background incident recovered natively in about
eight seconds, then the caller's no-position guard caused repeated failures to
the original two-minute deadline. The terminal message was read in eGui through
accessibility: **“The two-minute recovery deadline expired. A task was canceled.”**
This is evidence of notification delivery and of a failed recovery acceptance
case, not a passing end-to-end happy path. UI click geometry was unavailable and
capture retry timed out; loaded-trade acceptance remains unverified.

The approved recovery contract/caller compatibility change is implemented.
The filtered API integration suite passes **30 tests**, including same-PID native
replacement, retained C# PID enforcement, stale request identities, unchanged
generation/owner rejection, cleanup/restoration failures, and old/new status
serialization in both reader directions.

Receiver recovery now recreates an unexpectedly terminated managed delivery
owner after its old loops/callbacks actually join. Health becomes unavailable
immediately on terminal receiver failure. Options remain suspended until native
owner restoration; a newer independent futures fault cannot be cleared by an
older receiver repair. `EpochResourcesBridge` exercises this through real native
SDK records and resumed publications.

`EpochResourcesBridge --host-postgres` composes the concrete Host recovery service,
actual native epoch, actual API replacement validator and an isolated PostgreSQL
recovery store. Same-PID changed-owner/generation recovery clears error evidence
without replacing futures. Dropping only the fixture recovery table forces real
storage failures: attempts continue to the original shortened test deadline,
cleanup is not falsely acknowledged, and explicit retry succeeds after the table
is restored. This idle-workload test does not claim loaded-trade acceptance.

VX historical bootstrap is implemented as one session-signalled background owner.
Ten storage/history/invalid/cost/cancellation cases exactly match the separately
executed current Host oracle. A real quote-only native SDK service test proves
the historical estimate never becomes a live trade or falsifies native health.

The latest 1,000-reset component run measured p50 **31.389 ms**, p99 **32.493 ms**,
maximum **45.072 ms**; one active socket between resets and all sockets joined at
shutdown. After warm-up, process private memory changed from **34,299,904** to
**31,084,544** bytes. Retained managed memory changed from **2,675,632** to
**2,759,072** bytes; managed allocation churn was **62,766,624** bytes. These include
the fixture and CLR/native allocator behavior and are not by themselves a proof
of absence of leaks. Evidence: `.artifacts/rust-marketdata-offline/owner-memory.log`.

An explicit real-provider Release DLL check acquired **ESZ6 / ES20261218** through
the native historical/definition path in **7,965.2 ms**, qualified live trades,
MBP-1 and statistics subscriptions in **777.7 ms**, and qualified a same-process
new-generation reset in **302.6 ms**. Provider acknowledgements and new source
events arrived after reset, followed by joined shutdown. Both configured TCP DNS
servers were reachable. This checks the real provider boundary, not live actor/UI
acceptance. The probe initially used an unqualified raw symbol as a domain ID;
that fixture mistake was corrected to use the provider-qualified canonical ID
and instrument mapping used by the actual epoch.

Release API and separate native Host builds pass with zero build errors/warnings.
The native Release DLL is **6,588,416 bytes**, SHA-256
`ccb670deb6e22e78738d3c236f919fdba2a0f32082e00638b1184d46e4270bd2`.
The release executable smoke test passes NATS/readiness, no fabricated session,
no legacy provider loading, no dataset children and joined shutdown.

The concrete 71 method routes and two properties are now marked implemented in
the operation matrix; full identical-input public-operation acceptance remains
separate. This update supersedes earlier historical continuation notes below.

### Current continuation: native option calculations and owned fault recovery

The native coalesced scheduler now implements timestamp-first option ingress,
separate selection/full-risk sweeps, cached IV with the current >= refresh and
> read-freshness boundaries, fair bounded batches, exact Actual/365 and Actual/360
year fractions, required-risk demand, nullable prices, context refresh fencing
and physical-source retirement. Quote ingress performs no numerical calculation
or per-record allocation. Immutable result reads retain their exact inputs.

Ten stateful tests cover healthy, invalid/stale/missing input, capacity, ordering,
context/source replacement during calculation, cancellation, clock/reader faults,
timer shutdown and fair full sweeps without a duplicated tail batch. The kernels
already have unchanged-C# bit oracles; complete scheduler DTO/digest acceptance
against the current application is still required.

`LiveData` feeds the scheduler directly from copied SDK quotes. A real SDK test
proves generation-fenced risk after reset and shared tracked task/socket disposal.
`PricingTask` owns both native cadence loops and reports unexpected processing
faults through the existing lane recovery state machine even when the socket is
idle. Its original 120-second incident remains active until a newer provider
attempt and fresh required pricing evidence both succeed. A repeated calculation
failure retries calculations without repeatedly reconnecting the provider.
Terminal status is separately retained/signalled; it is not a fabricated provider
ErrorMsg and does not by itself establish Host/UI notification acceptance.

The options supervisor now accepts a complete qualified pricing configuration
before provider ingress, validates logical-generation and physical-route identity,
and includes pricing tasks in release/rollback/shutdown ownership. Its new native
priced-scope integration test has passed. Managed fixed-frame context/cache transport
and atomic reference refresh are implemented and compared with the current C# DTOs
and digests. Complete option event orchestration and real durable trade-evidence
writer integration remain open; public option API operations are not marked complete.

One runtime-wide DNS semaphore now bounds all live scopes, flat lanes and history
to sixteen admitted lookups. A real TCP test proves queued cancellation opens no
extra sockets and releases capacity for a fresh configuration. Physical intent
comparison includes DNS endpoints; a reused digest cannot hide changed routes.

The reproducible offline runner passed all eighteen stages with 148 native tests
before the latest statistics and output-coalescing extensions. Those extensions
require the subsequent combined rerun recorded in qualification artifacts. Its scoped managed DLL test includes reset/release/replacement,
same-digest changed-input refusal, Unicode names, auth timeout then fresh acquire,
shared diagnostics after release and futures isolation. Subsequent scheduler/SDK
tests are separate evidence until the combined suite is rerun on the final code.

### Current continuation: explicit facade, bootstrap and native quote qualification

The subsequent options ownership integration adds a native supervisor beneath
one logical options lane. It admits at most 64 independent physical scopes,
2,048 total physical registrations, 16 queued controls and 256 nested async
owners. Provider reads never use the control queue. Emergency cancellation
bypasses it and all parent/runtime shutdown proofs include descendant resource
drop. Real SDK tests cover independent reset/release, identical-acquire evidence,
conflicting intent, timeout/cancellation followed by fresh acquisition, control
saturation and shutdown while authentication is stalled. Both futures lanes
remain active. Retained internal readers are retired on parent cancellation;
an interrupted writer cannot prevent later readers from being invalidated.
Supervisor exit has a separate reliable failure observation and lane signal.

Scoped FFI/managed controls and coherent publication reads now connect this
supervisor through the DLL. The bridge requires the expected logical generation;
old releases/stops cannot retire a replacement. A scoped-mode/flat-mode exclusion
prevents duplicate options publishers. The managed SDK integration passes
prepare/install/reuse/reset/release/replace, delayed old-generation release,
Unicode scope names, authentication timeout followed by fresh acquisition,
ordered coherent snapshots, options-only shutdown and futures isolation.
Scoped provider diagnostics share the main runtime's bounded event-driven sink,
so releasing/replacing a scope cannot discard unread messages. Their original
text gains scope/physical-generation correlation; market results are unchanged.
Supervisor/scope failure remains separately readable after diagnostic drain.

Full pricing context/cache ownership, concrete epoch/Host composition and public
API acceptance are still open. These infrastructure operations must not be
described as completed option API operations.

- `RustMarketDataApi` now explicitly routes all 71 methods and both properties.
  Its 39 Host operations require implementations, and its epoch factory is a
  distinct native-only contract. A reflection qualification checks all 73 targets
  and all 40 abstract Host members, including the cache property. This prevents
  accidental default-interface fallthrough; it does not implement the missing
  concrete epoch/Host owners by itself.
- `NativeContractCatalog.CreateAsync` performs exact configured-symbol provider
  acquisition, then bounded atomic hydration under one original bootstrap budget.
  Every retained query is disposed. Parent discovery and exact acquisition share
  the same 65,536-byte source-line decoder. Coverage selection now uses the pinned
  SDK parser and preserves the existing dataset-end fallback. Malformed coverage
  fails before the definition query. Full MessagePack catalog parity passes.
- Stored-definition maturity and live-query maturity remain separate where the
  current implementation differs. A 420-case oracle calls the unchanged CLR
  query mapper; year-zero failure behavior and conflicting expiration/maturity
  dates are retained rather than silently changing contract identities.
- Rust now validates hot quote generation, identity/strike, prices, quantities,
  age, skew and clock lead through a 304-byte request and 16-byte result. Exact
  CLR decimal coefficients/scales cross the ABI explicitly. The managed pricing
  adapter uses this path. All 104 full pricing MessagePack cases and 10,000 direct
  unchanged-C# validator comparisons pass. Application reference/calendar
  qualification and semantic hashing are still retained application policy.
- Replay completion is an explicit ordered control publication, not a synthetic
  provider trade/statistic. Its captured session/VWAP state cannot acquire a later
  live trade's volume. The real SDK replay integration test passes.
- HTTP warnings now wake delivery independently through signal bit 4. Full text,
  endpoint, status and request ID survive success, invalid data, timeout and caller
  cancellation, with credentials redacted. A failed/undersized ABI copy leaves
  both the record and caller outputs intact. Maximum 8,192-byte warning and
  1,024-byte request-ID cases pass; storage remains capped at 256 warnings.
- Options may own exact underlying-futures dependencies within their isolated
  lane. The real SDK test proves resetting/stopping that owner does not reset the
  primary futures reader. This does not complete multiple independent option
  connection modes or option-risk ownership.
- Explicit disposal signals native cancellation before queuing managed cleanup.
  `StopStarvation` proves physical SDK socket closure while the CLR thread pool
  is fully starved. Cleanup still retains memory until joined release is safe.

`qualification/Verify-Offline.ps1` records each offline stage's exit code, elapsed
time and log path. Full Host composition, native option scope/context/snapshot
integration, complete reference/history application paths, publication/actor/UI
acceptance and live parity remain required. The operation matrix deliberately
does not label individual public operations complete from these component gates.

### Current continuation: catalog acquisition and publication boundary

- Atomic native catalog repository and C ABI support bounded streaming hydration,
  unique canonical lookup and revision checks. Failed/cancelled refreshes preserve
  the committed universe. Stale tokens cannot abort a newer refresh.
- Managed-to-DLL qualification passed 444 original C# definition cases and 1,000
  cancelled refreshes followed by successful rehydration.
- Native provider acquisition now hydrates the same repository transaction under
  one original deadline, without subprocesses or a managed provider client.
  Real HTTP tests cover success, malformed source, timeout and cancellation;
  committed state survives each failure and sockets/staging are released.
- Fixed-width publication records now retain coherent market-price, session and
  VWAP state captured by the source-record writer. Managed mapping reuses existing
  public market-price/statistics contracts. This is a publication boundary, not
  yet a claim of completed Host/NATS or option-risk integration.

### Current continuation: native data and pricing components

These components now exist and have differential or integrated evidence:

- Session OHLC/volume/replay accumulator: 1,020 exact C# transitions.
- VWAP source: 2,734 exact transitions including CLR decimal coefficient/scale,
  continuity faults, scale reduction and overflow mutation order.
- Market-price publication cache: 5,000 exact transitions, including tick-size
  suppression with independently advancing accepted trade continuity.
- Native book connects copied observations, raw readers, session/VWAP aggregates
  and publication-price state. Actual SDK integration proves a new attempt retires
  old readers. Replay seeds aggregates without publishing historical live prices.
- Fixed quote-buffer leases preserve storage through retirement, cancel saturated
  waits and reuse the same allocation across 10,000 rent/return cycles.
- Physical ownership deduplicates dependencies, applies bounded atomic changes and
  issues new runtime tokens for every restored generation. Durable expiry/renewal
  remains the existing application's responsibility; no second clock authority.
- Coherent latest-price/risk store: 2,513 C# transitions, generation retirement,
  bounded registration and concurrent-read verification.
- Unified futures option theoretical/Greek/IV engines, including American CRR,
  paid-upfront/futures-style premiums, and both configured European arithmetic
  profiles: 304 cases with exact IEEE results/failure identities.
- Exact C# decimal quote-midpoint preparation: 10,079 conversion cases.
- Source-exact definition parser: 444 valid/invalid cases; bounded catalog.
- Canonical identity parsing and current chain filtering: 1,021 IDs and 48 chain
  selections, including duplicate ordering and decimal precision behavior.
- Caller deadline propagation through lane admission/reset; repeated resets merge
  original deadlines rather than opening another recovery window.
- Owned historical HTTP/1/TLS/DNS, DBN streaming, estimates and raw JSONL
  definitions. Local-server tests cover cancellation, declared body limits,
  redirects, warning/error preservation, UTF-8 fragmentation and coverage dates.
- Additive asynchronous pricing ABI plus managed delivery: 1,520 exact C# outcomes,
  zero-budget rejection and 1,000 create/cancel/dispose cycles. Results retain a
  separate 256-slot reservation until copied, including cancelled unpolled work.
- Application/native pricing adapter: 104 exact MessagePack values/failures/context
  hashes, retaining application qualification and identity rules without invoking
  the old numerical calculator.

Additional completed components in the current continuation:

- All five pricing actions (theoretical, Greeks, IV/Greeks, price/delta, IV-only)
  match both C# engine profiles. American solving reuses one tree buffer; full
  IV/Greek work uses two, verified with allocation counting.
- Native business clock accepts only API anchors, independently from recovery time.
- Dataset lifetime registrations prevent runtime completion before actual lane exit.
- Live start/reset/stop/status/hot reads, source contributions and complete diagnostic
  delivery now cross the distinct DLL and managed bridge. Wake-ups are signalled,
  with no polling. Native tests cover 1,000 real SDK socket resets and managed
  qualification covers 100 real SDK start/cancel/dispose cycles plus ten resets.
- Stable public futures reader proxies match unchanged C# trade/quote snapshots
  through ten resets, preserving API ValueDate, exact timestamps, order counts and
  retirement. Internal generation-bound readers remain separately fenced.
- Explicit activation after terminal failure joins the exited lane before creating
  its replacement; attempt/incident identities continue increasing. It cannot
  merely send reset requests to an exited task. This is native recovery evidence,
  not yet a claim of Host/UI acceptance.
- Source output has separate bounded capacity per lane. Replay completion restores
  session volume and VWAP before native readiness. Saturation enters visible
  recovery; options replacement does not replace futures owners.
- Historical batch submit/list/details/files now use the owned HTTP transport and
  pinned-SDK forms/types. A 64 MiB file is streamed through one heap-owned fixed
  65,536-byte buffer, with exact length/SHA-256 validation. Failure tests cover
  wrong lengths/hash, truncated responses, consumer errors and stalled bodies.
  Cross-origin download URLs never receive the API credential.

The historical client polls its HTTP connection driver in its operation future;
it does not spawn or detach a pooled connection driver. Cancellation owns DNS,
TCP, TLS and HTTP teardown. Original provider definition JSON remains unchanged
because current persisted definition identities hash those exact UTF-8 bytes.

These are qualified components, **not completed public IMarketDataApi operations**.
The managed runtime is still a native operation bridge. The full application
facade, catalog hydration/subscription integration, aggregate/bar publication,
complete reference/pricing-context qualification, Host selection, durable restore,
end-to-end notification and full differential/performance/live gates remain open.
No current backend or Host composition has been changed or selected.

Latest broad regression: 109 passed, zero failed, three explicit wall-clock tests
excluded by default. Subsequent focused cases also verify all three auth text-line
stages against oversize, invalid UTF-8 and truncation. Updated lane/ABI/history
regression and managed DLL qualification are recorded separately; no current
backend production file has been changed.

The historical sections below record prior milestones and their test counts;
they are not a claim that the current full API is complete.

- Separate Rust crate/CDylib under the requested implementation root, with a
  distinct native ABI identity and an independently generated lockfile.
- Frozen normalized hashes for 729 existing source/build inputs, plus all 71
  interface methods and two properties. A Rust test verifies preservation.
- Cancellation/deadline primitive capped at the original 120-second budget.
- Pinned Databento SDK 0.58.0 owned-session qualification using local protocol
  fixtures and fake credentials. No running service/provider state was changed.
- Cancel during stalled authentication, startup metadata, record read and partial
  DBN record; peers observe socket EOF after owner completion.
- Bounded diagnostics cannot delay cancellation; invalid credentials are rejected
  before the SDK can log their contents.
- Independent deadline evidence and explicit tests showing that a cancellation
  request is not proof of a blocked task/thread exiting.
- Prototype bounded snapshot store with current C# version/generation behavior.
- Treasury tenor selection and the rate-conversion numerical core, compared to
  unchanged current C# functions. This is not the full curve/policy/decimal API.
- C# oracle/fixtures, including direct C#-to-new-DLL numerical comparison.
- Integer-handle runtime ABI, bounded operation/completion ownership, tracked task
  exits, signalled delivery, explicit stop/release and an independently built
  managed bridge with pooled completion batches and bounded async disposal.
- Exact trade action/side/condition normalization port, exhaustively compared
  against the unchanged production C# functions.
- Explicit implementation responsibility matrix for all 71 method signatures and
  both properties. Full API qualification remains zero until complete method
  implementations and parity evidence exist; numerical kernels are not API parity.

## Dataset-lane continuation and original blocker (historical)

The owned record boundary, real SDK transport and three logical lane owners are
now implemented as native components. Integration fixtures verify options-only
reset with both futures lanes continuing, original-episode retry, healthy reads
outliving setup deadlines, processing panic/error replacement, explicit workload
readiness, missing acknowledgement/proof failure and reset during stalled auth.
They are not yet exposed through the complete managed facade or production Host.

Resource qualification found a new dependency blocker: an eight-byte DBN metadata
prelude grew the pinned decoder from a 64-KiB initial buffer to 1 MiB before any
metadata body arrived. Source audit found no maximum for this allocation, plus an
unchecked-before-reservation mapping count path. The bounded reproduction confirms
the mechanism; no huge allocation or provider/production failure was injected.

At discovery, full-port acceptance stopped at this boundary as required by phase 1. An isolated
hardened decoder dependency or an owned bounded decoding path is required; smaller
initial buffers, timeouts and catch_unwind do not enforce the missing memory limit.
See [reproduction, source evidence and required resolution](DBNMETA-001.md).

The current increment's default suite passes 44 tests (including the explicit
dependency-gap reproduction), with Rustfmt, Clippy and Rustdoc gates passing.
That green regression run is not acceptance of the decoder's missing memory bound.
The frozen current-backend inputs remain unchanged.

All prior full-API/Host/actor/UI/performance gates remain incomplete. No claim that
these new lane components constitute the complete API is made.

## Runtime and normalization continuation

The default regression suite now passes **33 tests**. Additional ownership tests
cover full completion capacity, duplicate/stale identities, ABI/layout/argument
validation, cancellation/deadline resource drop, tracked task exit, panic
containment, condition-variable wakeup and work after shutdown.

The managed bridge builds with **zero warnings and zero errors**. Its qualification
executable loads the actual new DLL, matches all **1,107 C# corpus results exactly**
through asynchronous completion delivery, rejects expired/pre-cancelled calls and
passes **1,000 create/cancel/dispose cycles**, including concurrent disposal calls.
No current backend service or native library is selected or restarted by this test.

This cross-language test found an expired-budget defect: a newly expired Tokio
timer could still lose to an immediately-ready future. `Deadline::run` now checks
the monotonic instant before polling and again before accepting success. A focused
regression proves expired work is never polled; the managed test also verifies it.

Trade normalization matches the unchanged C# `DatabentoTradeNormalizer` for all
**256 action bytes**, **256 side bytes** and **65,536 header/DBN flag combinations**.
The oracle invokes production internal methods through test-only reflection;
expected outputs are not a second copy of the algorithm.

See [ABI/ownership details](ABI-OWNERSHIP.md), [operation assignments](OperationMatrix.json),
`.artifacts/rust-marketdata-runtime-tests.log` and
`.artifacts/rust-marketdata-managed-runtime-qualification.log`. This is phase-2 foundation plus
the first normalization kernel. Provider transport/data lanes, lease authority,
full application facade, financial/reference/historical closure, Host selection,
live actor/UI verification and performance gates are **not complete**.

After the deadline fix, both explicit 120-second tests were rerun and passed:

| Path | Terminal observation | Cleanup evidence |
|---|---:|---|
| DNS partial-body stall | 120.002291 seconds | Zero exchanges; both peer sockets closed; SDK not entered |
| SDK authentication stall | 120.003037 seconds | Peer SDK socket closed; independent deadline observer completed |

Logs: `.artifacts/rust-marketdata-runtime-dns-deadline.log` and
`.artifacts/rust-marketdata-runtime-sdk-deadline.log`. Thus the current evidence is
**33 default tests plus both explicitly executed wall-clock tests**, not two skipped
tests being represented as new passes. Scheduler latency is not a hard-real-time
guarantee. Rustfmt, all-target Clippy and Rustdoc with warnings denied pass; the new
DLL and managed bridge build successfully.

## Exact parity evidence so far

The C# oracle calls production `Framework.MarketData` functions without modifying
them. Tenor boundaries and snapshot-store scenarios match. Direct C#-to-native
ABI comparison matched all **1,107 Treasury corpus results exactly**, including
failure/output-preservation behavior.

The first Rust implementation used `ln_1p` and failed bit equality. It was corrected
to mirror the existing C# numerical sequence, `2 * ln(1 + half_yield)`. The mismatch
is resolved, not accepted using a tolerance. The qualified core receives the same
already-converted half-yield bits; full decimal input conversion, policy checks,
curve digest and reference acquisition still require implementation and tests.

The production wall-clock deadline test observed expiry at **120.005269 seconds**
and confirmed that the stalled SDK socket closed. This verifies the local SDK
lifecycle/observer boundary, not actor propagation or presentation of a UI dialog.

## SDKDNS-001: default SDK hostname resolution is not cancellable

The lifecycle qualification tests explicitly supply an IP `SocketAddr`, avoiding
DNS. That matters: the default production SDK gateway path has different cleanup
properties.

Verified against pinned dependency source:

1. `databento 0.58.0`, `src/live/client.rs`, `Client::new`: the default path passes
   `protocol::determine_gateway(&dataset)` to `TcpStream::connect`.
2. Its alternative `ClientBuilder::addr` in `src/live.rs` calls Tokio
   `lookup_host`; passing a hostname there does not remove the problem.
3. `tokio 1.53.1`, `src/net/addr.rs`, `ToSocketAddrsPriv for str` and for
   `(&str, u16)`: non-IP strings call `spawn_blocking` around
   `std::net::ToSocketAddrs`.
4. `src/task/blocking.rs` documents that started blocking jobs cannot be aborted;
   runtime shutdown can wait for them indefinitely. Stopping the wait with
   `shutdown_timeout` does not terminate those jobs.

Dropping the SDK connect future therefore does not prove all work from the old
generation has completed during an OS resolver stall. Retrying can accumulate
outstanding resolver jobs; claiming cleanup or replacing the runtime blindly
would violate the agreed lifecycle/resource rules.

The controlled blocking-job test verifies this Tokio primitive. It is **not** a
claim that a real OS DNS hang was reproduced or that every DNS call normally
hangs. The blocker is the absence of a bounded cleanup guarantee on a required
production path, which the source audit establishes and the IP-only tests cannot
qualify.

### Required resolution and implemented replacement

Provide a cancellable DNS/address-resolution path and pass resolved socket
addresses to the SDK. Qualify DNS timeout/cancellation, retries, address refresh,
IPv4/IPv6 and resolver task/socket completion under the original episode deadline.
An asynchronous DNS implementation or a platform API with verified cancellation
may supply it. Merely wrapping `lookup_host` in `select!`, moving it to another
blocking thread, hardcoding current Databento IPs, or using `shutdown_timeout`
does not establish the required guarantee.

Host-supplied addresses are an alternative only if that resolution service itself
has a bounded, verified lifecycle and refresh policy. Moving an unbounded system
resolver into Host would only relocate the problem.

The replacement is now `dns::Resolver` plus `qualify_named_session`. It uses owned
Tokio TCP sockets with Hickory 0.25.2's DNS packet codec, not its resolver/client
runtime. No independently running task, blocking job or OS resolver is started.
The codec is compiled without its Tokio/network feature. Source audit of Tokio's
`ToSocketAddrsPriv for &[SocketAddr]` verifies that the numeric slice supplied to
the SDK is converted immediately, without entering its hostname branch.

Numeric recursive DNS endpoints supporting TCP must be configured explicitly.
This is a documented transport/configuration difference from the current C#
backend. No actual provider IP is hardcoded. Names resolve afresh on each attempt;
there is no positive/negative cache to retain stale gateway addresses. CNAME
following and response sizes are bounded. Resolver calls have a shared capacity
limit, including cancellable waiters. Trusted DNS server configuration and TCP DNS
reachability remain deployment checks, not evidence provided by local fixtures.

DNS and SDK operations reuse the same absolute deadline. Losing resolver futures,
their sockets and semaphore permits drop before the lookup returns. Independent
A/AAAA queries run together; partial family errors preserve usable results.
If a family stalls, a complete alternate resolver can win; otherwise the original
deadline ends the lookup. No short DNS retry/SDK timers override that deadline.

No Rust subprocess, legacy backend change, SDK upgrade or abandoned blocking
thread was introduced. The original default SDK builder is still unqualified and
must never be used for hostname lookup in the new backend.

### Resolution qualification results

The full default regression suite passed **26 tests**, with the two long-running
wall-clock tests intentionally excluded from the default command. Eleven default
tests exercise the new DNS path, including multiple cases within individual tests:

- Explicit cancellation during no response, partial length and partial body;
  both family sockets terminate and the active-exchange count returns to zero.
- A/AAAA refresh on the next lookup, including changed IPv4 answers; no stale cache.
- CNAME answers in one packet and follow-up CNAME queries; cycles are rejected.
- Wrong ID/question, malformed packet, truncated TCP answer, unrelated owner,
  NXDOMAIN, SERVFAIL and empty answer produce errors, without OS DNS fallback.
- A successful family survives a reported error from the other family.
- A healthy resolver beats a stalled resolver; all losing exchanges close.
- A refused DNS connection falls back to another configured server.
- Numeric A/AAAA addresses pass through the real SDK; a refused IPv4 connection
  falls back to IPv6, then caller cancellation closes the accepted SDK socket.
- DNS delay plus real SDK authentication stall consume the same 350-ms test
  budget, rather than receiving separate budgets.
- Twenty-four simultaneous calls respect the 16-call capacity; cancelling also
  completes queued callers without opening extra sockets.
- **1,000** deadline/reset cycles leave zero active exchanges after every return.
- Invalid configuration is rejected; invalid credentials do not start DNS and
  are not echoed in errors.

The explicit production DNS deadline test also passed: a partial DNS response
stalled until the original 120-second deadline. Terminal completion was observed
at **120.016227 seconds**, with **zero active exchanges**, **two peer sockets
closed**, and **no SDK connect/authentication started**. The independent deadline
observer completed too. This is ordinary scheduler/timer latency, not a hard
real-time guarantee of physical execution by exactly 120.000 seconds.

Evidence: `.artifacts/rust-marketdata-dns-all-tests.log` and
`.artifacts/rust-marketdata-dns-production-deadline.log`. Rustfmt, Clippy across
all targets with warnings denied, and Rustdoc with warnings denied pass. The
baseline test confirms that all 729 frozen current-backend inputs remain intact.

These are real local DNS TCP exchanges and real pinned-SDK connections, using
fabricated credentials. They do not establish external DNS TCP reachability,
Databento live acceptance, actor recovery, FFI runtime disposal or UI notification.
Those remain later integration gates. This removes the identified OS DNS cleanup
blocker without claiming the entire production lifecycle/full backend is finished.

## Remaining phases

Performance baselines for the full current backend, concrete native epoch and
Host implementations behind the explicit managed facade, complete publication
and option context/cache/durable-evidence delivery, historical/reference operations,
durable restoration, Host selection and live
acceptance remain incomplete. Source hashes and prototype tests do not satisfy
those gates. The current backend remains the only production implementation.

## Reproducible qualification

From the repository root, the Rust commands are listed in the implementation
README. To regenerate the C# oracle and compare the new DLL:

```powershell
dotnet build TomasAI.IFM.Application.MarketData.Rust/qualification/CurrentBackendOracle/CurrentBackendOracle.csproj --artifacts-path .artifacts/rust-marketdata-oracle -m:4
cargo build --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml
dotnet .artifacts/rust-marketdata-oracle/bin/CurrentBackendOracle/debug/CurrentBackendOracle.dll TomasAI.IFM.Application.MarketData.Rust/baseline/FrameworkOracle.json TomasAI.IFM.Application.MarketData.Rust/target/debug/ifm_market_data_native.dll
```

This oracle does not start the application, provider subscriptions or persistent
services. The native library is unloaded only after synchronous test calls; no
dataset task/runtime was started through those FFI exports.


The expanded 444-case definition oracle also covers non-string provider fields,
exact raw JSON formatting/exponents, oversized numeric lexemes, JSON depth, CLR
integer whitespace, negative zero and trailing NUL behavior. Native projection
preserves `JsonElement.ToString` semantics rather than reserializing a parsed
JSON object. Tests invoke the unchanged C# parser and compare every projected
text field as well as numeric identity/sentinels; no tolerance is used.


Historical HTTP ownership is independently capped at four in-flight requests per
shared client, including download/body consumption. Queued work consumes its
original deadline and cancellation token before opening a socket. An eight-call
integration test proves four active sockets, cancellation of the queued calls,
closure of all accepted peers and zero active requests afterward. The full facade
must reuse its shared client rather than constructing one client per request.


The three explicit production wall-clock tests were also run after this
continuation: authentication terminal result at 120.000934 seconds (peer closed),
DNS at 120.015019 seconds (zero active exchanges, two peers closed, SDK not
entered), and metadata stall at 120.0042422 seconds (peer closed). All passed.
These are native ownership/deadline tests; they are not live Host/actor/UI tests.


### Option statistics and latest quote delivery

The native option statistics store matches 1,020 unchanged-C# transitions,
including replay watermarks, official volume, open-interest dates and overflow.
Logical state survives physical reset and primary futures cannot prune options.
Saved statistics can be supplied at scope preparation; fixed publication frames
retain independent dates and nullable values. Real SDK replay/reset tests pass.

Options output retains the latest pending quote per qualified instrument after
native accumulation. A real SDK fixture sends 20,000 quote sequences without
consumer draining: one connection remains Running, the latest option quote is
retained, and the trade remains available. Separate strict overflow tests protect
authoritative records. Unit tests exercise 99,999 replacements and FIFO bounds.

### Intentional differences requiring final acceptance

| Behavior | New implementation and reason |
| --- | --- |
| Pending option quote delivery | Intermediate pending quotes are replaced per contract, as requested for freshest trading state. Every input reaches native raw/pricing processing first; trades, statistics and orders remain ordered. Futures quotes are unchanged. |
| Atomic pricing-context refresh | Reads become unavailable until the new context produces a result, rather than temporarily exposing an old-context stale result. This prevents relabelling old calculations. Steady-state DTO/hash parity is tested. |
| Trade evidence deadline | Calculation and durable acknowledgment share the caller's remaining budget; the C# path starts another 15-second writer budget. The original recovery deadline must not be extended. |
| Unexpected trade calculation failure | Failure evidence is retained before the exception is propagated to recovery, rather than relying solely on a fault callback. |

The actual-trade pricing adapter matches 80 complete C# result/hash cases. Its
missing-context, failed-writer and bounded stalled-ack tests pass. This is component
evidence, not acceptance of a real database writer or fully wired source consumer.


### Stable option readers and native composition integration

`NativeOptionPriceReader` now exposes exact raw quote/trade values and frozen
quote Greeks through the existing public reader contract. A trade calculation
can be retained only after durable-writer acknowledgment, with physical source
and immutable context fencing. SDK/DLL tests compare enriched quotes and actual
trade Greeks with unchanged C# output, reject foreign identities and prove old
trade enrichment cannot survive physical reset. A retained reader follows fresh
physical pricing after context replacement; release retires all reads.

`NativeOptionCompositionSource` enumerates an already-owned complete scope from
native state, statistics and coalesced selection. Application lifetime/revision
checks fence retired preparation. `NativeMarketCompositionSnapshotProvider`
preserves current validation, canonical order and semantic hashing while routing
all numerical valuation through Rust. Eight complete selection/risk/browsing
combinations match C# snapshots and digests; nineteen invalid-input/source/timeout
comparisons also match. The existing strict selection + selected-risk combination
without browsing still returns its existing SelectionCalculationUnavailable;
that behavior has been preserved rather than silently changed during migration.

`NativeOptionChainRuntime` is the new application scope/lease orchestrator. Its
SDK fixture covers shared renewals, discovery and business ownership, complete
risk unions, revision/CAS fencing, explicit API ValueDate replay selection,
reference refresh without reconnect and joined expiry/release/disposal. A saved
business owner can repair a stopped native scope. Unknown control/expiry failures
must reach its required application fault sink; the concrete Host recovery/UI
composition of that sink is still outstanding. The full native-only API epoch and Host recovery composition remain outstanding.

Option slots are now accounted separately from native underlying dependencies:
maximum 2,048 options and 64 dependency registrations across up to 64 native
scopes. The application worker retains its existing 16-scope policy. This avoids
reducing the advertised option capacity by one dependency per scope. Pending
buffers are allocated only for activated lanes, avoiding three full FIFOs for
every independent options scope.


### Monitoring event delivery

`NativeOptionObservationPublisher` preserves the current full actor envelope,
midpoint, premium tick preparation and unavailable-Greeks mapping. Complete
MessagePack quote, asynchronous risk-completion and pending-risk events match the
unchanged C# publisher byte for byte. Monitoring fingerprints are bounded/pruned;
the generation-wide observation revision never resets on a scope transition.

`NativeOptionQuoteDelivery` reads the immutable monitoring directory on native
options signals. It does not poll, batch-wait, drain source trades or wait for
trade evidence. SDK tests prove monitoring only after business ownership, no
fabricated duplicate on an unchanged read, context/physical-reset rebinding,
monotonic revisions and no output after release. A deliberately stalled publisher
fails under its original remaining budget; at most one unjoined callback is kept,
and no new callback is detached on retry. After its acknowledgment joins, latest
view publication can resume. Concrete Host observer/UI wiring is still required.

### Source-time trade delivery

Native ingress now freezes each actual option trade's underlying, immutable
context/definition manifest, API valuation time and conservative qualification
boundary. Tick processing clones reference-counted owners; it never serializes
reference metadata. Queued source records keep the original context after a
reference refresh or native retirement.

The 816-byte v2 DLL drain copies complete binary manifests before popping each
source record. Transfer is bounded to sixteen records / four MiB, retained native
metadata to sixteen MiB / 16,384 versions. The managed reader uses one reusable
four-MiB buffer and a 2,048-entry LRU with a sixteen-MiB serialized-input weight
limit. This weight is not a claim that managed deserialization has zero overhead.
No borrowed buffer crosses an asynchronous write.

The SDK/DLL refresh-before-drain test matches complete original C# trade Greeks,
semantic digest and evidence after the current rate changes. Expired conservative
qualification is an explicit StaleData result. Physical/context fences prevent
old retained evidence being relabelled as current reader enrichment.

`NativeOptionTradeDelivery` invokes the existing idempotent actor-event mapping
only after durable-writer acknowledgment, preserving the baseline publication
contract. The SDK lease fixture verifies happy delivery, no duplicate drain,
stable-reader retention, a writer that ignores cancellation, no publication
before acknowledgment, no extra write on retry and no late canceled event.
Real database and NATS qualification remain outstanding; fake acknowledgments
do not establish production acceptance.

Priced managed scopes now require each registration's verified publisher. Native
ingress checks that identity before any book/pricing update; mismatches enter
owned recovery. Low-level unqualified transport fixtures may still use publisher
zero, but that path is rejected by priced application scope preparation.

### Single runtime event receiver

`NativeMarketDataDelivery` is the single native signal consumer. It fans out to
independent bounded quote, trade/evidence and primary publication wake-ups. It
fully drains diagnostics and HTTP warnings, forwards scope lifecycle changes to
mandatory application observers and surfaces unknown observer failures. It uses
no polling and never waits for a quote batch to fill. Shutdown signals every loop
before joining, then joins outstanding callbacks under one original budget.

`DeliveryBridge` proves latest quotes continue while durable trade acknowledgment
is deliberately blocked; the actor trade appears only after acknowledgment. It
also proves complete diagnostic/status delivery, unknown observer failure
reporting and joined worker/socket disposal. Actual Host recovery composition,
concrete NATS/database acknowledgment and futures publication remain required.

### Official Treasury acquisition

`reference/treasury_curve.rs`, the bounded reference ABI and `NativeTreasuryCurve`
now implement official monthly USTreasury XML HTTP acquisition, namespace-aware
parsing, null/missing tenor policy, duplicate/conflict handling, range/latest
selection and the existing two-slot/one-minute cache behavior. Cache hits preserve
the original retrieval instant; equal-expiry eviction matches the current cache
slot reuse. HTTP shares the qualified cancellable numeric DNS/TLS transport and
never forwards a Databento credential to the public reference source.

The unchanged C# source supplies 628 frozen cases: generated decimal precision,
signed zero, midpoint rounding, namespace/date/null/duplicate cases, every source
truncation, DTD and pagination failures. Native and DLL/service tests reproduce
exact decimal bits and complete conversion/digest results. The numerical core
runs in Rust; source/unit qualification and semantic hashing retain application
policy. A small allocation-free CLR-decimal parser is used because the evaluated
Rust decimal library rounded midpoint values differently and normalized signed
zero; that extra dependency was removed.

Transport tests cover cache hits/expiry/eviction, missing dates, concurrent cache
admission under cancellation, stalled headers/body, oversize/redirect/invalid XML,
complete warnings, EOF and fresh retry. ABI tests fill all 32 retained-result
slots, verify capacity refusal without shared-control leakage, preserve caller
outputs on small buffers, reject stale take, and prevent premature runtime
release until every result is taken/discarded. DLL cancellation also proves
closed sockets, clean retry and released results. These are component/service
gates; Host persistence/warm-pricing operation acceptance is still outstanding.

Additional bounded XML policy is 1 MiB input, 16,384 nodes and 31 entries; native
result slots are at most 512 KiB each. Treasury queries use an explicit caller
budget/cancellation and do not renew an outer recovery deadline. FinancialModelingPrep
reference operations are separate remaining work, not silently routed to this
source or treated as equivalent conventions.

### Futures publication continuation

`NativeFuturesPublication` now assembles existing futures actor packets from native
frozen state: quote batches, changed-price trades, VX quote-driven prices, replay
completion, ES VWAP checkpoints, session statistics and activated individual live
routes. It preserves source-time API observation ticks across the DLL; it never
retimestamps a queued source with the later managed drain clock. Application
packet leases transfer only on successful publisher acceptance and remaining
owned buffers require joined disposal. Unknown publisher failures escape to the
required recovery observer instead of being swallowed as successful processing.

The real-SDK differential fixture feeds the same source records through unchanged
C# processing and native processing. ES produces 11 matching packets from 16
records; VX produces 15. Comparisons normalize independently generated GUIDs only;
all financial fields, source metadata, subjects, packet order, tick sequences,
batch boundaries and timestamps match. Activated live routes, suppressed trades,
retired source fencing and publication failure propagation are also tested.
`NativeFuturesDelivery` reuses a sixteen-record array and serves as the single
receiver's primary callback. Both profiles pass through that event-driven path,
with no primary queue polling. Concrete epoch/Host integration, bars/history,
real NATS acknowledgment, pressure and actor-readiness acceptance remain required.

### Native epoch resource composition

`NativeEpochResources` now composes the native catalog bootstrap, logical option
owner, qualified futures mappings, single signal receiver, independent quote/trade
delivery and stable last-price directory. Startup and cleanup propagate one original
remaining budget rather than renewing two minutes for each component. Failure
cleanup still signals the native root when an earlier managed join fails; buffers
owned by an unjoined callback are not disposed underneath it. Shutdown flushes
remaining futures wire buffers only after delivery callbacks join, using API time.

`EpochResourcesBridge` exercises HTTP catalog acquisition through the DLL followed
by a real SDK wire session, twelve futures packets including statistics replay
completion, stable reads,
no option connection without an acquisition, and idempotent joined disposal. A
stalled HTTP body also proves the short original bootstrap budget is not renewed
and no live socket is acquired before catalog qualification. The SDK fixture now
includes both trades and statistics replay-complete markers, as bootstrap requests
both schemas from the API-provided replay start.

`NativeLastPriceDirectory` bounds issued handles and resolves current immutable
option monitoring bindings on each read. Existing handles follow reference CAS
and physical replacement, but cannot return released or retired values. The scope
lease fixture verifies those transitions, exact acknowledged trade evidence,
wrong-date/unknown/retired requests and stable identity. Refresh-pending calculations
remain unavailable until their exact new calculation is ready.

Ownership-only shared lease changes reuse unchanged monitoring bindings, preserving
the already acknowledged trade Greeks. Both futures receiver fixtures also inject
a callback that ignores cancellation: a 100 ms remaining shutdown budget fails
without claiming a join, and a second shutdown call cannot renew it.

Replay pressure qualification also exposed a startup FIFO failure: historical
rows were retained even though application publication suppresses them. Replay
now contributes fully to the native book without entering the live FIFO. A
20,001-trade replay, with no managed drain until startup completes, retains exact
session/VWAP totals and delivers only its completion checkpoint plus the actual
live continuation. Statistics replay completion now emits each usable retained
date, matching unchanged C# behavior through new native kind/update 6. ES with
that subscription produces twelve matching C# packets; ordinary ES and VX remain
eleven and fifteen. The lane suite passes 37 tests, with its two previously
qualified full-duration deadline tests intentionally ignored by the ordinary run.

These additions are data-plane composition gates. The public epoch interface
adapter/factory is now implemented and component-tested; full Host operations,
recovery supervisor and production selector remain unfinished.

### Remaining integration work (not accepted as complete API operations)

- Compose the tested runtime receiver with the concrete Host recovery observer;
  qualify concrete NATS cancellation/publication and real durable evidence acknowledgment.
- Complete futures bars and durable checkpoint/replay integration, and historical
  file/persistence integration. Native epoch/factory and FMP reference acquisition
  now have isolated acceptance evidence below; full Host composition remains required.
- Implement all required Host services, optional backend selection, API mirrors,
  actor readiness, UI recovery delivery and the remaining differential/performance,
  live-session/open-close and packaging gates. The existing backend remains frozen.

## Continuation: native FMP, public epoch and bounded historical backpressure

The public native epoch implements startup/stop, source readers, owner-counted
routes, qualified option scope operations, market/statistics reads and dataset
reset under one original budget. A successfully started epoch is published to
Host services; merely creating an epoch does not expose resources. A late,
superseded bootstrap cannot replace the latest owner. Composition routing checks
logical option generation and API ValueDate. `EpochResourcesBridge` passes via
this public interface, including an explicit reset and stale-owner rejection.
Physical reconnects preserve logical identity; successful explicit dataset reset
rotates only that dataset's logical identity. Readers remain stable. Health leaf
metrics now contain only their own dataset's counters, without duplicated totals.

Native FMP acquisition is implemented in `src/reference/fmp`, the existing DLL's
`fmp_configure_v1`/`fmp_submit_v1` exports, and managed `NativeFmpReferenceData`.
The managed adapter uses the existing `FinancialModelingPrepOptions` and classified
exception contracts. It does not instantiate the managed provider or HTTP client.
Both Treasury and calendar share native admissions/circuit state. Requests use
an `apikey` header on the configured origin; credentials are redacted from warnings.
No Databento credential is forwarded. Temporary managed credential bytes are zeroed.

Retained response limits are 1..8 MiB, normalized calendars at most 10,000 rows /
16 MiB, and all retained cold-query outputs together at most 16 MiB / 32 slots.
Output copies start with a 64 KiB pooled buffer and grow only after a non-consuming
capacity result. JSON documents own copied storage before pooled bytes are returned.
Each operation keeps one original deadline across admission, chunks, retries and
body consumption. The existing FMP request-header timeout is distinct from its
operation budget, as in the unchanged provider: a successfully received body does
not inherit an already-expired header timer. Runtime recovery budgets are not renewed.

FMP normalization preserves exact decimal words, including exponent input, signed
zero, CLR tie rounding and zero with exponent overflow. It preserves case-insensitive
last property wins, required tenors, inclusive chunk dates, duplicate conflict
checks, source retrieval time, raw scalar number/bool lexemes, UTC tick rounding,
simple invariant uppercase and UTF-16 ordinal calendar ordering.

Evidence: `fmp_reference` 13 passing tests; `fmp_abi` one passing result-slot and
failure-atomicity test; the existing `reference_abi` passes unchanged. `FmpBridge`
compares 59 cases through the development DLL against the unchanged C# provider,
including a multi-buffer calendar output, exact decimal words, conversion/digest
results, invalid data and HTTP exception classes. All exact comparisons match.
One explained exception is recorded below rather than hidden as an exact match.
Clippy all-targets and the managed build pass with zero warnings/errors.

### Explained difference FMP-NULL-001

- Fixture: `FmpBridge`, Treasury response `[null]`, range 2026-10-25..26.
- Current provider: JSON produces a null DTO; mapping throws `NullReferenceException`.
- New backend: rejects the null row and raises `FinancialModelingPrepContractException`.
- Reason: malformed provider data has a stable, classified failure; no partial or
  fabricated curve is returned. Financial results are unaffected. The current
  provider is unchanged. The fixture prints this difference explicitly; it is not
  presented as an exact exception-class match. Final acceptance must include this
  disposition in the difference review.

Historical acquisition now has an asynchronous visitor whose future owns copied
record fields. It applies bounded backpressure without blocking Tokio, accumulating
a whole history Vec, or dropping authoritative records. The existing synchronous
visitor forwards without allocation. A 10,000-record one-slot consumer test proves
ordered, lossless continuation after a deliberate stall; full-consumer cancellation
and original-deadline tests prove the pending send and HTTP socket are released.
The complete `history_owned` suite passes ten tests. The provider record-count
estimate has also been added; full historical managed/FFI/persistence integration
is still being implemented.

These are component/data-plane gates, not a claim that all 71 public API signatures
or concrete Host/NATS/actor/UI acceptance are complete. The operation matrix remains
unaccepted until those complete paths have evidence. No production server/feed was
started or stopped by these localhost fixtures.

### Full historical provider and definition source qualification

All seven historical provider methods now execute through the new native owner:
cost/size/count estimation, single batch submission, job status, file listing,
range reading, DBN/DBZ file reading and verified atomic download. Managed storage
uses asynchronous file I/O and fixed 65,536-byte transfer buffers; native code does
not spawn blocking filesystem tasks. Reader channels and retained results are bounded.

HistoricalBridge passes 28 comparisons, including 50,000 exact record/batch rows
against the unchanged C# provider and legacy DLL. It verifies cost conversion via
a separately pinned SDK oracle, cancellation, SHA validation and preservation of
an existing target after failed download. A download descriptor must still match
the provider's current descriptor before acquisition: this deliberately strengthens
the old provider's handling of stale caller file metadata.

DefinitionBridge passes 444 exact parser/DTO cases and 10,000 source rows through
the full-universe endpoint, with a forced consumer stall, cancellation and missing
schema-coverage rejection. Full-source refresh is streamed without the catalog's
100,000-row retention limit. Application reference snapshot/storage policy remains
unchanged. The API clock can now be configured before a live ValueDate exists;
FmpBridge passes 60 cases, including cold reference access and shared clock adoption
on later live configuration. These remain component gates, not full Host acceptance.

### Native Host composition and control qualification

A separate Host executable now lives under `host/`, with the current Host assembly
and process name in a separate output directory. The current Host source, project,
worker executables and default selection remain unchanged. The new executable
registers the Rust facade, native epoch factory, native cold references/history,
native definition provider, native rollover resolution, PostgreSQL stores and the
existing application qualification/durable-intent policies. It registers no legacy
feed factory or dataset process supervisor. Its forty required Host capability
members resolve against concrete application/native services.

`HostCompositionBridge` validates the complete DI graph and resolves the facade
without provider/database I/O. `NatsControlBridge` verifies existing request framing,
independent observation/stop subscribers while eight control handlers are occupied,
deduplication and rejection of conflicting operation input/signatures on random
subjects. `HostExecutableBridge` launches the new executable with fabricated
storage settings and no provider keys. DLL/NATS/HTTP readiness, empty pre-session
state, absence of legacy provider module loading and clean joined shutdown pass.
The Windows-owned System32 `conhost.exe` helper is permitted; dataset/application
children are not. No running production Host/API/UI or feed was changed.

The additive owner-aware Stage 4 overloads explicitly preserve the current C#
backend's `Disabled` results. Existing boolean routes and PostgreSQL-committed
business ownership remain separate, as in the baseline. This is contract parity,
not a claim that a currently disabled product feature has been activated.

`HostRecoveryBridge` passes retry-then-success with indicator clearing; continued
retry until the original deadline; terminal notification for a non-cooperative
callback; bounded single callback ownership; late join releasing the recovery gate;
and stale-owner rejection. Native option resets keep logical leases and replace
physical generations. Managed delivery callbacks must also join before recovery
can succeed. A provider incident already terminal after its native two-minute
budget is not granted another two minutes by Host.

`HostStateBridge` passes 10,000 unchanged hot signals producing one lifecycle log,
transition/severity preservation, retired observer fencing, intentional shutdown
exclusion and health evidence retained even when the logger throws. The receiver
reads raw status fields without allocating a new status record/error string for
unchanged hot notifications. Provider diagnostics remain an independent complete
stream; public health queries do not use this notification cache. Futures delivery
failures carry their lane mask and cannot be mislabeled as option-only failures.

Current-session indicator seeding no longer requires the legacy feed factory.
`ReplayBridge` passes 10,000 exact ordered records with bounded consumer stall,
explicit completion excluding the live tail, provider notice delivery, metadata
stall timeout, premature EOF, mismatched mapping, cancellation and joined sockets.
It uses an independent temporary Tokio socket and does not alter a primary lane.
Application seed-window/bar identity and validation policy are retained from Host.
Full real-store/actor/API operation parity and production performance/live gates
remain open; these checks do not mark the public operation matrix accepted.

### Approved backend ownership compatibility exception

The user approved the narrow compatibility change on 2026-10-27. Backend status
has additive MessagePack key 8, `RecoveryOwnership`, defaulting to `Process`.
The current backend's changed-PID validation remains intact. The native branch
requires cleanup/restoration acknowledgement, a stable Host identity, changed
logical owner and generation, matching operation/result/observed identities, and
healthy restored ownership. Four fresh legs, a newer complete plan, publication
and open mailboxes remain mandatory afterward.

`OptionRecoveryReplacementProofTests`: 14 cases passed, including old eight-field
wire defaulting to process ownership, same-PID C# rejection, and native proof
identity/cleanup/restoration failures. Frozen baseline hashes were not regenerated:
`baseline_preservation` reverses only the exact reviewed additive declaration in
`RecoveryCapabilityAddition.txt` before checking the original 729-source hashes.
All other baseline edits remain failures.

Whole native owner replacement now joins existing managed option callbacks,
excludes concurrent status/read callbacks, joins/drops the native subtree,
rotates logical identity, requalifies reference contexts and restores leases.
Futures delivery is independent. Stable handles become unavailable while replacing
and follow the new qualified binding afterward. A requalification failure retains
the desired scope set; an empty monitoring view cannot count as healthy idle.
`OwnerReplacementBridge` passed actual localhost socket cleanup, failed reference
qualification then successful retry, changed logical/physical identities,
old-generation rejection, stable-reader restoration and joined socket disposal.

Host wiring now adopts restored leases and reconciles date-scoped saved trade
intent. Full Host/actor/database/end-to-end acceptance of those paths is still
required. This does not mark the operation matrix accepted or enable cutover.

## 2026-10-27: standalone console acceptance corrections

The user corrected the forced-deadline UI observation: the visible dialog was
"Market feed status unavailable: Backend request failed: no responders", not
the terminal recovery notification. That UI acceptance gate remains unproven.
Standalone notifications now keep temporary feed-status request failures as
console diagnostics and preserve terminal modal errors. Seven focused tests and
the isolated NATS recovery publication check passed.

Market Outlook console refresh notices were previously produced only by the
hybrid .NET UI. The standalone projected snapshots without generating notices.
The API now decorates the existing snapshot publisher and asynchronously sends
notices through the existing five-minute backend status-console stream. Delivery
is event driven, independent of UI availability, and uses snapshot UTC timestamps.
A bounded 256-notice staging channel prevents console outages from blocking market
publication. Consecutive identical displayed notices are suppressed per contract
and value date; the deduplication directory is bounded to 128 contracts. Underlying
snapshot publication is never deduplicated by this console decorator.
Four focused tests passed: backend delivery without UI; blocked delivery and
bounded shutdown; expired notice exclusion; duplicate suppression with changed
prices and value-date rollover still delivered. API Release publication succeeded.
The first live UI read confirmed notices, but revealed duplicate component-refresh
flooding; the deduplication fix was then built and deployed for a second check.
Second live check at 07:40 Eastern passed: API launch-ready returned HTTP 200;
eGui accessibility showed current Outlook notices at 07:40:04, 07:40:08 and
07:40:09, with Component, EsTrade and EodSession triggers and changing prices
7732.75/7732.50. The repeated identical component-only flood was absent. Console
rows were newest first. Current acceptance Manager PID 66760; API PID 75848;
native Host PID 80764. This confirms console delivery, not the outstanding
forced-deadline modal acceptance gate or full production cutover.

## 2026-10-27: native healthy lease renewal contention

User-visible Renew/6c133108-8f21-4d84-b10f-7e0f9b1f6ab7 cancellation
originated in the existing discovery maintenance loop's three-second renewal
allowance. Native acquisition waited behind the global asynchronous option-control
gate. This was not a provider error. The existing health reporter correctly
escalated an unclassified cancellation, but healthy renewal must not need slow
provider admission in the first place. Similar renewal-triggered recoveries were
observed repeatedly; logs include successful recoveries after those incidents.

Only the new native implementation changes: an unchanged, already owned,
unexpired, running scope can atomically renew its temporary lease through a
bounded immutable scope directory. It validates API generation/date, maturity,
connection modes, identical pricing definitions and current context validity.
It does not create subscriptions, renew expired/released leases, repair unhealthy
scopes or suppress errors. New/changed/repair requests retain serialized control.
Temporary lease expirations use conditional key/value removal, preventing expiry
cleanup from deleting a concurrently renewed lease using an older timestamp.
The existing C# provider and its renewal timeout remain unchanged.

Qualification ScopeLeaseBridge passed real local SDK/native transport plus a
held-control-gate regression: existing healthy renewal completes under a 200ms
caller deadline, cancellation is honored, unknown owners and changed maturity
cannot bypass admission, and no recovery fault or additional socket is created.
Existing shared leases, saved statistics, durable ownership/revisions, expiry,
release, context CAS, stale requests and joined disposal also passed.
OwnerReplacementBridge separately passed joined native socket replacement,
failed-qualification retry, generation fencing and restored lease renewal.
Native Host Release publication succeeded; deployed to the opt-in acceptance
package and restarted owned Manager as PID 83104. Production acceptance remains
false. A live soak is still needed to establish that no other control contention
(e.g. actual context refresh/startup) causes similar maintenance timeouts.


## Native ownership continuation: copied evidence, publication and recovery authority

The revised native path remains under implementation. The selected prototype Host
still constructs `DatabentoMarketDataApi` policy and managed option orchestration;
these component results do not qualify full cutover or the complete operation matrix.

Implemented and independently compared with the current C# contracts:

- Native complete trade source identity, semantic hashes and frozen evidence.
  Forty-eight financial success/failure cases preserve complete evidence bytes.
  The ingress oracle additionally follows the actual C# DBN integer-nanounit
  conversion: a manually constructed `10.2500m` source is not the same wire
  representation as the real reader's `10.25m`. Both oracle forms remain tested.
- Fifty-six complete cached-risk results, quote snapshots and actor event
  envelopes; 2,688 premium-increment boundary cases. Legacy schema 1/2 ignores
  appended premium-style metadata just as the current C# engine does.
- Bounded copied evidence outbox with first-attempt retention and exact durable
  receipts. The native NATS IO spool publishes the existing actor envelope only
  after external storage acknowledges persistence. A blocked writer receives
  copied bytes only and cannot keep a dataset resource alive. The compiled Host
  storage endpoint is not yet selected by the active Host registration.
- Owned broker reply subscription and 8,192-byte inbound decoder limit, enforced
  before declared MSG/HMSG allocation, including the TLS connection path.
  Actual decoder failure detail reaches native recovery rather than being
  replaced by a generic disconnected message. TCP protocol tests exercise
  no-responders, silent writers, malformed/oversized replies and durable ordering.
- Current-value queries are paged by both count and encoded bytes (4 MiB maximum).
- Native panic capture uses a fixed ring and `try_lock`, with no synchronous
  stderr write or managed callback on the panic path. Raw copied text must be
  redacted before external publication. The capture hook does not replace normal
  task failure classification or make abort/OOM recoverable.
- Provider diagnostics retain the latest 256 records with an explicit replacement
  count. Logging saturation alone no longer manufactures a market-processing
  exception. Source error classification and retained terminal health are separate.
  The direct diagnostic encoder/publisher exists; the complete native diagnostic
  consumer and Host registration remain to be connected.
- Tracked native root services live outside the three replaceable dataset child
  slots. Root shutdown accounts for their actual exit even when a control handle
  is dropped or a copied-data consumer stalls.
- Dataset recovery authority retains one original 120-second monotonic window
  across retries and replacements. All required workload proofs must pass; late
  generation completions cannot certify health. A failure during partial recovery
  invalidates its earlier proofs. Failed status remains observable; explicit retry
  is supported without a permanent admission quarantine. A later independent
  incident gets a new allowance only after full successful recovery.

The authority is connected to concrete native resource factories as detailed in
the later checkpoint below. The persistent multi-scope business-intent coordinator
and thin active facade remain open. Component tests are not evidence that the
currently selected managed facade has become thin or full live acceptance passed.

Latest component evidence: `Native-option-ingress-tests.log`,
`Native-root-service-tests.log`, `Native-root-recovery-tests.log`,
`Native-broker-inbound-bound-tests.log`, `Native-nonblocking-panic-capture.log`,
`Native-evidence-real-120s.log`, under `.artifacts/rust-marketdata-offline`.
The real evidence-spool deadline test passed at 120.02 seconds; separate native
reference/broker 120-second tests passed previously. These are component gates,
not a claim of full Host/API acceptance.

### Native resource-group and snapshot checkpoint

The native persistent supervisor is now connected to concrete futures and options
resource factories. The concrete SDK/TCP integration test reconstructs an options
resource group, proves sibling isolation, and then starts a background prepared
chain with native reference acquisition, pricing and direct NATS cache delivery.
The old copied view does not acquire the replacement generation. No managed lease
or streaming consumer participates in those resource groups.

Trade evidence and individual option quotes share one ordered actor writer and
sequence authority. Durable storage receipts use a separate copied-byte channel.
An 80-message concurrent mixed-publication test verifies socket arrival order.

Native selection and coherent composition capture match actual C# backend oracles:
24 selection projections, 24 snapshot model encodings/hashes and 24 complete
captures across eight contract/context variants. Three additional offline tests
exercise incomplete/changed pages, duplicate definitions, quote/date/metadata
boundaries, cancellation and deadlines. Strict query limits enter context digests;
the browsing full-risk branch preserves the existing backend's qualification basis.

The actual C# OptionChainPublicationAssembler has reconstructed a native 2,048-item
publication across five 64 KiB chunks, with exact complete wire and semantic hash.
Missing chunks, changed generation/digest and corrupted payload are rejected.
Host publication bytes are plain MessagePack; only the outer NATS frame uses the
existing LZ4 block-array compression. This distinction is covered by the real C#
assembler check rather than assumed from a Rust round trip.

Native prepared-cache delivery now has one bounded in-flight batch and one
latest-wins pending batch (16 MiB each), with one copied chunk awaiting broker
flush. A real socket test replaces pending work 1,000 times while a flush is
withheld, then proves cancellation/join without a broker reply. Retained command
ports cannot retain provider/task ownership. Authoritative trade evidence does
not use this replaceable cache policy.

The remaining active integration work is the native business-intent/claim and
scope-union coordinator, discovery/ranking/cache policy, diagnostic/terminal UI
delivery, full thin FFI facade and Host selection. The currently selected managed
prototype is unchanged and is **not** yet the completed thin-adapter architecture.
All-operation and live Host/API/UI acceptance remain open. Component estimates
reported in progress messages are engineering estimates, not qualified matrix rows.

Evidence: `Native-background-cache-integration.log`, `Native-capture-tests.log`,
`Native-capture-boundaries.log`, `Native-cache-csharp-assembly.log`,
`Native-cache-latest-stall-tests.log`, `Native-ordered-publication-tests.log`, under
`.artifacts/rust-marketdata-offline`. Later aggregate regression gates supersede
these individual checkpoints when recorded.


### Native API owner, claims, terminal journal and diagnostic checkpoint

- The native business coordinator now derives the complete desired union from a
  reviewed catalog. Durable claims survive physical rebuild; ephemeral claims
  expire in Rust at their exact monotonic deadline. Idempotent, invalid and stale
  commands cannot reset another owner. Catalog replacements preserve active scope
  membership and can prune obsolete unclaimed templates.
- One native API owner composes all three dataset supervisors. Futures startup
  does not depend on option definitions or Treasury. Command/query FFI and thin
  managed marshaling exist for native lifecycle/catalog/claims/health/terminal
  messages and numeric snapshots. The legacy prototype cannot coexist in that runtime.
- Terminal messages have a separate sixteen-record per-dataset journal. Explicit
  stop/retry cannot erase an undisplayed failure. Queries do not consume records;
  external display confirmation is required to acknowledge them. Bound replacement
  counts remain visible. The journal tests pass; actual final UI notification
  delivery through the new owner remains an integration gate.
- Provider diagnostics start before source readiness and go directly from Rust to
  a passive Host logger. Exact hashes fence log acceptance receipts. Unknown system/
  error codes, original workers and full bounded text survive. One thousand records
  under a stalled log receipt retain the latest bounded ring with exact replacement
  accounting. Cancellation joins without waiting for a logger receipt and restores
  the unconfirmed copied record. Actual C# DTO round-trip and hash basis match.
- A copied 656-byte non-consuming query uses native numeric book views; the actual
  SDK rebuild test confirms old retained views become unavailable and do not pin
  source resources. This is query-only ABI, not a streaming FFI drain.

Latest evidence: `Native-business-claims-integration.log`,
`Native-terminal-journal-tests.log`, `Native-owner-command-query-tests.log`,
`Native-owner-actual-clr-abi.log`, `Native-direct-diagnostic-tests.log`,
`Native-diagnostics-csharp-wire.log`, `Native-diagnostic-resource-integration.log`,
`Native-diagnostic-receipt-host-build.log`, `Native-query-retirement-integration.log`.
The earlier combined component run passed 82 tests; newer additions require the
next aggregate run. No operation-matrix row is marked fully qualified on that basis.

Still open: complete native discovery/ranking/cache policy and all hot/query
projections, full replacement of `RustMarketDataApi`'s managed policy delegation,
selected Host wiring, terminal UI delivery/confirmation, full identical-input
operation acceptance and live tests. The running opt-in prototype/default C# backend
has not been overwritten or switched to the new owner by these source changes.

## Native owner cache/notification checkpoint (2026-10-27)

- Complete prepared-chain captures now coalesce on actual pending work, with an
  immediate first capture and minimum 100 ms between attempts. There is no idle
  interval or quote-publication delay. Continuous source updates cannot postpone
  due work; cancellation stays independently selectable.
- The explicit owner cache ABI returns existing five-key copied publications only
  for a current healthy ticket and before authoritative expiry. Invalid buffers,
  UTF-8 and absent scopes are tested. It is a query, not an FFI stream.
- Rebuilt native DLL calls through actual C# passed owner health, 656-byte state,
  cache absence, command, journal and awaited-disposal qualification. Managed build
  and all-target Clippy passed before the subsequent notification additions.
- Native terminal delivery now uses one separately bounded notification subtree
  outside the three physical dataset slots. It publishes the existing twenty-key
  MarketDataDatasetRecoveryFailedEvent directly to the existing UI actor subject.
  Stable operation identities deduplicate retries. Only pending terminal records
  cause delivery/retry work; no idle notification polling is started. Broker PONG
  does not consume the retained journal. The unchanged UI does not yet send display
  acknowledgments, so bounded records continue retrying until explicitly acknowledged.
- Actual TCP tests passed for normal/stalled publication, <1-second cancellation,
  sibling isolation, journal retention across stop and explicit display-ACK removal.
  Actual C# event deserialization/reserialization matches the native twenty-key
  payload exactly, including subject, ValueDate and error metadata.
- The native owner creates notification delivery upon its first futures/catalog
  command. A native owner epoch must not be reused for another ValueDate: this
  explicit guard prevents relabeling old retained failures. Full facade epoch
  startup/stop integration and actual live dialog verification remain open.
- The opt-in Host now registers the passive evidence persistence receiver on a
  dedicated broker connection. It receives copied bytes and owns no Rust handle
  or lease. Its storage transport retries independently.

Evidence: Native-cache-query-tests.log, Native-owner-query-clr-abi.log,
Native-cache-query-managed-build.log, Native-cache-cadence-integration.log,
Native-notification-owner-capacity.log, Native-terminal-delivery-integration.log,
Native-terminal-csharp-wire.log. Full revised operation parity and selected facade
cutover are not claimed complete; the running stack has not been replaced.

## Native policy and actual-ABI checkpoint (2026-10-27)

The aggregate Rust run passed **262 tests, 0 failed, 7 ignored** before the newest
policy additions. All seven ignored production-duration cases were then executed
explicitly and passed: DNS, SDK source, metadata, reference HTTP, pricing, broker
and copied-evidence persistence each honored their original real 120-second
window (approximately 120.0-120.1 seconds). These remain component qualifications,
not a full selected-Host/live-UI acceptance claim.

- Full quote-Greeks projection now matches 56 C# fixtures, including prerequisite,
  pending, stale, numerical and expiry outcomes. C# only materializes a typed
  twenty-four-field transfer into the existing public readonly struct.
- Complete-universe qualification matches 128 results from the actual existing
  EuropeanOptionUniverse, including metadata/version/digest conflicts, duplicates,
  exclusions, incomplete scope and expiry. Capacity/cancellation/original-deadline
  tests passed. Raw lookup transport and final discovery integration remain open.
- Current-futures/term-structure selection matches 90 actual C# resolver cases,
  including ES quarterly rules, VX month ordering, first-duplicate preservation,
  currency fallback, multiplier defaults and UTF-16 ordering. The actual DLL/C#
  query produces exact thirty-two-key public models and existing typed not-found
  failures. NativeCurrentFuturesContractResolver no longer performs this policy
  locally; it requests the native selection over detached definition rows.
- Typed owner command marshaling is present for futures, reviewed catalogs, scope
  membership and durable business enable/disable. No managed renewal timer or
  physical lease token is introduced. Actual command testing found a shutdown
  wait/release ownership cycle: a retained notification child guard could keep
  wait busy until release. Runtime stop now closes admission and detaches the
  native command directory; tracked task/socket exit remains mandatory. A bounded
  three-second regression test reproduces the guard-retention case and passes.

Latest evidence: Native-real120-*.log, Native-quote-greeks-parity.log,
Native-universe-parity.log, Native-current-futures-parity.log,
Native-current-futures-clr-abi.log, Native-owner-stop-cycle-regression.log.
Full thin-facade/selected-Host replacement, remaining native discovery/ranking
policy and actual live UI acceptance are still open. No default/running package
has been switched by these source changes.

## Native read and candidate-policy checkpoint (2026-10-27)

Overall estimate remains **88%**. The registered Host facade still delegates
managed orchestration; its removal and end-to-end cutover remain required.

- Raw trade/quote query: 112 C# wire cases. Full quote Greeks: 56 C# cases.
  Trade Greeks: 48 source-time calculations. Query-only owned readers have no
  managed pricing/preparation callbacks, renewal timers or provider handles.
- Durably acknowledged trade frames now retain canonical identity in the bounded
  native latest-value store. Replacement never replays old-generation frames.
- Actual SDK options integration passed preparation, pricing, durable receipt,
  publication, retained-query lookup and whole-dataset replacement together.
- Actual DLL/C# owner-only gate passed idle health, all four option-reader methods,
  optional exports, command/query boundaries, mutual exclusion and joined disposal.
- Candidate eligibility, delta ordering and exact wings match 69 C# scenarios.
  Double-to-decimal conversion matched 8,373 CLR boundary and sampled inputs.
- Actual publication identity and accepted demand are separate native query fields.
  Healthy idle control does not mean Live Feed is enabled.
- All-target Clippy passes with warnings denied.

Remaining: complete native universe planning/policy metadata, raw reference and
provider bootstrap, facade/Host cutover, retired-evidence overflow accounting,
full regression and live acceptance. Operation matrix remains unqualified.

## Native bootstrap / policy checkpoint (2026-10-27)

Estimated overall completion: **90%**, with final Host/facade selection and live
operation acceptance still open. The normal backend/launch defaults are unchanged.

- Complete strategy parameter import, native validation, MessagePack hash and
  typed pricing provenance match 8 CLR configurations / 24 metadata snapshots.
  Eight complete prepared-ranking snapshots also match exact C# bytes/digests.
- Native candidate/wing policy is connected to optional strategy cache scopes.
- Native universe planner matches 21 C# scenarios / 24 sequential planning steps,
  including buffered coverage reuse, capacity fallback and per-underlying forwards.
- Fixed-capacity decimal arithmetic matches 12,720 CLR outcomes; float-to-decimal
  conversion matches 8,373 CLR inputs. Coverage retains the existing arithmetic.
- Futures definition discovery now runs inside the same native group as SDK
  setup. HTTP discovery, replay construction, cancellation and integrated SDK
  replacement pass. A real original-deadline stall fails in 120.02 seconds,
  retains one terminal notice and joins native resources cleanly.
- Raw-reference I/O is paged directly over native NATS: 4 KiB pages, 4 MiB total,
  explicit SHA256, fixed-schema length checks before allocation, no FFI stream.
  C# storage owns copied facts only (calendar and exact immutable convention).
  Paging/cache mutation/conflict/capacity checks and native truncation, integrity,
  stalled-response cancellation and sibling isolation tests pass.
- Optional raw-reference bootstrap is integrated into the options resource group.
  Successful immutable facts stay in Rust across physical reset. Refresh policy,
  production inputs and native reference service/budget always use current intent.
- Native ABI feature flags distinguish the new command capabilities from an older
  DLL; they do not claim complete end-to-end operation acceptance.

Still required: finish thin facade/Host routing and native discovery/claim wiring,
retired-evidence overflow accounting, complete regression and live acceptance.
The operation matrix remains unqualified until those gates are met.


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


### Public owner boundary and atomic scope checkpoint ? approximately 95%

The rebuilt DLL now qualifies the complete native backend status and catch-up
snapshot through the actual C# MessagePack DTOs. Recovery rejection and composition
failure DTOs cross that same boundary without invoking managed context, pricing,
lease or epoch orchestration. The seven native observer tests include the exact
120-second boundary race; no second timer competes with the dataset authority.

Actual SDK fixtures qualify idempotent futures starts, rejection of wrong-date
starts without mutation, option composition capture, and physical replacement
while prior copied query data remains retained. Atomic scope membership plus
claim acquisition rejects invalid commands without leaving partial scopes. A
query-only managed release observer now waits for native teardown/readiness.

Evidence: Native-current-owner-clr.log, Native-owner-current-qualified.log,
Native-capture-command-boundaries.log, Native-atomic-scope-sdk.log and
Native-atomic-scope-managed-build.log under .artifacts/rust-marketdata-offline.

Remaining: finish automatic native strategy discovery/planning/claim wiring;
replace the registered prototype facade/Host epoch and managed lease/context
services with the native owner; run the complete operation, architecture, parity
and integrated acceptance matrix. The production selector has not changed. This
percentage is an approximate implementation estimate, not an acceptance pass rate.


### Native stop/restart and complete scope qualification ? approximately 95%

- The production-duration public-owner recovery test passed in 120.02 seconds:
  terminal result delivered successfully through the observer, native compressed
  twenty-field error event received on NATS, sibling dataset unchanged, and all
  source resources/completions retired. The first two runs corrected fixture
  decoding/completion-drain assertions; production code needed no change for them.
- Actual DLL/C# complete raw scope acquisition derives identities, underlyings,
  subscriptions, membership, cache and claim in Rust. Atomic scope rejection and
  native-clock expiry checks passed. The managed adapter retains no lease token.
- Ordinary stop waits across all native datasets with no managed timeout, refuses
  to certify stop superseded by new work, and preserves reusable controllers.
  Actual SDK stop/restart of the same accepted configuration passed.
- Shared typed native raw-source and reference-definition builders passed source
  bounds/conflicts/order and existing context/digest adapter fixtures.
- Native setup-only underlying acquisition passed actual SDK success/cancellation
  and joins its temporary connection before returning detached prices. Its quote
  arithmetic matches 105 actual CLR decimal bit patterns, including nanounits and
  maximum signed provider prices. Missing/crossed/stale/future inputs are covered.
- Native mark policy preserves fresh-trade precedence for futures and the existing
  crossed-option failure. Native-selected root-price lookup passed the SDK fixture.

Evidence: Native-public-owner-production-deadline.log,
Native-current-owner-inputs-clr.log, Native-public-owner-stop-restart.log,
Native-shared-raw-policy-tests.log, Native-typed-options-source-tests.log,
Native-background-underlying-sdk.log, Native-bootstrap-midpoint-parity.log,
Native-owned-price-policy-tests.log and Native-public-owner-price-query.log.

This checkpoint does not switch Host registrations or complete the outstanding
native background loop/facade integration. Those remain required before acceptance.


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


## Readiness regression and browsing primitives (2026-10-28)

Estimate: **93% engineering completion**, not final acceptance. No blocker found;
implementation continues. The complete-owner adapters remain unregistered, so no
existing launch/backend selection has changed.

- Native checkpoint before the browsing additions: 94 suites, **331 passed,
  zero failed, 11 explicitly ignored environment/qualification tests**. Evidence:
  Native-owner-readiness-regression.log. This is a checkpoint, not a claim that
  later browsing work has completed the full final regression.
- Composition/cache business compatibility: **17 passed**. Native workload
  proof: **36 passed**, including 256 scopes accepted, 257 rejected, incomplete
  and duplicate membership rejected after actual MessagePack transport.
- Thirty-five of thirty-nine Host operations now have complete-owner adapters;
  the cached preparation adapter uses the same immutable business persistence and
  semantic hashing as before. The preparation writer has only copied snapshots;
  it cannot acquire/release a physical dataset.
- Native browser-window selection matches **1,200 actual C# oracle cases**:
  ordered display/required membership, numeric Decimal bounds, labels and buffered
  subscriptions. Two discovered differences (midpoint label rounding and filtering
  before duplicate removal in buffering) were corrected. The fixture is compressed
  and has an explicit regeneration tool. Capacity, invalid/nonfinite input and
  cancellation tests passed. Evidence: Native-browser-source-boundaries.log.
- Native stored-input fallback matches **1,200 actual C# outcomes**, including
  EOD/daily/intraday precedence, source dates, nonfinite/subnormal double deviations,
  nullable indicator deviations, negative fallback prices and Decimal overflow.
  Evidence: Native-browser-input-fallback.log.
- Passive source kind 7 supports exact underlying/expiry/provider-root filters
  without expiry scans. Its browser projection appends the untouched option-type
  text (25 fields); background projection stays 24 fields. Full 100,000-row source
  and exact-filter CLR qualification passed. Native projection tests reject using
  background-only facts as browser facts. Source kind 8 copies EOD/current/previous
  and daily/five-minute bands; Rust chooses all fallback/eligibility behavior.
- The raw-reference cache pressure test now proves completed transfers are reused
  without evicting unfinished transfers or rereading their database snapshots.

Still required: native browsing lifecycle/result operation; uncached composition
preparation; committed ownership reconciliation/release; complete-owner Host DI
cutover; final operation matrix, actual-DLL and selected-stack acceptance. These
are not qualified by the helper/oracle tests above.


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


## Complete-owner registration and actual-adapter checkpoint (2026-10-28)

Engineering estimate: **98%**, not final acceptance. All 39 Host operations now
have complete-owner adapters and the opt-in native registration selects them.
The default C# backend and normal launch selection remain unchanged. There is
currently no blocker; regression, packaging and remaining acceptance continue.

- The selected graph no longer registers the managed epoch, option lease,
  discovery/renewal, pricing-context preparation, admission or live cache owner.
  Passive copied business/reference/statistics responders remain independent.
- Native browser view membership, required-leg union, expiry, metadata refresh,
  buffered window selection, ATM-IV sampling, statistics and recovery are owned
  beneath the native dataset. Capability mask **0x1ffff** identifies the complete
  owner; old 0xffff packages are deliberately rejected by this selected graph.
- The actual public C# API -> DLL -> SDK/NATS browser qualification passed with
  managed acquisition ports configured to fail if called. It checked typed
  quote/Greek/statistics materialization, two-view continuity, native ATM window,
  isolated release, physical recovery while old managed results remain retained,
  restored view demand, final release and joined shutdown.
  Evidence: `Native-complete-browser-clr-sdk.log`, one passed, zero failed.
- Committed business watermark-only updates preserve the physical SDK connection.
  Changed physical recipes replace and qualify the source before acknowledging
  delivery. A frozen physical signature prevents an old run from acknowledging
  new authority. Known conditional-write supersession races retry in place;
  unknown storage/parse/publication failures enter supervised native recovery.
  SDK evidence: `Native-committed-physical-change-sdk.log`, two passed.
- ATM-IV output matches 1,600 separately executed C# oracle cases bit for bit.
  Existing browser fallback/window fixtures cover 1,200 cases. Full-model and
  logical statistics retention is bounded to current desired instruments.

Full suite, strict Clippy, development DLL packaging, executable composition and
production-duration failure tests are in progress. Do not infer their results
from this checkpoint or from the successful individual browser test.


## Final packaging and regression continuation (2026-10-28, 99% estimate)

- Full regression checkpoint: **383 passed, zero failed, 14 explicit qualification
  gates ignored** (`Native-complete-full-regression-r2.log`). Strict Clippy passed
  with `-D warnings`. Later empty-result and metadata-maintenance tests are being
  included in the final rerun; that rerun is not accepted until all tests pass.
- All nine explicit SDK/DLL/production-duration gates passed
  (`Native-complete-production-deadlines-and-clr.log`). Metadata stalled-source
  retirement completed at 120.012 seconds; the pricing failure terminal result
  occurred at 120.035 seconds. No second dataset recovery window was granted.
- Actual public browser, discovery and blocked-business-commit preparation DLL
  gates passed. Complete-empty preparation also passed with the existing public
  immutable evidence validation/replay and exactly one explicit source recovery
  (`Native-complete-empty-preparation-clr-sdk-r3.log`).
- Native metadata maintenance read fresh copied source facts on its real
  thirty-second cadence without replacing unchanged provider resources
  (`Native-complete-browser-metadata-maintenance.log`).
- Actual executable startup, HTTP/NATS readiness/status, disabled additive lease
  compatibility, no legacy DLLs or dataset child processes, and clean joined
  shutdown passed (`Native-complete-host-executable-acceptance-r3.log`). The check
  uses an isolated, checksum-verified NATS 2.10.24 test broker and fabricated
  storage identities; no existing backend is stopped or reconfigured.
- Executable testing found and fixed repeated disposal of an already freed native
  root: Host and DI aliases now observe the same asynchronous disposal result.
  Native cancellation/join remains authoritative, without a managed datafeed gate.
- The development package passed native-profile/checksum and no-legacy-asset
  validation. Release packaging and the latest full regression continue.

Real-store/API actor/UI acceptance is currently blocked by unavailable local
PostgreSQL, Redis and NATS services. The user was asked to start those services
while offline qualification continues. This does not make mocked SDK acceptance
or the package check equivalent to live trade acceptance. Default selection and
current C# backend remain unchanged.


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
