# Behavioral differences and acceptance disposition

Baseline: commit `8f5c89f93430eea99600d1ffe04da19acfa36353` and the frozen
`baseline/CurrentBackend.json`. Exact financial/source values remain mandatory.
This register does not grant production acceptance or numerical tolerances.

| ID | Current backend | New backend | Reason, evidence and disposition |
|---|---|---|---|
| NATS-LZ4-001 | MessagePack-CSharp emits its own LZ4 token/block choices. | Rust emits standards-compatible LZ4 in the same extension-98 framing. | All 21 envelope role/kind variants have identical decompressed contract bytes and subjects. `StreamingWireBridge` also decodes every Rust frame with the actual C# production formatter and reserializes identical content. Compressed byte equality is not a contract requirement; no event value is normalized. This component evidence does not qualify the unwired full native publication path. |
| OWNER-001 | Options recovery requires a different positive child PID. | Real Host PID stays unchanged; logical owner and native generation change after joined replacement. | User approved Tokio-only ownership and the narrow consumer/contract exception. `OptionRecoveryReplacementProofTests`, `OptionPipelineRecoveryRuntimeTests`, `OwnerReplacementBridge`. Old/new MessagePack status shapes are tested in both directions. Existing C# PID rule remains enforced. |
| DNS-001 | SDK/system DNS can outlive the caller's cancellation. | Explicit numeric recursive TCP DNS endpoints, bounded exchanges and owned cancellable sockets. | Required by the approved no-child-process lifecycle. `dns_lifecycle` includes stalls, cancellation, invalid packets and an actual 120-second deadline. Deployment DNS reachability remains a separate gate. |
| META-001 | SDK allocates metadata using a wire-derived length. | Fixed 65,536-byte streaming input scratch; retained metadata and individual fields have hard bounds. | User explicitly requested bounded streaming everywhere. `metadata_streaming`, `metadata_memory`, patched SDK/DBN entry-point tests. Oversized retained semantic data returns a typed error; no truncated successful decode. Valid supported metadata matches the SDK oracle. |
| ID-001 | Worker IDs, request IDs and local receipt timestamps belong to the process backend. | IDs and receipt timestamps belong to independently run native owners. | Qualification compares required identity relationships, ordering and lifecycle transitions. Provider timestamps, ValueDate, prices, quantities and semantic digests are not normalized away. `FuturesPublicationBridge`, `NatsPublicationBridge`, pricing/definition oracles. |
| FMP-NULL-001 | A calendar array containing `null` can throw `NullReferenceException`. | Invalid provider content becomes an explicit contract/decoding failure. | `FmpBridge` documents the malformed payload and compares the other 60 calendar cases. No valid financial output differs. Explanation recorded; final acceptance disposition remains open. |
| DOWNLOAD-001 | An old caller-supplied batch-file descriptor can be used without proving it still matches the provider descriptor. | Revalidates descriptor identity before writing; failed size/checksum validation never promotes a provisional file over an existing target. | `HistoricalBridge` exercises descriptor mismatch, checksum, cancellation and preserved targets. Intentional input-integrity safeguard; final acceptance disposition remains open. |
| FAULT-001 | Explicit Development injection hangs the Options worker process. | Explicit Development injection reports a classified Options processing fault to the actual recovery monitor. | There is no child process to hang. Both require Development **and** `MarketDataRecovery:DevelopmentVerification:Enabled`, plus current owner/generation/date and a running target. Native transport stalls and uncooperative callbacks are tested independently. A successful injection response is not itself recovery proof. Final live harness acceptance remains open. |
| VX-001 | Historical bootstrap discovery uses a periodic service. | Session activation signals one bounded background owner. | `VxBootstrapBridge` compares ten stored/history/cost/invalid/cancellation cases against a separately executed current Host oracle. `VxBootstrapServiceBridge` proves the estimate does not become a live trade or falsify feed health. Values/source dates remain identical. |
| UNDERLYING-001 | Background coverage can fail when a required non-primary ES future is absent from the active catalog/reader. | Native option ownership acquires an exact bounded quote dependency without changing primary futures subscriptions. | Required to cover the actual published expiry range. `OptionUnderlyingDependencies` proves native definition/quote acquisition and whole-owner restoration, with no invented price or option pricing context. Public hot readers still perform no acquisition. |

XCBF 105/106 handling and authenticated batch download routing are parity fixes,
not tolerated mismatches: mixed publisher event bytes match the current C# service,
and download routing follows the pinned SDK's `get_with_path(url.path())` behavior.

WORKLOAD-001: User approved and implemented the native-only no-position exception.
Explicit confirmed idle ownership or ready background subscriptions with a fresh
eligible cache publication can pass without a trade plan. Unused fallback scopes
need no selected publication, matching current cache policy. Enabled trades cannot
use the exception. Existing C# behavior and all loaded-trade checks remain intact.
57 focused tests and actual native Host/PostgreSQL idle recovery pass. Live native
trade/recovery acceptance remains separate.

The additive Stage 4 owner-aware subscription methods retain the current
backend's `Disabled` results. This is matching behavior, not an implemented
product feature being silently disabled in Rust.

No unexplained financial mismatch is accepted. Complete real-provider,
Host/API/actor and UI acceptance is recorded separately from these component gates.


DIAGNOSTIC-BOUND-001: In the revised native path, diagnostic saturation replaces
oldest log records in a 256-record ring and carries an accumulated replacement
count. It does not turn a logging-consumer stall into a new datafeed exception.
The provider error itself is still classified separately, and terminal health is
retained outside this log ring. The original C# backend is unchanged. The legacy
native diagnostic FFI shape is preserved; replacement counts are available in the
new direct native diagnostic wire contract. Full consumer registration remains
an integration gate. `bounded_log_replacement_keeps_latest_full_error_and_exact_loss_count`
checks 1,000 errors, exact 744 replacements and preserved unknown provider code,
last flag and message text.

The schema 1/2 premium-style correction and decoder root-error propagation are
parity/correctness fixes, not accepted unexplained financial differences. The
actual source-ingress C# oracle normalizes DBN integer nanounits exactly as the
current source reader does; constructor-only decimal scale fixtures remain
separate and continue to preserve their original scales.

### CURRENT-RESULT-BOUND-001 (new native query)

Current-future/term-structure selection preserves existing normal and failure
results. An otherwise successful request exceeding 4,096 selected contracts is
rejected by the native copied-result capacity rather than allocating an arbitrarily
large DTO graph on a caller thread. Input query rows remain capped at 100,000 and
encoded output at 2 MiB. Normal ES/VX current/next queries are unchanged; 90 actual
C# fixtures and the actual DLL/C# path pass. This capacity difference is explicit
and does not qualify untested oversized operations.


### NATIVE-CACHE-PUBLISHED-TIME-001 ? resident status origin

The new query-only native cache reports `PublishedAtUtc` from its frozen native
capture time. The former C# receiving cache reported its local acceptance time
when a copied publication arrived. Direct native publication removes that
managed receiving hop. Both retain the original source/context validity and
version; no expiry or quote-age allowance is extended. Tests compare eligibility,
reason ordering, versions and snapshot data independently of broker transit time.
The former backend and its receipt-time semantics are unchanged.


### NATIVE-IDLE-STATUS-001: healthy idle is not a running feed

Native backend status reports `Healthy=true`, `Running=false`, `Lifecycle=Idle`
when a reusable dataset controller has no desired subscriptions. The earlier
prototype could report an idle controller as a running feed. This distinction
prevents the UI from claiming live market data is running without accepted demand.
The status retains the actual Host PID and actual native owner/generation IDs;
no child process identity is invented.


### NATIVE-EMPTY-PREPARATION-GENERATION-001: complete-empty business evidence

An authoritative empty option scope has no physical native source or quote
generation. Its immutable empty composition preparation uses the actual native
Host instance identity as its nonempty business generation. The old backend used
its admitted worker generation even though the snapshot contained no instruments.
No quote, price, rate, position or execution eligibility is manufactured. Both
paths preserve the complete-empty scope hash, frozen evidence/hash validation and
no-recapture rule. Nonempty preparations always use the actual dataset generation.
This difference requires explicit complete-empty integration qualification; it is
not qualified by the nonempty preparation test.


### NATIVE-IDLE-DIRECTORY-001: unstarted dataset controllers

An unstarted complete-owner Host exposes three idle dataset controllers, rather
than an empty dataset status list. They report no active ValueDate, no running
feed and no SDK subscriptions. This makes independently resettable resources
visible before activation without inventing provider connectivity. The executable
qualification explicitly checks these constraints.

### NATIVE-BROWSER-CAPACITY-001: bounded native view ownership

Native browsing bounds total views to 64, retained required references to 8,192,
selected distinct instruments to 2,048 and compact source rows to 100,000. An
excess fails explicitly before committing partial membership; results are never
silently clipped. The prior static managed dictionaries did not enforce these
aggregate bounds. Existing window/financial behavior is retained within capacity.
View expiry remains 90 seconds and native metadata maintenance remains 30 seconds;
neither creates an additional dataset recovery timeout.


### NATIVE-DATASET-SCOPE-001: scope descriptors and physical connections

The complete native owner preserves `SeparateContractConnection` and
`IndependentChainConnection` in copied public scope/reconstruction descriptors.
They do not create an additional process or independently supervised SDK client
per business scope. Native subscriptions are grouped under the independently
owned dataset/role (GLBX futures, XCBF futures, GLBX options). This is the requested
single native dataset cancellation/join/rebuild boundary. Shared instruments use
one qualified source and one logical statistics watermark; removing one business
owner cannot remove another owner's desired membership. Conflicting immutable
reference recipes for the same instrument are rejected rather than combined.

This intentionally changes the physical topology, not provider timestamps,
ValueDate, prices, statistics or financial formulas. Whole-dataset SDK recovery,
shared owner release, committed recipe replacement and replay accounting have
explicit tests. Process/PID identity differences are covered by OWNER-001.

### NATIVE-EMPTY-SOURCE-001: no physical source for authoritative empty scope

A complete, syntactically empty native discovery is bounded copied business
evidence, not subscription demand. It neither opens nor replaces an unrelated
SDK source. The one-shot result is capped at 64 pending owners and 120 seconds
of retention; duplicates do not extend retention. This retention is not a
recovery timer and does not grant a new recovery allowance. Explicit consumption,
superseding accepted nonempty demand and stopping feeds remove the result.
Actual C#/DLL preparation validates the complete-empty business hash and its
native Host fence, immutable replay and unchanged SDK connection count.
