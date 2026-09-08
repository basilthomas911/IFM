# Order Composition prerequisite closure audit

Reviewed 2026-09-08, following the user's authorization to make all changes needed to finish the prerequisites.

## Authorization and scope

Code, documentation, configuration, database publication and prerequisite verification are authorized. No repeat permission request is needed for that work. Authorization does not establish missing provider facts or turn an unexecuted test into passing evidence. Actual composer algorithms (OC-01..08) and broker execution remain separate downstream scope.

## Current prerequisite evidence

| Work | Completion evidence |
| --- | --- |
| Establish the rate input contract | Resolved by the [official Treasury provider implementation](OrderComposition-Official-Treasury-Implementation-v1.0.md): direct par/CMT source, verified conversion, versioned 2026 publication policy and live endpoint verification. FMP remains the calendar provider. |
| Publish the reviewed reference bundle | **Complete for the reviewed September scope:** 4,268 definitions across seven Tuesday/Thursday expiries, exact ESU6/ESZ6 linkage, schema-2 CME ticks, ACT/365F, exchange calendar and official Treasury policies. Every mapping was read back. Profile `CME-ES-TueThu-202609/v2`; exact bundles are in the [publication record](OrderComposition-Reference-Publication-and-Qualification-v1.0.md). |
| Qualify the combined live path | **Bounded qualification passed:** actual native DataBento -> supervised worker -> Black-76 -> immutable Scylla snapshots -> committed PostgreSQL handoff -> killed/replaced worker -> 30-minute live run -> position closure/final drain. Separate 30-minute 512-contract load, 100 supervised recovery cycles, 10,000 health reads and controlled next-value-date reconstruction passed. See the [live evidence register](OrderComposition-Live-Evidence-v1.0.md). |

The combined path now has actual live execution evidence. Controlled PostgreSQL two-/four-leg tests also cover both same-date replacement and next-value-date reconstruction. Natural overnight/session acceptance remains distinct from controlled clock advancement. Exact measurements and elapsed duration must accompany any sustained-qualification claim.

## Final bounded qualification, September 8

- Live run: 1800.077 seconds, 6,071 requests, 4,205 qualified snapshots, 1,502 incoherent-quote and 364 stale-data rejections. Unchanged age/skew thresholds. All-request p95/p99 latency: 1.9237 / 2.5752 ms. Final position close returned `ChainUnavailable`; all owned worker processes exited.
- Controlled maximum scope: 512 contracts, 1800.045 seconds, 16,253,440 quotes, 9,029.46 quotes/second. This exceeds twice the live peak 30-second sampled option-record rate of 210.11/second by a wide margin (42.97 times that peak). No native ring overruns were observed in the live run.
- Managed-load snapshot p95/p99: 83.83 / 124.82 ms. Allocation baseline: 236.52 MB/second; this is measured cost, not a claim of optimization. Average RSS decreased from 158.79 MB in the early measured window to 148.90 MB in the final five minutes.
- Separate process stress: 90 cooperative resets plus ten actual kills/replacements; every generation was fenced and admitted correctly, with worker RSS 56.73-62.84 MB. All 100 cycles passed.
- Final principal suites: MarketData application 476 passed (5 explicit skips), MarketData domain 195, Trade 797, Application lifecycle 30. Additional real storage/ownership, stress and live runs have their own passing records; focused counts overlap these suites.
- API build: zero warnings/errors. Actual composition-root startup verification passed. Repeatable runner and source diff checks passed.

The runtime/design prerequisites are ready for composer implementation. Full-session, natural overnight/OffTrading/Closed, Linux and all-profile/UI rollout acceptance remain the later Stage 4/OCP-T25 matrix. They were not waived or relabelled as completed by this bounded test.

## Tick-rule gap corrected in this continuation

The current [CME rulebook, Chapter 358A01.C](https://www.cmegroup.com/rulebook/CME/IV/350/358A/358A.pdf), reviewed on September 8, 2026, supersedes the older two-band FAQ for current mapping purposes. CME Globex outright and combination net premiums use these index-point increments:

| Premium magnitude | Increment |
| --- | --- |
| At most 5 | 0.05 |
| Above 5, at most 20 | 0.10 |
| Above 20, at most 100 | 0.25 |
| Above 100 | 0.50 |

Allocated combination legs use 0.05. ClearPort, derived blocks and box spreads have separate provisions and are outside this rule's supported use. This rule resolves a trading increment; theoretical prices/Greeks are not rounded to it.

`OptionPricingConvention` schema 2 appends `PremiumTickRule` at MessagePack key 24. `OptionPremiumTicks` resolves the pinned rule and rejects inconsistent product/version/minimum-increment metadata. `TickSize` is the minimum increment for a banded rule; future order construction must resolve the actual premium increment. Schema 1 remains readable with its original fixed-rule semantics and semantic JSON shape. The existing Scylla table stores both immutable schema versions without a destructive migration.

Verification: `.test-results/ocp/ocp-tick-market.trx`: **441 passed, 1 platform-specific skip, 0 failed**, including 16 new boundary/serialization/invalid-mapping cases. `.test-results/ocp/ocp-tick-scylla.trx`: **1 passed**, covering schema-2 write/read/restart/idempotency/conflicting-version refusal alongside legacy mappings. These tests do not publish reviewed production records or qualify a live pricing run.

## FMP ambiguity replaced by direct authoritative input

The [FMP Treasury endpoint documentation](https://site.financialmodelingprep.com/developer/docs/stable/treasury-rates) and [official FAQ](https://site.financialmodelingprep.com/faqs?code=data) establish daily data availability. The reviewed pages do not establish the exact source series, compounding convention or publication cutoff. Treasury's [official CMT explanation](https://home.treasury.gov/policy-issues/financing-the-government/interest-rate-statistics/interest-rates-frequently-asked-questions) establishes its own series convention, not FMP's lineage. Matching sample values alone is not evidence of that lineage.

The user authorized the direct-source redesign, now implemented and tested in the [official Treasury record](OrderComposition-Official-Treasury-Implementation-v1.0.md). The API obtains observations directly from Treasury and preserves `USTreasury` provenance. No claim about FMP lineage or publication SLA is required. Product reference publication is now complete for the explicit September 10 scope; combined-path evidence is recorded above.
