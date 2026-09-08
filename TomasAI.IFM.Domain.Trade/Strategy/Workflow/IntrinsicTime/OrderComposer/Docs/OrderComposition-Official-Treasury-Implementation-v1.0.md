# Official Treasury pricing input — implementation and verification

Date: 2026-09-08. This decision replaces the FMP Treasury runtime dependency for pricing and yield-curve imports. FMP remains the economic-calendar provider. Historical FMP download logs and immutable route plans retain their original identity.

## Source and calculation

`UsTreasuryCurve` implements the existing `ITreasuryCurve` API against the [official monthly XML feed](https://home.treasury.gov/treasury-daily-interest-rate-xml-feed), requesting `data=daily_treasury_yield_curve&field_tdr_date_value_month=YYYYMM`. No API key is required. The provider maps the 12 existing supported maturities, including `BC_1MONTH`, `BC_2MONTH` and `BC_3MONTH`; additional feed columns do not change the established contract. Percentages retain their published units.

The [Treasury FAQ](https://home.treasury.gov/policy-issues/financing-the-government/interest-rate-statistics/interest-rates-frequently-asked-questions) identifies par/CMT quotations, including short maturities, as semiannual bond-equivalent yields. The pinned policy is:

| Field | Value |
|---|---|
| Source / DownloadLog provider | `USTreasury` |
| Series | `daily_treasury_yield_curve` |
| Convention | `UsTreasuryCmtNominalSemiannual` |
| Conversion version | `USTreasury-ParCmt-Semiannual/v1` |
| Conversion | `2 * ln(1 + RatePercent / 200)` |
| Modeling policy | `FlatSelectedCmtProxy/v1` |

This remains a flat selected par-yield proxy, not a bootstrapped zero curve. The existing trading-day buckets select 1/2/3 months without interpolation. Unknown source/version, missing selected tenor and invalid input fail. A published zero is distinct from a missing rate. There is no automatic FMP fallback.

Each snapshot retains source, value date and response-observed UTC time. The selected rate retains the conversion/evidence, observation time and semantic curve digest in the pricing context. Cache hits never change the observation time. The legacy durable yield-curve read model still stores its established date/rate fields; DownloadLog records acquisition provider/date/counts/timing. Pricing does not infer provenance from those legacy rate-only rows.

## Freshness and lifecycle

`USTreasury-2026-18ET/v1` defines **18:00 America/New_York** as the application availability deadline. It is an application allowance, not a Treasury SLA. Its explicit coverage is `[2026-01-01T00:00Z, 2027-01-01T00:00Z)` and its initial expected observation is 2025-12-31. Coverage expiration fails closed and requires publishing a reviewed replacement policy before 2027.

Weekends and the 2026 full holidays are excluded: January 1/19, February 16, May 25, June 19, July 3, September 7, October 12, November 11/26 and December 25. Early closes remain publication days. In particular, Good Friday 2026 is not a full closure: see the [New York Fed statement](https://www.newyorkfed.org/markets/opolicy/operating_policy_260312) and [SIFMA schedule](https://www.sifma.org/resources/general/holiday-schedule). This calendar is independent of CME trading days. An unexpected publication disruption rejects stale pricing; it does not silently extend validity.

Before the next deadline, the previous required observation remains usable. After the deadline the expected observation is required. A newer already-observed curve may be used early. The 14-day retrieval search is not a freshness allowance. Outages reuse a cached rate only while the date, source and observability checks still pass.

The API registers the official provider after FMP registration, replacing only `ITreasuryCurve`. It registers both policy records for construction of **new** pricing/discovery inputs. Callers must carry these exact policies into new immutable route plans; existing accepted plans are not rewritten or relabeled.

`UsTreasuryRefreshHostedService` runs asynchronously after bootstrap health and actor readiness. It submits the required date through the existing import coordinator and warms `TreasuryPricingProvider`. Successful warm-up/submission checks repeat after 30 minutes; unavailable warm-up or submission failure retries after five minutes. Actor readiness retries after ten seconds. Shutdown cancels pending waits. Acquisition/persistence completion remains asynchronous and independently observable; submission alone is never reported as a completed download.

The normal FMP startup and optional scheduled range import now request economic calendars only. Manual imports through the existing coordinator can still request both datasets. The existing Treasury duplicate policy remains `Overwrite` in the checked-in API configuration, allowing repeat refreshes/corrections.

Provider HTTP work is bounded to 30 seconds per range, 366 dates per request, one MiB per monthly response, 31 entries per month and two cached months with a one-minute TTL. Concurrent cache misses are serialized. XML DTDs/external resolution, conflicting duplicate observations, duplicate fields, invalid rates, wrong-month dates and unexpected pagination are rejected. There are no HTTP calls on quote callbacks.

## Durable logging

The mapped yield-curve import extension gets provider identity before acquisition so failures also carry `USTreasury`. The existing flow remains:

`ImportYieldCurveRatesCommand → import event → completed/failed terminal event → InsertMarketDataDownloadLogCommand → DownloadLog validation/state → committed private event → durable projector → Scylla`.

Provider-aware validation accepts `FMP` for either established dataset and `USTreasury` only for `TreasuryCurve`. Query with partition `(TreasuryCurve, USTreasury, US, valueDate)`. No table migration is needed because provider already belongs to the Scylla partition key. FMP and Treasury rows cannot satisfy each other's query. Empty completion remains an acquisition outcome, not proof of a usable pricing curve. Terminal delivery failures continue to recover the same immutable outcome, without turning log recovery into a new download.

## Verification

The public endpoint returned all 12 supported tenors. On 2026-09-08 at 12:49:47 UTC, its latest observation was **2026-09-04**, correctly admitted across Labor Day and before the next application deadline:

| Tenor | Published percentage | Annual continuous decimal |
|---|---:|---:|
| One month | 3.79 | 0.0375453706465672 |
| Two months | 3.90 | 0.03862462206474577 |
| Three months | 3.91 | 0.03872270695723964 |

Digest: `921f2543c8c17691337ef499a903343e28660c19a0cf294a2d0ce1efecae72dc`.

Results are recorded under `.test-results/ocp`: `treasury-market.trx` (460 passed, one platform skip, live Treasury test enabled), `treasury-domain.trx` (185 passed), and `treasury-startup.trx` (30 passed, including asynchronous start/stop and readiness gating; API compiled successfully). `treasury-integration.trx` has 11 passing real Scylla/NATS/PostgreSQL tests, including live official acquisition through terminal handlers and durable projection, provider isolation, idempotency/conflict rejection and the existing MarketCondition calendar consumer. The focused integration host needed a registration correction: excluded Trade function actors must not require context aliases during a MarketData-only test.

Final verification also read the imported 1/2/3-month percentages back from the Scylla test database and matched the live provider snapshot. The final API build completed with zero warnings/errors. The final bounded provider checks passed all 19 cases (`treasury-final-provider.trx`); the integration rerun passed all 11 cases. Nine changed Markdown documents passed local-link checks. The live import used isolated test messaging and test database tables; the normal API activates the new worker on its next startup.

This closes the source-series/conversion ambiguity by obtaining observations directly from the authoritative source. It does not establish qualification of an entire live option chain, seed product calendars/conventions or complete the separate Order Composition business-workflow gates.
