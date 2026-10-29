# Native runtime and managed delivery foundation

This is phase-2 foundation work, not a complete `IMarketDataApi` implementation.
Submitted operations cover Treasury conversion, all five futures-option pricing
actions, and live dataset start/reset/stop. Status, source records, diagnostics and
hot prices use separate bounded fixed-layout reads. No provider/lease/history operation falls through to a
pretend success, and neither Host nor the current backend registers this library.

## Lifetime and delivery

1. Load the distinct DLL by absolute path, verify ABI version 1 and the 32-byte
   completion layout. Do not use the old Databento library resolver.
2. Create one native runtime. Its integer handle is never an object pointer;
   released identities are never reused. At most four runtimes can exist, with
   1-4 Tokio workers each. Production composition will own one.
3. Reserve an operation ID and completion capacity before submitting work. The
   hard 256-operation bound includes completed but undrained results. Overflow is
   an explicit submission error; no admitted completion is overwritten or lost.
4. Each future owns its resources and a task-tracker token registered atomically
   with admission. Cancel and the original absolute deadline drop the future.
   A panic is caught after unwinding its resources and becomes a failed outcome.
5. Signal a condition variable when completion becomes available. One dedicated
   managed delivery thread waits without polling, copies currently available
   records into a pooled batch, then resolves asynchronous continuations. Native
   tasks do not call actors, UI, or managed callbacks synchronously.
6. Stop closes admission, cancels all operations and wakes delivery independently
   of completion capacity. Draining frees reservations, including failed results.
   Task-tracker waits provide explicit task-exit evidence in native qualification.
7. Await delivery completion, then release the native runtime outside Tokio. A
   running/concurrent/undrained owner returns `Busy` and remains valid. Release
   joins the runtime workers; work-owned resources have already dropped. Registry
   locks are not held while joining workers or waiting for events.

Managed disposal uses the remaining caller budget, capped at 120 seconds. A
recovery coordinator must pass its existing remaining budget to
`StopAndDisposeAsync`, rather than start a second recovery timer. Ordinary
`DisposeAsync` uses a two-minute lifecycle budget. Repeated calls share the first
disposal task/budget. An independent managed deadline covers stop, event delivery
and release. On expiry the caller receives failure while cleanup retains ownership
until safe; live native memory is never freed or code unloaded to force completion.

The DLL remains loaded for process lifetime. A SafeHandle finalizer only requests
stop; it cannot prove cleanup or join arbitrary work. Explicit `DisposeAsync` is
mandatory. Forgetting it can retain a runtime/delivery thread and eventually hit
the hard runtime capacity. This fails explicitly rather than reusing a stale
pointer. Finalization is not the production shutdown strategy.

## Contract boundaries

- `include/ifm_market_data.h` documents scalar widths, status/outcome values,
  pointer alignment, capacities and ownership. Errors preserve outputs.
- The managed interop DTO mirrors the layout exactly, checks size/reserved fields,
  rejects unknown/repeated completions and never retains a borrowed native buffer.
- Cancellation requests are not completion evidence. A cancellation race may
  complete successfully if work already finished; no old result is delivered to
  a reused operation identity while its reservation exists.
- An expired budget is checked against the monotonic clock before polling work
  and again before accepting success. This closes an actual cross-language test
  failure involving an immediately-ready future and a newly expired Tokio timer.
- Business dates, timestamps and market session rules are not derived here; they
  remain explicit inputs to the later provider/API implementation.
- There is no quote/data queue yet. Later latest-per-contract output must have its
  own bounded capacity; it must not consume these control/completion reservations.

## Qualification

Native tests cover malformed ABI/version/capacity/output arguments, duplicate and
stale identities, full completion capacity, independent stop, signal wakeup,
cancel/deadline resource drop, task-exit waits and contained task panic. Managed
qualification compares the 1,107 unchanged C# numerical fixtures through actual
async DLL calls and performs 1,000 create/cancel/dispose cycles, including concurrent
disposal and rejection of work after disposal.

This does not qualify the full provider runtime, three dataset lanes, complete
application facade, full financial/reference behavior, actor/UI delivery or
performance acceptance. Those remain separate phase exits.

## Pricing result ownership

The additive pricing request/result layouts are 80 and 64 bytes respectively.
Requests are copied before returning from submission. Results use a separate
256-slot table: draining a completion does not free a payload before managed
delivery has copied it. Even an unpolled cancelled request has an owned Drop guard
which makes its result slot readable. Managed delivery takes/releases that slot
before signalling its Task and before runtime disposal can complete. Invalid
size/alignment, pending reads and duplicate takes consume no valid payload.

Runtime release returns Busy for retained pricing results. Treasury submission
also rejects IDs still retained in the pricing table, preventing reuse after
completion drain. Numerical failures retain their explicit failure codes; runtime
cancellation/deadline expiry still uses the terminal completion outcome.

The managed application pricing adapter retains existing application reference
qualification and canonical digest construction, then invokes the native engine.
It does not invoke the old numerical calculator. Its comparison covers 104 exact
MessagePack values/failures/digests. Raw managed/native pricing qualification covers
1,520 exact outcomes and 1,000 cancellation/disposal cycles.


## Live data-plane bridge

One immutable API ValueDate/instance is configured per runtime. Business UTC ticks
are explicitly supplied by the application (since Unix epoch); advancing mode
adds monotonic elapsed time. Recovery timers never use business time. General
historical operations can carry up to 30 minutes; lane start/reset/stop still
clamp to the original remaining 120-second recovery deadline.

The native runtime tracks all three long-lived dataset tasks separately from
finite command completions. Root stop cancels their linked tokens, while stopping
options does not cancel either futures lane. A cancellation request is not exit
proof: final delivery waits until registered task guards actually drop.

Cold start controls have a 2 MiB input limit, enough for the configured 2,048
contracts with canonical identifiers. JSON exists only at this cold configuration
boundary; per-record/hot-price transport is fixed blittable data. The original
start budget includes managed serialization and native parsing. Every admitted
start/reset/stop has a retained 4,112-byte result slot, even if never polled or
cancelled. Completion drain does not release that slot. IDs cannot be reused by
pricing or Treasury operations before it is taken. Slots and operation admission
are independently bounded; runtime destruction refuses retained results.

`runtime_wait` wakes for completions **or notification bits**. Delivery drains
completions and then consumes `live_signals`; bits 1/2/3 identify the corresponding
lanes. Notifications coalesce wake-ups only, never source contributions or errors.
The managed single-consumer `WaitForLiveChangesAsync` waits on a bounded signalled
channel; there is no timer/request polling. Each notification requires consumers
to read statuses and drain data/diagnostics until caught up.

Authoritative records retain independent 8,192-record buffers **per lane**, after
native session/VWAP accumulation. Batch drain is fair across ready lanes. Full
capacity produces an explicit unhealthy processing error and recovery; it never
silently drops a contribution and pretends health. Replacement retires readers,
clears that lane's superseded backlog and allocates a fresh opaque generation.
Stopped/recovering lanes do not publish queued records as current. This is a
bounded upstream bridge, not yet downstream actor/trade-plan acceptance proof.

### 128-byte source-event payload

Common slots 0..4 are sequence, event nanoseconds, receive nanoseconds, schema,
and `publisher | (adapter_flags << 16)`. Remaining slots 5..11 are:

| Kind | Slots 5..11 |
|---|---|
| 1 quote | bid, ask, bid size, ask size, bid count, ask count, zero |
| 2 trade | price, size, action byte, side byte, DBN flags, depth, delta ns |
| 3 order | price, size, action, side, DBN flags plus channel shifted 8, order ID bits, delta ns |
| 4 statistic | price, quantity, reference ns, type, channel, action, flags |
| 5 replay completion | all slots zero; instrument identifies the completed futures replay |
| 6 statistics replay completion | all slots zero; one frozen usable retained-date session snapshot per instrument/date |

Prices remain exact nanounits; order IDs preserve all unsigned bits in their
signed slot. A generation identifies the exact source owner. No borrowed decoder
pointer crosses the ABI. `DrainLiveEvents(Span<LiveMarketEvent>)` copies into
caller-owned storage without per-record allocation or JSON serialization.

Kind 5 is a control transition, with publication update 5. It has no fabricated
provider sequence, publisher or timestamp. The coherent captured session/VWAP
state describes replay completion before subsequent live records. Consumers use
their qualified instrument mapping for contract identity. A market-price flag
is absent when only historical replay has occurred.

Kind/update 6 preserves the current C# statistics replay-complete publication,
including usable older reference dates. No provider sequence/time is fabricated.
Historical replay observations contribute fully to native aggregates but do not
enter the live-delivery FIFO: application consumers suppress those historical
rows. Trade completion carries its exact frozen session/VWAP state; statistics
completion carries each retained usable session. A 20,001-trade bootstrap test
exceeds the 8192-entry FIFO without a managed drain, preserves exact totals and
produces only the completion checkpoint and subsequent actual live trade.

### Independent option scopes

The scoped extension uses the same completion/take protocol as flat lane control.
Actions prepare/replace the logical options owner, install/reset/release one
physical scope, or stop the options subtree. Every request carries the expected
logical generation. A delayed older release/stop is a no-op; acquisitions/resets
with a stale generation fail. Replacement joins physical cleanup before admitting
its successor. Preparation alone does not prove observations, risk, actor or
trade-plan readiness. The flat compatibility options lane and scoped mode are
mutually exclusive.

Scoped state uses the existing 656-byte DTO and ordered drain, with no per-record
JSON/string allocation. Scope names retain the current 128 UTF-16-unit limit
(at most 512 UTF-8 bytes across the ABI). Price reads return absent flags during
startup/retirement. Diagnostics use the existing runtime-owned sink and include
scope/physical-generation context without dropping the original text; unread
messages survive scope release and owner replacement. Scoped failure reads are
non-consuming and independent of diagnostic capacity. The managed bridge uses
these exports but does not register the full API backend yet.

Idempotent scope installation compares the typed subscription plan, contract
registrations, evidence policy, gateway and DNS endpoints as well as its opaque
digest. A reused digest cannot hide changed physical inputs. Scope status reads
expose physical recovery attempts; a retiring scope never reports Running.

All live scopes, flat lanes and historical requests in one runtime share sixteen
DNS admission slots. Distinct server configurations do not multiply that limit;
queued lookups consume the original deadline and can be cancelled without opening
a socket. Each admitted lookup races at most four servers and two address families.

### Qualified scoped pricing extension

Scope install action 0 accepts optional `pricing` containing `digest`,
`policy_version`, `selection_ms`, `risk_ms`, `iv_ms`, `batch` and the complete
`options` batch. Each row contains `instrument`, `underlying`, `required`,
`identity`/`generation` (low/high uint64 arrays), `expiration`/`valid_until`
(Unix UTC ticks), `days_in_year` (360 or 365), validator `flags`, `maximum_age`,
`maximum_skew`, `maximum_lead`, `strike`/`definition_strike` (four CLR decimal
words), `strike_double`, `rate`, `american` and `futures_style`. This is bounded
cold configuration, not quote serialization. Application validation supplies the
entire qualified time window, including earlier reference/session/tenor cutovers.

Action 5 refreshes the complete immutable context batch. It carries `scope` and
`pricing` with `expected_digest`, new `digest` and `options`. Each option also
contains `expected` (the prior context identity). All comparisons, physical
definition constraints and quote-limit restrictions succeed before any context
changes. Current quotes survive; old IV/risk is retired. No reconnect occurs.
Idempotent installation compares typed contexts, numerical refresh settings and
cadences as well as opaque digests. A changed context must have a new identity.

`scope_calculation_v1` copies one 640-byte cached record, using the configured API
clock for freshness. It returns the current exact inputs and complete retained
IV inputs/calculation time, separately from the current selection/risk time.
The four policy words are SHA-256 of the exact UTF-8 refresh-policy version;
readers cannot relabel IV with a different version. Presence flags identify
selection, full risk, IV and financial failure. Failure kinds distinguish missing
underlying, expired reference, quote validation, numerical failure, stale input
and a newer quote awaiting full risk. No read acquires a subscription, performs
I/O, renews a lease or runs a solver. Wrong logical generation and unready/retiring
scopes return absent. Invalid arguments preserve the destination.

`NativeOptionPricingCache` maps these records to the existing application
`ChainSelectionCalculation` and `OptionPricingPassResult`, with current reference
validation and exact semantic hashing. Native pricing commits wake the existing
event-driven options delivery signal even if no further provider quote arrives.
The SDK/DLL qualification compares complete MessagePack records with unchanged
C# calculation paths, including retained IV provenance and risk digests.

### HTTP warning delivery

Signal bit 4 announces pending historical HTTP warnings independently of lane
bits 1..3. `ifm_market_data_history_warning_v1` copies and consumes one bounded
UTF-8 JSON object containing endpoint/status/request_id/text. Capacity must be
1..65,536 bytes; length zero means empty. Failed copies preserve the warning and
both caller outputs. Warning text is capped at 8,192 bytes, request ID at 1,024,
and retained warnings at 256. `DrainHistoricalWarnings` returns at most sixteen;
drain until caught up after the wake. Unknown/malformed warning JSON is preserved
as original header text, rather than silently ignored.

### Exact hot quote qualification

`ifm_market_data_qualify_quotes_v1` accepts a 304-byte request and writes a 16-byte
result. Each quote is 112 bytes, including explicit decimal price/quantity words,
source times/offsets, sequence and generation. Decimal words are low/middle/high
coefficient plus flags, not the CLR's internal memory layout. Scales 0..28 and
signed zero are retained. Comparison divides coefficients rather than multiplying
them into overflowing intermediates. This call allocates no task or buffer and
performs no I/O. Application immutable reference/calendar qualification precedes
the call; generation, identity/strike, invalid-quote, stale and skew failures retain
the unchanged validator's first-failure ordering. Errors preserve caller output.

Diagnostics use independent 256-message capacity and 4,128-byte copy records.
They preserve full redacted provider text, unknown codes and end-of-error-batch
markers. Partial batch drains retain their tail. Status failure detail is also
non-consumingly readable, so draining logs cannot hide a terminal recovery failure.
Application Host logging/publication and terminal UI routing are still required
composition work; these exports do not themselves claim those gates.

Qualification: `LiveBridge` verifies optimized-DLL startup, ten physical resets,
exact hot prices/source events/full warnings, event-driven delivery and 100 real
SDK cancellation/disposal cycles. Native ABI tests add 1,000 physical resets,
result retention, every truncated control prefix, layout rejection, replay
completion, output saturation and independent lane ownership.


Public futures readers are API-epoch proxies, distinct from generation-bound
native book handles. This preserves the current C# `ResetContracts` behavior:
a retained public reader sees the replacement dataset's values, while retired
internal book handles cannot see a newer generation. Closing the date-scoped
runtime retires its public readers. The 104-byte raw observation includes both
order counts as well as quantities; the managed value is a struct, not a new
object graph on each hot read. `LiveBridge` compares unchanged C# snapshots
through ten resets with the same retained reader objects and checks retirement.
# Atomic catalog hydration and coherent publication continuation

`catalog_configure_v1` configures at most 100,000 rows and 64 MiB of raw
definition source per universe. There is one committed and at most one staging
universe. Projection/index/allocator overhead is separate from the raw-byte
budget; these limits require final workload qualification before rollout.
`catalog_begin/append/finish` uses monotonic tokens; invalid source or capacity
failure discards staging. Failed/stale cancellation cannot abort a later refresh.
An existing committed universe remains readable until atomic commit. Canonical
resolution requires the committed revision and rejects ambiguous mappings.

`history_configure_v1` establishes one shared native HTTP owner. It accepts a
bounded cold JSON configuration, uses qualified cancellable numeric DNS, and
retains the historical four-request concurrency limit. `catalog_refresh_v1`
streams one to three dataset universes into one replacement under the original
remaining deadline (maximum 120 seconds). Cancellation drops HTTP ownership and
the RAII staging guard. Its retained control result must be consumed with
`live_take_v1`, including on cancellation/expiry. `catalog_revision_v1` reads the
committed revision without provider I/O.

`live_publications_v1` copies up to sixteen **656-byte** records to reusable
caller storage. It and `live_events_v1` consume the same authoritative FIFO;
choose one delivery representation. Each publication contains the 128-byte raw
source record, size/flags/update/reserved u32 fields, then 64 i64 slots. Derived
values are frozen under the book writer immediately after the source input.
They are not assembled from a later mutable hot-cache read. Retained FIFO
memory is explicitly larger than raw-record-only delivery, but stays bounded at
8,192 authoritative records per lane. Options additionally retain at most one pending
quote per qualified instrument (maximum 2,048), replacing older pending quotes
after native raw/pricing accumulation. An intrusive queue uses preallocated slots;
merging retained quotes and authoritative records preserves their processing order.
Futures retain every quote in the authoritative FIFO. Benchmark this profile at the final performance gate.

Presence flags: market 1, quote 2, trade 4, session 8, VWAP 16, option statistics 32. Update identities:
unchanged 0, quote 1, accepted suppressed-price trade 2, published-price trade 3,
statistics 4. Presence does not by itself authorize an extra price publication.

| Slots | Values in order |
| --- | --- |
| 0 | Market ValueDate as YYYYMMDD |
| 1–12 | Quote sequence, event/receive/local ticks since Unix epoch; bid present, price, quantity, order count; ask present, price, quantity, order count |
| 13 | Reserved zero |
| 14–24 | Trade sequence, event/receive ticks, price, quantity, normalized action/side/conditions, stream epoch low/high bits, ordinal |
| 25–32 | Session YYYYMMDD, open/high/low, sequence, event nanoseconds, volume, quality |
| 33–51 | VWAP epoch low/high, ordinal, sequence, event ticks, price-volume coefficient low/high and scale, volume, eligible/rejected counts, last-price coefficient low/high and scale, valid, replay complete, generation low/high, invalid reason |
| 52 | Option-statistics presence mask: volume=1, OI=2, statistics time=4, volume date=8, OI date=16, volume time=32, OI time=64, replay complete=128, final volume=256 |
| 53-62 | Option volume, OI, statistics ticks, independent volume/OI YYYYMMDD, volume/OI ticks, observed replay volume, last event nanoseconds, last source sequence |
| 63 | Source-time API observation Unix UTC ticks for queued records; zero for non-consuming state views |

Prices use exact nanounits. Decimal coefficients are unsigned low/high bit
patterns copied through signed slots; reconstruct them without floating point.
The managed adapter maps existing market-price and session contracts directly.
Complete option risk/context events and full
Host/NATS publication orchestration remain separate integration work.



### Complete business/display risk demand

Scope action 6 accepts `{scope,pricing:{expected_digest,options}}`. Each option
contains its registered `instrument`, immutable context `identity:[low,high]`,
and boolean `required`. The complete duplicate-free batch must match the scope
pricing digest and every context before any flag changes. This changes no provider
subscription or context identity. Its completion acknowledges control admission;
fresh risk and actor readiness remain separate evidence. Stale digest/context,
partial union, duplicate IDs and retired scopes fail without partial mutation.
Application business-owner revision/lease checks precede this native control.


### Subscription acknowledgement identity and SDK chunks

The pinned SDK assigns a logical subscription ID and splits symbol batches into
500-symbol protocol messages sharing that ID, with `is_last` completing the
request. Readiness requires every requested logical ID; duplicate acknowledgments
do not advance a counter. Canonical provider messages identify the subscription
and schema (for example, ?Subscription request 1 for mbo data succeeded?); legacy
ID-only messages remain supported. Unknown IDs, malformed text or a mismatched
schema enter recovery with the full original diagnostic retained. See the pinned
`vendor/databento/src/live/protocol.rs` and Databento's
[live dispatch example](https://databento.tech/docs/examples/basics-live/live-dispatch).
This is transport evidence, not proof of current actor/trade processing.

## Source-time actual-trade v2 drain

`ifm_market_data_scope_publications_v2` returns sixteen or fewer 816-byte copied
rows and up to 4 MiB of complete opaque MessagePack `WorkerOptionDefinition`
manifests in disjoint caller-owned storage. Layout: publication[656], size/flags
[8], context[16], source-time valuation and qualified-until Unix ticks[16],
underlying quote[112], manifest offset/length[8]. Flags: captured=1,
underlying=2, manifest=4, actual non-replay option trade=8. Generation mismatch
returns zero counts. A trade that cannot fit remains queued. A valid prefix may
be returned; if the first record cannot fit, Capacity preserves outputs and queue.
Choose v1 OR v2 for a given source queue, never drain both concurrently.

Metadata is serialized only at cold preparation. Native retained payloads share
one runtime budget of 16 MiB and 16,384 live identities; each is at most 4 MiB.
Repeated identical live identities reuse their immutable owner, while changed
bytes under a live identity fail. Whole-context CAS replaces manifests atomically;
queued trades retain their source version through RAII. Cold control JSON is at
most 8 MiB including base64 metadata. Caller transfer buffers are never retained.

The managed reader deserializes before returning its pooled transfer storage.
Its cache is keyed by context identity AND SHA-256 of the complete binary manifest,
with LRU limits of 2,048 entries and 16 MiB serialized-input weight. Actual managed
object overhead is additional. Returned source records own immutable metadata.
A reference refresh cannot rewrite their source-time underlying or valuation time.

Priced application registrations require a nonzero verified publisher; native
processing rejects a mismatched provider publisher before book/pricing mutation.
The optional zero publisher exists only for low-level unqualified transport
fixtures and is not accepted by priced scope preparation.

## Official Treasury reference ownership

Configure `reference_configure_v1` after the API clock, using endpoint and numeric
DNS server sockets. It opens no connection. `reference_submit_v1` reserves one of
32 result slots and the shared operation/control identity. Mode 0 is inclusive
range; mode 1 is latest on/before the explicit API as-of date. The original
capped deadline covers cache admission, DNS/TLS/HTTP, bounded XML and serialization.
Cancellation drops the same asynchronous future and its socket before completion.

Take the standard control completion, then call `reference_take_v1` with up to
512 KiB of disjoint caller storage. Insufficient capacity preserves result and
outputs. Successful copy consumes the result. `reference_discard_v1` is idempotent;
a late completion cannot recreate a discarded pending result. Runtime release
returns Busy while a reference result remains. The managed owner tracks and
discards every remaining query before native release, including concurrent stop.

Responses are UTF-8 JSON arrays of `{date:yyyyMMdd,retrieved:UnixUtcTicks,
source:"USTreasury",rates:[{months,bits:[low,middle,high,signScale]}]}`. Decimal
rounding/signed zero match CLR NumberStyles.AllowLeadingSign|AllowDecimalPoint.
The input HTTP body is at most 1 MiB, XML at most 16,384 nodes and 31 entries.
DTD and continuation pages fail. No missing rate/tenor/date is synthesized.
Original cache retrieval times and the existing two-slot eviction policy persist.

HTTP warning availability uses bit 4 and the existing `history_warning_v1`
transactional drain for either/both configured clients. A small warning buffer
retains the complete warning; the single receiver drains until caught up.

## Futures publication assembly

Native queued outputs freeze the API processing instant before mutable-book
updates/managed delivery can alter its label. Slot 63 carries that instant; raw
source nanoseconds remain unchanged. The application event assembler uses this
source time for market events, batch boundaries, statistics and checkpoint cadence.
It performs no price-selection, session-volume or VWAP numerical accumulation.

`NativeFuturesPublication` preserves current batch capacity/full/trade/stop
behavior, emitted tick sequence, suppressed-price trade routing, VX quote marks,
replay completion, session statistics and two-second ES checkpoint publication.
Application quote-wire buffers are bounded caller reservations; successful
publisher acceptance transfers the lease, and exceptions retain owned buffers
until joined recovery/disposal. An assembler must not be disposed while an
application publisher callback still owns a borrowed lease. No native pointer
crosses an await. Physical-source predicates fence obsolete source frames.

`NativeFuturesDelivery` is the primary handler of the single runtime receiver. It
reuses sixteen copied records, drains immediately on native signals and preserves
the tail across a failing callback. The failed contribution is reported, not
silently replayed with another identity; the mandatory observer owns recovery.
On physical replacement buffered old records are checked by the assembler fence.

The C# differential fixture invokes the unchanged service's processing routine
with the exact same normalized SDK records. It normalizes only independently
generated event/command/stream/recovery GUIDs; it does not normalize values,
subjects, timestamps, units, order or publication counts. ES: 16 records / 11
matching packets. VX: 16 records / 15 matching packets. Independent activated
live routes also match; unknown publication exceptions escape to recovery.
Both profiles pass again through the single event-driven receiver. Concrete
Host/NATS and actor readiness remain unqualified and are not implied by this gate.

## FMP reference ownership

The existing DLL also exports `fmp_configure_v1` (JSON <=16 KiB) and
`fmp_submit_v1` (query JSON <=8 KiB). Configuration requires the API clock, explicit
endpoint/key/DNS and the current FMP settings; it performs no I/O. Modes 0/1/2
are Treasury range/latest/calendar. Dates are yyyyMMdd. Queries use standard
control completion before `reference_take_v1`; errors retain a classified failure
envelope. `reference_discard_v1` is idempotent and late work cannot resurrect it.

All retained reference/FMP results together are capped at 32 slots / 16 MiB.
Official Treasury results still have their original 512 KiB per-query limit.
A small transfer buffer fails without consuming/changing the native result or
caller outputs. Managed FMP copies grow from 65,536 bytes under the 16 MiB cap;
returned JSON documents never borrow a returned ArrayPool buffer. Native owner
release is Busy while retained query results exist. The warning drain and signal
bit 4 now serve history, official Treasury and FMP without losing/redacting away
provider correlation or forwarding credentials to another origin.


## Options deadline escalation

`ifm_market_data_scope_stop_signal_v1(handle, low, high)` synchronously signals
only the matching logical Options owner, with no operation reservation, queue
slot, await or new deadline. A stale generation is an idempotent no-op. It leaves
futures/reference owners untouched. Success is cancellation requested, **not**
cleanup confirmed. Awaited shutdown/replacement remains mandatory for proof.
The managed facade revokes option read authority before signaling terminal stop.
Explicit release can still remove local intent; a later explicit owner replacement
cannot restore a released lease. Native scope disposal also signals before waiting.

## Native ownership command additions (2026-10-27)

Scope action 7 imports bounded raw reviewed convention/calendar/publication/rate
policy data and physical subscription intent. Rust performs preparation, refresh,
qualification, manifest construction and source ownership. It does not import a
managed-prepared context or execute a managed callback.

The three optional scope-claim exports retain, renew and release copied intent.
Their 64-byte record contains size/flags, generation, serial, business-owner identity
and reserved words; it contains no pointer or resource handle. TTL zero is durable;
ephemeral claims are at most 120 seconds. Renew/release do not await provider I/O.
Physical stop/join proof remains separately observable. Old generations cannot
renew a replacement; stale releases are harmless. The actual managed/DLL bridge
qualifies source cancellation while unrelated scope setup is stalled.

Legacy stream-drain exports remain for historical component compatibility. Their
presence does not authorize use in the revised active feed path: streaming market
publications must originate from Rust NATS. The active facade has not cut over yet.


## Persistent native dataset owner ABI

The new `ifm_market_data_owner_*_v1` command/query group is additive. It must not
be mixed with the prototype flat/scoped live-owner ABI in one runtime. Creation
requires an explicit API clock; it does not require an option catalog or start
Treasury/provider I/O. Optional managed binding keeps older prototype packages usable.

- `owner_create`: starts idle control for GLBX futures, XCBF futures and GLBX options.
- `owner_catalog`: copies complete raw reviewed option/catalog/source configuration.
  Native catalog replacement preserves active business claims; an incomplete
  replacement cannot drop a live leg. Future-only startup works without this call.
- `owner_futures`: accepts native futures intent. Returning success means accepted
  intent, not that transport/pricing/publication readiness has been certified.
- `owner_command`: bounded cold JSON for scope configuration, stable-owner enable/
  disable, explicit option clear and desired-feed stop. Durable business ownership
  needs no managed lease object, renewal loop or lifecycle semaphore.
- `owner_health`: fixed 64-byte copied phase/proof/ticket/deadline/journal counts.
- `owner_state`: fixed 656-byte non-consuming state query. It reuses the qualified
  exact native scalar layout. Streaming publications still go directly to NATS.
- `owner_failures`: explicit bounded query of at most 48 terminal records. The
  maximum encoded result is 4 MiB; undersized buffers consume nothing.
- `owner_failure_ack`: consumes terminal records only after external UI-delivery
  confirmation. A broker PONG or log acceptance receipt is not UI confirmation.
- `owner_recover`: native whole-dataset reconstruction of unchanged accepted intent;
  repeat incident commands cannot extend the original 120-second deadline.

Native query views retain numeric book data only, with no provider client, native
child, task, lease or foreign callback. Retirement invalidates availability even
when old numeric views are retained. The actual SDK integration verifies this.
Runtime stop signals all services and waits for actual tracked exit before release.

Provider diagnostic messages use a separate direct native NATS request/reply path.
The existing ten-key copied log DTO preserves unknown codes/text and source-time
worker identity. A fixed five-key hash receipt confirms external **log acceptance**
only; sharing its bounded receipt codec with evidence does not turn it into durable
trade persistence or authorize an actor event. A stalled logger retains one copied
record; the persistent 256-record ring replaces oldest records with an explicit
accumulated replacement count. Cancellation restores an unconfirmed pending record.
Ambiguous receipts may replay a log; no exactly-once physical-disk guarantee is made.
The Host logger is a passive external IO consumer on a separate broker connection.
It owns no Rust feed resources. Unknown native logging-task exceptions reach dataset
recovery; known external log-transport retries do not certify or extend feed recovery.

### Current cache query and terminal publisher

`ifm_market_data_owner_cache_v1` takes a bounded UTF-8 scope (<=128 UTF-16
units / 512 UTF-8 bytes), caller-owned output (<=4 MiB+4096) and required length.
Zero length means no current unexpired cache. Insufficient capacity reports the
required length without consuming anything. The existing five-key plain Host
publication bytes are returned; the query retains no provider resources.

Terminal UI events use an independently tracked notification child: one slot
separate from the three dataset slots. The current twenty-key actor event and
subject are published directly by Rust. Stable runtime/lane/ordinal-derived
identities survive retries. A broker PONG never deletes the native failure journal.
Only explicit confirmed-display acknowledgment removes records. Until UI display
ACK integration is complete, pending failures retry every five seconds; there is
no idle timer. One native owner epoch is restricted to the same ValueDate per lane
to prevent replaying an old failure under a later date.

## Complete price queries

`owner_raw_prices_v1` copies existing complete trade/quote DTOs and physical
identity into at most 4 KiB. `owner_quote_greeks_v1` and `owner_trade_greeks_v1`
copy frozen full-Greek DTOs into at most 16 KiB. Empty means unavailable, never
fabricated zero. Trade Greeks require the exact durable-acknowledged source to
remain current. No query transfers a resource, subscription or renewal callback.

`owner_identity_v1` is 56 bytes. Flag 1 means an actual copied publication header;
flag 2 means accepted non-idle intent. Retained identity is not readiness proof.
Queries do not consume source data. Runtime stop retires its command directory
before waiting for the actual owned tasks to exit.


## Raw monitored commands and bounded stale-evidence accounting

Owner capabilities currently report `0x7ff`. Flags describe available kernel ABI,
not end-to-end Host acceptance. `owner_catalog_v1` accepts `raw_only` with complete
45-field raw option models, copied endpoints and global policy. It constructs
plans/dependencies/calendars in Rust. Optional `monitor` carries workflow type,
workflow ID, leg ID and canonical contract. Rust derives a length-prefixed SHA-256
business identity and accepts durable demand. `unmonitor` releases that exact
business identity; physical generations do not participate in the identity.
Native scope configuration/claim admission is atomic; copied catalog additions
may remain after rejected demand but cannot remove another accepted claim.

Raw IO kind 3 stages missing immutable reference facts. The model is at most
8 KiB, within the 16 KiB request limit. External validation/persistence receives
copied bytes only and cannot retain a dataset task or block native retirement.

`owner_retired_evidence_v1` returns a fixed 40-byte structure: size/reserved u32,
retained outboxes u64, evicted generations/records/bytes u64. Invalid size/pointer
leaves output untouched. At most four old outboxes / 64 MiB are retained, plus
one active 16 MiB outbox. Duplicate archive insertion is idempotent, completed
entries are pruned, and bounded eviction is accounted exactly. These are provider
market-data prints, not user order or execution records. Queries never replay
old evidence, renew claims, acquire resources or require broker/database access.


## Native complete startup and public recovery control

`owner_start_v1` accepts one complete copied futures-demand array. Native code
validates all lanes before changing desired state, constructs Trades/MBP1/
Statistics subscriptions from raw roots, and resolves exact source mappings.
`owner_wait_v1` selector zero observes all futures controls; selectors one through
three observe one lane. These observers use native dataset recovery deadlines,
not a second competing operation timer. Root/caller cancellation still drops the
observer and releases its bounded completion reservation.

`owner_backend_status_v1` returns the existing nine-key backend status (<=64 KiB).
Native intent, actual source identity, health, real PID, native API time and native
monotonic observation revisions are authoritative. Host RPC readiness is a copied
boolean and cannot certify dataset readiness. Healthy idle is distinct from live.

`owner_read_snapshot_v1` returns the existing eight-key complete catch-up DTO
(<=16 MiB). Models, manifests, last frames, prepared caches and workload evidence
are copied inside Rust. Bounded complete results are not silently truncated;
owner/ticket/phase changes during capture reject the mixed observation. This is
an explicit startup/reconnect query, never a streaming drain or managed lease.

`owner_recovery_request_v1` accepts copied role/dataset, API date, operation and
expected source identities. Native authority applies stale-owner fencing and
idempotence before returning from submission. Its observer uses the existing
bounded copied-result protocol (<=8 KiB), with no independent recovery timer.
Cancelling the observer cannot erase accepted recovery. No C# gate, retry loop,
reference preparation or source reconstruction is invoked. A failed result never
claims cleanup that was not proved by completed native readiness.


#### Public native owner command/query qualification

The current feature mask is `0xfff`. The actual DLL/C# gate exercises complete
backend status, eight-key read snapshot, the nine-key recovery result and the
snapshot-or-typed-failure composition result. These are copied results; they hold
no physical source owner or callback. Recovery waits use native-health observers
without an additional recovery timer.

`claim-scope` atomically validates membership and acquires one stable native
business owner. It accepts either durable intent or an explicit UTC expiration
interpreted by the native API clock and converted to monotonic lifetime. Failed
validation does not leave a scope or claim. Repeated starts join unchanged native
setup/recovery; they do not rebuild a healthy source or renew an incident deadline.


### Complete-owner query/background capabilities (current mask `0x7fff`)

The current owner requires canonical binding/mark/risk queries and native
background discovery/maintenance in addition to the earlier owner ABI. Streaming
remains direct native NATS. All 27 owner exports are declared in the C header.

NativeOptionWorkloadEvidence format 1 appends two keys to its original six-field
MessagePack array: FormatVersion=1 and NativeResourceProofsComplete. The latter is
true only for the exact native supervisor's fully proven Running ticket. Its
ReadyScopes refer to resource readiness, not availability of executable financial
candidates. Every owned background lookup is included; up to 64 explicit scopes, 64 canonical monitored scopes, 64 deferred
discovery scopes and 64 background lookups are supported (256 total). Version 0 retains the original 64-scope
and selected-publication compatibility proof. Old readers skip appended fields;
new readers default missing fields to version 0 / false. Loaded-trade per-leg,
risk, plan and publication acceptance remains independent and mandatory. Unknown
enabled-trade intent remains null until the Host's business-intent store supplies
it; the native adapter must never manufacture an empty list.


Complete-owner copied queries include `scope-receipt` and `scope-discovery` on
prepared-cache query ABI. A rejected or complete-empty deferred demand is removed
only after its result was actually copied, never during a sizing query. Accepted
scope demand remains native until explicit release/session close; cancellation of
an observer alone does not withdraw accepted desired work.

Composition commands optionally include boolean `wait` (absent means false).
Options readiness then waits on native raw-observation/pricing notifications,
with generation fencing and immediate source retirement. Only QuoteUnavailable,
UnderlyingQuoteUnavailable, StaleData and IncoherentQuotes retry. The original
request deadline is capped at 120 seconds and never renewed. This is a query
budget, not a new dataset recovery episode. No FFI streaming or managed polling
is introduced.


Raw fact kind 7 additionally accepts an exact `underlying` and equal `from`/`until`
expiry, optional `provider_roots`, and `browser:true`. These are copied SQL filters,
not managed selection policy. Browser rows append original option-type text to
24-field compact source rows; background-only rows cannot supply browser ordering.
All existing snapshot/page/digest/byte/count bounds remain in force.

Raw fact kind 8 returns four copied candidates: current EOD, previous EOD, daily
Bollinger and five-minute Bollinger. EOD close and indicator prices/deviations are
invariant Decimal text; raw EOD double deviation is hexadecimal binary64 bits.
Rust selects fallbacks and dates. This port holds no runtime, context, lease or
provider reference, and never invokes a native resource from a storage callback.


The existing owner-command export accepts `scope-alias` with copied owner,
expected generation, prior scope name and a 64-hex sealed business plan name.
It cannot acquire, renew or rebuild provider resources. Native validation rejects
ended/unqualified owners, stale generations and names concealing different
accepted mappings; excluded candidates do not enter the sealed plan. Exact
retries are idempotent. Names remain native desired metadata through physical
recovery. Released copied entries are reclaimed before collision checks, avoiding
permanent rejection when another owner reuses the same immutable plan.


## Complete owner capability and final copied-fact boundaries

Feature mask **0x1ffff** includes the complete browser/statistics owner bit
0x10000. The selected complete-owner Host rejects an older 0xffff DLL rather
than silently composing managed live lease/preparation services around it.
No additional streaming callbacks or C# feed pumps are introduced.

Raw fact kind **10** copies saved option statistics as bounded JSON rows. The
managed responder does not choose the eligible date/watermark or merge replay;
those policies and logical counters are native. The response is capped at 4 MiB
and 100,000 rows, with selected native retention bounded to current desired
instruments. It is a passive persistence port, not a provider/lease owner.

Browser commands/results reuse the retained command ABI, with bounded adaptive
copied-result buffers (up to 16 MiB). Owner membership, metadata/window policy,
view expiry and source reconstruction remain native. An abandoned managed
observer cannot retain a physical source. A complete syntactically empty scope
uses one-shot bounded native result evidence and opens no provider resource.
Host and API disposal aliases observe one native join result.

See `QUALIFICATION-REPORT.md` for the accepted tests and remaining live gate.
