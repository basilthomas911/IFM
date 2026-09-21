# Stage 2 readiness and identity decision

Date: 2026-09-19. Status: Stage 2 implementation and scoped fixture verification complete. Stage 1 remains complete. Production activation remains gated.

The [final Stage 2 implementation and verification record](Option-Pricing-Stage-2-Implementation-and-Verification.md) supersedes every progress/remaining-work statement below. Durable trade acknowledgment/storage, external selection snapshots, historical versions, actor Add/Change transport, populated UI scenarios and crash/retry tests are now implemented and verified. The sections below are retained as historical progress and failure evidence, not an active blocker list.

## Historical progress

### Implementation update after refresh-policy approval

The user approved configurable **test defaults** of 250 ms for chain price/Delta scheduling, 5 seconds for IV refresh, and 1 second for selected/held full-risk scheduling. This is not production approval. The bounded policy is configured at `AppSettings:Databento:OptionPricingRefresh`, forwarded to supervised worker processes, and validated there. Production chain admission rejects the default policy before allocating a provider feed. Production requires an explicitly approved, separately versioned policy and review evidence identity. These are scheduler cadences, not a guarantee that every contract in a maximum-size American chain can be recalculated within each interval.

Additional implemented work:

- Shared asynchronous, cancellable, snapshot-pinned provider selector in both futures and futures-option Add/Change flows; bounded storage pages and HTTP/NATS/actor query wiring. Filters include dataset, root, exchange, UTC expiry, right, underlying provider instrument ID, strike range, and lifecycle. Changed filters invalidate displayed selections. Provider facts are read-only; reviewed conventions are entered separately. Options require the exact existing underlying future.
- Immutable provider/IFM identity reservations and collision rejection, staged immutable reference versions, and publication after base/projection writes. Reviewed option pricing conventions derive from the same reference version. Unpublished versions cannot be returned as qualified pricing contexts. Prior exact versions remain readable.
- Schema-3 routing through the unified European/American futures pricer, with explicit strike, right and premium style. Appended fields are propagated through the domain composition adapter without changing older field numbers.
- Worker-local bounded/coalesced price/Delta calculation with separately refreshed IV and its original source provenance. Quote ingress performs no option calculation. Held/selected business ownership requests a separate full-risk schedule. Stale quotes, stale underlying observations, changed reference context and worker generation invalidate usable outputs.
- A separate full-Greek trade-price calculator uses the actual trade mark and explicit Trade provenance; it does not substitute quote-midpoint IV. Missing inputs retain explicit failure and no usable Greek values.
- Selector disposal is repeatable; imported provider fields are locked during Change; visible filter labels and minimum-window layout are checked.

Latest verification (these results do not close Stage 2):

- Domain market-data unit suite: **224 passed**, zero skipped/failed, `stage2-import-contracts.trx`.
- Application market-data full suite: **507 passed, 5 skipped**, zero failed, `stage2-marketdata-full.trx`. One earlier run failed a now-corrected unsupported-schema fixture and a native read-buffer-release worker-reset test. That reset passed in isolation and on the subsequent full-suite run; its intermittent failure is recorded, not silently discarded.
- Combined pricing/worker selection: **63 passed, 1 skipped**, `stage2-pricing-combined.trx`. The skipped case is the opt-in sustained-load/100-reconstruction test, not a successful load qualification.
- Disposable Scylla reference publication and definition paging: **2 passed**, `stage2-reference-selection-storage.trx`. This includes old/new exact versions, unpublished-version exclusion, immutable-content rejection, collision rejection, paged snapshot fencing and an old catalog requiring index refresh.
- Selector/editor view-model regression: **19 passed**, `stage2-selector-editors.trx`.
- Selector rendering checks: **3 passed**, `stage2-selector-rendering.trx`, with bitmap artifacts under the UI system-test output `artifacts/stage2` directory. These check actual initialized WinForms layout; they do not replace end-to-end Add/Change save automation.
- API server and UI Views builds passed with zero warnings/errors.

**Open implementation and verification work:** retained option-trade evidence is not yet durably written through the worker-to-host path to Scylla; the existing worker publication pipe is one-way and is not a database acknowledgment. The trade calculator alone does not satisfy retention. The coalesced selection result remains a worker-local read API; external selection snapshot/consumer propagation still needs completion, and existing composition capture can still perform full-scope valuation on demand. Complete historical/effective-version queries, concurrent/crash publication tests, real HTTP/NATS actor qualification, populated UI Add/Change workflow automation, sustained-load evidence, prior-stage regressions and isolated application startup qualification remain open. No live API/UI restart, production policy activation, application-keyspace migration or broker order was performed.

The foundation list below is historical context. Items implemented in this update supersede the older remaining-work list; the explicit open-work list above is authoritative.

The user approved ordinary fractional notation: ES20260918C6500.5. The proposed D separator below is superseded and must not be implemented.

FuturesOptionContractId exposes decimal StrikePrice, accepts exact invariant fractional strikes, and provides canonical Create and exact TryParseStrike methods. New formatting strips unnecessary fractional zeros; existing saved IDs are preserved. Inputs that decimal.TryParse would silently round are rejected. Numerical option-pricer kernels remain double precision.

The two EconomicCalendarRangeTests calls were changed to the existing ExecuteAsync API. Three equivalent stale securities-test calls were also repaired. No calendar runtime behavior changed. Normal unit suites now compile and pass without excluded source files.

For diagnostic isolation only, the same identity test command was run with -p:DefaultItemExcludesInProjectFolder=**/EconomicCalendarRangeTests.cs. All 22 FuturesOptionContractIdTests passed (zero failures/skips), covering exact fractions, legacy IDs, overflow/precision loss, culture, MessagePack entity round trips and actor-subject parsing. Evidence: Domain.MarketData.UnitTests/TestResults/fractional-strike-ids-isolated.trx. This excluded-file run is not a full-suite or Stage 2 gate pass and is not live NATS qualification. One initial fixture expected rejection of a representable decimal; the fixture was corrected to exceed decimal precision before the final pass.

Implemented foundations:

- Existing futures and option MessagePack keys 0-10 remain unchanged. Appended fields carry provider identity/evidence, UTC times, conventions, decimal economics and review/version metadata. Canonical StrikePriceDecimal is appended; legacy double key 9 is retained and conflicts are rejected.
- Reference qualification distinguishes legacy/unknown/draft from reviewed usable facts. Missing style, calendar, underlying or evidence does not become a guessed default.
- Additive referencePayload columns on the four securities base/projection tables preserve the complete reference payload. Reads reject corrupt, oversized and inconsistent payloads. Existing startup schema creation includes the additive migration.
- The option editor accepts/displays exact fractional strikes and previews canonical IDs. Change preserves metadata and the stored ID; identity fields are locked and identity replacement requires Add.
- Securities adapters preserve input reference records. Imported option references bypass legacy broker enrichment; legacy fractional references retain their exact decimal strike and non-rounded local symbol.
- The Databento JSON definition summary now projects raw fixed-point strike/tick values, multiplier and maturity week. Provider undefined sentinels remain null; raw JSON is unchanged.

Remaining Stage 2 work (not a new approval request): paged/cancellable Databento selectors in both editors; immutable provider identity and collision enforcement across concurrent saves; one reviewed version publication workflow with history and exact underlying validation; American/European chain router integration; bounded/coalesced snapshots and IV reuse; retained trade full-Greek enrichment/provenance; required BDD/API/actor/live-transport/UI automation and startup gates. These are not claimed complete by the foundation tests.

## Verified baseline

Latest implementation evidence:

- Domain.MarketData.UnitTests: 221 passed, zero failed/skipped; TestResults/stage2-reference-contracts.trx.
- Domain.MarketData.Securities.UnitTests: 20 passed, zero failed/skipped; TestResults/stage2-securities-unit.trx.
- InstrumentDefinitionClientTests: 4 passed, zero failed/skipped; Framework.MarketData.DataBento.UnitTests/TestResults/stage2-definition-metadata.trx. Native build emitted existing unused-function/linker-output warnings.
- Existing futures/option editor view-model regression tests: 17 passed, zero failed/skipped; UI.Net.Presentation.UnitTests/TestResults/stage2-reference-editors.trx. This is not automated verification of the new selector or visible WinForms controls.
- UI.Net.Views build after editor changes: zero warnings/errors.
- Disposable Scylla metadata/paging tests: 2 passed; Storage.IntegrationTests/TestResults/stage2-reference-storage.trx. Tests cover migration of a pre-existing legacy row, repeatable schema creation, decimal round trip after reopening the context, and projection backfill/paging.
- Only uniquely named test-owned keyspaces were created/dropped. No application database migration or live reference-data update was performed. Restart of the actual API/UI remains unverified.

- Application.MarketData build: passed, zero warnings/errors.
- UI.Net.Views build: passed, zero warnings/errors.
- Existing FuturesContractEditorViewModelTests and FuturesOptionContractEditorViewModelTests: 17 passed, zero failed/skipped. Evidence: UI.Net.Presentation.UnitTests/TestResults/stage2-baseline-editors.trx.
- Local TCP endpoints for Scylla (9042), NATS (4222), PostgreSQL (5432), and Redis (6379) are reachable. Reachability is not storage/transport integration qualification.
- Existing unrelated worktree edits were preserved. The original readiness review made no application changes; the subsequently approved identity implementation is described above. No migrations or reference-data writes have been performed.

## Original identity decision (resolved)

The approved plan requires decimal strikes, deterministic conversion to the application ID convention, preservation of existing IDs, and collision rejection. The existing convention does not represent fractional strikes:

- Domain.MarketData.Shared/FuturesOptionContractId.cs parses the strike as an integer and exposes an integer StrikePrice.
- Domain.MarketData.UnitTests/FuturesOptionContractIdTests.cs explicitly rejects ES20260910C6500.5.
- UI.Net.Views/MarketData/FuturesOptionContractEditorControl.cs accepts integer strikes for Add/Change and displays them with F0.
- Domain.MarketData.Securities/FuturesOptionContract/Command/Model/FuturesOptionSecuritiesContract.cs reconstructs IDs with integer formatting rather than preserving the supplied ContractId.
- Application.MarketData/Pricing/ReviewedEsOptionReference.cs restricts its reviewed profile to integral strikes.

Appending a decimal strike property to the reference schema alone does not resolve the identifier contract. Rounding a fractional strike for its ID is not acceptable. Choosing a new persistent ID grammar needs confirmation because the original requirement calls for the existing application convention.

Approved extension: preserve existing IDs byte-for-byte; use an invariant decimal point for fractional strikes, for example ES20260918C6500.5, with no trailing fractional zeros in newly generated IDs. Store the exact decimal strike and provider identity separately. Distinct provider instruments that map to an occupied IFM ID must still fail with an explicit collision; never invent a suffix to bypass that check.

The original isolated parser test run above is historical evidence. Current reference/storage/editor foundations and outstanding work are listed in Current progress. No Stage 2 completion claim is made.
