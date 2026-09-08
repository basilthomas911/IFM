# Order Composition Prerequisite Implementation Plan v1.0

Current closure status and schema-2 premium tick-rule requirements: [closure audit](OrderComposition-Closure-Audit-v1.0.md).

The approved Treasury source change is implemented: see [official Treasury provider and verification](OrderComposition-Official-Treasury-Implementation-v1.0.md). This supersedes the FMP Treasury source/lineage requirements in the original baseline below. FMP still supplies economic calendars.

| Item | Value |
| --- | --- |
| Date | 2026-09-07 |
| Status | Runtime prerequisites and reviewed September reference publication implemented; committed ownership/recovery, workflow preparation and bounded qualification executed; current elapsed live status is in the closure audit |
| Purpose | Resolve pricing, contract-universe, live-chain and snapshot dependencies; establish a complete basis for the Order Composition implementation document |
| Scope | ES outright futures and verified European-style ES options on futures; one triggering Daily, Weekly or Monthly horizon |
| Specification | [Order Composition specification v1.0](OrderComposition-Specification-v1.0.md) |
| Pricing authority | [Stage 4 pricing specification](../../../../../../Documents/system/Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md) |
| Runtime dependency plan | [Market Data Stage 4 implementation plan](../../../../../../Documents/system/Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md) |
| Actor authority | [System actor conventions, section 13.3](../../../../../../Documents/system/Actor-Implementation-Conventions.md#133-functionactor-convention) |
| Current evidence | [Prerequisite implementation record](OrderComposition-Prerequisite-Implementation-Record-v1.0.md) |

This is the prerequisite plan. The [full composer implementation document](OrderComposition-Implementation-Plan-v1.0.md) and linked implementation record now exist separately. Names introduced below are proposed until identified as implemented in that record. The initial version was documentation-only; the subsequent implementation delivers a tested foundation, not completion of all packages. Historical counts do not establish that every package passes.

## 1. Decisions fixed for implementation

1. The initial option universe includes only verified European-style **options on ES futures**. Exclude American-style and unknown-style definitions before subscription/candidate generation. European stock or cash-index options are not added by this decision. Outright futures remain supported independently.
2. Validate the actual option series and every leg; neither the ES root, an expiration date, nor the workflow horizon establishes exercise style. Unknown classification is not a default to European. Preserve excluded definitions and reasons for audit; do not delete raw reference records.
3. Reuse the existing Black-76 implementation. Do not introduce an American-option approximation, replace the engine, or treat successful arithmetic as proof of contract support.
4. Use official daily U.S. Treasury par/CMT data through the existing `ITreasuryCurve` contract. Select one tenor from remaining exchange trading days: 0..29 one month, 30..59 two months, 60..89 three months; 90 or more is `TreasuryHorizonUnsupported`. No interpolation, extrapolation, adjacent-tenor fallback or per-tick HTTP.
5. Use the verified source convention to convert the selected rate to a continuously compounded annual decimal. For verified nominal semiannual CMT quotations, retain `FlatSelectedCmtProxy/v1`; this is a flat par-yield proxy, not a bootstrapped zero curve. Unknown source convention fails.
6. Trading-day tenor selection, elapsed-day strategy DTE, and pricing year fraction are three separate quantities. Exact expiry, market timezone, versioned calendar and product-specific day-count mapping determine the pricing inputs. No global ACT/365 fallback.
7. Missing, stale, incoherent or invalid required pricing inputs and solver failures produce structured `Failed` outcomes. `NoCandidate` means a complete, trustworthy evaluation found no permitted construction. Valid economic rejection is not a pricing failure.
8. All twelve catalog variants remain eligible for any one supported horizon: long/short futures, four credit/debit verticals, and long/short iron condors with balanced/bullish/bearish bias. Each invocation consumes the one accepted selection, exact ConfigurationDb versions and Fund authority. No family/timeframe shortcut.
9. Composition produces one normalized unit. Portfolio Risk Management owns final units and financial authorization. IBKR emulator precedes live broker integration; neither broker connectivity nor UI changes are prerequisites for these calculations.

The current specification is corrected alongside this plan: fixed ACT/365F assumptions, continuous-zero interpolation and missing-pricing `NoCandidate` language are superseded. Existing numerical strategy values remain offline engineering fixtures, not approved live settings.

## 2. Verified baseline and concrete blockers

Read-only repository review on 2026-09-07 established the following. Paths link to current implementation, not proposed APIs.

| Boundary | Existing implementation | Remaining gap |
| --- | --- | --- |
| Definition queries | [DatabentoMarketDataQueries](../../../../../../TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoMarketDataQueries.cs) resolves option definitions by maturity/right/underlying | Definition availability is not a supported-series classification |
| Chain definition DTO | [FeedContracts](../../../../../../TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedContracts.cs) carries right, strike, optional exact expiry, tick and multiplier | No explicit qualified exercise-style/settlement/calendar/day-count contract |
| Framework chain session | [DatabentoOptionChainSessionManager](../../../../../../TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/DatabentoOptionChainSessionManager.cs) processes quote/trade updates through an enricher | A production pricing adapter and application/worker wiring are missing; only the test fake was found implementing the enricher |
| Current chain state | [OptionChainStateStore](../../../../../../TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/OptionChainStateStore.cs) returns copied session state | A copy alone does not establish source-time, pricing-context or generation coherence |
| Application start | [DatabentoMarketDataEpoch.StartOptionChainAsync](../../../../../../TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataEpoch.cs) throws for missing Treasury session rate | Replace this guard with validated context construction and real lifecycle wiring, not an unconditional success |
| Typed subscription API | [IMarketDataApi](../../../../../../TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs) has additive owner-aware APIs returning Disabled by default | Host intent exists as an offline subset; deployed ownership, physical routing, recovery and public integration remain dependencies |
| Startup guard | [Stage4SubscriptionOptions](../../../../../../TomasAI.IFM.Application.MarketData/Subscriptions/Stage4SubscriptionOptions.cs) rejects enabled startup | This is an explicit readiness boundary, not a switch to turn on to make chains work |
| Treasury API | [ITreasuryCurve](../../../../../../TomasAI.IFM.Framework.MarketData/Contracts/ReferenceData/ITreasuryCurve.cs) fetches latest/range snapshots | Continuous conversion, verified convention metadata and publication-aware pricing admission remain |
| Pricer | [OptionCalculator](../../../../../../TomasAI.IFM.Framework.OptionPricer/Black76/OptionCalculator.cs) is DateOnly/days-365; [OptionModel](../../../../../../TomasAI.IFM.Framework.OptionPricer/Black76/OptionModel.cs) already accepts explicit time-to-expiry | Preserve legacy calls, expose qualified fractional T through the integration, attach input provenance and structured failures |
| Composer | Typed upstream selection and historical Order Composition dispatch/workflow scaffolding exist | A complete composer, frozen preparation, typed acceptance and actual chain consumption are still planned in specification gates OC-01..OC-08 |

The [Stage 4 implementation record](../../../../../../Documents/system/Market-Data-Resiliency-Stage-4-Implementation-Record-v1.0.md) records the implemented offline subset and separate operational gaps. Its earlier selection-stage assumptions and deleted builder-document references are historical; the current OrderComposer specification governs composition behavior.

## 3. Three distinct readiness milestones

| Milestone | Evidence required | What it permits |
| --- | --- | --- |
| D: Implementation-document ready | Corrected specification; frozen logical producer/consumer contracts; exact package ownership/dependencies; error and test matrix; unresolved external evidence explicitly assigned | Write the full Order Composition implementation document with OC-01..OC-08 tasks; provider connection is not required to write it |
| R: Offline runtime ready | Implemented prerequisite contracts/calculations/lifecycle/snapshot path; unit, BDD, integration and verification evidence through real application paths | Integrate and test the composer against controlled/replayed market data; no claim of live operation |
| L: Live-data ready | Verified product and FMP metadata, publication policy, quote/provider limits, native/platform/recovery/soak evidence and applicable Stage 3/4 acceptance | Qualify actual live chain consumption under the existing rollout process; this does not authorize order submission |

Do not wait for a working composer to define the snapshot API, and do not require a live provider session before writing the composer plan. Stage 4 S4G-08 consumes the future composer; it cannot also be an entry requirement to document that composer. Resolve the contract at D, build and test each side independently at R, then close the joint integration gate when both exist.

## 4. Implementation packages and sequence

Each package requires an implementation record with revision, exact commands, pass/fail/skipped counts and artifacts. A fixture using real application code with a controlled external source is offline integration evidence; it must not be labeled a live feed test.

### OCP-00: Reconcile documentation and freeze boundary contracts

**Current status:** policy corrections, foundation wire manifests, reference-store placement and producer/consumer layering are documented and implemented in the linked record. Full composition domain DTO manifests remain OC-01 work.

- Adopt the decisions in section 1 in the current specification and HLD authority notice. Keep historical documents clearly subordinate.
- Freeze provider-neutral logical contracts for contract qualification, Treasury conversion, immutable pricing context, typed readiness/failure and complete bounded market snapshots. Publish exact append-only serialization manifests and compatibility fixtures as the first implementation task.
- Preserve current public fetch/query APIs, raw definitions, percentage units and legacy DateOnly pricer behavior. New fields with absent/unknown metadata never qualify old rows automatically.
- Assign Application.MarketData ownership of preparation/data readiness; Framework owns provider adapters and pricing math; Trade owns workflow preparation, composition policy and pure construction Models. No market-data-to-Trade dependency cycle.
- Pin pricing and freshness policies by exact version/hash in composition evidence. Resolve conflicting default values as described in section 6.

**Exit:** the full composer document can name exact input/output responsibilities, dependency gates and failure behavior without inventing a second pricing or subscription system. Maps to OC-01/OC-03 and S4G-01; no runtime gate closes from prose alone.

### OCP-01: Qualified European ES contract universe

**Depends on:** OCP-00. **Owners:** Reference contracts/storage mapping and DataBento adapter.

- Extend normalized metadata with exercise style, settlement/delivery type, exact expiry and last-trading instants, linked canonical future ID, multiplier, tick rules, currency, calendar and day-count convention IDs/versions, provenance and effective dates.
- Use feed fields only where their meaning is established. Store reviewed exchange/product mappings separately from raw provider facts; use versioned reference/configuration infrastructure and pin the resolved mapping in each snapshot. Record the chosen storage/access path before coding; do not guess conventions from ticker text or CFI without a verified mapping.
- Audit existing instrument_definition coverage for representative ES series. Add additive projection/query fields where required; leave original raw records available. Refresh/backfill only through explicit ingestion scope; unknown historical fields remain unknown.
- Query and classify the bounded definition scope before opening physical subscriptions. Known American series are excluded with counts/reasons. Unknown styles remain excluded, and unresolved classification that prevents proving scope completeness yields `Failed.ContractMetadataUnavailable`.
- A complete classified universe containing no permitted European contracts may yield `NoCandidate.NoEligibleEuropeanContract`. An American/unknown contract explicitly supplied for pricing fails `PricingModelUnsupported`/`ContractMetadataUnavailable`.

**Exit:** representative eligible and ineligible series have verified metadata fixtures; every admitted leg can be priced using a supported convention. Maps to pricing P2/P4 and OC-C09/C12/C15.

### OCP-02: Daily Treasury conversion and freshness

**Depends on:** OCP-00; OCP-01 calendar data for tenor counts. **Owners:** Framework reference contracts/FMP adapter and Application.MarketData.

- Add the pure continuous-rate conversion result/policy function to ITreasuryCurve as specified in pricing P1; preserve GetLatestAsync/GetRangeAsync and existing DecimalRate meaning.
- Freeze source-series evidence, selected tenor, raw percentage, convention/version, continuous decimal, curve value date/digest and modeling-policy version. Conversion never fetches data.
- Implement the exact trading-day buckets and publication-aware cache admission from the pricing specification. Reuse the daily download infrastructure and DownloadLog as provenance; verify actual points and value date independently.
- Define publication calendar/deadline, provider availability allowance and retry policy explicitly. A startup download or recent retrieval timestamp is not proof that today's required curve is available. Corrections change the digest even with the same value date.
- Fetch/cache asynchronously outside tick processing; bound concurrent refreshes and retry work. Expired/missing data returns a structured failure and never a substitute rate.

**Exit:** P1/P2/P3 boundary tests pass; missing tenor, unknown convention, stale/future data and >=90 trading days fail explicitly. No per-leg/tick HTTP, interpolation or fallback. Maps to S4G-04 and OC-C15/C20.

### OCP-03: Explicit-time Black-76 adapter and coherent pricing context

**Depends on:** OCP-01/OCP-02. **Owners:** Framework.OptionPricer and Application.MarketData.

- Add an explicit positive finite year-fraction path to OptionCalculator while preserving its DateOnly constructor. Reuse OptionModel/managed/native engines, which already accept T; no replacement pricing formula.
- Implement the production IOptionChainGreeksEnricher using an immutable IOptionPricingContextProvider result. Resolve metadata/rate/calendar before session creation; workers receive bounded serialized inputs through shared serializers, not a service locator or Treasury client.
- Separate stable session reference inputs from per-pass valuation, exact T and linked underlying quote. Recompute passage-of-time inputs for each pricing pass; do not freeze T for the lifetime of the session. Context identity binds these inputs, engine/version and host/dataset generation.
- Derive IV from a valid two-sided midpoint for composition; a last-trade enrichment may remain monitoring data but cannot replace executable bid/ask evidence. Validate bounds, convergence and finite outputs. Expose structured status/cause alongside values rather than relying on a success-shaped zero result.
- Pin numerical engine behavior and Greek units. Signed leg ratios/multipliers are applied once by composition. Rate/underlying/generation changes invalidate earlier readiness; preserve appropriately marked monitoring state for active order/position owners.

**Exit:** P4/P5 pass, including explicit fractional expiry, engine parity, Greek-unit vectors and no-arbitrage/solver failures. Outright futures passes P6 with rates/options unavailable. Maps to S4G-04 and OC-C10/C12/C15/C20.

### OCP-04: Connect production chain lifecycle and ownership

**Continuation status (2026-09-08 UTC):** qualified discovery reaches the existing worker. Committed durable snapshots install into the coordinator; replayable outbox delivery and acquire-before-release handoff are tested. Worker ownership has revision fences, independent non-expiring owners and terminal tombstones. Concrete committed source adapters, automatic realization/recovery, temporary and durable context rebinding, and selected-leg integration are implemented. Real PostgreSQL tests cover two/four legs and next-value-date reconstruction; supervised stress covers 100 recovery cycles. Snapshot transport is on demand. See the implementation record and live evidence for elapsed qualification; broad Stage 4 enablement remains guarded.

**Depends on:** OCP-03 and relevant Stage 4 ownership/protocol work. **Owners:** Application.MarketData, worker runtime and subscription persistence adapters.

- Route the application API through qualified coordinator and worker lifecycle paths. Reuse framework session machinery behind the owning worker boundary; do not create a second host-owned native feed that bypasses Stage 3 containment.
- Replace StartOptionChainAsync's unconditional Treasury exception only when context validation and actual session creation are connected. Missing prerequisites allocate no physical feed resources; startup failure/cancellation releases any provisional resources.
- Implement remaining owner-to-business-state adapters, durable intent/outbox/coordinator integration, canonical physical sharing, option-worker manifests/mirrors and restart reconciliation through S4G-02/03/05/06/07. The offline intent store alone does not provide these behaviors.
- Acquire bounded temporary composition discovery ownership. Renew within workflow lifetime, release on failure/stop, and define atomic selected-leg handoff before temporary discovery release. Preserve independent strategy/order/position owners and shared underlying references; closing UI must not stop their feeds.
- Fence host/dataset generations, reject conflicting chain scope without altering active scope, and invalidate snapshot readiness during recovery. Preserve cancellation, exact-owner release and idempotency contracts.

**Exit:** actual public application path is exercised with controlled feed/worker and real test persistence; sharing, handoff and restart invariants pass. Existing live-enablement guards remain until their own readiness requirements are met. Maps to S4G-01..07; full S4G-08 awaits the composer.

### OCP-05: Complete immutable composition snapshots

**Continuation status (2026-09-08 UTC):** concrete worker option/futures snapshots and immutable Scylla preparation now feed a mapped Domain.Trade acceptance command. PostgreSQL commits the saved Start dispatch before Realtime sends it; real NATS/PG/Scylla integration verifies identical retry bytes and no duplicate business reservation. Complete-empty universes are durable outcomes. The future Execute Function DTO and final composer-specific fee/session evidence remain OC-01/03 work. Schema-2 construction policies pin a finite reviewed `marketData` plan; schema-1 policy hashes remain unchanged. No production plan or reviewed mappings were fabricated.

**Depends on:** OCP-01..04; logical interface can be frozen at OCP-00. **Owners:** Application.MarketData producer and Trade workflow preparation consumer.

- Implement IMarketCompositionSnapshotProvider with bounded requested root/expiry/right/strike scope, full-scope completeness, immutable definitions/quotes/reference inputs, versions, epoch and digest. Enumerate every page; overflow is an explicit failure, never a truncated winner.
- Capture one evaluation instant; validate event-time quote ages and cross-leg/underlying skew, calendar/session status and context identity. Locking/copying a dictionary does not establish financial coherence.
- Reprice final selected legs under one common valuation/rate/underlying context per candidate. Different candidate expirations may link to different futures; each combo must have one compatible underlying/expiry/settlement. Do not mix independently refreshed Greeks.
- Return typed Ready, complete business-empty, or Failed preparation outcomes with bounded reason evidence. Persist the accepted request/snapshot hash in workflow preparation before Function dispatch as specified in OC-03. Do not call the composer for a failed preparation.
- Retry after accepted preparation uses the exact frozen request; pre-acceptance recapture is permitted under workflow revision fencing. Stale accepted input cannot be silently refreshed during calculation or acceptance.

**Exit:** snapshot producer contract is usable independently of the full composer; preparation race/restart fixtures prove frozen replay. Maps to OC-03 and OC-C08..12/C20/C25/C28.

### OCP-06: Verification and live evidence register

**Depends on:** relevant packages above. **Owners:** package owners; joint workflow/market-data integration.

- Execute the matrix in section 7 against real implementation paths. Use deterministic clocks and fixture data for repeatable unit/BDD tests; exercise actual application wiring and stores in integration tests.
- Keep source/series/calendar evidence, product coverage, provider entitlement/limits, event-time freshness, native Windows/Linux tests, recovery/rollover/soak and rollback outcomes in a separate live-evidence register.
- Run the existing Stage 4 regression runners as applicable after implementation; provision dedicated test PostgreSQL/Scylla/NATS and record fixture scope. No destructive fixture may target application databases.
- Do not label controlled snapshot tests as authenticated live-chain verification. Preserve current startup guards until the existing Stage 3/4 rollout prerequisites and explicit live acceptance are satisfied.

**Exit:** report D/R/L separately, with failing/skipped/missing evidence and exact remaining dependencies. The current documentation task does not execute these tests.

### OCP-07: Produce the Order Composition implementation document

**Depends on:** milestone D, not completion of OCP-06 live evidence.

Create `OrderComposition-Implementation-Plan-v1.0.md` in this folder. Expand existing specification gates OC-01..OC-08 into file/project changes, exact key manifests and error allocations, catalog publication, Model algorithms, five-map Function lifecycle, storage/query access paths, workflow preparation/acceptance and test commands. Reference OCP packages for data/pricing dependencies rather than duplicating their implementation.

State which gates can run with controlled snapshots and which require OCP runtime completion. Keep the joint S4G-08 composer/ownership tests as downstream acceptance, avoiding a circular entry dependency. Do not mark the composer complete when only pricing/session prerequisites are implemented.

## 5. Producer/consumer contract checklist

Freeze these logical fields at milestone D; additive MessagePack keys and golden compatibility vectors are the first code deliverable. Provider-neutral records belong at existing Framework/Application contract boundaries; Trade-specific bindings and workflow identities remain in Trade.Shared. Every result needs explicit success/failure discrimination and bounded evidence.

| Proposed contract | Required content and behavior |
| --- | --- |
| Qualified option definition | Canonical/provider IDs; definition and mapping versions/hashes; verified European style; settlement/delivery; exact expiry/last-trading UTC; underlying future; right/strike; exchange/currency; tick/multiplier; calendar/day-count mappings and provenance |
| Contract-universe qualification | Requested scope; complete enumeration evidence; admitted definitions; excluded counts/reasons; unresolved classification; no silent omission or permissive unknown enum |
| TreasuryContinuousRateResult | Selected tenor/trading-day count; curve value date/digest/observed-at; raw percentage; verified source-series and conversion policy; continuous decimal and FlatSelectedCmtProxy version; typed failure |
| Option pricing reference context | Contract/mapping/calendar/rate/engine versions, applicable publication and expiry validity, immutable shared reference inputs; no fixed session-lifetime T |
| Option pricing pass | Evaluation UTC; exact expiry and positive fractional T; linked underlying quote identity/value/time; leg quote/mark identity; reference-context digest; dataset/host generation; explicit result units/readiness/failure |
| MarketCompositionSnapshot request | Exact product and bounded expiry/right/strike/contract scope, one horizon, frozen policy versions/limits, deadline/cancellation and owner-aware acquisition context |
| MarketCompositionSnapshot result | Full schema in specification section 7; coverage, definitions, source-time quotes, reference inputs, immutable evaluation/context identities, validity, diagnostics and hash; no mutable cache handles |
| Preparation failure | Stable code, phase, safe details, missing/invalid input, contract/context identity, value date and observed/allowed age, correlation and retryability; no usable candidate or fabricated values |
| Lease handoff | Exact versioned business owner, operation/idempotency identity, selected canonical contracts and shared underlying, atomic acquire-before-release semantics, cancellation/restart result |

Pricing inputs crossing process boundaries use the shared MessagePack serializer. No nested byte-array result payload, manual domain-message serialization, MessagePack cloning or serializer-based fingerprint normalization. Content-size checks use the existing shared measurement support; canonical semantic hashes retain the owning contract's versioned algorithm.

## 6. Numerical and external-evidence decisions

| Item | Treatment in this plan | Closure responsibility |
| --- | --- | --- |
| European futures-option restriction | Approved now; unknown/American are excluded, not approximate-priced | OCP-01 implements and verifies per-series rules |
| Treasury buckets/source/failure semantics | Approved existing requirements; no new numerical trading decision needed | OCP-02 implements pricing P1..P3 |
| FMP series convention | Do not infer compounding from API field names or equal sample numbers; attach verified provider/series evidence | OCP-02; live evidence register |
| Publication deadline/provider allowance | Must be explicit, versioned and tested; do not invent a provider SLA | OCP-02 and live-data qualification |
| Exchange trading-day count | Retain the pricing specification's proposed exclusive-start/inclusive-expiry count; version and review product-calendar coverage | OCP-01/02 |
| Day-count mapping | Reviewed per product; ACT/365F is allowed only where explicitly mapped, with fractional time | OCP-01/03 |
| Quote thresholds | Stage 4 offline ceilings: age 5000 ms, skew 2000 ms, wait 10000 ms. Composer fixture limits: age 1000 ms, skew 250 ms. Effective age/skew are the stricter applicable limits; wait is bounded by remaining workflow/request lifetime | Freeze both policy versions; OCP-03/05 tests boundary equality; live settings require qualification |
| Solver baseline | Match existing calculator: absolute premium tolerance 1e-10, maximum 100 iterations, maximum accepted IV 4.0 annual decimal, existing no-arbitrage bound checks; preserve engine/version and regression vectors | OCP-03; any numerical change needs a separately versioned qualification |
| Output comparison normalization | Composer proposal remains decimal 12 fractional digits, ToEven; solver precision and comparison normalization are different policies | OC-01/04 golden vectors and deterministic replay |
| Strategy DTE | Elapsed UTC days still filter candidate expiry. Apply supported pricing horizon before rate lookup: an explicitly requested >=90-trading-day pricing input fails; do not select a longer tenor. Validity of a broad deployment DTE bound does not imply all expirations are priceable | OCP-01/02; OC-02 validates rules admit supported expiries |
| Numerical strategy thresholds | Existing complete offline defaults remain; no optimized trading performance claim or automatic catalog publication | Full composer OC-02/04; all 36 positive variant/horizon fixtures |

The full implementation document can record externally unverified items as activation gates with named required evidence. Such items do not justify unknown defaults or prevent implementing deterministic offline contracts.

## 7. Required test matrix

All rows are **planned**, not executed by this documentation update. BDD scenarios must call actual application/Model paths rather than duplicate expected formulas in test-only implementations. Numerical golden cases should include independent reference vectors, not only comparisons between engines sharing an algorithm.

| ID | Layer | Required evidence |
| --- | --- | --- |
| OCP-T01 | Unit/integration | Mixed ES definition scope: verified European admitted; American excluded; unknown cannot qualify; workflow horizon does not classify style |
| OCP-T02 | Unit/integration | Underlying linkage, exact expiry/last trade, settlement, tick, multiplier and currency preserved; conflicting/absent mappings fail |
| OCP-T03 | Unit/integration | Complete classification with zero eligible contracts is business-empty; incomplete/unknown classification preventing coverage is Failed |
| OCP-T04 | Unit | Trading-day boundaries 0/29/30/59/60/89/90; negative/inconsistent count; no interpolation or alternate-tenor fallback |
| OCP-T05 | Unit | Verified CMT percent-to-continuous conversion, zero/negative/domain cases, missing tenor and unknown convention; no percent normalization twice |
| OCP-T06 | Unit/integration | Publication deadline before/at/after; weekend/holiday; provider delay/outage; correction digest; freshly retrieved stale/future/unobserved curve |
| OCP-T07 | Unit/platform | Same-day before/at/after exact expiry; fractional T; DST, leap year, overnight value date, holiday/early close; unknown calendar coverage fails |
| OCP-T08 | Unit/native verification | Existing DateOnly behavior preserved; explicit-T golden vectors and managed/native parity; missing native backend cannot silently change pinned engine |
| OCP-T09 | Unit | IV no-arbitrage bounds/nonconvergence; invalid/NaN/infinite input; expired option cannot use engine intrinsic-value fallback as a successful live candidate |
| OCP-T10 | Unit/BDD | Required rate, forward, quote or Greeks unavailable is Failed with cause; valid priced candidate outside premium/delta/liquidity policy is economic rejection |
| OCP-T11 | Unit | Greek units, signed ratios and multiplier applied once; decimal normalization and engine version stable across replay |
| OCP-T12 | Integration | Public application chain start reaches production enricher with controlled feed; valid inputs produce ready state; invalid inputs allocate no physical resources |
| OCP-T13 | Integration | No HTTP on quote/trade path; bounded shared refresh; session creation/cancellation failure cleans provisional resources |
| OCP-T14 | Integration | Curve refresh, new underlying quote, aged queued option quote or generation reset invalidates old context; snapshot never combines incompatible values |
| OCP-T15 | Integration/storage | Shared owners and underlying references; atomic two-/four-leg transfer; temporary release preserves order/position leases; UI closure has no business ownership effect |
| OCP-T16 | Integration/process | Worker/API restart and value-date change restore current authorized intent; no expired/released resurrection; conflicting chain leaves existing scope unchanged |
| OCP-T17 | Unit/integration | All definition pages consumed for bounded scope; duplicate conflict/incomplete scope/overflow cannot select a truncated winner |
| OCP-T18 | Unit/integration | Strictest applicable age/skew and remaining-wait budget, exact boundary, future timestamp and crossed/locked quotes; no receipt-time freshness substitution |
| OCP-T19 | Integration/workflow | Preparation before/after commit crashes, competing capture, cancellation and deadline; one accepted snapshot and identical retry request |
| OCP-T20 | BDD | Complete valid chain but no permitted strikes/premium/delta yields NoCandidate; unsupported explicit pricing request/missing inputs yields Failed; neither dispatches risk |
| OCP-T21 | BDD/integration | Long and short outright futures continue with Treasury, chain and option solver unavailable on each triggering horizon |
| OCP-T22 | Architecture/serialization | Shared boundary serialization, explicit append-only manifests, unknown schema rejection, bounded content, no cross-domain layering cycle |
| OCP-T23 | Composer integration (later) | Actual composer handles 12 variants x 3 horizons, European legs only, exactly one unit, exact Fund/catalog versions and coherent final-leg valuation |
| OCP-T24 | Function/workflow integration (later) | Five maps/list validation/typed context/policy; completed-only persistence, typed terminal handlers, projection/append faults, expiry/replay and no failed continuation |
| OCP-T25 | Live verification (later) | Authenticated entitled chain, sampled exchange/source metadata, real event times, native platforms, session rollover/recovery/soak and rollback evidence; separate from offline passes |

Use existing Framework.MarketData.DataBento, Framework.MarketData.FinancialModelingPrep, Framework.OptionPricer and Application.MarketData unit projects for their owning tests. Add storage integration cases to Application.Storage.IntegrationTests and workflow/BDD/verification cases to the existing Domain.Trade test projects. Extend the existing Stage 4 runners after implementation; record precise filters and actual counts rather than forecasting passing totals.

## 8. Completion checklist and current disposition

### Roadblock audit - 2026-09-08, after approved clock synchronization

The [implementation record](OrderComposition-Prerequisite-Implementation-Record-v1.0.md) is the authority for executed tests. The former software blockers now have concrete implementations:

1. Committed workflow/option-order/option-position readers, PostgreSQL projection receipts and exact source-version validation are implemented. Real two-/four-leg lifecycle, replay and crash-between-commit-and-receipt tests pass.
2. Startup outbox reconciliation and immutable route-plan reconstruction are registered. A real supervised child-process test passes replacement, core rollover retention and explicit terminal removal.
3. Durable pricing-context refresh is implemented away from callbacks. Controlled-feed worker tests keep two-/four-leg selected positions priced after discovery expiry and context replacement.
4. Mapped prepared completion records exact selected contracts; durable handoff receipts release discovery only after current ownership realization. Unit tests cover ready/unready/replaced generations without refreshing accepted evidence. A joined genuine PostgreSQL-source/delivery/real worker-pricing test now passes for two/four legs, including replacement runtime and explicit closure. Controlled UTC feed transport and direct worker calls are used; the separate actual child-process test covers supervision/rollover. Neither is a live soak.
5. Reviewed provider/product/tick/calendar/day-count configuration is now published for the complete E2D September 10 scope, profile `CME-ES-TueThu-202609/v2`. Official Treasury replaces the unresolved FMP rate lineage. See the [publication record](OrderComposition-Reference-Publication-and-Qualification-v1.0.md); no provider-evidence question remains pending with the user.
6. Native working-set reservation was corrected in C++ and Rust. Both supported backends passed required concurrent ring-lock tests without granting machine-wide privileges.
7. The user approved Windows Time startup and synchronization. These completed, followed by a bounded measured correction. StrictProduction DataBento live quotes passed with unchanged freshness rules. A combined Black-76/Scylla/committed-ownership/process-replacement canary has now passed; sustained results are recorded separately in the live evidence register.

Do not report all prerequisites complete until the remaining live-reference and recovery/soak requirements have actually passed. Do not describe unexecuted integration work as an external-data blocker.

- [x] Record the approved European-only futures-option scope.
- [x] Implement pricing qualification, exact reference storage and supervised snapshot/preparation boundaries.
- [x] Implement concrete committed business-source projection and durable route reconstruction/refresh.
- [x] Implement and test mapped workflow acceptance, identical redispatch and exact selected-leg completion validation.
- [x] Implement durable discovery-release receipts and test 2/4-leg ownership/pricing components.
- [x] Repair clock/native locking and pass strict native live quotes.
- [x] Write the full composer implementation document with explicit OC gates.
- [x] Complete joined selected-leg committed-source/worker pricing/replacement acceptance with controlled UTC feeds.
- [x] Complete 30-minute live recovery/soak and 30-minute maximum-scope controlled load after reference qualification, with 100 actual supervised recovery cycles; full-session/platform acceptance remains OCP-T25.
- [x] Publish verified product/calendar/tick/rate-source bundle and complete bounded-plan construction support for the explicit qualification scope; see the publication record.
- [x] Qualify the combined live priced capture, immutable persistence, committed ownership, worker replacement and final position drain for the reviewed canary scope.
- [ ] Implement composer OC-01..OC-08 (separate downstream scope).

Existing live-enablement guards remain. Source completion alone does not end a workflow owner; an explicit origin-linked order transfer or terminal fact is required. Future composer/order dispatch must emit that fact when transferring ownership. No broker/emulator execution is implied by the prerequisite boundary tests.
